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
    White
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

public class CardBase
{
    public CardInfo CardInfo = new CardInfo();
    
    
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

    public void RandomizeCardElements()
    {
        var random = new Random();
        var typerand = random.Next(0, 5);

        var newType = (CardType)typerand;
        
        
        
        
    }
    
}
