using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using Microsoft.Extensions.Configuration;
using Uno.Helpers;
using Uno.Windows;

namespace Uno;

//  CommandBytes sent to Server
public enum MessageTypeSend
{
    Ping = 00,
    Login = 01,
    Logout = 02,
    StartGame = 03,
    EndGame = 04,
    CreateRoom = 05,
    JoinRoom = 06,
    LeaveRoom = 07
}

//  CommandBytes received from Server
public enum MessageTypeReceive
{
    Ping = 0,
    Pong,
    Login,
    Logout,
    StartGame,
    EndGame,
    JoinRoom,
    LeaveRoom,
    UpdateRoom,
    Error = 99
}

public unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public Delegates Delegates { get; private set; }
    public CommandManager Cm { get; private set; }
    public IPlayerCharacter? LocPlayer { get; set; }
    public readonly GroupManager* GM;
    
    public bool BServer { get; set; }
    public bool BPing { get; set; }
    
    public MessageTypeSend MessageTypeSend;
    public MessageTypeReceive MessageTypeReceive;
    public TcpClient? client;
    public NetworkStream? Stream;
    public byte[] buffer;
    public int bytesRead;
    
    public string XivName { get; set; }
    private bool BInUnoGame { get; set; }
    private DateTime LastPingSent { get; set; }
    private DateTime LastPingReceived { get; set; }
    public int? CurrentRoomId { get; set; }

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
        
        SaveLocPlayer();
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
        
        var builder = new ConfigurationBuilder()
                      .SetBasePath(Services.PluginInterface.GetPluginConfigDirectory())
                      .AddJsonFile("appsettings.json", 
                                   optional: true, reloadOnChange: true);
        
        
        
        Services.Log.Information($"{Services.PluginInterface.GetPluginLocDirectory()}");
        Services.Log.Information($"{Services.PluginInterface.GetPluginConfigDirectory()}");
        
        if (builder == null)
        {
            Services.Log.Information("Dis bitch null");
        }
        
        Services.Log.Information($"{Directory.GetCurrentDirectory()}");


        var configuration = builder.Build();
        
        Services.Log.Information($"{configuration}");
        
        var ip = configuration["AppSettings:ServerIP"];

        try
        {
            client = new TcpClient(ip, 6347);
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
        XivName = LocPlayer.Name.ToString();
    }
    
    public void HandleDeltaTime()
    {
        DeltaTime = (float)Services.Framework.UpdateDelta.TotalSeconds;
    }
    
    
    
    /***************************
     *         RECEIVE         *
     *          DATA           *
     ***************************/
    
    public void ReceiveMessage()
    {
        if (!BServer || Stream == null)
        {
            return;
        }
        
        byte[] buffer = new byte[1024]; // Buffer for incoming data
        int bytesRead = Stream.Read(buffer, 0, buffer.Length);
        if (bytesRead > 0)
        {
            string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            ReceiveCommand(response);
            
            Services.Log.Information($"Received: {response[2..]}");
        }
    }
    
    private void ReceiveCommand(string message)
    {
        if (string.IsNullOrEmpty(message) || message.Length < 2)
        {
            Services.Log.Information($"Received empty response from server: {message}");
            return;
        }
        
        var commandByte = int.Parse(message.Substring(0, 2));
        var commandArgument = message[1..];
        
        var route = (MessageTypeReceive)(commandByte); 
        /*
        switch (route)
        {
            //  Ping = 00
            case MessageTypeReceive.Ping:
                Ping();
                break ;
            case MessageTypeReceive.Ping:
                Ping();
                break ;
            //  Login = 01
            case MessageTypeReceive.Login:

                break ;
            //  Logout = 02
            case MessageTypeReceive.Logout:

                break ;
            //  StartGame = 03
            case MessageTypeReceive.StartGame:

                break ;
            //  EndGame = 04
            case MessageTypeReceive.EndGame:

                break ;
            //  JoinRoom = 05
            case MessageTypeReceive.JoinRoom:

                break ;
            //  LeaveRoom = 06
            case MessageTypeReceive.LeaveRoom:
                CurrentRoomId = null;
                break ;
            //  UpdateRoom = 07
            case MessageTypeReceive.UpdateRoom:

                break ;
            //  Error = 99
            case MessageTypeReceive.Error:
                HandleErrorMsg(commandArgument);
                break ;
            default:
                Services.Log.Information("Invalid Response received.");
                break;


        }
        */
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
    
    
    /***************************
     *         Client          *
     *         Commands        *
     ***************************/
    
    /*
    public String Ping()
    {
        if (!BServer)
        {
            Services.Log.Information("Plugin::Ping():: ");
            return;
        }
        
        if (client == null)
        {
            Services.Log.Information("Delegates::PingServer(): Server is null....Please let me know");
            BServer = false;
            return;
        }
        
        if (LastPingReceived.Second >= 240) 
        {
            BPing = true; 
            SendMsg(0.ToString()); 
        }

        if (BPing) { BPing = false; }
    }

    public void Pong()
    {
        LastPingReceived = DateTime.Now;
    }
    
    public string StartGame(string command)
    {
        return "StartGame was entered";
    }
    
    public string EndGame(string command)
    {
        return "EndGame was entered";
    }
    
    public string JoinRoom(string command)
    {

       
    }
    
    public string CreateRoom(string command)
    {
        
    }
    
    public string LeaveRoom(string command)
    {
        
    }

    private void HandleErrorMsg(string message)
    {
        Services.Log.Information($"[ERROR]: {message[1..]}");
        Services.Chat.PrintError("[UNO]: Error response received from server. Please check xllog (/xllog) for error message.");
    }
    
    
    public string CommandType(MessageTypeSend r, string message)
    {
        return $"{(int)r:D2}" + message;
    }
    
    */
    
    
    
    
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => UnoInterface.Toggle();
    
    

}
