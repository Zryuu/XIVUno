using System;
using Dalamud.Interface.Textures.TextureWraps;
using Uno.Helpers;

namespace Uno.Cards;


public class CardNumber: CardBase
{
    
    public CardNumber()
    {
        CardInfo.CardType = CardType.Number;
        
        RandomizeCardElements(CardInfo.CardType);

        Dir = SetCardTex();
    }
    
    public string SetCardTex()
    {
        var newDir = "Uno.Cards.Data.Numbers.";
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
        
        //  Add Number to Dir
        switch (CardInfo.Number)
        {
            case 0:
                newDir += "0";
                break;
            case 1:
                newDir += "1";
                break;
            case 2:
                newDir += "2";
                break;
            case 3:
                newDir += "3";
                break;
            case 4:
                newDir += "4";
                break;
            case 5:
                newDir += "5";
                break;
            case 6:
                newDir += "6";
                break;
            case 7:
                newDir += "7";
                break;
            case 8:
                newDir += "8";
                break;
            case 9:
                newDir += "9";
                break;
        }

        newDir += ".png";
        return newDir;
    }
}
