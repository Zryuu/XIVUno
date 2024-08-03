using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Uno.Helpers;
using Uno.Windows;

namespace Uno;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public Delegates Delegates { get; private set; }
    public CommandManager Cm { get; private set; }
    

    
    public IPlayerCharacter? LocPlayer;
    public SeString? LocPlayerName;
    
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Uno");
    private ConfigWindow ConfigWindow { get; init; }
    private UnoInterface UnoInterface { get; init; }

    public Plugin()
    {
        PluginInterface.Create<Services>();
        
        //  Initing Helpers
        Delegates = new Delegates(this);
        Cm        = new CommandManager(this);
        
        Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        UnoInterface = new UnoInterface(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(UnoInterface);

        Services.PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        Services.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
    }

    //  DISPOSE
    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        //  Windows
        ConfigWindow.Dispose();
        UnoInterface.Dispose();
        
        //  Helpers
        Delegates.DisposeDelegates();
        Cm.DisposeCommands();
    }
    
    //  Saves a Ref to local player and local player's name.
    public void SaveLocPlayer()
    {
        //  Checks if user is logged in. If not, instant return
        if (!Services.ClientState.IsLoggedIn)
        {
            return;
        }
        
        //  Saves user ref
        LocPlayer = Services.ClientState.LocalPlayer!;

        //  Get User name from user ref.
        LocPlayerName = LocPlayer.Name;
    }
    
    //  Gets Party Member's names and saves them in an array
    public void SetPartyMembers()
    {
        //  This may cause some memory issues......maybe.
        Delegates.partyMembers = new SeString[Services.Party.Length];

        for (int i = 0; i < Services.Party.Length; i++)
        {
            Delegates.partyMembers[i] = GetPartyMemberNames(i)!;
        }
    }
    
    //  Gets part Member's names from array.
    public string? GetPartyMemberNames(int i)
    {
        //  Checks if Party Member count is less than 1, if so then no party.
        if (Services.Party.Length < 1)
        {
            Services.Log.Information("[ERROR]: GetPartyMemberNames ran with no active party.");
            return null;
        }
        
        var member = Services.Party.CreatePartyMemberReference(Services.Party.GetPartyMemberAddress(i));
        
        //Services.Log.Information(member.Name.ToString());

        return member!.Name.ToString();
    }
    
    
    
    //  This is most likely not needed and can be removed.
    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }
    
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => UnoInterface.Toggle();
}
