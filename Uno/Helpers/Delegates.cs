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
        Services.ClientState.Logout += OnLogOut;
        Services.ClientState.Login += OnLogIn;
        
    }
    
    //  Calls every Plugin Tick.
    public void OnFrameworkTick(IFramework framework)
    {
        plugin.HandleDeltaTime();
       
        
        if (plugin is { Stream: { DataAvailable: true }, BServer: true })
        {
            plugin.ReceiveMessage();
            plugin.PingServer();
        }
        else
        {
            Services.Log.Information("It didn't work...womp womp");
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
    


    //  Disposes of Subscribed delegates.
    public void DisposeDelegates()
    {
        Services.Framework.Update -= OnFrameworkTick;
        Services.ClientState.Logout -= OnLogOut;
    }
    
}
