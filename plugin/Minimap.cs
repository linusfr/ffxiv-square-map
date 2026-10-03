using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SquareMap;

internal sealed unsafe class Minimap : IDisposable
{
    private const uint CircularCollisionFlag = 1 << 23;

    private readonly PenumbraBridge _penumbra = new(Plugin.PluginInterface);

    private nint _addonAddress;
    private nint _zoomAddonAddress;
    private float _nativeZoom;
    private float _lastAppliedZoom = float.NaN;
    private State? _lastState;
    private bool _adjustmentsApplied;
    private bool _layoutCaptured;
    private NodeTransform _mapBaseTransform;
    private NodeTransform _maskTransform;
    private NodeTransform _collisionTransform;

    internal bool IsPenumbraAvailable => _penumbra.IsAvailable;
    internal string SquareMaskStatus => _penumbra.Status;
    internal bool IsSquareMaskActive => _penumbra.IsRegistered;

    internal void Apply(Configuration config)
    {
        _penumbra.Update(config.Enabled && config.Square);

        if (!TryGetAddon(out var addon))
        {
            _addonAddress = 0;
            _lastState = null;
            _adjustmentsApplied = false;
            _layoutCaptured = false;
            return;
        }

        var address = (nint)addon;
        if (_addonAddress != address)
        {
            _addonAddress = address;
            _lastState = null;
            _adjustmentsApplied = false;
            _layoutCaptured = false;
        }

        if (!_layoutCaptured && TryGetNodes(addon, out var initialNodes))
        {
            _mapBaseTransform = NodeTransform.From(initialNodes.MapBase);
            _maskTransform = NodeTransform.From(initialNodes.Mask);
            _collisionTransform = NodeTransform.From(initialNodes.Collision);
            _layoutCaptured = true;
        }

        if (!config.Enabled)
        {
            if (_adjustmentsApplied)
                Restore(addon);

            _lastState = null;
            _adjustmentsApplied = false;
            return;
        }

        var state = State.From(config);
        if (_lastState == state)
            return;

        if (!TryGetNodes(addon, out var nodes))
            return;

        SetVisible(nodes.FancyBorder, !state.Square && !state.HideFancyBorder);
        SetVisible(nodes.Border, !state.Square && !state.HideBorder);
        SetVisible(nodes.Coordinates, !state.HideCoordinates);
        SetVisible(nodes.CompassLock, !state.HideCompassLock);
        SetVisible(nodes.Directions, !state.HideDirections);
        SetVisible(nodes.ZoomIn, !state.HideZoom);
        SetVisible(nodes.ZoomOut, !state.HideZoom);
        SetVisible(nodes.Sun, !state.HideSun);
        SetVisible(nodes.Weather, !state.HideWeather);
        SetSquareCollision(nodes.Collision, state.Square);
        SetViewport(nodes, state.Square ? state.ViewportWidth : 1f, state.Square ? state.ViewportHeight : 1f);

        _lastState = state;
        _adjustmentsApplied = true;
    }

    internal void DrawBorder(Configuration config)
    {
        if (!config.Enabled || !config.Square || config.HideBorder)
            return;

        if (!TryGetAddon(out var addon))
            return;

        var mask = addon->GetNodeById(17);
        if (mask == null)
            return;

        var position = GetNodePosition(mask) + ImGui.GetMainViewport().Pos;
        var scale = GetNodeScale(mask);
        var size = new Vector2(mask->Width, mask->Height) * scale;
        var maximum = position + size;
        var drawList = ImGui.GetBackgroundDrawList();

        drawList.AddRect(position, maximum, ImGui.GetColorU32(new Vector4(0.03f, 0.03f, 0.03f, 0.95f)), 0f, ImDrawFlags.None, 5f);
        drawList.AddRect(position, maximum, ImGui.GetColorU32(new Vector4(0.48f, 0.46f, 0.40f, 0.95f)), 0f, ImDrawFlags.None, 1.5f);
    }

