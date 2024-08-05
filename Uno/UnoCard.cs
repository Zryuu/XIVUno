using System;
using System.Collections.Generic;
using System.Numerics;
using Lumina.Data.Parsing;
using Uno.Helpers;

namespace Uno;

public struct CardInfo
{
    public CardColor CardColor;
    public CardType CardType;
    public int Number;
    public int Weight;
};

public enum CardColor
{
    Blue,
    Red,
    Yellow,
    Green
}

public enum CardType
{
    Number,
    Swap,
    Block,
    PlusTwo,
    PlusFour,
    WildCard
}

public class UnoCard
{
    public CardInfo CardInfo;
    public int[] CardWeightAmounts = new int[5];
    public int Number = 50, Swap = 20, Block = 10, PlusTwo = 10, PlusFour = 5, WildCard = 5;
    public Vector4 Color = new Vector4(0, 0, 0, 0);

    public Dictionary<CardType, int> weight = new Dictionary<CardType, int>
    {
        { CardType.Number, 50},
        { CardType.Swap, 20},
        { CardType.Block, 10},
        { CardType.PlusTwo, 10},
        { CardType.PlusFour, 5},
        { CardType.WildCard, 5},
    };

    public void SetCardColor()
    {
        switch (CardInfo.CardColor)
        {
            case CardColor.Blue:
                Color = new Vector4(0, 0, 1, 1);
                break;
            case CardColor.Red:
                Color = new Vector4(1, 0, 0, 1);
                break;
            case CardColor.Green:
                Color = new Vector4(0, 1, 0, 1);
                break;
            case CardColor.Yellow:
                Color = new Vector4(1, 1, 0, 1);
                break;
        }
    }

    public Vector4 GetCardColor()
    {
         var vec = new Vector4();
        
        switch (CardInfo.CardColor)
        {
            case CardColor.Blue:
                vec = new Vector4(0, 0, 1, 1);
                break;
            case CardColor.Red:
                vec = new Vector4(1, 0, 0, 1);
                break;
            case CardColor.Green:
                vec = new Vector4(0, 1, 0, 1);
                break;
            case CardColor.Yellow:
                vec = new Vector4(1, 1, 0, 1);
                break;
        }
        
        return vec;
    }
    
    public void SetCardsWeight(CardType type, int newWeight)
    {
        int val;
        if (weight.TryGetValue(type, out val))
        {
            weight[type] = newWeight;
        }
        else
        {
            Services.Chat.Print("[ERROR]: UnoCard::ChangeWeightOfCard::No Value exists at given Key.", 
                                null, 0xF800);
            Services.Log.Information("[ERROR]: UnoCard::SetCardsWeight::No Value exists at given Key.");
        }

    }
    
    public CardInfo GetCardInfo(UnoCard card)
    {
        return card.CardInfo;
    }

    public void SetCardInfoElements(CardColor color, CardType type, int num)
    {
        CardInfo.CardColor = color;
        CardInfo.CardType = type;
        CardInfo.Number = num;
        CardInfo.Weight = weight[type];
    }
    
}
