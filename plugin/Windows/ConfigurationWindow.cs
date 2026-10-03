using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace SquareMap.Windows;

public sealed class ConfigurationWindow
{
    private readonly Plugin _plugin;
    private Configuration Config => _plugin.Config;

    public bool IsVisible { get; set; }

    public ConfigurationWindow(Plugin plugin) => _plugin = plugin;

    public void Draw()
    {
        if (!IsVisible)
            return;

        ImGui.SetNextWindowSize(new Vector2(430, 430), ImGuiCond.FirstUseEver);
        var visible = IsVisible;
        if (!ImGui.Begin("Square Map###SquareMapSettings", ref visible))
        {
            IsVisible = visible;
            ImGui.End();
            return;
        }

        IsVisible = visible;

        DrawToggle("Enable minimap adjustments", nameof(Config.Enabled), Config.Enabled);

        ImGui.Spacing();
        ImGui.TextUnformatted("Shape");
        DrawToggle("Enable square/rectangle shape", nameof(Config.Square), Config.Square);
        DrawViewportSize();
        DrawZoom();
        if (Config.Enabled && Config.Square)
        {
            Hint(_plugin.SquareMaskStatus);
            if (!_plugin.IsPenumbraAvailable)
                Hint("Install Penumbra, then reload Square Map or relog.");
            else if (!_plugin.IsSquareMaskActive)
                Hint("The frame is square but the mask is not — see the line above.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted("Frame");
        DrawToggle("Hide ornamental frame", nameof(Config.HideFancyBorder), Config.HideFancyBorder);
        DrawToggle("Hide border", nameof(Config.HideBorder), Config.HideBorder);

        ImGui.Spacing();
        ImGui.TextUnformatted("Elements");
        DrawToggle("Hide coordinates", nameof(Config.HideCoordinates), Config.HideCoordinates);
        DrawToggle("Hide compass lock", nameof(Config.HideCompassLock), Config.HideCompassLock);
        DrawToggle("Hide cardinal directions", nameof(Config.HideDirections), Config.HideDirections);
        DrawToggle("Hide zoom buttons", nameof(Config.HideZoom), Config.HideZoom);
        DrawToggle("Hide sun", nameof(Config.HideSun), Config.HideSun);
        DrawToggle("Hide weather", nameof(Config.HideWeather), Config.HideWeather);

        ImGui.End();
    }

    private void DrawViewportSize()
    {
        var square = Config.SquareViewport;
        if (ImGui.Checkbox("Square viewport", ref square))
        {
            Config.SquareViewport = square;
            if (square)
                Config.ViewportHeight = Config.ViewportWidth;
            _plugin.SaveConfig();
        }

        if (Config.SquareViewport)
        {
            var size = Config.ViewportWidth * 100f;
            ImGui.SetNextItemWidth(220f);
            if (!ImGui.SliderFloat("Viewport size", ref size, 100f, 200f, "%.0f%%"))
                return;

            Config.ViewportWidth = size / 100f;
            Config.ViewportHeight = Config.ViewportWidth;
            _plugin.SaveConfig();
            return;
        }

        var width = Config.ViewportWidth * 100f;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Viewport width", ref width, 100f, 200f, "%.0f%%"))
        {
            Config.ViewportWidth = width / 100f;
            _plugin.SaveConfig();
        }

        var height = Config.ViewportHeight * 100f;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Viewport height", ref height, 100f, 200f, "%.0f%%"))
        {
            Config.ViewportHeight = height / 100f;
            _plugin.SaveConfig();
        }
    }

    private void DrawZoom()
    {
        var extraPercent = (Config.ZoomOutMultiplier - 1f) * 100f;
        ImGui.SetNextItemWidth(220f);
        if (ImGui.SliderFloat("Extra zoom out", ref extraPercent, 0f, 500f, "+%.0f%%"))
        {
            Config.ZoomOutMultiplier = 1f + (extraPercent / 100f);
            _plugin.SaveConfig();
        }

        Hint("Applied only beyond the native maximum zoom-out level; native zoom-in levels remain unchanged.");
    }

    private void DrawToggle(string label, string property, bool value)
    {
        if (!ImGui.Checkbox(label, ref value))
            return;

        typeof(Configuration).GetProperty(property)!.SetValue(Config, value);
        _plugin.SaveConfig();
    }

    private static void Hint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }
}
