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
        
        RandomizeCardElements(CardType.PlusFour);

        Dir = SetCardTex();
    }
    
    public string SetCardTex()
    {
        var newDir = "Uno.Cards.Data.Special.";
        //  Add Color to Dir
        switch (CardInfo.CardColor)
        {
            case CardColor.Blue:
                newDir += "Blue";
                break;
            case CardColor.Red:
                newDir += "Red";
                break;
            case CardColor.Yellow:
                newDir += "Yellow";
                break;
            case CardColor.Green:
                newDir += "Green";
                break;
            default:
                break;
        }
        
        //  Add skip to Dir
        newDir += "plusfour.png";
        
        return newDir;
    }
}
