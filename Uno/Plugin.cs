using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Windowing;
using Newtonsoft.Json.Linq;
using Uno.Cards;
using Uno.Helpers;
using Uno.Windows;

namespace Uno;

//  CONTINUE: Add SendTurn and ReceiveTurn
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
    GameSettings,
    UpdateHost,
    KickPlayer,
    Turn
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
    GameSettings,
    UpdateHost,
    KickPlayer,
    Turn,
    Error = 99
}

//  Uno Settings Struct
public struct UnoSettings
{
    public int StartingHand;
    public bool IncludeZero;
    public bool IncludeActionCards;
    public bool IncludeSpecialCards;
    public bool IncludeWildCards;

    public UnoSettings()
    {
        StartingHand = 6;
        IncludeZero = true;
        IncludeActionCards = true;
        IncludeSpecialCards = true;
        IncludeWildCards = true;
    }
}

public class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public Delegates Delegates { get; private set; }
    public CommandManager Cm { get; private set; }
    
    
    //  Server Vars
    internal MessageTypeSend MessageTypeSend;
    internal MessageTypeReceive MessageTypeReceive;
    public bool ConnectedToServer { get; set; }
    public bool Ping { get; set; }
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
    public UnoSettings UnoSettings;
    public bool isTurn { get; set; }
    public bool liveGame { get; set; }
    public bool Host;
    public List<CardBase> LocPlayerCards;
    public int[] RemotePlayersHeldCards;
    
    //  XIV Vars
    public string? XivName { get; set; }
    public IPlayerCharacter? LocPlayer { get; set; }

    //  Misc Vars
    public float DeltaTime;
    
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("UNO");
    private ConfigWindow ConfigWindow { get; init; }
    private UnoInterface UnoInterface { get; init; }

    public Plugin()
    {
        //  Initing Helpers
        PluginInterface.Create<Services>();
        Delegates     = new Delegates(this);
        Cm            = new CommandManager(this);
        
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

        UnoSettings = new UnoSettings();
        
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

        string? serverIp;
        using (var stream = assembly.GetManifestResourceStream("Uno.appsettings.json"))
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

        //  Get Username.
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

        var commands = message.Split("\n");

        foreach (var command in commands)
        {
            if (command.Length < 2)
            {
                continue;
            }
        
            var commandByte = int.Parse(command.Substring(0, 2));
            var commandArgument = command[2..];
            
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
                //  GameSettings = 09
                case MessageTypeReceive.GameSettings:
                    ReceiveGameSettings(commandArgument);
                    break;
                //  UpdateHost = 10
                case MessageTypeReceive.UpdateHost:
                    ReceiveUpdateHost(commandArgument);
                    break;
                //  KickPlayer = 11
                case MessageTypeReceive.KickPlayer:
                    ReceiveKickPlayer(commandArgument);
                    break;
                //  Turn = 12
                case MessageTypeReceive.Turn:
                    ReceiveTurn(commandArgument);
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
    public void SendMsg(string message)
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

        SendMsg(ResponseType(MessageTypeSend.Ping, XivName!));
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
        //  idk why this was still running when this bool was false.....This brute forces a fix.
        if (!ConnectedToServer)
        {
            return;
        }
        
        //  If LastPingReceived is > AfkTimer
        if (LastPingReceived >= AfkTimer)
        {
            ConnectedToServer = false;
            Services.Chat.PrintError("[UNO]: Server timed out, Disconnected from server. Check log (/xllog) for more info");
            Services.Log.Information("Last ping received more than 5 minutes ago...Client isn't receiving Pong. " +
                                     "Try restarting plugin and attempt to connect again after a few minutes.");
            SendLogout();
            return;
        }
        
        //  If LastPingSent is > AfkTimer
        if (LastPingSent >= AfkTimer)
        {
            ConnectedToServer = false;
            Services.Chat.PrintError("[UNO]: Client timed out, Disconnected from server. Check log (/xllog) for more info");
            Services.Log.Information("Last ping sent more than 5 minutes ago...Client isn't sending Pings to server." +
                                     "Try restarting plugin.");
            SendLogout();
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
        SendMsg(ResponseType(MessageTypeSend.Logout, $"logout"));
    }
    
    //  Server removed client.
    public void ReceiveLogout(string command)
    {
        ConnectedToServer = false;
        Services.Chat.Print($"[UNO]: {command}");
    }
    public void SendStartGame()
    {
        Services.Chat.Print($"[UNO]: Attempting to Start game...");
        SendMsg(ResponseType(MessageTypeSend.StartGame, $"start"));
    }
    
    public void ReceiveStartGame(string command)
    {
        Services.Log.Information("Received StartGame");
        
        //  Init Loc player cards.
        InitCards();
        
        Services.Log.Information("Inited cards");
        
        //  starting player name
        isTurn = XivName == command;
        
        liveGame = true;
        
        Services.Chat.Print($"[UNO]: GAME IS LIVE! {command} has first turn!");
    }
    
    public void SendEndGame()
    {
        Services.Chat.Print($"[UNO]: Attempting to end game...");
        SendMsg(ResponseType(MessageTypeSend.EndGame, $"end"));
    }
    
    public void ReceiveEndGame(string command)
    {
        var parts = command.Split(";");
        var forced = bool.Parse(parts[0]);
        var winner = parts[1];

        isTurn = false;
        liveGame = false;
        LocPlayerCards.Clear();
        
        if (!forced)
        {
            Services.Chat.Print($"[UNO]: Game ended. Winner: {winner}");
            return;
        }
        
        Services.Chat.Print("[UNO]: Host ended Game.");
    }

    public void SendTurn(string turnType, CardBase card)
    {
        var cardType = (int)card.GetCardType();
        var cardColor = card.GetCardColor();
        var cardNumber = card.GetCardNumber();
        
        
        Services.Log.Information($"[UNO]: Turn sent to server.");
        Services.Log.Information(ResponseType(MessageTypeSend.Turn, $"{turnType};{cardType};{cardColor};{cardNumber}"));
        
        SendMsg(ResponseType(MessageTypeSend.Turn, $"{turnType};{cardType};{cardColor};{cardNumber}"));
    }

    public void ReceiveTurn(string command)
    {
        var parts = command.Split(";");
        var turnType = parts[0];
        var type = (CardType)int.Parse(parts[1]);
        var color = int.Parse(parts[2]);
        var number = int.Parse(parts[3]);
        var name = parts[4];

        
        //  Update held cards for other players.
        switch (turnType)
        {
            case "Draw":
                for (var i = 0; i < CurrentPlayersInRoom.Count; i++)
                {
                    if (name == CurrentPlayersInRoom[i])
                    {
                        RemotePlayersHeldCards[i]++;
                    }
                }
                return;
            case "Play":
                for (var i = 0; i < CurrentPlayersInRoom.Count; i++)
                {
                    if (name == CurrentPlayersInRoom[i])
                    {
                        RemotePlayersHeldCards[i]--;
                    }
                }
                break;
        }
        
        //  Update Current Card
        switch (type)
        {
            case CardType.Number:
                UnoInterface.CurrentPlayedCard = new CardNumber();
                UnoInterface.CurrentPlayedCard.SetCardElements((CardColor)color, type, number);
                break;
            case CardType.Swap:
                UnoInterface.CurrentPlayedCard = new CardSwap();
                UnoInterface.CurrentPlayedCard.SetCardElements((CardColor)color, type, null);
                break;
            case CardType.Block:
                UnoInterface.CurrentPlayedCard = new CardBlock();
                UnoInterface.CurrentPlayedCard.SetCardElements((CardColor)color, type, null);
                break;
            case CardType.PlusTwo:
                UnoInterface.CurrentPlayedCard = new CardPlusTwo();
                UnoInterface.CurrentPlayedCard.SetCardElements((CardColor)color, type, null);
                break;
            case CardType.PlusFour:
                UnoInterface.CurrentPlayedCard = new CardPlusFour();
                UnoInterface.CurrentPlayedCard.SetCardElements((CardColor)color, type, null);
                break;
            case CardType.WildCard:
                UnoInterface.CurrentPlayedCard = new CardWild();
                UnoInterface.CurrentPlayedCard.SetCardElements(null, type, null);
                break;
        }

        isTurn = name == XivName;

        //  if its client's turn
        if (isTurn)
        {
            
            //  If Client's turn was blocked.
            if (type == CardType.Block)
            {
                isTurn = false;
                Services.Chat.Print("[UNO]: Your Turn was blocked!");
                //  PLay blocked anim or something....idk but something the player knows they got blocked.
                SendTurn("Play", UnoInterface.CurrentPlayedCard);
            }

            if (!CheckIfMatchingCard(LocPlayerCards))
            {
                SendTurn("Draw", UnoInterface.CurrentPlayedCard);
            }
            
            Services.Chat.Print("[UNO]: It's your turn!");
        }
        else
        {
            Services.Chat.Print($"[UNO]: It's {name}'s turn!");
        }
        
        
        //  Func to point or highlight the player whose turn it is.
        
    }
    
    public void SendCreateRoom(string command)
    {
        SendMsg(ResponseType(MessageTypeSend.CreateRoom, $"{command}"));
        Services.Chat.Print($"[UNO]: Creating new Room...");
    }
    
    //  Tells server user is joining an active room.
    public void SendJoinRoom(string command)
    {
        SendMsg(ResponseType(MessageTypeSend.JoinRoom, $"{command}"));
        Services.Chat.Print($"[UNO]: Attempting to join room: {command}");
    }
    
    //  This only fires if the client successfully joins a room.
    public void ReceiveJoinRoom(string command)
    {
        var parts = command.Split(";");
        var id = parts[0];
        var host = parts[1];
        
        CurrentRoomId = int.Parse(id);
        Services.Log.Information($"ID var: {CurrentRoomId}");
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
        SendMsg(ResponseType(MessageTypeSend.LeaveRoom, $"{CurrentRoomId}"));
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
            
            /*
            if (part == parts.First())
            {
                var s = part;
                s = "\u2606 " + part;
                CurrentPlayersInRoom.Add(s);
                continue;
            }
            */
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

    //  Sends the Game Settings to the Server.
    public void SendGameSettings(string command)
    {
        SendMsg(ResponseType(MessageTypeSend.GameSettings, $"{command}"));
        Services.Chat.Print($"[UNO]: Applying game settings, waiting for server response....");
    }

    //  Gets the Game Settings from the Server.
    public void ReceiveGameSettings(string command)
    {
        var parts = command.Split(";");
        var starting = int.Parse(parts[0]);
        var zero = bool.Parse(parts[1]);
        var action = bool.Parse(parts[2]);
        var special = bool.Parse(parts[3]);
        var wild = bool.Parse(parts[4]);

        UnoSettings.StartingHand = starting;
        UnoSettings.IncludeZero = zero;
        UnoSettings.IncludeActionCards = action;
        UnoSettings.IncludeSpecialCards = special;
        UnoSettings.IncludeWildCards = wild;

        Services.Chat.Print("[UNO]: Updated Game Settings.");
    }

    //  Sends Kick Player to Server.
    public void SendKickPlayer(string command)
    {
        if (CurrentRoomId == null || !Host)
        {
            Services.Chat.PrintError($"[UNO]: Unable to kick player. You're not in a valid room or not the room's host.");
            return;
        }
        
        Services.Chat.Print("[UNO]: Attempting to Kick Player...");
        SendMsg(ResponseType(MessageTypeSend.KickPlayer, $"{command}"));
    }
    
    //  Receives Kick Player response from Server.
    public void ReceiveKickPlayer(string command)
    {
        if (CurrentRoomId == null)
        {
            return;
        }
        
        if (XivName == command)
        {
            ReceiveLeaveRoom(CurrentRoomId.ToString()!);
            Services.Chat.Print($"[UNO]: You were kicked from the room.");
            return;
        }

        Services.Chat.Print($"[UNO]: {command} was kicked from the room");
    }

    public void SendPromoteHost(string command)
    {
        // Checks if host.
        if (!Host)
        {
            Services.Chat.PrintError($"[UNO]: Couldn't Promote Host. Please check log for more info (/xllog)");
            Services.Log.Information($"[ERROR]: Couldn't send command PromoteHost. Not Host of a room.");
            return;
        }
        
        SendMsg(ResponseType(MessageTypeSend.UpdateHost, $"{command}"));
        
    }
    
    //  Receives Update Host Response from Server.
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
        Services.Log.Information($"[ERROR]: {message}");
        Services.Chat.PrintError("[UNO]: Error response received from server. Please check xllog (/xllog) for error message.");
    }
    public static string ResponseType(MessageTypeSend r, string message)
    {
        return $"{(int)r:D2}" + message;
    }
    
    
    /***************************
     *          Uno            *
     *         Logic           *
     ***************************/


    //  This only handles Local player cards. Remote Player cards are handled in UnoInterface.cs
    public void InitCards()
    {
        //  Local Player cards.
        LocPlayerCards = new List<CardBase>(UnoSettings.StartingHand);
        var random = new Random();
        
        for (var i = 0; i < UnoSettings.StartingHand; i++)
        {
            var ran = random.Next(1, 6);
            switch (ran)
            {
                case 1:
                    var number = new CardNumber();
                    number.SetPossibleCards(UnoSettings.IncludeZero, UnoSettings.IncludeSpecialCards, 
                                            UnoSettings.IncludeActionCards, UnoSettings.IncludeWildCards);
                    LocPlayerCards.Add(number);
                    break;
                
                case 2: 
                    var block = new CardBlock();
                    block.SetPossibleCards(UnoSettings.IncludeZero, UnoSettings.IncludeSpecialCards, 
                                           UnoSettings.IncludeActionCards, UnoSettings.IncludeWildCards);
                    LocPlayerCards.Add(block);
                    break;
                
                case 3: 
                    var swap = new CardSwap();
                    swap.SetPossibleCards(UnoSettings.IncludeZero, UnoSettings.IncludeSpecialCards, 
                                          UnoSettings.IncludeActionCards, UnoSettings.IncludeWildCards);
                    LocPlayerCards.Add(swap);
                    break;
                
                case 4: 
                    var plusFour = new CardPlusFour();
                    plusFour.SetPossibleCards(UnoSettings.IncludeZero, UnoSettings.IncludeSpecialCards, 
                                              UnoSettings.IncludeActionCards, UnoSettings.IncludeWildCards);
                    LocPlayerCards.Add(plusFour);
                    break;
                
                case 5: 
                    var plusTwo = new CardPlusTwo();
                    plusTwo.SetPossibleCards(UnoSettings.IncludeZero, UnoSettings.IncludeSpecialCards, 
                                                 UnoSettings.IncludeActionCards, UnoSettings.IncludeWildCards);
                    LocPlayerCards.Add(plusTwo);
                    break;
                
                case 6: 
                    var wild = new CardWild();
                    wild.SetPossibleCards(UnoSettings.IncludeZero, UnoSettings.IncludeSpecialCards, 
                                              UnoSettings.IncludeActionCards, UnoSettings.IncludeWildCards);
                    LocPlayerCards.Add(wild);
                    break;
            }
        }

        RemotePlayersHeldCards = new int[CurrentPlayersInRoom.Count];

        for (var i = 0; i < RemotePlayersHeldCards.Length; i++)
        {
            RemotePlayersHeldCards[i] = UnoSettings.StartingHand;
        }
    }

    public void SetCurrentPlayedCard(CardBase newCard)
    {
        UnoInterface.CurrentPlayedCard = newCard;
    }

    public bool CheckIfMatchingCard(List<CardBase> cards)
    {
        var currentCard = UnoInterface.CurrentPlayedCard;
        foreach (var card in cards)
        {
                    
            //  If picked card is a wild card.
            if (card.GetCardType() == CardType.WildCard)
            {
                return true;
            }
            //  If Card colors match
            if (card.GetCardColor() == currentCard.GetCardColor())
            {
                return true;
            }
            //  If Card numbers match (and they aren't -1)
            if (currentCard.GetCardNumber() != -1 && card.GetCardNumber() == currentCard.GetCardNumber())
            {
                return true;
            }
        }

        return false;
    }

    public bool CheckIfCardMatches(CardBase card)
    {
        var currentCard = UnoInterface.CurrentPlayedCard;

        //  If its first move
        if (currentCard == new CardBack())
        {
            return true;
        }
        
        //  If currentCard = wild, ture
        if (currentCard.GetCardType() == CardType.WildCard)
        {
            return true;
        }
        
        //  If card = wild, ture;
        if (card.GetCardType() == CardType.WildCard)
        {
            return true;
        }
        
        //  If colors match, true
        if (card.GetCardColor() == currentCard.GetCardColor())
        {
            return true;
        }
        
        //  If Card numbers match (and they aren't -1), true
        if (currentCard.GetCardNumber() != -1 && card.GetCardNumber() == currentCard.GetCardNumber())
        {
            return true;
        }
        
        return false;
    }
    
    private void DrawUi() => WindowSystem.Draw();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => UnoInterface.Toggle();
    
    

}
