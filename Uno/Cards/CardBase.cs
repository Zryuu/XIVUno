using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;
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
    public IDalamudTextureWrap? Texture;
    
    public string Dir                   = "Uno.Cards.Data.back.png";
    public float Y                      = 600f;   //  Y Position of UI.
    public const int HoverAmount        = 50;     //  Amount to move card when Hovered.
    public const float HoverLerpSpeed   = 0.1f;   //  This amount is applied to the Lerp's alpha every frame (0 -> 1)
    public float HoverLerpAlpha         = 0f;     //  Current amount of Lerp.
    public bool CardWasHovered          = false;  //  Used to reverse the Lerp.  
    
    public void SetPossibleCards(bool zero, bool special, bool action, bool wild)
    {
        Zero = zero;
        Special = special;
        Action = action;
        Wild = wild;
    }

    //  Returns an Int. Used to send info to Server
    public int GetCardColor()
    {
        if (CardInfo.CardColor == null)
        {
            return -1;
        }
        
        return (int)CardInfo.CardColor;
    }

    public void SetCardColor(CardColor? color)
    {
        CardInfo.CardColor = color;
    }

    //  Wild Card color change.
    public void WildCardColorChange(CardColor newColor)
    {
        if (CardInfo.CardType != CardType.WildCard)
        {
            return;
        }
        CardInfo.CardColor = newColor;
    }
    
    public CardType GetCardType()
    {
        return CardInfo.CardType;
    }

    public void SetCardType(CardType type)
    {
        CardInfo.CardType = type;
    }
    
    //  Returns an Int. Used to send info to Server
    public int GetCardNumber()
    {
        if (CardInfo.Number == null)
        {
            return -1;
        }
        
        return (int)CardInfo.Number;
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
        if (CardInfo.CardColor != null)
        {
            CardInfo.Name += CardInfo.CardColor + " ";
        }
        
        if (CardInfo.CardType == CardType.WildCard)
        {
            CardInfo.Name = "Wild Card ";
        }
        else
        {
            CardInfo.Name += CardInfo.CardType + " ";
        }

        if (CardInfo.Number != null)
        {
            CardInfo.Name += CardInfo.Number + " ";
        }
    }

    public void SetCardElements(CardColor? color, CardType type, int? n)
    {
        SetCardColor(color);
        SetCardType(type);
        SetCardNumber(n);
        SetCardName();
    }

    public void SetCardTexture(IDalamudTextureWrap? texture)
    {
        Texture = texture;
    }

    //  This prob won't get used
    public IDalamudTextureWrap GetTex()
    {
        return Texture;
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

        SetCardName();
    }
    
}
