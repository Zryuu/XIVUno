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
        
        Texture = Services.TextureProvider.GetFromFile(Dir += "plusfour").GetWrapOrEmpty().ImGuiHandle;
    }
}
