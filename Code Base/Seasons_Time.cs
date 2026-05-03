using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public enum WeatherType
    {
        Clear, PartlyCloudy, Overcast, Fog, Drizzle, Rain, Thunderstorm,
        Hail, Snow, Blizzard, Sleet, DustStorm, Windy // NEW: Added Windy
    }
    public class DailyForecast
    {
        public ClimateState Morning, Day, Afternoon, Evening;
    }

    public struct ClimateState
    {
        public float TempC;       // -30 to 45 (Celsius)
        public float Humidity;    // 0 to 100 (Percentage)
        public float WindKph;     // 0 to 150 (Kilometers per hour)
        public float PressureHpa; // 980 (Storm) to 1030 (Clear)
        public float Instability; // 0 (Stable) to 100 (Violent updrafts)
    }
    public struct SeasonalProfile
    {
        public Vector2 TempRange { get; set; }
        public Vector2 HumidityRange { get; set; }
        public Vector2 WindRange { get; set; }
        public Vector2 PressureRange { get; set; }
        public Vector2 InstabilityRange { get; set; }
        public float StormProbability { get; set; }
        public float FogProbability { get; set; }
    }
    public enum Season
    {
        Spring, Summer, Monsoon, Autumn, Fall, Winter
        // Note: Autumn and Fall are split per your request. 
        // (E.g., Autumn = early cooling, Fall = late freezing/leaf drop)
    }
    public enum SeasonPhase
    {
        Early, Mid, Late
    }

    // This is the clean data handed over to the ShaderManager
    public struct WeatherVisualParams
    {
        public float RainIntensity;  // 0.0 to 1.0
        public float SnowIntensity;  // 0.0 to 1.0
        public float FogDensity;     // 0.0 to 1.0
        public Vector2 WindVector;   // Direction and Speed
        public float CloudCover;     // 0.0 to 1.0
        public float StormIntensity; // 0.0 to 1.0 (for lightning/screen flashes)
    }
}
