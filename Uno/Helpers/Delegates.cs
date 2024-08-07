using System.Collections.Generic;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Uno.Windows;

//  THIS FILE HOLDS ALL FUNCTIONS THAT ARE SUBSCRIBED TO A DELEGATE.

namespace Uno.Helpers;

public class Delegates
{
    private Plugin plugin;
    
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
        plugin.HandleDeltaTime();

        plugin.PingServer();
        
        if (plugin.stream.DataAvailable && plugin.BServer)
        {
            plugin.ReceiveMessage();
        }
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
        
        foreach (var p in plugin.PartyMembers!)
        {
            if (sender.ToString().Substring(1) == p!.ToString())
            {
                var capturedMessage = cmessage.TextValue;

                if (capturedMessage[0] == '+' && capturedMessage[1] == '+')
                { 
                    capturedMessage = capturedMessage.Substring(2);
                    plugin.RouteReceivedMessage(capturedMessage, sender);
                }
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
