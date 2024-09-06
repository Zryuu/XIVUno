using System;
using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBack: CardBase
{
    public const string Dir = "Uno.Cards.Data.back.png";
    public IDalamudTextureWrap? Texture;
    
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
            
            foreach (var resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                Services.Log.Information($"Resource: {resourceName}");
            }
            
            Texture = Services.TextureProvider.GetFromManifestResource(Assembly.GetExecutingAssembly(), Dir).GetWrapOrEmpty();

            if (Texture == null)
            {
                Services.Log.Information("No texture found.");
            }
        });
    }
}
