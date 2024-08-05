using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    
    public IPlayerCharacter? LocPlayer;
    public GroupManager* GM;
    public string LocPlayerName;
    private int index = 0; //  this is badly named and will get renamed later (Just stops multiple messages being sent)
    
    private List<string> capturedMessages = new List<string>();
    public SeString[]? PartyMembers;
    public bool isLeader;
    public long? currentPartyId;
    public MessageType MessageType;
    
    public float DeltaTime = 0;
    
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
        
        
        isLeader = GM->MainGroup.IsEntityIdPartyLeader(Services.ClientState.LocalPlayer!.EntityId);
        
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
    
        
    /***************************
     *         RECEIVE         *
     *        MESSAGES         *
     *                         *
     ***************************/

    //  Message format: "++Type;info1;info2;info3;info4"
    //  EG. "++Settings;10;true;true;true;true"
    public void RouteReceivedMessage(string message, SeString sender)
    {
        if (message.Length < 2 || sender.ToString() == LocPlayerName)
        {
            return;
        }
        
        var parts = message.Split(";");
        var part = parts[0];

        switch (part)
        {
            //  Settings
            case "Settings":
                UnoInterface.ReceiveSettings(message);
                break;
            //  StartGame
            case "StartGame":
                UnoInterface.ReceiveStartGame(message);
                break;
            //  EndGame
            case "EndGame":
                UnoInterface.ReceiveEndGame(message);
                break;
            //  Turn
            case "Turn":
                UnoInterface.ReceiveTurn(message, sender);
                break;
            //  Draw
            case "Draw":
                UnoInterface.ReceiveDrawCard(message, sender);
                break;
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
        if (ProcessChatBox == null)
            throw new InvalidOperationException("Could not find signature for chat sending");
        
        var mes = Utf8String.FromSequence(message);
        ProcessChatBox(UIModule.Instance(), mes, IntPtr.Zero, 0);
        mes->Dtor(true);
    }
    //  Creates String message version. Sends to Sanitise, then sends to SendMsgUnsafe.
    public unsafe void SendMsg(string message)
    {

        message = "/p " + message;
        
        var bytes = Encoding.UTF8.GetBytes(message);
        
        if (index > 0)
        {
            return;
        }
        
        if (bytes.Length == 0)
            throw new ArgumentException("message is empty", nameof(message));

        if (bytes.Length > 500)
            throw new ArgumentException("message is longer than 500 bytes", nameof(message));

        if (message.Length != SanitiseText(message).Length)
            throw new ArgumentException("message contained invalid characters", nameof(message));
        
        SendMsgUnsafe(bytes);
    }
    //  Cleans text and makes sure it only sends characters a player could normally send.
    private string SanitiseText(string text)
    {
        var uText = Utf8String.FromString(text);

        uText->SanitizeString( 0x27F, (Utf8String*)nint.Zero);
        var sanitised = uText->ToString();
        uText->Dtor(true);

        return sanitised;
    }
    
    
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => UnoInterface.Toggle();
    
    

}
