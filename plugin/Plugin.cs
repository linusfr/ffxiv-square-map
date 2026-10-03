using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SquareMap.Windows;

namespace SquareMap;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string Command = "/squaremap";

    internal Configuration Config { get; }
    internal bool IsPenumbraAvailable => _minimap.IsPenumbraAvailable;
    internal string SquareMaskStatus => _minimap.SquareMaskStatus;
    internal bool IsSquareMaskActive => _minimap.IsSquareMaskActive;

    private readonly Minimap _minimap;
    private readonly ConfigurationWindow _window;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _minimap = new Minimap();
        _window = new ConfigurationWindow(this);

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Square Map settings. /squaremap toggle turns it off and on.",
        });

        Framework.Update += OnUpdate;
        AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "_NaviMap", OnNaviMapPreUpdate);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "_NaviMap", OnNaviMapPreReceiveEvent);
        AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "_NaviMap", OnNaviMapPostReceiveEvent);
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi += OpenConfig;

        Log.Info("SquareMap: Plugin loaded.");
    }

    private void OnUpdate(IFramework framework) => _minimap.Apply(Config);

    private void OnNaviMapPreUpdate(AddonEvent type, AddonArgs args) =>
        _minimap.ApplyExtendedZoom(Config, args.Addon.Address);

    private void OnNaviMapPreReceiveEvent(AddonEvent type, AddonArgs args) =>
        _minimap.PrepareNativeZoom(args.Addon.Address);

    private void OnNaviMapPostReceiveEvent(AddonEvent type, AddonArgs args) =>
        _minimap.ApplyExtendedZoom(Config, args.Addon.Address);

    private void OnDraw()
    {
        _minimap.DrawBorder(Config);
        _window.Draw();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("toggle", System.StringComparison.OrdinalIgnoreCase))
        {
            Config.Enabled = !Config.Enabled;
            SaveConfig();
            return;
        }

        _window.IsVisible = !_window.IsVisible;
    }

    private void OpenConfig() => _window.IsVisible = true;

    internal void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
        AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, "_NaviMap", OnNaviMapPreUpdate);
        AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "_NaviMap", OnNaviMapPreReceiveEvent);
        AddonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, "_NaviMap", OnNaviMapPostReceiveEvent);
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;
        CommandManager.RemoveHandler(Command);
        _minimap.Dispose();
    }
}
