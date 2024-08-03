using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

//  THIS FILE HOLDS ALL FUNCTIONS THAT ARE SUBSCRIBED TO A DELEGATE.

namespace Uno.Helpers;

public class Delegates
{
    private Plugin plugin;
    private List<string> capturedMessages = new List<string>();
    public SeString[]? partyMembers;
    
    //  Constructor
    public Delegates(Plugin plugin)
    {
        this.plugin = plugin;
        
        //  Subscribing to Delegates
        Services.Framework.Update += OnFrameworkTick;
        Services.Chat.ChatMessage += OnChatMessage;
        Services.ClientState.Logout += OnLogOut;
        Services.ClientState.Login += OnLogIn;
        
    }
    
    //  Calls every Plugin Tick.
    public void OnFrameworkTick(IFramework framework)
    {
        
        plugin.SetPartyMembers();
    }
    
    //  Fires on Login
    public void OnLogIn()
    {
        plugin.SaveLocPlayer();
    }
    
    //  Fires on logout
    public void OnLogOut()
    {
        plugin.LocPlayer = null;
        plugin.LocPlayerName = null;
    }
    
    //  Handles all ChatMessages. Subscribed to ChatMessage delegate.
    public void OnChatMessage(
        XivChatType type, int senderId, ref SeString sender, ref SeString cmessage, ref bool isHandled)
    {
        if (isHandled) return;
        
        if (type != XivChatType.Party)
        {
            return;
        }
        
        foreach (var p in partyMembers)
        {
            if (sender.ToString().Substring(1) == p!.ToString())
            {
                string capturedMessage = cmessage.TextValue;
                Services.Log.Information($"{type}, {senderId}, {sender}, '{cmessage}', {isHandled}");
                break;
            }
        }
    }


    //  Disposes of Subscribed delegates.
    public void DisposeDelegates()
    {
        Services.Framework.Update -= OnFrameworkTick;
        Services.Chat.ChatMessage -= OnChatMessage;
        Services.ClientState.Logout -= OnLogOut;
    }
    
}
