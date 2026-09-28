using System;

using Dalamud.Configuration;

namespace SquareMap;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool Enabled { get; set; } = true;
    public bool Square { get; set; } = true;
    public bool SquareViewport { get; set; } = true;
    public float ViewportWidth { get; set; } = 1f;
    public float ViewportHeight { get; set; } = 1f;
    public float ZoomOutMultiplier { get; set; } = 2f;
    public bool HideFancyBorder { get; set; } = true;
    public bool HideBorder { get; set; }
    public bool HideCoordinates { get; set; }
    public bool HideCompassLock { get; set; }
    public bool HideDirections { get; set; }
    public bool HideZoom { get; set; }
    public bool HideSun { get; set; }
    public bool HideWeather { get; set; }
}
