using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardPlusTwo: CardBase
{
    public string Dir = "Data/Special/";
    public IntPtr Texture;
    
    public CardPlusTwo()
    {
        CardInfo.CardType = CardType.PlusTwo;
        
        RandomizeCardElements(CardInfo.CardType);
        Services.Framework.RunOnFrameworkThread(() =>
        {
            Texture = SetCardTex();
        });
    }
    
    public IntPtr SetCardTex()
    {
        //  Add Color to Dir
        switch (CardInfo.CardColor)
        {
            case CardColor.Blue:
                Dir += "blue";
                break;
            case CardColor.Red:
                Dir += "red";
                break;
            case CardColor.Yellow:
                Dir += "yellow";
                break;
            case CardColor.Green:
                Dir += "green";
                break;
        }
        
        //  Add plustwo to Dir
        Dir += "plustwo";
        
        //  Set Texture with new Dir
        Services.Log.Information($"Dir: {Dir}");
        return Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
