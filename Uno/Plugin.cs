using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using Microsoft.Extensions.Configuration;
using Uno.Helpers;
using Uno.Windows;

namespace Uno;

public enum MessageType
{
    Settings,
    StartGame,
    EndGame,
    Turn,
    Draw
}

public unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public Delegates Delegates { get; private set; }
    public CommandManager Cm { get; private set; }
    
    //  From: https://git.anna.lgbt/anna/XivCommon/src/branch/main/XivCommon/Functions/Chat.cs
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    private readonly delegate* unmanaged<UIModule*, Utf8String*, nint, byte, void> ProcessChatBox = null!;
    
    public IPlayerCharacter? LocPlayer { get; set; }
    public readonly GroupManager* GM;
    public string LocPlayerName { get; set; }
    public bool BServer { get; set; }
    public bool BPing { get; set; }
    
    private List<string> capturedMessages = new List<string>();
    public SeString[]? PartyMembers;
    public bool bIsLeader, bDebug = false, bConnected = false;
    public long? currentPartyId;
    public MessageType MessageType;
    public TcpClient client;
    public NetworkStream stream;
    public byte[] buffer;
    public int bytesRead;

    public float DeltaTime = 0, LastPing = 0;
    
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("UNO");
    private ConfigWindow ConfigWindow { get; init; }
    private UnoInterface UnoInterface { get; init; }

    public Plugin()
    {
        PluginInterface.Create<Services>();
        
        var builder = new ConfigurationBuilder()
                      .SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddEnvironmentVariables();

        var configuration = builder.Build();
        
        string IP = configuration["AppSettings:ServerIP"];
        
        client = new TcpClient(IP, 6347);
        stream = client.GetStream();
        buffer = new byte[1024];
        
        //  Initing Helpers
        Delegates     = new Delegates(this);
        Cm            = new CommandManager(this);
        GM            = GroupManager.Instance();
        
        Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        ConfigWindow  = new ConfigWindow(this);
        UnoInterface  = new UnoInterface(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(UnoInterface);

        Services.GameInteropProvider.InitializeFromAttributes(this);

        Services.PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        Services.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
    }

    //  DISPOSE
    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        //  Windows
        ConfigWindow.Dispose();
        UnoInterface.Dispose();
        
        //  Helpers
        Delegates.DisposeDelegates();
        Cm.DisposeCommands();
    }
    
    
    /***************************
     *          PLAYER &       *
     *          PARTY          *
     *        VARIABLES        *
     ***************************/
    
    //  Saves a Ref to local player and local player's name.
    public void SaveLocPlayer()
    {
        //  Checks if user is logged in. If not, instant return
        if (!Services.ClientState.IsLoggedIn)
        {
            return;
        }
        
        //  Saves user ref
        LocPlayer = Services.ClientState.LocalPlayer!;

        //  Get User name.
        LocPlayerName = GM->MainGroup.GetPartyMemberByEntityId(Services.ClientState.LocalPlayer!.EntityId)->NameString;
    }
    
    //  Gets Party Member's names and saves them in an array
    public void SetPartyMembers()
    {

        if (Services.Party.Length < 1)
        {
            return;
        }
        
        //  This may cause some memory issues......maybe.
        PartyMembers = new SeString[Services.Party.Length];

        for (var i = 0; i < Services.Party.Length; i++)
        {
            PartyMembers[i] = GetPartyMemberNames(i)!;
            
        }

        if (GM->MainGroup.PartyMembers.Length > 1)
        {
            bIsLeader = GM->MainGroup.IsEntityIdPartyLeader(Services.ClientState.LocalPlayer!.EntityId);
        }
        
        //  Checks if PartyID has changed.
        if (currentPartyId == null || Services.Party.PartyId != currentPartyId)
        {
            currentPartyId = Services.Party.PartyId;
            
            //  Saves Loc player
            SaveLocPlayer();
        }
    }
    
    //  Gets part Member's names from array.
    public string? GetPartyMemberNames(int i)
    {
        //  Checks if Party Member count is less than 1, if so then no party.
        if (Services.Party.Length < 1)
        {
            Services.Log.Information("[ERROR]: Uno::Plugin.cs::GetPartyMemberNames ran with no active party.");
            return null;
        }
        
        var member = Services.Party.CreatePartyMemberReference(Services.Party.GetPartyMemberAddress(i));
        
        //Services.Log.Information(member.Name.ToString());

        return member!.Name.ToString();
    }

    public void HandleDeltaTime()
    {
        DeltaTime = (float)Services.Framework.UpdateDelta.TotalSeconds;
    }
    
    public void PingServer()
    {
        if (client == null!)
        {
            Services.Log.Information("Delegates::PingServer(): Server is null....Please let me know");
            BServer = false;
            return;
        }

        if (!BServer) { return; }
        
        if (LastPing >= 300) { BPing = true; SendMsg(0.ToString());; }
        
        LastPing += DeltaTime;

        if (BPing) { BPing = false; }
    }
    
    
    
    /***************************
     *         RECEIVE         *
     *        MESSAGES         *
     *                         *
     ***************************/

    //  Message format: "++Type;info1;info2;info3;info4"
    //  EG. "++Settings;10;true;true;true;true"
    public void RouteReceivedMessage(string message, SeString sender)
    {
        if (message.Length < 2 || sender == Services.ClientState.LocalPlayer!.Name)
        {
            return;
        }
        
        var parts = message.Split(";");
        var part = parts[0];

        switch (part)
        {
            //  Settings
            case "Settings":
                UnoInterface.ReceiveSettings(parts);
                break;
            //  StartGame
            case "StartGame":
                UnoInterface.ReceiveStartGame(parts);
                break;
            //  EndGame
            case "EndGame":
                UnoInterface.ReceiveEndGame(parts);
                break;
            //  Turn
            case "Turn":
                UnoInterface.ReceiveTurn(parts, sender);
                break;
            //  Draw
            case "Draw":
                UnoInterface.ReceiveDrawCard(parts, sender);
                break;
        }
    }

    public void ReceiveMessage()
    {
        byte[] buffer = new byte[1024]; // Buffer for incoming data
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        if (bytesRead > 0)
        {
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Services.Log.Information($"Received: {response}");
        }
    }
    
    
    
    
    /***************************
     *         SEND            *
     *        MESSAGES         *
     *                         *
     ***************************/
    
    //  From: https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/GameFunctions/ChatBox.cs
    //  Converts String message to Bytes, this is the func that actually sends the message.
    public void SendMsgUnsafe(byte[] message)
    {
        if (client == null)
            throw new InvalidOperationException("Server is null");
        
        stream.Write(message, 0, message.Length);
    }
    //  Creates String message version. Sends to Sanitise, then sends to SendMsgUnsafe.
    public unsafe void SendMsg(string message)
    {

        message += $"{Services.ClientState.LocalPlayer!.Name}";
        var messageBytes = Encoding.ASCII.GetBytes(message);
        
        switch (messageBytes.Length)
        {
            case 0:
                throw new ArgumentException("message is empty", nameof(message));
            case > 500:
                throw new ArgumentException("message is longer than 500 bytes", nameof(message));
        }
        
        SendMsgUnsafe(messageBytes);
    }
    
    
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => UnoInterface.Toggle();
    
    

}
