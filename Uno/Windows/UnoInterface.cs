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
    public NetworkStream stream;
    public byte[] buffer;
    public int bytesRead;
    
    public UnoSettings UnoSettings;
    
    public UnoInterface(Plugin plugin) : base("UNO###001", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;

        UnoSettings = new UnoSettings();
        card = new UnoCard();
        locPlayerCards = new List<UnoCard>();
        
        stream = plugin.client.GetStream();
        buffer = new byte[1024];
        
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

    //  Handles the turn for local player.
    public void HandleTurnLocal(UnoCard passedCard)
    {
        if (!bIsTurn)
        {
            Services.Chat.PrintError("[UNO]: Please wait until its your turn before choosing a card...");
            return;
        }
        
        currentPlayedCard ??= passedCard;
        
        if (card.CardInfo.CardType == currentPlayedCard.CardInfo.CardType 
            || card.CardInfo.CardColor == currentPlayedCard.CardInfo.CardColor 
            || card.CardInfo.Number == currentPlayedCard.CardInfo.Number)
        {
            foreach (var c in locPlayerCards)
            {
                if (c == passedCard)
                {
                    locPlayerCards.Remove(c);
                    
                    if (numHeldCards > 2)
                    {
                        numHeldCards--;
                        break;
                    }
                    else
                    {
                        //  Make player hit button that says "UNO"; 
                    }

                    break;
                }
            }
        }
        
        SendTurn(passedCard);
        
    }

    public void DrawCard()
    {

        if (!bIsTurn)
        {
            Services.Chat.PrintError("[UNO]: Cannot draw will its not your turn");
            return;
        }
        
        var newcard = new UnoCard();
        RandomizeCard(newcard);
        locPlayerCards.Add(newcard);
        numHeldCards = locPlayerCards.Count;

        SendDrawCard();
    }
    
    //  Calculates Total Weight so the text can be the correct color.
    private int  CalculateTotalWeight()
    {
        var totalWeight = Number + Swap + Block + PlusTwo + PlusFour + WildCard;

        return totalWeight;
    }
    
    //  Inits cards in hand (Randoms them and adds the amount listed in the Starting Hand settings)
    private void InitHeldCards()
    {

        if (numHeldCards == UnoSettings.StartingHand)
        {
            return;
        }
        
        numHeldCards = UnoSettings.StartingHand;
        
        for (var i = 0; i < numHeldCards; i++)
        {
            var newcard = new UnoCard();
            
            RandomizeCard(newcard);
            locPlayerCards.Add(newcard);
        }
        
        foreach (var member in plugin.PartyMembers!)
        {
            if (member.ToString() == plugin.LocPlayerName)
            {
                return;
            }
            
            for (var i = 0; i < UnoSettings.StartingHand; i++)
            {
                partynumHeldCardsCards[i]++;
            }
        }
    }

    //  Gets Party Order
    private string GetCurrentPartyOrder(SeString[]? members)
    {

        if (members == null)
        {
            Services.Chat.PrintError("[UNO]: Couldn't get Party Members. Please check log..");
            Services.Log.Information("[ERROR]: UNO::UnoInterface::GetCurrentPartyOrder:: " +
                                     "SeString parameter is null.");
        }
        
        var returnString = "";
        
        foreach (var member in members!)
        {

            if (member == members.Last())
            {
                returnString += member.ToString();
            }
            else
            {
                returnString += member + ";";
            }

        }
        return returnString;
    }
    
    //  Returns Weights to default.
    private void SetCardSettingsWithDefaultWeights()
    {
        
        switch (UnoSettings)
        {
            //  Type
            case { IncludeActionCards: false, IncludeSpecialCards: false, IncludeWildCards: false }:
            {
                card.SetCardsWeight(CardType.Number, 100);
                card.SetCardsWeight(CardType.Swap, 0);
                card.SetCardsWeight(CardType.Block, 0);
                card.SetCardsWeight(CardType.PlusTwo, 0);
                card.SetCardsWeight(CardType.PlusFour, 0);
                card.SetCardsWeight(CardType.WildCard, 0);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: true, IncludeSpecialCards: false, IncludeWildCards: false  }:
            {
                card.SetCardsWeight(CardType.Number, 60);
                card.SetCardsWeight(CardType.Swap, 20);
                card.SetCardsWeight(CardType.Block, 20);
                card.SetCardsWeight(CardType.PlusTwo, 0);
                card.SetCardsWeight(CardType.PlusFour, 0);
                card.SetCardsWeight(CardType.WildCard, 0);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: false, IncludeSpecialCards: true,  IncludeWildCards: false }:
            {
                card.SetCardsWeight(CardType.Number, 60);
                card.SetCardsWeight(CardType.Swap, 0);
                card.SetCardsWeight(CardType.Block, 0);
                card.SetCardsWeight(CardType.PlusTwo, 20);
                card.SetCardsWeight(CardType.PlusFour, 20);
                card.SetCardsWeight(CardType.WildCard, 0);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: false, IncludeSpecialCards: false,  IncludeWildCards: true }:
            {
                card.SetCardsWeight(CardType.Number, 90);
                card.SetCardsWeight(CardType.Swap, 0);
                card.SetCardsWeight(CardType.Block, 0);
                card.SetCardsWeight(CardType.PlusTwo, 0);
                card.SetCardsWeight(CardType.PlusFour, 0);
                card.SetCardsWeight(CardType.WildCard, 10);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: true, IncludeSpecialCards: true,  IncludeWildCards: false }:
            {
                card.SetCardsWeight(CardType.Number, 60);
                card.SetCardsWeight(CardType.Swap, 15);
                card.SetCardsWeight(CardType.Block, 15);
                card.SetCardsWeight(CardType.PlusTwo, 5);
                card.SetCardsWeight(CardType.PlusFour, 5);
                card.SetCardsWeight(CardType.WildCard, 0);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: false, IncludeSpecialCards: true,  IncludeWildCards: true }:
            {
                card.SetCardsWeight(CardType.Number, 60);
                card.SetCardsWeight(CardType.Swap, 0);
                card.SetCardsWeight(CardType.Block, 0);
                card.SetCardsWeight(CardType.PlusTwo, 15);
                card.SetCardsWeight(CardType.PlusFour, 10);
                card.SetCardsWeight(CardType.WildCard, 15);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: true, IncludeSpecialCards: false, IncludeWildCards: true }:
            {
                card.SetCardsWeight(CardType.Number, 60);
                card.SetCardsWeight(CardType.Swap, 15);
                card.SetCardsWeight(CardType.Block, 15);
                card.SetCardsWeight(CardType.PlusTwo, 0);
                card.SetCardsWeight(CardType.PlusFour, 0);
                card.SetCardsWeight(CardType.WildCard, 10);
                CalculateTotalWeight();
                break;
            }
            case { IncludeActionCards: true, IncludeSpecialCards: true, IncludeWildCards: true }:
            {
                card.SetCardsWeight(CardType.Number, 60);
                card.SetCardsWeight(CardType.Swap, 10);
                card.SetCardsWeight(CardType.Block, 10);
                card.SetCardsWeight(CardType.PlusTwo, 5);
                card.SetCardsWeight(CardType.PlusFour, 5);
                card.SetCardsWeight(CardType.WildCard, 10);
                CalculateTotalWeight();
                break;
            }
                default:
                break;
        }

        bSetDefaultWeights = false;
    }
    
    //   Randomizes the cards info
    private void RandomizeCard(UnoCard passedCard)
    {
        Random random = new Random();
        
        //  Numbers
        var ranNum = random.Next(!UnoSettings.IncludeZero ? 1 : 0, 9); //  Gets random Number from 0-9
        
        //  Color
        var ranColor = random.Next(0, 3);
        
        //  Type
        var ranType = CardType.Number;
        var ran = random.Next(CalculateTotalWeight());

        foreach (var type in card.Weight)
        {
            if (ran < type.Value)
            {
                ranType = type.Key;
                break;
            }

            ranType = CardType.Number;
        }

        var ranName = "";

        if (ranType != CardType.Number)
        {
            ranName = ranType.ToString();
        }
        else
        {
            ranName = ranNum.ToString();
        }
        
        Services.Log.Information(ranName);
        Services.Log.Information(ranType.ToString());
        
        passedCard.SetCardInfoElements((CardColor)ranColor, ranType, ranNum, ranName);
        
    }

    private void EndGame()
    {
        numHeldCards = 0;
        locPlayerCards.Clear();
        gameSeed = 0;
        
        for (var i = 0; i < UnoSettings.StartingHand; i++)
        {
            partynumHeldCardsCards[0] = 0;
        }
    }

    
    /***************************
     *                         *
     *        MESSAGES         *
     *                         *
     ***************************/
    
    //  SETTINGS
    public void SendSettings()
    {
        if (!bCanSyncSettings)
        {
            Services.Chat.Print("[UNO]: Please wait a few seconds before trying to Sync Settings again...");
            return;
        }

        if (CalculateTotalWeight() != 100)
        {
            Services.Chat.Print("[UNO]: Total Weight must equal 100 to Sync Settings...");
            return;
        }
        
        var messageType = MessageType.Settings.ToString();
        
        if (!plugin.bDebug)
        {
            plugin.SendMsg($"++{messageType};{UnoSettings.StartingHand};" +
                           $"{UnoSettings.IncludeZero};" +
                           $"{UnoSettings.IncludeActionCards};" +
                           $"{UnoSettings.IncludeSpecialCards};" +
                           $"{UnoSettings.IncludeWildCards};" +
                           $"{Number};{Swap};{Block};{PlusTwo};{PlusFour};{WildCard}");
        }
        
        bCanSyncSettings = false;
    }

    public void ReceiveSettings(string[] parts)
    {
        if (bLiveGame || (!plugin.bIsLeader && plugin.PartyMembers!.Length > 1))
        {
            return;
        }
        
        UnoSettings newSettings;

        newSettings.StartingHand        = int.Parse(parts[1]);
        newSettings.IncludeZero         = bool.Parse(parts[2]);
        newSettings.IncludeActionCards  = bool.Parse(parts[3]);
        newSettings.IncludeSpecialCards = bool.Parse(parts[4]);
        newSettings.IncludeWildCards    = bool.Parse(parts[5]);
        
        Number = int.Parse(parts[6]);
        Swap = int.Parse(parts[7]);
        Block = int.Parse(parts[8]);
        PlusTwo = int.Parse(parts[9]);
        PlusFour = int.Parse(parts[10]);
        WildCard = int.Parse(parts[11]);

        UnoSettings = newSettings;

    }
    
    //  Cool Down so people don't spam SyncSettings button.
    private void SyncSettingsCoolDown()
    {
        if (bCanSyncSettings)
        {
            return;
        }

        elapsedTime += plugin.DeltaTime;
        
        if (elapsedTime >= SyncSettingsCD)
        {
            bCanSyncSettings = true;
            elapsedTime = 0;
        }
    }
    
    //  START GAME
    private void SendStartGame()
    {

        if (!plugin.bIsLeader)
        {
            return;
        }

        partynumHeldCardsCards = new int[plugin.PartyMembers!.Length];
        
        const MessageType messageType = MessageType.StartGame;
        var random = new Random();
        var seed = random.NextInt64(0101,60545);

        bIsTurn = true;
        gameSeed = seed;

        if (!plugin.bDebug)
        {
            plugin.SendMsg($"++{messageType};{gameSeed};{GetCurrentPartyOrder(plugin.PartyMembers)}");
            
        }

        InitHeldCards();
    }
    
    public void ReceiveStartGame(string[] parts)
    {

        if (parts[0] != "StartGame" || bLiveGame)
        {
            Services.Log.Information("Receive didnt start because bLiveGame is live");
            return;
        }
        
        if (bLiveGame)
        {
            Services.Chat.PrintError("[UNO]: Someone tried to start a new game while a game is live...");
            Services.Log.Information($"UNO::UnoInterface::ReceiveStartGame:: " +
                                     $"live game is set to {bLiveGame}.");
            return;
        }
        
        bLiveGame = true;
        gameSeed = long.Parse(parts[1]);

        //  Getting party member order
        MemberOrder = new string[plugin.PartyMembers!.Length];
        orderIndex = 0;
        for (var i = 2; i < parts.Length; i++)
        {

            if (MemberOrder[i] == plugin.LocPlayerName)
            {
                orderIndex = i;
            }
            
            MemberOrder[i] = parts[i];
        }
        
        InitHeldCards();
        
    }
    
    //  END GAME
    private void SendEndGame(bool forceStop)
    {
        if (!plugin.bIsLeader)
        {
            if (plugin.PartyMembers!.Length < 1)
            {
                EndGame();
                return;
            }
            
            Services.Chat.Print("[UNO]: Only the party's leader can end the game.");
            return;
        }
        
        const MessageType messageType = MessageType.EndGame;

        if (!plugin.bDebug)
        {
            plugin.SendMsg($"++{messageType};{gameSeed};{forceStop}");
        }

        //  Clears MemberOrder.
        for (var i = 0; i < MemberOrder.Length; i++)
        {
            MemberOrder[i] = "";
        }
        
        EndGame();
    }

    public void ReceiveEndGame(string[] parts)
    {

        if (parts[0] != "EndGame" || !bLiveGame)
        {
            return;
        }
        
        if (gameSeed == long.Parse(parts[1]))
        {
            gameSeed = 0;
            bLiveGame = false;
        }

        if (bool.Parse(parts[2]))
        {
            Services.Chat.PrintError("[UNO]: Party Leader has force stopped the game.");
        }
        
        EndGame();
    }
    
    //  TURN
    private void SendTurn(UnoCard sentCard)
    {
        const MessageType messageType = MessageType.Turn;
        
        var offset = 0;
        
        if (bReverse)
        {
            offset = -1;
        }
        else
        {
            offset = 1;
        }
        
        if (!plugin.bDebug)
        {
            plugin.SendMsg($"++{messageType};" +
                           $"{numHeldCards};" +
                           $"{sentCard.CardInfo.CardType};" +
                           $"{sentCard.CardInfo.CardColor};" +
                           $"{sentCard.CardInfo.Number}" +
                           $"{MemberOrder[orderIndex+offset]}");
        }

        bIsTurn = false;
    }

    public void ReceiveTurn(string[] parts, SeString sender)
    {
        var checker = 0;
        
        if (parts[0] != "Turn" || bIsTurn)
        {
            return;
        }

        for (var i = 1; i < plugin.PartyMembers!.Length; i++)
        {
            if (plugin.PartyMembers[i] == sender)
            {
                partynumHeldCardsCards[i] = int.Parse(parts[1]);
                checker++;
            }
        }

        if (checker < 1)
        {
            Services.Log.Information("DIDNT FIND ANYONE WITH SENT NAME - ReceiveTurn");
        }
        
        currentPlayedCard.CardInfo.CardType = (CardType)int.Parse(parts[2]);
        currentPlayedCard.CardInfo.CardColor = (CardColor)int.Parse(parts[3]);
        currentPlayedCard.CardInfo.Number = int.Parse(parts[4]);

        // This is to keep up with whose turn it is....I need to make a list that holds each party member
        //MemberTurn[parts[5]] = true;
        
        //  This handles if a PLusTwo or PLusFour was put by someone else
        //if ()
    }

    //  This func is called to update the others players showing a new card was pulled.
    private void SendDrawCard()
    {
        const MessageType messageType = MessageType.Draw;

        if (!plugin.bDebug)
        {
            plugin.SendMsg($"++{messageType};{gameSeed};{numHeldCards}");
        }
    }
    
    public void ReceiveDrawCard(string[] parts,  SeString sender)
    {
        var checker = 0;

        if (parts[0] != "Draw")
        {
            return;
        }

        if (parts[1] != gameSeed.ToString())
        {
            Services.Chat.PrintError($"[UNO]: Received Draw Card payload from the wrong game seed. " +
                                     $"Current Seed: {gameSeed} | " +
                                     $"Received Seed: {parts[2]}");
        }
        
        for (var i = 1; i < plugin.PartyMembers!.Length; i++)
        {
            if (plugin.PartyMembers[i] == sender)
            {
                partynumHeldCardsCards[i] = int.Parse(parts[2]);
                checker++;
            }
        }
        
        if (checker < 1)
        {
            Services.Log.Information("DIDNT FIND ANYONE WITH SENT NAME - DRAW");
        }
        
    }
    
    
    
    
    
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

    //  This func will handle Drawing the actual game.
    private void DrawUnoTab()
    {
        if (ImGui.Button("End Game"))
        {
            if (!plugin.bIsLeader || !bLiveGame)
            {
                return;
            }
            
            SendEndGame(true);
        }
        

        if (bLiveGame)
        {
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() / 2);
            ImGui.Text($"Seed: {gameSeed}");
        }
        
        //  Create cards
        for (var i = 0; i < locPlayerCards.Count; i++)
        {
            var c = locPlayerCards[i];
            
            ImGui.SetCursorPos(new Vector2((ImGui.GetWindowWidth() / 4) + (i * 100), ImGui.GetWindowHeight() - 150));
            ImGui.PushID(i);
            if (ImGui.ColorButton($"{c.CardInfo.Name}", c.GetCardColor(), ImGuiColorEditFlags.None, new Vector2(100, 100)))
            {
                if (bIsTurn)
                {
                    if (c.CardInfo.CardType == CardType.WildCard)
                    { 
                        ImGui.SetCursorPosY(ImGui.GetWindowHeight() / 2);
                        ImGui.PushID("Wild###");
                        if (ImGui.ColorButton($"Blue", new Vector4(0,0,1,1), ImGuiColorEditFlags.None,
                                            new Vector2(100, 100)))
                        {
                            c.SetCardColor(CardColor.Blue);
                        }
                        ImGui.SameLine();
                        ImGui.PushID("Wild###");
                        if (ImGui.ColorButton($"Green", new Vector4(0,1,0,1), ImGuiColorEditFlags.None,
                                              new Vector2(100, 100)))
                        {
                            c.SetCardColor(CardColor.Green);
                        }
                        ImGui.SameLine();
                        ImGui.PushID("Wild###");
                        if (ImGui.ColorButton($"Red", new Vector4(1,0,0,1), ImGuiColorEditFlags.None,
                                              new Vector2(100, 100)))
                        {
                            c.SetCardColor(CardColor.Red);
                        }
                        ImGui.SameLine();
                        ImGui.PushID("Wild###");
                        if (ImGui.ColorButton($"Yellow", new Vector4(1,1,0,1), ImGuiColorEditFlags.None,
                                              new Vector2(100, 100)))
                        {
                            c.SetCardColor(CardColor.Yellow);
                        }

                    }
                    
                    
                    HandleTurnLocal(c);
                    locPlayerCards.Remove(c);
                }
            }
            
            ImGui.SetCursorPos(new Vector2((ImGui.GetWindowWidth() / 4), ImGui.GetWindowHeight()));
            ImGui.Text(c.CardInfo.Number.ToString());
        }

        if (ImGui.Button("DRAW"))
        {
            DrawCard();
        }
    }
    
    //  UI for Uno Tab
    private void UnoTab()
    {
        using var id = ImRaii.PushId("Uno");
        ImGuiUtil.HoverTooltip("Uno Tab");
        
        ImGui.BeginChild("Uno");

        //  If Game is live, draw Uno UI
        if (bLiveGame)
        {
            DrawUnoTab();
        }
        //  If Game isn't Live, Draw Standby UI
        else
        {
            //  In party, Is Leader
            if (plugin.bIsLeader)
            {
                ImGui.SetCursorPos(new Vector2(ImGui.GetWindowWidth() / 2, ImGui.GetWindowHeight() / 2));
                ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(0,1,0,1));
                if (ImGui.Button("START GAME", new Vector2(100, 100)))
                {
                    SendStartGame();
                }
                ImGui.PopStyleColor();
                ImGuiUtil.HoverTooltip("Starts Game for all players...\nMake sure you've synced Settings in the Sync Tab");
            }
            //  In party, Not leader.
            else if (Services.Party.Length > 1)
            {
                var offset = ImGui.CalcTextSize("Waiting for PartyLeader to start game...");
                ImGui.SetCursorPos(new Vector2((ImGui.GetWindowWidth() / 2) - (offset.X / 2), ImGui.GetWindowHeight() / 2));
                ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1,1,0,1));
                ImGui.Text("Waiting for PartyLeader to start game...");
                ImGui.PopStyleColor();
            }
            //  Not party, Not Leader
            else
            {
                var offset = ImGui.CalcTextSize("Waiting for PartyLeader to start game...");
                ImGui.SetCursorPos(new Vector2((ImGui.GetWindowWidth() / 2) - (offset.X / 2), ImGui.GetWindowHeight() / 2));
                ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1,0,0,1));
                ImGui.Text("Join a Party to start a game...");
                ImGui.PopStyleColor();
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

        //  Input logic to grey out settings if not party leader.
        
        if (!bLiveGame || plugin.bIsLeader || plugin.PartyMembers!.Length < 1)
        {
            //  How many cards to start with.
            ImGui.SetNextItemWidth(100.0f);
            ImGui.InputInt("Starting Hand", ref UnoSettings.StartingHand, 0, 0);
            ImGuiUtil.HoverTooltip("Amount of Cards each person has at the start of the game.");

            
            //  Which type of cards to include
            ImGui.Checkbox("Include Zeros?", ref UnoSettings.IncludeZero);
            ImGuiUtil.HoverTooltip("Includes '0' cards.");
            
            ImGui.Checkbox("Include Action Cards?", ref UnoSettings.IncludeActionCards);
            ImGuiUtil.HoverTooltip("Includes 'Swap' and 'Block' cards.");
            
            ImGui.Checkbox("Include Special Cards?", ref UnoSettings.IncludeSpecialCards);
            ImGuiUtil.HoverTooltip("Includes '+4' and '+2' cards.");
            
            ImGui.Checkbox("Include Wild Cards?", ref UnoSettings.IncludeWildCards);
            ImGuiUtil.HoverTooltip("Includes Wild Cards (Color change cards).");
            
            ImGui.Spacing();

            //  Card Weights
            ImGui.Text("Card Weights");

            DrawWeightsSection();

            ImGui.Dummy(new Vector2(0,10));
            
            //  TotalWeight text
            switch (CalculateTotalWeight())
            {
                case < 100:
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 0, 1));
                    ImGui.Text($"Total Weight (Must equal 100): {CalculateTotalWeight()}");
                    ImGui.PopStyleColor();
                    break;
                case > 100:
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1));
                    ImGui.Text($"Total Weight (Must equal 100): {CalculateTotalWeight()}");
                    ImGui.PopStyleColor();
                    break;
                default:
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0, 1, 0, 1));
                    ImGui.Text($"Total Weight (Must equal 100): {CalculateTotalWeight()}");
                    ImGui.PopStyleColor();
                    break;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Default Weights", ref bSetDefaultWeights)) { SetCardSettingsWithDefaultWeights(); }
            
            ImGui.Separator();

            //  Sync Settings
            if (bCanSyncSettings)
            {
                if (ImGui.Button("Sync Settings"))
                {
                    SendSettings();
                }
                ImGuiUtil.HoverTooltip("Sends current settings to all PartyMembers.");
            }
            else
            {
                if (ImGui.Button("Please Wait"))
                {
                    SendSettings();
                }
                ImGuiUtil.HoverTooltip("Please Wait a few seconds before trying to sync again...");
            }
            
        }
        else
        {
            ImGui.Text($"Either a Live game({bLiveGame}) is happening or You're not the PartyLeader(PartyLeader:{plugin.bIsLeader})");
        }
        
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
        bytesRead = stream.Read(buffer, 0, buffer.Length);
        ImGui.Text(bytesRead.ToString());
        
        //DrawTabs();
        
        //SyncSettingsCoolDown();

        if (ImGui.Button("Ping", new Vector2(100, 100)))
        {
            string msg = "ping";
            byte[] message = Encoding.ASCII.GetBytes(msg);
            stream.Write(message, 0, message.Length);
            
        }
        
    }
}

