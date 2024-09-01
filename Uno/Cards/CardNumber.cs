using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardNumber: CardBase
{
    public string Dir = "Data/Numbers/";
    public IntPtr Texture;
    
    public CardNumber()
    {
        CardInfo.CardType = CardType.Number;
        Texture = Services.TextureProvider.GetFromFile("Uno/Cards/Data/back.png").GetWrapOrEmpty().ImGuiHandle;
        
        RandomizeCardElements(CardInfo.CardType);
        SetCardTex();
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
        
        //  Add Number to Dir
        switch (CardInfo.Number)
        {
            case 0:
                Dir += "0";
                break;
            case 1:
                Dir += "1";
                break;
            case 2:
                Dir += "2";
                break;
            case 3:
                Dir += "3";
                break;
            case 4:
                Dir += "4";
                break;
            case 5:
                Dir += "5";
                break;
            case 6:
                Dir += "6";
                break;
            case 7:
                Dir += "7";
                break;
            case 8:
                Dir += "8";
                break;
            case 9:
                Dir += "9";
                break;
        }
        
        //  Set Texture with new Dir
        Services.Log.Information($"Dir: {Dir}");
        return Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
