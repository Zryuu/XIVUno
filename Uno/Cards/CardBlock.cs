using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBlock: CardBase
{
    public string Dir = "Data/Special/";
    
    public CardBlock()
    {
        CardInfo.CardType = CardType.Block;
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
        
        //  Add skip to Dir
        Dir += "skip";
        
        //  Set Texture with new Dir
        Services.Log.Information($"Dir: {Dir}");
        return Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
