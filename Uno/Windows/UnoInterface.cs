using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Uno.Helpers;
using OtterGui;
using ImRaii = OtterGui.Raii.ImRaii;

namespace Uno.Windows;

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


public unsafe class UnoInterface: Window, IDisposable
{
    private Plugin plugin;
    private bool bIsTurn = false, bLiveGame = false;
    private UnoCard card, currentPlayedCard;
    private List<UnoCard> locPlayerCards;
    private int numHeldCards;
    private int[] partynumHeldCardsCards;
    private float elapsedTime = 0;
    private int gameSeed;
    
    public int TypedRoomId;     //  This is the RoomID the player types. not the actual ID of the room the player is in.
    public int MaxPlayers;
    public string RoomPassword = "";

    public bool ShowJoinRoomWindow = false;
    public bool ShowCreateRoomWindow = false;
    public bool RoomPrivate;
    
    public UnoSettings UnoSettings;
    
    public UnoInterface(Plugin plugin) : base("UNO###001", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;

        UnoSettings = new UnoSettings();
        card = new UnoCard();
        locPlayerCards = new List<UnoCard>();
        
        Services.GameInteropProvider.InitializeFromAttributes(this);
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1445, 750),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
    }

    //  This is a joke and will prob get taken out later or put into an external json file
    public static void FunnyQuote(string player)
    {
        switch (player)
        {
            case "Airi Amorette" or "Kin Tsubaki":
                ImGuiUtil.HoverTooltip("'I Love role playing big tiddy bara men. THERE I SAID IT' - Airi 2024");
                break;
            case "Sven Grimore" or "Sven Grimore":
                ImGuiUtil.HoverTooltip("Waiting for quote");
                break;
            
            
            
            
            
            case "Ben Gibson" or "Alacea Na'Sha" or "Alacea Na'Sha" or "Sho Nan" or "Bao Sen":
                ImGuiUtil.HoverTooltip("This name makes me think you've read my favorite book series." +
                                       "\nIf you know who Azarin's Sister's name or Ben's wife's name," +
                                       "\nMessage me on Discord and I'll add you and give you a special name in the plugin." +
                                       "\nYou can find me in the Dalamud's Discord with my GitHub name");
                break;
        }
    }
    
    
    public void Dispose() { }

    
    /***************************
     *          UNO            *
     *          LOGIC          *
     ***************************/

    
    
    
    
    /***************************
     *                         *
     *          UI             *
     *                         *
     ***************************/
    
    //  Draws the Tabs at the top of the Interface window.
    private void DrawMainTabs()
    {
        using var tabBar = ImRaii.TabBar("MainMenuTabs###", ImGuiTabBarFlags.Reorderable);
        
        if (!tabBar)
        {
            Services.Log.Error("[ERROR]: ImRaii TabBar isn't valid. Please tell me about this error.");
            return;
        }

        if (ImGui.BeginTabItem("Uno"))
        {
            UnoTab();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Settings"))
        {
            SettingsTab();
            ImGui.EndTabItem();
        }
    }

    private void DrawUnoTabs()
    {
        using var tabBar = ImRaii.TabBar("UnoMenuTabs###", ImGuiTabBarFlags.Reorderable);
        
        if (ImGui.BeginTabItem("Players"))
        {
            PlayersTab();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Room Settings"))
        {
            RoomSettingsTab();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Game Settings"))
        {
            GameSettingsTab();
            ImGui.EndTabItem();
        }
        
    }

    private void CreateRoomChildWindow()
    {
        
        ImGui.Begin("Create Room", ref ShowCreateRoomWindow, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize);

        if (ImGui.RadioButton("Public", !RoomPrivate))
        {
            RoomPrivate = false;
        }
        ImGui.Indent(50);
        ImGui.SameLine();
        if (ImGui.RadioButton("Private", RoomPrivate))
        {
            RoomPrivate = true;
        }
        ImGui.Unindent(50);
        
        if (RoomPrivate)
        {
            ImGui.SetNextItemWidth(100);
            ImGui.InputText("Password", ref RoomPassword, 4);
            ImGuiUtil.HoverTooltip("Enter a 4 character password for the room.");
        }
        
        ImGui.Dummy(new Vector2(0, 10));
                
        if (ImGui.Button("Create Room###CreateRoomSmallWindow"))
        {
            if (RoomPassword.Length != 4 && RoomPrivate)
            {
                Services.Chat.PrintError($"[Error]: Password required to be 4 characters.");
            }
            else
            {
                MaxPlayers = 4;
                plugin.SendCreateRoom($"{MaxPlayers};{RoomPassword}");
                Services.Log.Information("Sent Create room to Server");
                ShowCreateRoomWindow = false;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel###CreateRoomSmallWindow"))
        {
            ShowJoinRoomWindow = false;
        }
        
        ImGui.End();
    }
    
    private void JoinRoomChildWindow()
    {
        ImGui.Begin("Join Room", ref ShowJoinRoomWindow, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize);
                    
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Room ID", ref TypedRoomId, 0);
        ImGuiUtil.HoverTooltip("ID for the Uno room you're currently in.");
                
        ImGui.SetNextItemWidth(100);
        ImGui.InputText("Room Password", ref RoomPassword, 4);
        ImGuiUtil.HoverTooltip("Password for the room.");
                
        ImGui.Dummy(new Vector2(0, 10));
        ImGui.Indent(100);
                
        if (ImGui.Button("Join Room###JoinRoomSmallWindow"))
        {
            plugin.SendJoinRoom($"{TypedRoomId};{RoomPassword}");
            Services.Log.Information("Sent Join Room to Server");
            ShowJoinRoomWindow = false;
        }

        if (ImGui.Button("Cancel"))
        {
            ShowJoinRoomWindow = false;
        }
                    
        ImGui.End();
    }
    
    
    //  UI for Uno Tab
    private void UnoTab()
    {
        using var id = ImRaii.PushId("Uno###");
        ImGuiUtil.HoverTooltip("Uno Tab");
        
        ImGui.BeginChild("Uno");
        
        //  Not Connected to Server.
        if (!plugin.ConnectedToServer)
        { 
            var color = new Vector4(1, 0, 0, 1);
            const string buttonText = "Connect to Server";
            
            var textSize = ImGui.CalcTextSize(buttonText);
        
            var windowSize = ImGui.GetWindowSize();
            var buttonWidth = textSize.X + (ImGui.GetStyle().FramePadding.X * 2);
            var buttonHeight = textSize.Y + (ImGui.GetStyle().FramePadding.Y * 2);

            var buttonPosX = (windowSize.X - buttonWidth) * 0.5f;
            var buttonPosY = (windowSize.Y - buttonHeight) * 0.5f;
            
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.SetCursorPos(new Vector2(buttonPosX, buttonPosY));
            if (ImGui.Button(buttonText, new Vector2(buttonWidth, buttonHeight)))
            {
                plugin.SendLogin();
            }
            ImGui.PopStyleColor();
            
        }
        //  Connected to Server.
        else
        {
            //  Connected to Room
            if (plugin.CurrentRoomId != null)
            {
                ImGui.Indent(1156);
                DrawUnoTabs();
            }
            //  Not Connected to Server
            else
            {
                if (ShowJoinRoomWindow)
                {
                    ImGui.SetNextWindowPos(ImGui.GetMousePos(), ImGuiCond.Appearing);
                    JoinRoomChildWindow();
                }

                if (ShowCreateRoomWindow)
                {
                    ImGui.SetNextWindowPos(ImGui.GetMousePos(), ImGuiCond.Appearing);
                    CreateRoomChildWindow();
                }
                
                ImGui.Dummy(new Vector2(0, 25));
                
                if (ImGui.Button("Join Room###JoinRoom"))
                {
                    ShowJoinRoomWindow = true;
                }
                
                ImGui.Indent(75);
                ImGui.SameLine();
                
                if (ImGui.Button("Create Room###CreateRoom"))
                {
                    ShowCreateRoomWindow = true;
                }
            }
        }
        ImGui.EndChild();
    }
    
    //  UI for Settings Tab
    private void SettingsTab()
    {
     
        using var id = ImRaii.PushId("Settings");
        ImGuiUtil.HoverTooltip("Settings Tab");
        
        ImGui.BeginChild("Settings");
        
        ImGui.EndChild();

    
    }

    private void PlayersTab()
    {
        using var id = ImRaii.PushId("Players###");
        ImGuiUtil.HoverTooltip("List of players present in room.");
        
        ImGui.BeginChild("PlayerList");
        ImGui.BeginTable("Players", 1);

        ImGui.TableNextColumn();
        foreach (var player in plugin.CurrentPlayersInRoom)
        {
            var size = ImGui.CalcTextSize(player);
            ImGui.SetNextItemWidth(size.X + (5 * 2));

            //  This should prob be swapped to the server....Though it MAY cause issues since it adds a char to the name...
            /*
            if (player == plugin.CurrentPlayersInRoom.First())
            {
                var s = player;
                s = "\u2606 " + player;
            }
            */
            
            if (plugin.Host && player != plugin.XivName)
            {
                TextElementWithContext(player);
            }
            else
            {
                ImGui.Text(player);
            }
            FunnyQuote(player); //  This will get changed with a Json file.
            
            ImGui.TableNextRow();
        }
        
        ImGui.EndTable();
        ImGui.EndChild();
    }

    private void RoomSettingsTab()
    {
        using var id = ImRaii.PushId("RoomSettings###");
        ImGuiUtil.HoverTooltip("Settings for the room.");
        
        if (ImGui.BeginTable("Room Settings", 2))
        {
            //  Max Players
            ImGui.TableNextColumn(); // 0
            ImGui.Text("Max Players");
            ImGui.TableNextColumn(); // 1
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("", ref MaxPlayers, 0);
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // 0
            
            //  Send Settings
            if (plugin.Host)
            {
                const string buttonText = "Apply Settings";
                var textSize = ImGui.CalcTextSize(buttonText);
                if (ImGui.Button("Apply Settings", new Vector2(textSize.X + 10, 25)))
                {
                    //  Format {maxPlayers};{NewHost}. using ; as separator
                    plugin.SendRoomSettings($"{MaxPlayers};{RoomPassword}");
                }
                ImGuiUtil.HoverTooltip("Updates room settings.");
            }

            // Leave Room
            ImGui.TableNextColumn(); // 1
            if (ImGui.Button("Leave", new Vector2(ImGui.CalcTextSize("Leave").X + 10, 25)))
            {
                plugin.SendLeaveRoom();
            }
            
            ImGui.TableNextColumn();
            ImGui.TableNextRow();
            ImGui.Text($"Room ID: {plugin.CurrentRoomId.ToString()}");
            
            ImGui.EndTable();
        }
       
    }

    private void GameSettingsTab()
    {
        using var id = ImRaii.PushId("GameSettings###");
        ImGuiUtil.HoverTooltip("Settings for the game.");
        
        ImGui.SetCursorPosX(1156);
        ImGui.Columns(2);
        
        ImGui.NextColumn();
    }

    //  Makes a Text Element with a context menu.
    private void TextElementWithContext(string name)
    {
        
        ImGui.Text(name);
        
        if (ImGui.BeginPopupContextItem(name+"ContextMenu###"))
        {
            if (ImGui.MenuItem("Kick Player"))
            {
                Services.Chat.Print("It worked");
            }
            if (ImGui.MenuItem("Promote to Host"))
            {
                Services.Chat.Print("It also worked");
            }

            ImGui.EndPopup();
        }
    }
    
    public override void Draw()
    {
        DrawMainTabs();
    }
}

