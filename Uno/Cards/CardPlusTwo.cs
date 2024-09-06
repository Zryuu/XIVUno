using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardPlusTwo: CardBase
{
    
    public CardPlusTwo()
    {
        CardInfo.CardType = CardType.PlusTwo;
        
        RandomizeCardElements(CardInfo.CardType);
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
        newDir += "plustwo.png";
        
        return newDir;
    }
}