    private static Vector2 GetNodePosition(AtkResNode* node)
    {
        var position = new Vector2(node->X, node->Y);
        position -= new Vector2(node->OriginX * (node->ScaleX - 1), node->OriginY * (node->ScaleY - 1));

        for (var parent = node->ParentNode; parent != null; parent = parent->ParentNode)
        {
            position *= new Vector2(parent->ScaleX, parent->ScaleY);
            position += new Vector2(parent->X, parent->Y);
            position -= new Vector2(parent->OriginX * (parent->ScaleX - 1), parent->OriginY * (parent->ScaleY - 1));
        }

        return position;
    }

    private static Vector2 GetNodeScale(AtkResNode* node)
    {
        var scale = new Vector2(node->ScaleX, node->ScaleY);
        for (var parent = node->ParentNode; parent != null; parent = parent->ParentNode)
            scale *= new Vector2(parent->ScaleX, parent->ScaleY);

        return scale;
    }

    internal void PrepareNativeZoom(nint addonAddress)
    {
        if (addonAddress == 0 || addonAddress != _zoomAddonAddress)
            return;

        var addon = (AddonNaviMap*)addonAddress;
        addon->MarkerPositionScaling = _nativeZoom;
        addon->NaviMap.MarkerPositionScaling = _nativeZoom;
    }

    internal void ApplyExtendedZoom(Configuration config, nint addonAddress)
    {
        if (addonAddress == 0)
            return;

        var addon = (AddonNaviMap*)addonAddress;
        var currentZoom = addon->MarkerPositionScaling;
        if (_zoomAddonAddress != addonAddress)
        {
            _zoomAddonAddress = addonAddress;
            _nativeZoom = currentZoom;
            _lastAppliedZoom = float.NaN;
        }
        else if (!float.IsNaN(_lastAppliedZoom) && MathF.Abs(currentZoom - _lastAppliedZoom) > 0.001f)
        {
            _nativeZoom = currentZoom;
        }

        var atNativeMaximumZoomOut = _nativeZoom <= 0.5001f;
        var multiplier = config.Enabled && atNativeMaximumZoomOut
            ? Math.Clamp(config.ZoomOutMultiplier, 1f, 6f)
            : 1f;
        var targetZoom = _nativeZoom / multiplier;
        addon->MarkerPositionScaling = targetZoom;
        addon->NaviMap.MarkerPositionScaling = targetZoom;
        _lastAppliedZoom = targetZoom;
    }

    private static bool TryGetAddon(out AtkUnitBase* addon)
    {
        var handle = Plugin.GameGui.GetAddonByName("_NaviMap");
        if (handle.IsNull || !handle.IsReady)
        {
            addon = null;
            return false;
        }

        addon = (AtkUnitBase*)handle.Address;
        return addon != null;
    }

    private static bool TryGetNodes(AtkUnitBase* addon, out Nodes nodes)
    {
        nodes = new Nodes(
            addon->GetNodeById(13),
            addon->GetNodeById(15),
            addon->GetNodeById(5),
            addon->GetNodeById(4),
            addon->GetNodeById(8),
            addon->GetNodeById(2),
            addon->GetNodeById(3),
            addon->GetNodeById(16),
            addon->GetNodeById(14),
            addon->GetNodeById(18),
            addon->GetNodeById(17),
            addon->GetNodeById(19));

        return addon->RootNode != null && nodes.AllPresent;
    }

    private void Restore(AtkUnitBase* addon)
    {
        if (!TryGetNodes(addon, out var nodes))
            return;

        SetVisible(nodes.FancyBorder, true);
        SetVisible(nodes.Border, true);
        SetVisible(nodes.Coordinates, true);
        SetVisible(nodes.CompassLock, true);
        SetVisible(nodes.Directions, true);
        SetVisible(nodes.ZoomIn, true);
        SetVisible(nodes.ZoomOut, true);
        SetVisible(nodes.Sun, true);
        SetVisible(nodes.Weather, true);
        SetSquareCollision(nodes.Collision, false);
        SetViewport(nodes, 1f, 1f);
    }

