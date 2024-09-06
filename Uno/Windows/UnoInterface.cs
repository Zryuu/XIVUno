using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Uno.Helpers;
using OtterGui;
using Uno.Cards;
using ImRaii = OtterGui.Raii.ImRaii;

namespace Uno.Windows;




public unsafe class UnoInterface: Window, IDisposable
{
    private Plugin plugin;
    
    //  UI settings
    public int TypedRoomId; //  This is the RoomID the player types. not the actual ID of the room the player is in.
    public float DeckPosY = 600f;

    
    public Assembly ass = Assembly.GetExecutingAssembly();  //  This should prob be moved to the Ctor
    
    public CardBase CurrentPlayedCard = new CardBack();

    
    //  Room Settings
    public int MaxPlayers;
    public string RoomPassword = "";
    public bool ShowJoinRoomWindow;
    public bool ShowCreateRoomWindow;
    public bool RoomPrivate;
    
    //  These aren't used currently but might be later.
    private float elapsedTime = 0;
    private int gameSeed; 
    
    
    public UnoInterface(Plugin plugin) : base("UNO###001", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        
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
            
            case "Ben Gibson" or "Alacea Na'Sha" or "Patrica Kim" or "Sho Nan" or "Bao Sen":
                ImGuiUtil.HoverTooltip("This name makes me think you've read my favorite book series." +
                                       "\nIf you know either Azarin's Sister's name or Ben's wife's name," +
                                       "\nMessage me on Discord and I'll add you and give you a special name in the plugin." +
                                       "\nDiscord name: .zryu");
                break;
        }
    }
    
    
    public void Dispose() { }

    
    /***************************
     *          UNO            *
     *          UI             *
     ***************************/

    public void DrawCurrentPlayedCard(CardBase card)
    {
        var texture = Services.TextureProvider.GetFromManifestResource(ass, card.Dir).GetWrapOrDefault();

        if (texture != null)
        {
            card.SetCardTexture(texture);
            ImGui.Image(card.Texture!.ImGuiHandle, new Vector2(130, 182));
        }
    }

    public void DrawCurrentDeck(List<CardBase> deck)
    {
        ImGui.SetCursorPos(new Vector2(ImGui.GetWindowWidth() / 4, DeckPosY));
        
        for (var i = 0; i < deck.Count; i++)
        {
            
            ImGui.PushID($"card###{i}");
            
            //  Setting Card Texture
            var texture = Services.TextureProvider.GetFromManifestResource(ass, deck[i].Dir).GetWrapOrDefault();

            if (texture != null)
            {
                deck[i].SetCardTexture(texture);
                var hoveredY = HoverLerp(DeckPosY, DeckPosY - deck[i].HoverAmount, deck[i].HoverLerpAlpha);
                deck[i].Y = hoveredY;
                
                ImGui.SetCursorPosY(deck[i].Y);
            
                //  Card clicked
                if (ImGui.ImageButton(deck[i].Texture!.ImGuiHandle, new Vector2(80, 140)))
                {
                    if (!plugin.isTurn)
                    {
                        Services.Chat.Print("[UNO]: Please wait your turn...");
                        return;
                    }
                    
                    if (plugin.CheckIfCardMatches(deck[i]))
                    {
                        plugin.SendTurn("Play", deck[i]);
                    }
                    else
                    {
                        Services.Chat.Print("[UNO]: Card can't be played (Card doesn't have any similarities to last played card).");
                    }
                }
            }
            
            //  Card is hovered
            if (ImGui.IsItemHovered())
            {
                deck[i].CardWasHovered = true;
                deck[i].Y = HoverLerp(deck[i].Y, DeckPosY - deck[i].HoverAmount, deck[i].HoverLerpAlpha);
                deck[i].HoverLerpAlpha += deck[i].HoverLerpSpeed;
                deck[i].HoverLerpAlpha = Math.Clamp(deck[i].HoverLerpAlpha, 0f, 1f);
                
                //  Play hover up anim
            }

            //  Card was hovered
            if (deck[i].CardWasHovered && !ImGui.IsItemHovered())
            {
                deck[i].Y = HoverLerp(deck[i].Y, DeckPosY - deck[i].HoverAmount, deck[i].HoverLerpAlpha, true);
                deck[i].HoverLerpAlpha -= deck[i].HoverLerpSpeed;
                deck[i].HoverLerpAlpha = Math.Clamp(deck[i].HoverLerpAlpha, 0f, 1f);

                if (Math.Abs(deck[i].Y - DeckPosY) < 0.001)
                {
                    deck[i].CardWasHovered = false;
                    deck[i].HoverLerpAlpha = 0;
                }
            }
            
            ImGui.PopID();
            
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();
        }
    }
    
    public void DrawRemotePlayersCards()
    {

        foreach (var player in plugin.CurrentPlayersInRoom)
        {
            
        }
        
        foreach (var playerCards in plugin.RemotePlayersHeldCards)
        {
            for (var i = 0; i < playerCards; i++)
            {
                
            }
        }
    }

    public float HoverLerp(float start, float end, float alpha, bool reverse = false)
    {
        if (reverse)
        {
            return end + ((start - end) * alpha);
        }
        
        return start + ((end - start) * alpha);
    }
    
    /***************************
     *          UI             *
     ***************************/
    
    //  Draws the Tabs at the top of the Main window.
    private void DrawMainWindowTabs()
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
                using (ImRaii.Group())
                {
                    //  Uno Game is live
                    if (plugin.liveGame)
                    {
                        
                        //  End Game 
                        if (plugin.Host)
                        {
                            if (ImGui.Button("End Game"))
                            {
                                plugin.SendEndGame();
                            }
                        }
                        
                        //  CONTINUE: Check if this works correctly.
                        ImGui.Dummy(new Vector2(0, ImGui.GetWindowHeight() / 4));
                        ImGui.Indent(578);
                        DrawCurrentPlayedCard(CurrentPlayedCard);
                        
                        DrawCurrentDeck(plugin.LocPlayerCards);

                    }
                    else //  Uno Game isn't live
                    {
                        ImGui.Indent(578);

                        if (plugin.Host)
                        {
                            if (ImGui.Button("Start Game!"))
                            {
                                plugin.SendStartGame();
                            }
                        }
                        else
                        {
                            ImGui.Text("Waiting for Host to Start Game!");
                        }
                        ImGui.Unindent(578);
                        ImGui.SameLine();
                        using (ImRaii.Group())
                        {
                            ImGui.Indent(500);
                            DrawRoomTabs();
                        }
                    }
                    
                }
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
    
    //  Child Window for Creating a new room.
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
    
    //  Child Window for Joining a room.
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

    //  Draws Room Tabs.
    private void DrawRoomTabs()
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
    
    
    private void PlayersTab()
    {
        using var id = ImRaii.PushId("Players###");
        ImGuiUtil.HoverTooltip("List of players present in room.");
        
        ImGui.BeginChild("PlayerList");
        if (ImGui.BeginTable("Players", 1))
        {
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
        }
        ImGui.EndChild();
    }

    private void RoomSettingsTab()
    {
        using var id = ImRaii.PushId("RoomSettings###");
        ImGuiUtil.HoverTooltip("Settings for the room.");
        
        ImGui.BeginChild("RoomSettings");
        
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
        
        ImGui.EndChild();
    }

    private void GameSettingsTab()
    {
        using var id = ImRaii.PushId("GameSettings###");
        ImGuiUtil.HoverTooltip("Settings for the game.");
        ImGui.BeginChild("GameSettings");

        //  Host Table
        if (plugin.Host)
        {
            if (ImGui.BeginTable("Game Settings###Host", 2))
            {
                //  Starting Hand
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Starting Hand");
                ImGuiUtil.HoverTooltip("How many cards each player starts with?");
                ImGui.TableNextColumn(); // 1
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("", ref plugin.UnoSettings.StartingHand, 0);
                
                ImGui.TableNextRow();
                
                //  Include Zero
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Zero Cards?");
                ImGuiUtil.HoverTooltip("Includes cards with Zero.");
                ImGui.TableNextColumn(); // 1
                ImGui.Checkbox("", ref plugin.UnoSettings.IncludeZero);
                
                ImGui.TableNextRow();
                
                //  Include Special
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Special Cards?");
                ImGuiUtil.HoverTooltip("Includes +2 and +4 cards.");
                ImGui.TableNextColumn(); // 1
                ImGui.Checkbox("", ref plugin.UnoSettings.IncludeSpecialCards);
                
                ImGui.TableNextRow();
                
                //  Include Action
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Zero Cards?");
                ImGuiUtil.HoverTooltip("Includes Swap and Block cards.");
                ImGui.TableNextColumn(); // 1
                ImGui.Checkbox("", ref plugin.UnoSettings.IncludeActionCards);
                
                ImGui.TableNextRow();
                
                //  Include Wild
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Zero Cards?");
                ImGuiUtil.HoverTooltip("Includes Color swap cards.");
                ImGui.TableNextColumn(); // 1
                ImGui.Checkbox("", ref plugin.UnoSettings.IncludeWildCards);
                
                ImGui.TableNextRow();
                
                //  Apply Settings
                ImGui.NextColumn(); // 0
                if (ImGui.Button("Apply Settings"))
                {
                 
                    Services.Chat.PrintError("[UNO]: Unable to change these settings as they're currently under construction. Please check back later.");
                    
                    /*
                    if (!plugin.Host)
                    {
                        Services.Chat.PrintError("[UNO]: Only the Room's host can apply settings.");
                    }
                    else
                    {
                        plugin.SendGameSettings($"{plugin.UnoSettings.StartingHand};{plugin.UnoSettings.IncludeZero};{plugin.UnoSettings.IncludeActionCards};{plugin.UnoSettings.IncludeSpecialCards};{plugin.UnoSettings.IncludeWildCards}");
                    }
                    */
                }
                ImGuiUtil.HoverTooltip("Applies Settings to the room. [CURRENTLY UNAVAILABLE]");
                ImGui.EndTable();
            }
            
        }
        
        //  Visitor Table
        else
        {
            if (ImGui.BeginTable("Game Settings###Visitor", 2))
            {
                //  Starting Hand
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Starting Hand");
                ImGuiUtil.HoverTooltip("How many cards each player starts with?");
                ImGui.TableNextColumn(); // 1
                ImGui.SetNextItemWidth(100);
                ImGui.Text($"{plugin.UnoSettings.StartingHand}");
                
                ImGui.TableNextRow();
                
                //  Include Zero
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Zero Cards?");
                ImGuiUtil.HoverTooltip("Includes cards with Zero.");
                ImGui.TableNextColumn(); // 1
                ImGui.Text($"{plugin.UnoSettings.IncludeZero}");
                
                ImGui.TableNextRow();
                
                //  Include Special
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Special Cards?");
                ImGuiUtil.HoverTooltip("Includes +2 and +4 cards.");
                ImGui.TableNextColumn(); // 1
                ImGui.Text($"{plugin.UnoSettings.IncludeSpecialCards}");
                
                ImGui.TableNextRow();
                
                //  Include Action
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Zero Cards?");
                ImGuiUtil.HoverTooltip("Includes Swap and Block cards.");
                ImGui.TableNextColumn(); // 1
                ImGui.Text($"{plugin.UnoSettings.IncludeActionCards}");
                
                ImGui.TableNextRow();
                
                //  Include Wild
                ImGui.TableNextColumn(); // 0
                ImGui.Text("Include Zero Cards?");
                ImGuiUtil.HoverTooltip("Includes Color swap cards.");
                ImGui.TableNextColumn(); // 1
                ImGui.Text($"{plugin.UnoSettings.IncludeWildCards}");
                
                ImGui.TableNextRow();
                
                //  Apply Settings
                ImGui.NextColumn(); // 0

                if (ImGui.Button("Apply Settings"))
                {
                    Services.Chat.PrintError("[UNO]: Only the Room's host can apply settings.");
                }
                ImGuiUtil.HoverTooltip("Only the Room's host can apply settings.");
                
                ImGui.EndTable();
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
        
        ImGui.SetCursorPos(new Vector2(ImGui.GetWindowWidth(), ImGui.GetWindowHeight()));
        ImGui.Text("Under Construction, Come back later.");
        
        ImGui.EndChild();
    }
    
    //  Makes a Text Element with a context menu.
    private void TextElementWithContext(string name)
    {
        
        ImGui.Text(name);
        
        if (ImGui.BeginPopupContextItem(name+"ContextMenu###"))
        {
            if (ImGui.MenuItem("Kick Player"))
            {
                plugin.SendKickPlayer(name);
            }
            if (ImGui.MenuItem("Promote to Host"))
            {
                plugin.SendPromoteHost(name);
            }
        }
        ImGui.EndPopup();
    }
    
    public override void Draw()
    {
        DrawMainWindowTabs();
    }
}

