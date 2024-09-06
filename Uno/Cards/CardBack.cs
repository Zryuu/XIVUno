using System;
using System.IO;
using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBack: CardBase
{
    
    public CardBack()
    {
        CardInfo = new CardInfo
        {
            CardType = CardType.Number,
            CardColor = null,
            Number = null
        };
        
        Dir = "Uno.Cards.Data.back.png";
    }
}
