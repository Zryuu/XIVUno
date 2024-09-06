using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardSwap: CardBase
{
    
    public CardSwap()
    {
        CardInfo.CardType = CardType.Swap;
        
        RandomizeCardElements(CardInfo.CardType);
        Dir = SetCardTex();
    }
    
    public string SetCardTex()
    {
        var newDir = "Uno.Cards.Data.Action.";
        
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
        
        //  Add reverse to Dir
        newDir += "reverse.png";

        return newDir;
    }
}