    private void SetViewport(Nodes nodes, float width, float height)
    {
        nodes.MapBase->SetScale(_mapBaseTransform.ScaleX * width, _mapBaseTransform.ScaleY * height);
        nodes.Mask->SetScale(_maskTransform.ScaleX * width, _maskTransform.ScaleY * height);

        var collisionScaleX = _collisionTransform.ScaleX * width;
        var collisionScaleY = _collisionTransform.ScaleY * height;
        var centerX = _collisionTransform.X + (nodes.Collision->Width * _collisionTransform.ScaleX / 2f);
        var centerY = _collisionTransform.Y + (nodes.Collision->Height * _collisionTransform.ScaleY / 2f);
        nodes.Collision->SetScale(collisionScaleX, collisionScaleY);
        nodes.Collision->SetPositionFloat(
            centerX - (nodes.Collision->Width * collisionScaleX / 2f),
            centerY - (nodes.Collision->Height * collisionScaleY / 2f));
    }

    private static void SetVisible(AtkResNode* node, bool visible) => node->ToggleVisibility(visible);

    private static void SetSquareCollision(AtkResNode* node, bool square)
    {
        if (square)
            node->DrawFlags &= ~CircularCollisionFlag;
        else
            node->DrawFlags |= CircularCollisionFlag;
    }

    public void Dispose()
    {
        if (TryGetAddon(out var addon))
        {
            if (_adjustmentsApplied && (nint)addon == _addonAddress)
                Restore(addon);

            if ((nint)addon == _zoomAddonAddress)
            {
                var naviMap = (AddonNaviMap*)addon;
                naviMap->MarkerPositionScaling = _nativeZoom;
                naviMap->NaviMap.MarkerPositionScaling = _nativeZoom;
            }
        }

        _penumbra.Dispose();
    }

    private readonly record struct State(
        bool Square,
        float ViewportWidth,
        float ViewportHeight,
        bool HideFancyBorder,
        bool HideBorder,
        bool HideCoordinates,
        bool HideCompassLock,
        bool HideDirections,
        bool HideZoom,
        bool HideSun,
        bool HideWeather)
    {
        internal static State From(Configuration config)
        {
            var width = Math.Clamp(config.ViewportWidth, 1f, 2f);
            var height = config.SquareViewport
                ? width
                : Math.Clamp(config.ViewportHeight, 1f, 2f);

            return new State(
                config.Square,
                width,
                height,
                config.HideFancyBorder,
                config.HideBorder,
                config.HideCoordinates,
                config.HideCompassLock,
                config.HideDirections,
                config.HideZoom,
                config.HideSun,
                config.HideWeather);
        }
    }

    private readonly struct Nodes
    {
        internal readonly AtkResNode* FancyBorder;
        internal readonly AtkResNode* Border;
        internal readonly AtkResNode* Coordinates;
        internal readonly AtkResNode* CompassLock;
        internal readonly AtkResNode* Directions;
        internal readonly AtkResNode* ZoomIn;
        internal readonly AtkResNode* ZoomOut;
        internal readonly AtkResNode* Sun;
        internal readonly AtkResNode* Weather;
        internal readonly AtkResNode* MapBase;
        internal readonly AtkResNode* Mask;
        internal readonly AtkResNode* Collision;

        internal Nodes(
            AtkResNode* fancyBorder,
            AtkResNode* border,
            AtkResNode* coordinates,
            AtkResNode* compassLock,
            AtkResNode* directions,
            AtkResNode* zoomIn,
            AtkResNode* zoomOut,
            AtkResNode* sun,
            AtkResNode* weather,
            AtkResNode* mapBase,
            AtkResNode* mask,
            AtkResNode* collision)
        {
            FancyBorder = fancyBorder;
            Border = border;
            Coordinates = coordinates;
            CompassLock = compassLock;
            Directions = directions;
            ZoomIn = zoomIn;
            ZoomOut = zoomOut;
            Sun = sun;
            Weather = weather;
            MapBase = mapBase;
            Mask = mask;
            Collision = collision;
        }

        internal bool AllPresent =>
            FancyBorder != null &&
            Border != null &&
            Coordinates != null &&
            CompassLock != null &&
            Directions != null &&
            ZoomIn != null &&
            ZoomOut != null &&
            Sun != null &&
            Weather != null &&
            MapBase != null &&
            Mask != null &&
            Collision != null;
    }

    private readonly record struct NodeTransform(float X, float Y, float ScaleX, float ScaleY)
    {
        internal static NodeTransform From(AtkResNode* node) =>
            new(node->X, node->Y, node->ScaleX, node->ScaleY);
    }
}
