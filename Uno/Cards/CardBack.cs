using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBack: CardBase
{
    public const string Dir = "Data/back.png";
    public IntPtr Texture;
    
    public CardBack()
    {
        CardInfo = new CardInfo
        {
            CardType = CardType.Number,
            CardColor = null,
            Number = null
        };
        
        Services.Framework.RunOnFrameworkThread(() =>
        {
            Texture = Services.TextureProvider.GetFromFile(Dir).GetWrapOrEmpty().ImGuiHandle;
        });
    }
}
