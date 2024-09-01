using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBack: CardBase
{
    public string Dir = "Data/back.png";
    public IntPtr Texture;
    
    public CardBack()
    {
        CardInfo.CardType = CardType.Number;
        CardInfo.CardColor = null;
        CardInfo.Number = null;
        Texture = Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
    }
}
