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

    private Vector4 connectBuuttonTextColor;
    private string connectBuuttonText;
    
    public int typedRoomId;     //  This is the RoomID the player types. not the actual ID of the room the player is in.
    public int maxPlayers;
    
    public UnoSettings UnoSettings;
    
    public UnoInterface(Plugin plugin) : base("UNO###001", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;

        UnoSettings = new UnoSettings();
        card = new UnoCard();
        locPlayerCards = new List<UnoCard>();
        
        Services.GameInteropProvider.InitializeFromAttributes(this);
        
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(750, 750),
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

    private void SetRoomID()
    {
        
    }
    
    //  Draws the Tabs at the top of the Interface window.
    private void DrawTabs()
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
    
    //  UI for Uno Tab
    private void UnoTab()
    {
        using var id = ImRaii.PushId("Uno###");
        ImGuiUtil.HoverTooltip("Uno Tab");
        
        ImGui.BeginChild("Uno");

        if (!plugin.ConnectedToServer)
        { 
            connectBuuttonTextColor = new Vector4(1, 0, 0, 1);
            connectBuuttonText = "Connect";
        }
        else
        {
            connectBuuttonTextColor = new Vector4(0, 1, 0, 1);
            connectBuuttonText = "Connected";
        }
        
        ImGui.PushStyleColor(ImGuiCol.Text, connectBuuttonTextColor);
        if (ImGui.Button(connectBuuttonText, new Vector2(100, 30)))
        {
            plugin.SendLogin();
        }
        ImGui.PopStyleColor();
        
        ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) - 100, 20));
        ImGui.SetNextItemWidth(100);
        ImGui.InputInt("Room ID###", ref typedRoomId, 0);
        
        
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
        
        ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) - 100, 50));
        ImGui.Text("Players:");
        DisplayPlayers();
        
        ImGui.EndChild();

    }

    public void DisplayPlayers()
    {

        if (plugin.CurrentPlayersInRoom.Count < 1)
        {
            //Services.Log.Information("It was 0");
        }
        
        foreach (var player in plugin.CurrentPlayersInRoom)
        {

            if (plugin.CurrentPlayersInRoom.Contains(player))
            {
                continue;
            }
            
            ImGui.PushID("playername###");
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
    
    public override void Draw()
    {
        DrawTabs();
    }
}

