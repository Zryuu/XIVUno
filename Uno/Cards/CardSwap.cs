using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardSwap: CardBase
{
    public string Dir = "Data/Action/";
    public IntPtr Texture;
    
    public CardSwap()
    {
        CardInfo.CardType = CardType.Swap;
        Texture = Services.TextureProvider.GetFromFile("Uno/Cards/Data/back.png").GetWrapOrEmpty().ImGuiHandle;
        
        RandomizeCardElements(CardInfo.CardType);
        Texture = SetCardTex();
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
        
        //  Add reverse to Dir
        Dir += "reverse";
        
        //  Set Texture with new Dir
        Services.Log.Information($"Dir: {Dir}");
        return Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
