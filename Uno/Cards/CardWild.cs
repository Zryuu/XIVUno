using System;
using Uno.Helpers;

namespace Uno.Cards;


public class CardWild: CardBase
{
    
    public CardWild()
    {
        CardInfo.CardType = CardType.WildCard;
        CardInfo.CardColor = null;
        CardInfo.Number = null;
        
        Dir = "Uno.Cards.Data.Wild.wild.png";
        
    }
}
