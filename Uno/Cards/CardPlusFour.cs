using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardPlusFour: CardBase
{
    public string Dir = "Data/Special/";
    public IntPtr Texture;
    
    public CardPlusFour()
    {
        CardInfo.CardType = CardType.PlusFour;
        CardInfo.CardColor = null;
        CardInfo.Number = null;
        
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
        
        //  Add skip to Dir
        Dir += "plusfour";
        
        //  Set Texture with new Dir
        Services.Log.Information($"Dir: {Dir}");
        return Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
