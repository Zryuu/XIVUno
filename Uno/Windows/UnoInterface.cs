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
    private bool bIsTurn = false, bLiveGame = false, bSetDefaultWeights = false, bCanSyncSettings = true, bReverse = false;
    private UnoCard card, currentPlayedCard;
    private List<UnoCard> locPlayerCards;
    private int numHeldCards, orderIndex;
    private int[] partynumHeldCardsCards;
    public int Number = 50, Swap = 20, Block = 10, PlusTwo = 10, PlusFour = 5, WildCard = 5;
    private float elapsedTime = 0, SyncSettingsCD = 5;
    private long gameSeed;
    private string[] MemberOrder;
    private bool[] MemberTurn;

    private Vector4 connectBuuttonTextColor;
    private string connectBuuttonText;
    
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
        using var id = ImRaii.PushId("Uno");
        ImGuiUtil.HoverTooltip("Uno Tab");
        
        ImGui.BeginChild("Uno");

        if (!plugin.BServer)
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
            plugin.SendMsg(1.ToString());
        }
        ImGui.PopStyleColor();
        
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

    private void DrawWeightsSection()
    {
        ImGui.Columns(2, "WeightsColumns", false);
            
        ImGui.Text("Numbers");
        ImGui.NextColumn();
        ImGui.SetColumnWidth(0, 100f);
        ImGui.SetNextItemWidth(100.0f);
        ImGui.InputInt("##Numbers", ref Number, 0, 0);
        ImGui.NextColumn();
            
        ImGui.Text("Swap");
        ImGui.NextColumn();
        ImGui.SetColumnWidth(1,100f);
        ImGui.SetNextItemWidth(100.0f);
        ImGui.InputInt("##Swap", ref Swap, 0, 0);
        ImGui.NextColumn();
            
        ImGui.Text("Block");
        ImGui.NextColumn();
        ImGui.SetColumnWidth(2,100f);
        ImGui.SetNextItemWidth(100.0f);
        ImGui.InputInt("##Block", ref Block, 0, 0);
        ImGui.NextColumn();
            
        ImGui.Text("+2");
        ImGui.NextColumn();
        ImGui.SetColumnWidth(3,100f);
        ImGui.SetNextItemWidth(100.0f);
        ImGui.InputInt("##+2", ref PlusTwo, 0, 0);
        ImGui.NextColumn();
            
        ImGui.Text("+4");
        ImGui.NextColumn();
        ImGui.SetColumnWidth(4,100f);
        ImGui.SetNextItemWidth(100.0f);
        ImGui.InputInt("##+4", ref PlusFour, 0, 0);
        ImGui.NextColumn();
            
        ImGui.Text("WildCard");
        ImGui.NextColumn();
        ImGui.SetColumnWidth(5,100f);
        ImGui.SetNextItemWidth(100.0f);
        ImGui.InputInt("##WildCard", ref WildCard, 0, 0);
        ImGui.NextColumn();
            
        ImGui.Columns(1);
    }
    
    public override void Draw()
    {
        DrawTabs();
    }
}

