using System;
using Dalamud.Interface.Textures.TextureWraps;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBlock: CardBase
{
    
    public CardBlock()
    {
        CardInfo.CardType = CardType.Block;
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
        
        //  Add skip to Dir
        newDir += "skip.png";
        
        return newDir;
    }
}
