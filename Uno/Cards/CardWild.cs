using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardWild: CardBase
{
    public string Dir = "Data/Wild/";
    public IntPtr Texture;
    
    public CardWild()
    {
        CardInfo.CardType = CardType.WildCard;
        CardInfo.CardColor = null;
        CardInfo.Number = null;
        Texture = Services.TextureProvider.GetFromFile(Dir += "wild.png").GetWrapOrEmpty().ImGuiHandle;
    }
}
