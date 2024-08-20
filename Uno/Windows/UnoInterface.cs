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
    public string RoomPassword;
    
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
    public void FunnyQuote(string player)
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
                ImGui.SetCursorPosX(1156);
                DrawUnoTabs();
            }
            //  Not Connected to Server
            else
            {
                ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) - 100, 20));
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Room ID###", ref TypedRoomId, 0);
                ImGuiUtil.HoverTooltip("ID for the Uno room you're currently in.");
        
        
                ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) + 75, 20));
                if (ImGui.Button("Join Room"))
                {
                    plugin.SendJoinRoom(TypedRoomId.ToString());
                    Services.Log.Information("Sent Join Room to Server");
                }
        
                ImGui.SetCursorPos(new Vector2(((ImGui.GetWindowWidth() / 3) * 2) + 150, 20));
                if (ImGui.Button("Create Room"))
                {
                    var password = "";
                    ImGui.BeginChildFrame(ImGui.GetID("CreateRoomPassword"),  new Vector2(300, 300));

                    if (ImGui.Button("Create"))
                    {
                        if (password.Length != 4)
                        {
                            
                        }
                    }
                    ImGui.SameLine();
                    ImGui.Indent(100);

                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.EndChildFrame();
                        ImGui.CloseCurrentPopup();
                        return;
                    }
                    ImGui.EndChildFrame();
                    plugin.SendCreateRoom(4.ToString());
                    Services.Log.Information("Sent Create room to Server");
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
        ImGui.Indent(1156);
        ImGui.BeginTable("Players", 1);

        ImGui.TableNextColumn();
        foreach (var player in plugin.CurrentPlayersInRoom)
        {
            var size = ImGui.CalcTextSize(player);
            ImGui.SetNextItemWidth(size.X + (5 * 2));

            //  This should prob be swapped to the server....Though it MAY cause issues since it adds a char to the name...
            if (player == plugin.CurrentPlayersInRoom.First())
            {
                var s = player;
                s = "\u2606 " + player;
            }
            
            if (plugin.Host)
            {
                TextElementWithContext(player);
            }
            else
            {
                ImGui.Text(player);
            }
            FunnyQuote(player);
            
            ImGui.TableNextRow();
        }
        
        ImGui.EndTable();
        ImGui.Unindent(1156);
        ImGui.EndChild();
    }

    private void RoomSettingsTab()
    {
        using var id = ImRaii.PushId("RoomSettings###");
        ImGuiUtil.HoverTooltip("Settings for the room.");
        
        var typedPassword = "";


        ImGui.Indent(1156);
        if (ImGui.BeginTable("Room Settings", 2))
        {
            //  Max Players
            ImGui.TableNextColumn(); // 0
            ImGui.Text("Max Players");
            ImGui.TableNextColumn(); // 1
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("", ref MaxPlayers, 0);
            
            //  Room Password
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // 0
            ImGui.Text("Room Password");
            ImGui.TableNextColumn(); // 1
            if (ImGui.InputText("", ref typedPassword, 4))
            {
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    if (typedPassword.Length != 3)
                    {
                        Services.Chat.PrintError("[UNO]: Room passwords have to be 4 characters long. Please enter a new password.");
                    }

                    RoomPassword = typedPassword;
                }
            }
            ImGuiUtil.HoverTooltip("Password has to be 4 characters long.");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // 0
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // 1
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // 0
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); // 1
            ImGui.TableNextRow();
            
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
            
            ImGui.EndTable();
        }
        ImGui.Unindent(1156);
       
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
    private static void TextElementWithContext(string name)
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

