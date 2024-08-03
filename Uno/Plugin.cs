using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Uno.Windows;

namespace Uno;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    
    public const string UnoCommand = "/UNO";
    
    private readonly Dictionary<string, CommandInfo> commands = new();
    
    private List<string> capturedMessages = new List<string>();
    private SeString[]? partyMembers;
    private IPlayerCharacter locPlayer;
    private SeString locPlayerName;
    
    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("Uno");
    private ConfigWindow ConfigWindow { get; init; }
    private UnoInterface UnoInterface { get; init; }

    public Plugin()
    {
        PluginInterface.Create<Services>();
        Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        UnoInterface = new UnoInterface(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(UnoInterface);
        
        Services.CommandManager.AddHandler(UnoCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Something Uno related"
        });

        Services.PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        Services.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        Services.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        //  Subscribing to Delegates
        Services.Framework.Update += OnFrameworkTick;
        Services.Chat.ChatMessage += OnChatMessage;

        InitializeCommands();
        SaveLocPlayer();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        UnoInterface.Dispose();

        Services.CommandManager.RemoveHandler(UnoCommand);
        DisposeCommands();
        Services.Framework.Update -= OnFrameworkTick;
        Services.Chat.ChatMessage -= OnChatMessage;
    }
    
    //  Calls every Plugin Tick.
    private void OnFrameworkTick(IFramework framework)
    {
        SetPartyMembers();
    }

    //  Saves a Ref to local player and local player's name.
    private void SaveLocPlayer()
    {
        //  Checks if user is logged in. If not, instant return
        if (!Services.ClientState.IsLoggedIn)
        {
            return;
        }
        
        //  Saves user ref
        var locPlayer = Services.ClientState.LocalPlayer;

        //  Get User name from user ref.
        locPlayerName = locPlayer.Name;
    }
    
    private void OnChatMessage(
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
    
    //  Gets Party Member's names and saves them in an array
    public void SetPartyMembers()
    {
        //  This may cause some memory issues......maybe.
        partyMembers = new SeString[Services.Party.Length];

        for (int i = 0; i < Services.Party.Length; i++)
        {
            partyMembers[i] = GetPartyMemberNames(i)!;
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
    
    private void InitializeCommands()
    {
        commands[UnoCommand] = new CommandInfo(UnoCi)
        {
            HelpMessage = "Use to open the Uno Window.",
            ShowInHelp = true,
                
        };

        foreach (var (command, info) in commands)
            Services.CommandManager.AddHandler(command, info);
    }
    
    private void DisposeCommands()
    {
        foreach (var command in commands.Keys)
            Services.CommandManager.RemoveHandler(command);
    }
    
    //  This is most likely not needed.
    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }
    
    private void UnoCi(string command, string args) { ToggleMainUI(); }
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => UnoInterface.Toggle();
}
