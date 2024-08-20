using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using Newtonsoft.Json.Linq;
using Uno.Helpers;
using Uno.Windows;

namespace Uno;

//  CommandBytes sent to Server.
public enum MessageTypeSend
{
    Ping = 0,
    Login,
    Logout,
    StartGame,
    EndGame,
    CreateRoom,
    JoinRoom,
    LeaveRoom,
    UpdateRoom,
    RoomSettings,
    UpdateHost,
    KickPlayer,
}

//  CommandBytes received from Server
public enum MessageTypeReceive
{
    Ping = 0,
    Login,
    Logout,
    StartGame,
    EndGame,
    JoinRoom,
    LeaveRoom,
    UpdateRoom,
    RoomSettings,
    UpdateHost,
    KickPlayer,
    Error = 99
}

public unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public Delegates Delegates { get; private set; }
    public CommandManager Cm { get; private set; }
    public IPlayerCharacter? LocPlayer { get; set; }
    public readonly GroupManager* GM;
    
    public bool ConnectedToServer { get; set; }
    public bool Ping { get; set; }
    
    //  Server Vars
    internal MessageTypeSend MessageTypeSend;
    internal MessageTypeReceive MessageTypeReceive;
    public float LastPingSent { get; set; }
    public float LastPingReceived { get; set; }
    public int? CurrentRoomId { get; set; }
    public List<string> CurrentPlayersInRoom = new List<string>();
    
    // TCP vars
    
    public TcpClient? Client;
    public NetworkStream? Stream;
    public byte[] Buffer;
    public int BytesRead;
    public int AfkTimer = 300; //   5Mins
    
    //  Uno Vars
    private bool BInUnoGame { get; set; }
    public bool Host;
    
    //  XIV Vars
    public string XivName { get; set; }

    public float DeltaTime;
    
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

        Services.PluginInterface.UiBuilder.Draw += DrawUi;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        Services.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button that is doing the same but for the main ui of the plugin
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        
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
        Services.Log.Information("connecting to server");
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Uno.appsettings.json";

        string? serverIp;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        using (var reader = new StreamReader(stream!))
        {
            var json = reader.ReadToEnd();
            var jObject = JObject.Parse(json);
            serverIp = jObject["AppSettings"]?["ServerIP"]?.ToString();
            
        }

        if (!string.IsNullOrEmpty(serverIp))
        {
            try
            {
                Client = new TcpClient(serverIp, 6347);
                Stream = Client.GetStream();
                Buffer = new byte[1024];
                ConnectedToServer = true;
            }
            catch (Exception e)
            {
                Services.Log.Information("Uno Server was unresponsive. It might be down...");
                throw;
            }
        }
        else
        {
            Services.Log.Information("[ERROR]: Json object null.");
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
        if (!ConnectedToServer || Stream == null)
        {
            Services.Log.Information("Server not connected.");
            return;
        }
        
        var buffer = new byte[1024]; // Buffer for incoming data
        var bytesRead = Stream.Read(buffer, 0, buffer.Length);
        if (bytesRead > 0)
        {
            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
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
        var commandArgument = message[2..];
        
        var route = (MessageTypeReceive)(commandByte); 
        
        switch (route)
        {
            //  Ping = 00
            case MessageTypeReceive.Ping:
                ReceivePing();
                break;
            //  Login = 01
            case MessageTypeReceive.Login:
                ReceiveLogin(commandArgument);
                break;
            //  Logout = 02
            case MessageTypeReceive.Logout:
                ReceiveLogout(commandArgument);
                break;
            //  StartGame = 03
            case MessageTypeReceive.StartGame:
                ReceiveStartGame(commandArgument);
                break;
            //  EndGame = 04
            case MessageTypeReceive.EndGame:
                ReceiveEndGame(commandArgument);
                break;
            //  JoinRoom = 05
            case MessageTypeReceive.JoinRoom:
                ReceiveJoinRoom(commandArgument);
                break;
            //  LeaveRoom = 06
            case MessageTypeReceive.LeaveRoom:
                ReceiveLeaveRoom(commandArgument);
                break;
            //  UpdateRoom = 07
            case MessageTypeReceive.UpdateRoom:
                ReceiveUpdateRoom(commandArgument);
                break;
            //  RoomSettings = 08
            case MessageTypeReceive.RoomSettings:
                ReceiveRoomSettings(commandArgument);
                break;
            //  UpdateHost = 09
            case MessageTypeReceive.UpdateHost:
                ReceiveUpdateHost(commandArgument);
                break;
            //  KickPlayer = 10
            case MessageTypeReceive.KickPlayer:
                ReceiveKickPlayer(commandArgument);
                break;
            
            //  Error = 99
            case MessageTypeReceive.Error:
                HandleErrorMsg(commandArgument);
                break;
            default:
                Services.Log.Information("Invalid Response received.");
                break;

        }
        
    }

    
        

    /***************************
     *         SEND            *
     *         Data            *
     ***************************/
    
    // Sends data to uno server
    public void SendMsgUnsafe(byte[] message)
    {
        if (Client == null)
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
    
    //  This is called in Delegates.cs (OnFrameworkTick)
    public void SendPing()
    {
        if (!ConnectedToServer)
        {
            Services.Log.Information("Tried to send Ping while not connected to server");
            return;
        }
        
        if (Client == null)
        {
            Services.Log.Information("Server is null....Please let me know");
            ConnectedToServer = false;
            return;
        }

        SendMsg(ResponseType(MessageTypeSend.Ping, XivName));
        LastPingSent = 0;

        if (Ping) { Ping = false; }
    }

    //  Handles pings sent by the server (aka pong).
    public void ReceivePing()
    {
        LastPingReceived = 0;
    }
    
    //  This func handles all pings, sent and received, as well as logging out if the server doesnt respond to a ping.
    public void HandlePings()
    {
        
        //  If LastPingReceived is > AfkTimer
        if (LastPingReceived >= AfkTimer)
        {
            Services.Chat.PrintError("[UNO]: Server timed out, Disconnecting...Check log (/xllog)");
            Services.Log.Information("Last ping received was over 5mins ago...Pong never received.");
            SendLogout();
            ConnectedToServer = false;
            return;
        }
        
        //  If LastPingSent is > AfkTimer
        if (LastPingSent >= AfkTimer)
        {
            Services.Chat.PrintError("[UNO]: Client can't reach server, Disconnecting...Check log (/xllog)");
            Services.Log.Information("Last ping sent was over 5mins ago...Client isn't sending Pings to server.");
            SendLogout();
            ConnectedToServer = false;
            return;
        }

        //  If LastPingSent is 100 seconds from AfkTimer.
        // ReSharper disable once PossibleLossOfFraction
        if (LastPingSent >= AfkTimer - (AfkTimer / 5))
        {
            SendPing();
            Services.Log.Information("Sent Ping");
        }
    }
    
    //  This fires once connected to server.
    public void SendLogin()
    {
        ConnectToServer();
        
        SendMsg(ResponseType(MessageTypeSend.Login, $"{XivName}"));
        LastPingSent = 0;
    }
    
    //  This fires once connected to server.
    public void ReceiveLogin(string command)
    {
        ConnectedToServer = true;
        Services.Chat.Print($"{command}");
        
        LastPingReceived = 0;
    }

    //  Tells server to remove client as an active client.
    public void SendLogout()
    {
        Services.Chat.Print($"[UNO]: Disconnecting from server...");
        SendMsg(ResponseType(MessageTypeSend.Logout, $""));
    }
    
    //  Server removed client.
    public void ReceiveLogout(string command)
    {
        ConnectedToServer = false;
        Services.Chat.Print($"[UNO]: {command}");
    }
    
    
    public string SendStartGame(string command)
    {
        return "StartGame was entered";
    }
    
    public string ReceiveStartGame(string command)
    {
        return "StartGame was entered";
    }
    
    public string SendEndGame(string command)
    {
        return "EndGame was entered";
    }
    
    public string ReceiveEndGame(string command)
    {
        return "EndGame was entered";
    }
    
    public void SendCreateRoom(string maxPlayers)
    {
        SendMsg(ResponseType(MessageTypeSend.CreateRoom, $"{4.ToString()}"));
        Services.Chat.Print($"[UNO]: Creating new Room...");
    }
    
    //  Tells server user is joining an active room.
    public void SendJoinRoom(string command)
    {
        SendMsg(ResponseType(MessageTypeSend.JoinRoom, $"{UnoInterface.TypedRoomId};{UnoInterface.RoomPassword}"));
        Services.Chat.Print($"[UNO]: Attempting to join room: {command}");
    }
    
    //  This only fires if the client successfully joins a room.
    public void ReceiveJoinRoom(string command)
    {
        var parts = command.Split(";");
        var id = parts[0];
        var host = parts[1];
        
        CurrentRoomId = int.Parse(id);
        UnoInterface.TypedRoomId = (int)CurrentRoomId;

        if (host == XivName)
        {
            Host = true;
        }
        
        Services.Chat.Print($"[UNO]: Joined Room: {id}");
    }
    
    //  Tells the server to remove client.
    public void SendLeaveRoom()
    {
        SendMsg(ResponseType(MessageTypeSend.LeaveRoom, $"{UnoInterface.TypedRoomId}"));
        Services.Chat.Print($"[UNO]:Attempting to leave room: {CurrentRoomId}");
    }
    
    //  This only fires if the client successfully leaves a room.
    public void ReceiveLeaveRoom(string command)
    {
        CurrentRoomId = null;
        UnoInterface.TypedRoomId = 0;
        
        CurrentPlayersInRoom.RemoveRange(0, CurrentPlayersInRoom.Count);

        Host = false;
        
        Services.Chat.Print($"[UNO]: Left room: {command}");
    }
    
    //  Gets updated list of players in room.
    public void ReceiveUpdateRoom(string command)
    {
        var parts = command.Split(";");

        CurrentPlayersInRoom.RemoveRange(0, CurrentPlayersInRoom.Count);
        
        foreach (var part in parts)
        {

            if (part == parts.First())
            {
                var s = part;
                s = "\u2606 " + part;
                CurrentPlayersInRoom.Add(s);
                continue;
            }

            CurrentPlayersInRoom.Add(part);
        }
    }

    //  Tells server Room Settings.
    public void SendRoomSettings(string command)
    {
        SendMsg(ResponseType(MessageTypeSend.RoomSettings, $"{command}"));
        Services.Chat.Print($"[UNO]: Applying settings, waiting for server response....");
    }
    
    //  Gets Room Settings from Server.
    public void ReceiveRoomSettings(string command)
    {
        var parts = command.Split(";");
        var newMaxPlayers = int.Parse(parts[0]);
        var newHost = parts[1];

        UnoInterface.MaxPlayers = newMaxPlayers;
        
        if (newHost == XivName)
        {
            Host = true;
        }
        
        Services.Chat.Print($"[UNO]: Settings accepted. Room Settings applied.");
    }

    public void ReceiveKickPlayer(string command)
    {
        if (CurrentRoomId == null)
        {
            return;
        }
        
        if (XivName != command)
        {
            Services.Chat.Print($"[UNO]: {command} was kicked from the room");
        }

        ReceiveLeaveRoom(CurrentRoomId.ToString()!);
    }

    public void ReceiveUpdateHost(string command)
    {
        if (CurrentRoomId == null)
        {
            return;
        }
        
        if (command == XivName)
        {
            Host = true;
            Services.Chat.Print($"[UNO]: You were promoted to room's host.");
            return;
        }
        Services.Chat.Print($"[UNO]: {command} is now the host of the room.");
    }
    
    private static void HandleErrorMsg(string message)
    {
        Services.Log.Information($"[ERROR]: {message[1..]}");
        Services.Chat.PrintError("[UNO]: Error response received from server. Please check xllog (/xllog) for error message.");
    }
    public static string ResponseType(MessageTypeSend r, string message)
    {
        return $"{(int)r:D2}" + message;
    }
    
    
    
    
    private void DrawUi() => WindowSystem.Draw();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => UnoInterface.Toggle();
    
    

}
