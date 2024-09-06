using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardPlusFour: CardBase
{
    
    public CardPlusFour()
    {
        CardInfo.CardType = CardType.PlusFour;
        CardInfo.CardColor = null;
        CardInfo.Number = null;

        Dir = SetCardTex();
    }
    
    public string SetCardTex()
    {
        var newDir = "Uno.Cards.Data.Special.";
        //  Add Color to Dir
        switch (CardInfo.CardColor)
        {
            case CardColor.Blue:
                newDir += "blue";
                break;
            case CardColor.Red:
                newDir += "red";
                break;
            case CardColor.Yellow:
                newDir += "yellow";
                break;
            case CardColor.Green:
                newDir += "green";
                break;
        }
        
        //  Add skip to Dir
        newDir += "plusfour.png";
        
        return newDir;
    }
}
