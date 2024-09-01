using System;
using System.Collections.Generic;
using System.Numerics;
using Lumina.Data.Parsing;
using Uno.Helpers;

namespace Uno.Cards;

public struct CardInfo
{
    public CardColor? CardColor;
    public CardType CardType;
    public int? Number;
    public string Name;
    public int Weight;
};

public enum CardColor
{
    Blue,
    Red,
    Yellow,
    Green,
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

public abstract class CardBase()
{
    protected CardInfo CardInfo;
    public bool Zero, Special, Action, Wild;
    
    public void SetPossibleCards(bool zero, bool special, bool action, bool wild)
    {
        Zero = zero;
        Special = special;
        Action = action;
        Wild = wild;
    }

    public CardColor? GetCardColor()
    {
        return CardInfo.CardColor;
    }

    public void SetCardColor(CardColor color)
    {
        CardInfo.CardColor = color;
    }
    
    public CardType GetCardType()
    {
        return CardInfo.CardType;
    }

    public void SetCardType(CardType type)
    {
        CardInfo.CardType = type;
    }
    
    public int? GetCardNumber()
    {
        return CardInfo.Number ?? null;
    }

    public void SetCardNumber(int? n)
    {
        CardInfo.Number = (CardInfo.CardType == CardType.Number) ? n : null;
    }

    public string GetCardName()
    {
        return CardInfo.Name;
    }
    
    public void SetCardName()
    {
        CardInfo.Name = $"{CardInfo.CardColor};{CardInfo.CardType};{CardInfo.Number}";
    }

    public void SetCardElements(CardColor color, CardType type, int? n)
    {
        SetCardColor(color);
        SetCardType(type);
        SetCardNumber(n);
    }

    public void RandomizeCardElements(CardType type)
    {
        var random = new Random();
        
       
        //  Number
        if (type == CardType.Number)
        {
            CardInfo.Number = random.Next(Zero ? 0 : 1, 9);
        }
        else
        {
            CardInfo.Number = null;
        }

        //  Color
        if (CardInfo.CardType == CardType.WildCard)
        {
            CardInfo.CardColor = null;
        }
        else
        {
            CardInfo.CardColor = (CardColor)random.Next(0, 3);
        }
    }
    
}
