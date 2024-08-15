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
    
    public int typedRoomId;     //  This is the RoomID the player types. not the actual ID of the room the player is in.
    public int maxPlayers;
    public int startingHand;
    
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
    
    //  UI for Uno Tab
    private void UnoTab()
    {
        using var id = ImRaii.PushId("Uno###");
        ImGuiUtil.HoverTooltip("Uno Tab");
        
        ImGui.BeginChild("Uno");
        
        
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
        else
        {
            if (plugin.CurrentRoomId != null)
            {
                ImGui.SetCursorPosX(1156);
                DrawUnoTabs();
            }
            else
            {
                ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) - 100, 20));
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Room ID###", ref typedRoomId, 0);
                ImGuiUtil.HoverTooltip("ID for the Uno room you're currently in.");
        
        
                ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) + 75, 20));
                if (ImGui.Button("Join Room"))
                {
                    plugin.SendJoinRoom(typedRoomId.ToString());
                    Services.Log.Information("Sent Join Room to Server");
                }
        
                ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) + 150, 20));
                if (ImGui.Button("Create Room"))
                {
                    plugin.SendCreateRoom(4.ToString());
                    Services.Log.Information("Sent Create room to Server");
                }
            }
        }
        ImGui.EndChild();
    }

    public void DisplayPlayers()
    {

        if (plugin.CurrentPlayersInRoom.Count < 1)
        {
            return;
        }
        
        foreach (var player in plugin.CurrentPlayersInRoom)
        {
            ImGui.PushID("player Name###");
            ImGui.SetCursorPosX(((ImGui.GetWindowWidth() / 3) * 2) - 100);
            ImGui.Text($"{player}");
        }
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
        
        ImGui.BeginChild("Uno");
        ImGui.Columns(1);

        foreach (var player in plugin.CurrentPlayersInRoom)
        {
            var size = ImGui.CalcTextSize(player);
            ImGui.SetNextItemWidth(size.X + (5 * 2));
            ImGui.SetCursorPosX(1156);
            ImGui.Text(player);
            ImGui.NextColumn();
        }
        
        ImGui.EndChild();
    }

    private void RoomSettingsTab()
    {
        using var id = ImRaii.PushId("RoomSettings###");
        ImGuiUtil.HoverTooltip("Settings for the room.");

        var players = new string[plugin.CurrentPlayersInRoom.Count];
        var selectedPlayer = plugin.CurrentPlayersInRoom[0];
        
        
        
        //  Max Players
        ImGui.SetCursorPosX(1156);
        ImGui.Text("Max Players");
        ImGui.SetNextItemWidth(100);
        ImGui.SetCursorPosX(1156);
        ImGui.InputInt("", ref maxPlayers, 0);
        
        //  Current Host
        ImGui.SetCursorPosX(1156);
        ImGui.Text("Host");
        ImGui.SetNextItemWidth(200);
        ImGui.SetCursorPosX(1156);
        if (ImGui.BeginCombo("###HostCombo",selectedPlayer))
        {
            foreach (var player in plugin.CurrentPlayersInRoom)
            {
                var isSelected = player == selectedPlayer;
                
                if (ImGui.Selectable(player, isSelected))
                {
                    selectedPlayer = player;
                }
                
                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        
        //  Send Settings
        const string buttonText = "Connect to Server";
        var textSize = ImGui.CalcTextSize(buttonText);
        ImGui.SetCursorPos(new Vector2(1156, 500));
        if (ImGui.Button("Send Settings", textSize with { Y = 25 }))
        {
            //plugin.SendRoomSettings();
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
    
    
    public override void Draw()
    {
        DrawMainTabs();
    }
}

