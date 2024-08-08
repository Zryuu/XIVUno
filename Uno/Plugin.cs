using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
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
    
    public IPlayerCharacter? LocPlayer { get; set; }
    public readonly GroupManager* GM;
    public string LocPlayerName { get; set; }
    public bool BServer { get; set; }
    public bool BPing { get; set; }
    
    private List<string> capturedMessages = new List<string>();
    public SeString[]? PartyMembers;
    public bool bIsLeader, bDebug = false;
    public long? currentPartyId;
    public MessageType MessageType;
    public TcpClient? client;
    public NetworkStream? Stream;
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

    public void ConnectToServer()
    {
        var IP = Environment.GetEnvironmentVariable("UnoServerIP");

        try
        {
            client = new TcpClient(IP, 6347);
            Stream = client.GetStream();
            buffer = new byte[1024];
            BServer = true;
        }
        catch (Exception e)
        {
            Services.Log.Information("Uno Server was unresponsive. It might be down...");
            throw;
        }
        

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
    
    public void HandleDeltaTime()
    {
        DeltaTime = (float)Services.Framework.UpdateDelta.TotalSeconds;
    }
    
    public void PingServer()
    {
        if (!BServer) { return; }
        
        if (client == null)
        {
            Services.Log.Information("Delegates::PingServer(): Server is null....Please let me know");
            BServer = false;
            return;
        }
        
        if (LastPing >= 300) { BPing = true; SendMsg(0.ToString());; }
        
        LastPing += DeltaTime;

        if (BPing) { BPing = false; }
    }
    
    
    
    /***************************
     *         RECEIVE         *
     *          DATA           *
     ***************************/

    public void ReceiveMessage()
    {
        if (!BServer)
        {
            return;
        }
        
        byte[] buffer = new byte[1024]; // Buffer for incoming data
        int bytesRead = Stream.Read(buffer, 0, buffer.Length);
        if (bytesRead > 0)
        {
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Services.Log.Information($"Received: {response}");
        }
    }
    
    
    
    
    /***************************
     *         SEND            *
     *         Data            *
     ***************************/
    
    // Sends data to uno server
    public void SendMsgUnsafe(byte[] message)
    {
        if (client == null)
            throw new InvalidOperationException("Server is null");
        
        Stream!.Write(message, 0, message.Length);
    }
    //  Gets message ready to send to Server. Converts string to Byte[].
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
