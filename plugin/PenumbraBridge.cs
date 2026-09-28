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

    private readonly ICallGateSubscriber<(int Breaking, int Features)> _apiVersion;
    private readonly ICallGateSubscriber<string, Dictionary<string, string>, string, int, int> _addTemporaryMod;
    private readonly ICallGateSubscriber<string, int, int> _removeTemporaryMod;
    private readonly Dictionary<string, string> _paths;

    private DateTime _nextCheck;
    private bool _registered;

    internal bool IsAvailable { get; private set; }
    internal string Status { get; private set; } = "Penumbra is not available.";

    internal PenumbraBridge(IDalamudPluginInterface pluginInterface)
    {
        _apiVersion = pluginInterface.GetIpcSubscriber<(int Breaking, int Features)>("Penumbra.ApiVersion.V5");
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
                return;
            }

            IsAvailable = true;
            if (_registered)
                return;

            foreach (var path in _paths.Values)
            {
                if (!File.Exists(path))
                {
                    Status = $"Square mask resource is missing: {Path.GetFileName(path)}";
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
        }
        catch (Exception)
        {
            IsAvailable = false;
            _registered = false;
            Status = "Penumbra is required for the visual square mask.";
        }
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
        Status = "Square mask is disabled.";
    }

    public void Dispose() => Remove();
}
