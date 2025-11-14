using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public enum Season
    {
        Spring,
        Summer,
        Monsoon,
        Autumn,
        Fall,
        Winter
    }

    public class WeatherSettings
    {
        public Dictionary<string, WeatherProfile> Profiles { get; set; }
    }

    public class ProceduralRainPreset
    {
        public int CountPrimary { get; set; }
        public int CountSecondary { get; set; }
        public float SlantMultiplier { get; set; } // A single value to control slant
        public float SpeedPrimary { get; set; }
        public float SpeedSecondary { get; set; }
        public float BlurPrimary { get; set; }
        public float BlurSecondary { get; set; }
        public Vector2 SizePrimary { get; set; }
        public Vector2 SizeSecondary { get; set; }
        public Color RainColor { get; set; }
        public float Alpha { get; set; }
    }

    public class WeatherProfile
    {
        public WeatherPreset Weather { get; set; }
        public ColorGradingPreset ColorGrading { get; set; }
        public ProceduralRainPreset ProceduralRain { get; set; }
    }

    public class WeatherPreset
    {
        public int RainCount { get; set; }
        public float WindSpeed { get; set; }
        public float DistortionStrength { get; set; }
        public float WindSpeedMultiplier { get; set; }
    }

    public class ColorGradingPreset
    {
        public float Desaturation { get; set; }
        public Color TintColor { get; set; }
    }
}
