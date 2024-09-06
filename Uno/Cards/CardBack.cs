using System;
using System.Reflection;
using Dalamud.Interface.Textures.TextureWraps;
using Uno.Helpers;

namespace Uno.Cards;


public class CardBack: CardBase
{
    public const string Dir = "Cards/Data/back.png";
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
            //  CONTINUE: Textures won't load
            Texture = Services.TextureProvider.GetFromFile(Services.PluginInterface.GetPluginLocDirectory() + Dir).GetWrapOrEmpty();

            if (Texture == null)
            {
                Services.Log.Information("No texture found.");
            }
        });
    }
}
