using System;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Uno.Helpers;

namespace Uno.Cards;


public class CardNumber: CardBase
{
    public string Dir = "Data/Numbers";
    public IntPtr Texture;
    
    public CardNumber()
    {
        CardInfo.CardType = CardType.Number;
        Texture = Services.TextureProvider.GetFromFile("Uno/Cards/Data/card_back.png").GetWrapOrEmpty().ImGuiHandle;
        
        RandomizeCardElements(CardInfo.CardType);
    }


    public IntPtr SetCardTex()
    {
        switch (CardInfo.Number)
        {
            case 0:
                break;
            case 1:
                break;
            case 2:
                break;
            case 3:
                break;
            case 4:
                break;
            case 5:
                break;
            case 6:
                break;
            case 7:
                break;
            case 8:
                break;
            case 9:
                break;
        }
        
        return Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
