using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Uno;

#pragma warning disable CS8618
public class Services {
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; }
    [PluginService] public static IClientState ClientState { get; set;}
    [PluginService] public static IFramework Framework { get; set;}
    [PluginService] public static IObjectTable ObjectTable { get; set;}
    [PluginService] public static IDutyState DutyState { get; set; }
    [PluginService] public static IGameGui GameGui { get; set; }
    [PluginService] public static IPluginLog Log { get; set; }
    [PluginService] public static ITextureProvider TextureProvider { get; set; }
    [PluginService] public static ICondition Condition { get; set; }
    [PluginService] public static IDataManager DataManager { get; set; }
    [PluginService] public static IChatGui Chat { get; set; }
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; set; }
    [PluginService] public static IAddonEventManager AddonEventManager { get; set; }
    [PluginService] public static ICommandManager CommandManager { get; private set; }
    [PluginService] public static IPartyList Party { get; private set; }
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; }
}
