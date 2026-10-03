using System;
using System.Collections.Generic;
using System.IO;

using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace SquareMap;

internal sealed class PenumbraBridge : IDisposable
{
    private const string Tag = "SquareMap.VisualMask";
    private const int Priority = 0;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(3);

    private readonly ICallGateSubscriber<(int Breaking, int Features)> _apiVersion;
    private readonly ICallGateSubscriber<string> _modDirectory;
    private readonly ICallGateSubscriber<string, Dictionary<string, string>, string, int, int> _addTemporaryMod;
    private readonly ICallGateSubscriber<string, int, int> _removeTemporaryMod;
    private readonly Dictionary<string, string> _paths;

    private DateTime _nextCheck;
    private bool _registered;

    /// The frame is squared by this plugin alone, but the round mask only goes
    /// away through Penumbra. When that half fails the map looks half-finished
    /// and the reason is invisible, so it is said once in chat.
    private bool _warned;

    internal bool IsAvailable { get; private set; }

    /// <summary>True once Penumbra has actually taken the mask.</summary>
    internal bool IsRegistered => _registered;

    private string _status = "Penumbra is not available.";

    internal string Status
    {
        get => _status;

        // Every transition is logged: without this the only record of why the
        // mask never applied is a line in a settings window nobody has open.
        private set
        {
            if (_status == value)
                return;

            _status = value;
            Plugin.Log.Information($"SquareMap: {value}");
        }
    }

    internal PenumbraBridge(IDalamudPluginInterface pluginInterface)
    {
        // The first check waits: the plugin loads while Dalamud is still
        // starting others, and the Penumbra call is not cheap on that frame.
        _nextCheck = DateTime.UtcNow + FirstCheckDelay;

        _apiVersion = pluginInterface.GetIpcSubscriber<(int Breaking, int Features)>("Penumbra.ApiVersion.V5");
        _modDirectory = pluginInterface.GetIpcSubscriber<string>("Penumbra.GetModDirectory.V5");
        _addTemporaryMod = pluginInterface.GetIpcSubscriber<string, Dictionary<string, string>, string, int, int>("Penumbra.AddTemporaryModAll.V5");
        _removeTemporaryMod = pluginInterface.GetIpcSubscriber<string, int, int>("Penumbra.RemoveTemporaryModAll.V5");

        var assemblyDirectory = pluginInterface.AssemblyLocation.Directory
            ?? throw new InvalidOperationException("Could not locate the Square Map plugin directory.");
        _paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ui/uld/navimap_mask.tex"] = Path.Combine(assemblyDirectory.FullName, "assets", "square-mask.tex"),
            ["ui/uld/navimap_mask_hr1.tex"] = Path.Combine(assemblyDirectory.FullName, "assets", "square-mask-hr1.tex"),
        };
    }

    internal void Update(bool enabled)
    {
        if (!enabled)
        {
            Remove();
            return;
        }

        if (DateTime.UtcNow < _nextCheck)
            return;

        _nextCheck = DateTime.UtcNow + CheckInterval;

        try
        {
            var version = _apiVersion.InvokeFunc();
            if (version.Breaking != 5)
            {
                IsAvailable = false;
                Status = $"Penumbra API {version.Breaking} is unsupported; API 5 is required.";
                Warn();
                return;
            }

            IsAvailable = true;
            if (_registered)
                return;

            // A freshly installed Penumbra has no mod directory, and in that
            // state it silently ignores temporary mods. This is the common
            // reason the frame goes square and the map stays round.
            if (string.IsNullOrWhiteSpace(ModDirectory()))
            {
                Status = "Penumbra has no mod directory yet; set one in /penumbra, restart the game, and the mask will apply.";
                Warn();
                return;
            }

            foreach (var path in _paths.Values)
            {
                if (!File.Exists(path))
                {
                    Status = $"Square mask resource is missing: {Path.GetFileName(path)}";
                    Warn();
                    return;
                }
            }

            var result = _addTemporaryMod.InvokeFunc(Tag, _paths, string.Empty, Priority);
            if (result is 0 or 1)
            {
                _registered = true;
                Status = "Square mask is active through Penumbra.";
                return;
            }

            Status = $"Penumbra rejected the square mask (error {result}).";
            Warn();
        }
        catch (Exception exception)
        {
            IsAvailable = false;
            _registered = false;
            Status = "Penumbra is required for the visual square mask.";
            Plugin.Log.Debug(exception, "SquareMap: the Penumbra call failed.");
            Warn();
        }
    }

    /// <summary>Penumbra's mod root, or empty when it has never been set.</summary>
    private string ModDirectory()
    {
        try
        {
            return _modDirectory.InvokeFunc();
        }
        catch (Exception)
        {
            // An older Penumbra without this call is not a reason to refuse the
            // mask; let the registration below decide.
            return "assume it is set";
        }
    }

    /// <summary>Says once why the map is only half square.</summary>
    private void Warn()
    {
        if (_warned)
            return;

        _warned = true;
        Plugin.ChatGui.Print($"[Square Map] the frame is square, but the round mask is not: {Status}");
    }

    private void Remove()
    {
        if (!_registered)
            return;

        try
        {
            _removeTemporaryMod.InvokeFunc(Tag, Priority);
        }
        catch (Exception)
        {
            // Penumbra may already be unloading. Its caller tracking removes the
            // temporary mod when this plugin is disposed.
        }

        _registered = false;
        _warned = false;
        Status = "Square mask is disabled.";
    }

    public void Dispose() => Remove();
}
