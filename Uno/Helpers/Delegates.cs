using Dalamud.Plugin.Services;

//  THIS FILE HANDLES ALL FUNCTIONS THAT ARE SUBSCRIBED TO A DELEGATE.

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

        if (plugin.ConnectedToServer)
        {
            plugin.HandlePings();
            plugin.LastPingSent += plugin.DeltaTime;
            plugin.LastPingReceived += plugin.DeltaTime;
        }
        
        if (plugin is { Stream: { DataAvailable: true }})
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
        plugin.XivName = null;
    }
    


    //  Disposes of Subscribed delegates.
    public void DisposeDelegates()
    {
        Services.Framework.Update -= OnFrameworkTick;
        Services.ClientState.Logout -= OnLogOut;
    }
    
}
