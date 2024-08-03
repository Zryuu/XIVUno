using System.Collections.Generic;
using Dalamud.Game.Command;

namespace Uno.Helpers;

public class CommandManager
{
    private readonly Dictionary<string, CommandInfo> commands = new();
    public const string UnoCommand = "/UNO";
    private Plugin plugin;
    
    public CommandManager(Plugin plugin)
    {
        this.plugin = plugin;
        InitializeCommands();
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
    
    public void DisposeCommands()
    {
        foreach (var command in commands.Keys)
            Services.CommandManager.RemoveHandler(command);
    }
    
    private void UnoCi(string command, string args) { plugin.ToggleMainUI(); }
    
}
