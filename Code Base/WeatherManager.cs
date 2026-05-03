using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{

    public class WeatherSimulator
    {
        public ClimateState CurrentClimate { get; private set; }
        public float EffectiveTemperature { get; private set; }

        public WeatherType? ForcedWeather { get; set; } = null;
        public WeatherType CurrentWeather { get; private set; }
        public Season CurrentSeason { get; set; } = Season.Spring;

        public WeatherVisualParams Visuals { get; private set; }

        private DailyForecast _currentForecast;

        // Custom LCG random to ensure determinism across hardware
        private uint _rngState;
        private float NextFloat() { _rngState = (_rngState * 1103515245 + 12345) & 0x7FFFFFFF; return _rngState / (float)0x7FFFFFFF; }
        private float NextRange(float min, float max) => min + NextFloat() * (max - min);

        // Core profiles mapped to [Season]
        private readonly Dictionary<Season, SeasonalProfile> _profiles = new Dictionary<Season, SeasonalProfile>
        {
            { Season.Spring, new SeasonalProfile { TempRange = new Vector2(10, 20), HumidityRange = new Vector2(40, 70), WindRange = new Vector2(10, 25), PressureRange = new Vector2(1005, 1020), StormProbability = 0.15f, FogProbability = 0.1f } },
            { Season.Summer, new SeasonalProfile { TempRange = new Vector2(22, 35), HumidityRange = new Vector2(20, 50), WindRange = new Vector2(5, 15), PressureRange = new Vector2(1010, 1025), StormProbability = 0.05f, FogProbability = 0.0f } },
            { Season.Monsoon, new SeasonalProfile { TempRange = new Vector2(25, 38), HumidityRange = new Vector2(60, 95), WindRange = new Vector2(10, 40), PressureRange = new Vector2(995, 1015), StormProbability = 0.6f, FogProbability = 0.05f } },
            { Season.Autumn, new SeasonalProfile { TempRange = new Vector2(15, 25), HumidityRange = new Vector2(30, 60), WindRange = new Vector2(5, 20), PressureRange = new Vector2(1010, 1020), StormProbability = 0.1f, FogProbability = 0.15f } },
            { Season.Fall, new SeasonalProfile { TempRange = new Vector2(5, 15), HumidityRange = new Vector2(40, 70), WindRange = new Vector2(15, 45), PressureRange = new Vector2(1000, 1015), StormProbability = 0.2f, FogProbability = 0.4f } },
            { Season.Winter, new SeasonalProfile { TempRange = new Vector2(-15, 5), HumidityRange = new Vector2(50, 85), WindRange = new Vector2(10, 35), PressureRange = new Vector2(990, 1025), StormProbability = 0.3f, FogProbability = 0.6f } }
        };

        public void GenerateDailyForecast(int worldSeed, int year, Season season, int day)
        {
            CurrentSeason = season;

            // 1. Create a unique seed for this specific month
            uint monthHash = (uint)Math.Abs((worldSeed * 73856093) ^ (year * 19349663) ^ ((int)season * 83492791));
            _rngState = monthHash;

            // 2. Generate Monthly Bias
            float biasT = NextRange(-4, 4);
            float biasH = NextRange(-15, 15);
            float biasP = NextRange(-8, 8);

            // 3. Create a unique seed for this specific day
            uint dayHash = (uint)Math.Abs((monthHash * 13) ^ (day * 37));
            _rngState = dayHash;

            var p = _profiles[season];

            float baseT = NextRange(p.TempRange.X, p.TempRange.Y) + biasT;
            float baseH = NextRange(p.HumidityRange.X, p.HumidityRange.Y) + biasH;
            float baseW = NextRange(p.WindRange.X, p.WindRange.Y);
            float baseP = NextRange(p.PressureRange.X, p.PressureRange.Y) + biasP;

            _currentForecast = new DailyForecast();
            _currentForecast.Morning = new ClimateState { TempC = baseT - 5, Humidity = baseH + 20, WindKph = baseW, PressureHpa = baseP, Instability = 10 };
            _currentForecast.Day = new ClimateState { TempC = baseT + 2, Humidity = baseH - 10, WindKph = baseW + 5, PressureHpa = baseP, Instability = 20 };
            _currentForecast.Afternoon = new ClimateState { TempC = baseT + 5, Humidity = baseH - 20, WindKph = baseW + 10, PressureHpa = baseP, Instability = 40 };
            _currentForecast.Evening = new ClimateState { TempC = baseT - 2, Humidity = baseH + 10, WindKph = baseW - 5, PressureHpa = baseP, Instability = 10 };

            if (NextFloat() < p.FogProbability)
            {
                _currentForecast.Morning.Humidity = 95; _currentForecast.Morning.WindKph = 2; _currentForecast.Morning.TempC -= 3;
            }

            if (NextFloat() < p.StormProbability)
            {
                bool lateStorm = NextFloat() > 0.5f;
                ClimateState stormMod(ClimateState c) { c.PressureHpa -= NextRange(15, 30); c.Humidity = 95; c.WindKph += NextRange(20, 50); c.Instability = 80; c.TempC -= 5; return c; }

                if (lateStorm) _currentForecast.Evening = stormMod(_currentForecast.Evening);
                else { _currentForecast.Afternoon = stormMod(_currentForecast.Afternoon); _currentForecast.Evening = stormMod(_currentForecast.Evening); }
            }

            // Clamp all slots
            void Clamp(ref ClimateState c) { c.TempC = Math.Clamp(c.TempC, -30, 45); c.Humidity = Math.Clamp(c.Humidity, 0, 100); c.WindKph = Math.Clamp(c.WindKph, 0, 150); c.PressureHpa = Math.Clamp(c.PressureHpa, 960, 1040); }
            Clamp(ref _currentForecast.Morning); Clamp(ref _currentForecast.Day); Clamp(ref _currentForecast.Afternoon); Clamp(ref _currentForecast.Evening);
        }

        public void Update(GameTime gameTime, float timeOfDay)
        {
            if (_currentForecast == null) GenerateDailyForecast(42, 1, Season.Spring, 1);

            ClimateState c1, c2; float lerpFactor;
            if (timeOfDay < 9) { c1 = _currentForecast.Morning; c2 = _currentForecast.Day; lerpFactor = (timeOfDay - 4f) / 5.0f; }
            else if (timeOfDay < 14) { c1 = _currentForecast.Day; c2 = _currentForecast.Afternoon; lerpFactor = (timeOfDay - 9f) / 5.0f; }
            else if (timeOfDay < 19) { c1 = _currentForecast.Afternoon; c2 = _currentForecast.Evening; lerpFactor = (timeOfDay - 14f) / 5.0f; }
            else { c1 = _currentForecast.Evening; c2 = _currentForecast.Evening; lerpFactor = 1.0f; }

            lerpFactor = MathHelper.Clamp(lerpFactor, 0f, 1f);

            CurrentClimate = new ClimateState
            {
                TempC = MathHelper.Lerp(c1.TempC, c2.TempC, lerpFactor),
                Humidity = MathHelper.Lerp(c1.Humidity, c2.Humidity, lerpFactor),
                WindKph = MathHelper.Lerp(c1.WindKph, c2.WindKph, lerpFactor),
                PressureHpa = MathHelper.Lerp(c1.PressureHpa, c2.PressureHpa, lerpFactor),
                Instability = MathHelper.Lerp(c1.Instability, c2.Instability, lerpFactor)
            };

            EffectiveTemperature = CurrentClimate.TempC;
            CurrentWeather = ForcedWeather ?? EvaluateWeatherMatrix();
            UpdateVisualParameters();
        }

        private WeatherType EvaluateWeatherMatrix()
        {
            if (CurrentClimate.WindKph > 50 && CurrentClimate.Humidity < 30 && EffectiveTemperature > 25) return WeatherType.DustStorm;
            if (CurrentClimate.WindKph > 30 && CurrentClimate.Humidity < 65) return WeatherType.Windy;

            bool isPrecipitating = CurrentClimate.Humidity > 85 || (CurrentClimate.PressureHpa < 1005 && CurrentClimate.Humidity > 65);

            if (isPrecipitating)
            {
                if (EffectiveTemperature <= 0f) return CurrentClimate.WindKph > 45 ? WeatherType.Blizzard : WeatherType.Snow;
                if (EffectiveTemperature > 0f && EffectiveTemperature <= 3f) return WeatherType.Sleet;

                // Hail: Very high instability, very cold air high up (surface temp < 15)
                if (CurrentClimate.Instability > 80 && EffectiveTemperature < 15f) return WeatherType.Hail;

                // Thunderstorm: Heavy rain + lightning
                if (CurrentClimate.Instability > 60) return WeatherType.Thunderstorm;

                // Drizzle/Dew: Low wind, low instability
                if (CurrentClimate.Instability < 30 && CurrentClimate.WindKph < 15) return WeatherType.Drizzle;

                // Heavy Rain: Very high humidity, dropping pressure
                if (CurrentClimate.Humidity > 95 && CurrentClimate.PressureHpa < 995) return WeatherType.Rain; // You can create a HeavyRain enum if you want to separate it from normal rain

                // Normal Rain
                return WeatherType.Rain;
            }

            if (CurrentClimate.Humidity > 85 && CurrentClimate.WindKph < 15 && EffectiveTemperature < 18) return WeatherType.Fog;
            if (CurrentClimate.PressureHpa >= 1018) return WeatherType.Clear;
            if (CurrentClimate.PressureHpa < 1010 || CurrentClimate.Humidity > 70) return WeatherType.Overcast;
            if (CurrentClimate.Humidity > 40) return WeatherType.PartlyCloudy;

            return WeatherType.Clear;
        }

        private void UpdateVisualParameters()
        {
            WeatherVisualParams v = new WeatherVisualParams();
            v.WindVector = new Vector2(1f, 0f) * (CurrentClimate.WindKph * 2.5f);

            float hFac = CurrentClimate.Humidity / 100f;
            float pFac = MathHelper.Clamp((1025f - CurrentClimate.PressureHpa) / 30f, 0f, 1f);
            v.CloudCover = MathHelper.Clamp(hFac * pFac, 0f, 1f);

            // --- BUG FIX: IF WEATHER IS FORCED, OVERRIDE THE MATH ---
            if (ForcedWeather.HasValue)
            {
                switch (ForcedWeather.Value)
                {
                    case WeatherType.Rain: v.RainIntensity = 1.0f; v.CloudCover = 0.8f; break;
                    case WeatherType.Thunderstorm: v.RainIntensity = 1.0f; v.StormIntensity = 1.0f; v.CloudCover = 1.0f; break;
                    case WeatherType.Drizzle: v.RainIntensity = 0.3f; v.CloudCover = 0.6f; break;
                    case WeatherType.Hail: v.RainIntensity = 1.0f; v.CloudCover = 0.9f; break;
                    case WeatherType.Snow:
                    case WeatherType.Blizzard:
                    case WeatherType.Sleet:
                        v.SnowIntensity = 1.0f; v.CloudCover = 0.9f;
                        if (ForcedWeather.Value == WeatherType.Blizzard) v.WindVector = new Vector2(1f, 0f) * 150f;
                        break;
                    case WeatherType.Fog: v.FogDensity = 0.8f; break;
                    case WeatherType.DustStorm: v.FogDensity = 0.9f; v.WindVector = new Vector2(1f, 0f) * 150f; break;
                    case WeatherType.Windy: v.WindVector = new Vector2(1f, 0f) * 100f; break;
                    case WeatherType.Overcast: v.CloudCover = 0.9f; break;
                    case WeatherType.PartlyCloudy: v.CloudCover = 0.4f; break;
                }
            }
            else
            {
                // Normal Simulation Math
                if (CurrentWeather == WeatherType.Rain || CurrentWeather == WeatherType.Thunderstorm) v.RainIntensity = MathHelper.Clamp((CurrentClimate.Humidity - 60f) / 40f, 0.1f, 1f);
                else if (CurrentWeather == WeatherType.Drizzle) v.RainIntensity = 0.15f;
                else if (CurrentWeather == WeatherType.Hail) v.RainIntensity = 1.0f;

                if (CurrentWeather == WeatherType.Snow || CurrentWeather == WeatherType.Blizzard || CurrentWeather == WeatherType.Sleet)
                    v.SnowIntensity = MathHelper.Clamp((CurrentClimate.Humidity - 60f) / 40f, 0.1f, 1f);

                if (CurrentWeather == WeatherType.Fog) v.FogDensity = MathHelper.Clamp((CurrentClimate.Humidity - 80f) / 20f, 0.3f, 1.0f);
                if (CurrentWeather == WeatherType.DustStorm) v.FogDensity = 0.8f;
                if (CurrentWeather == WeatherType.Thunderstorm) v.StormIntensity = CurrentClimate.Instability / 100f;
                if (CurrentWeather == WeatherType.PartlyCloudy) v.CloudCover = Math.Max(0.2f, v.CloudCover);
            }

            Visuals = v;
        }
        public string GetDebugInfo(float timeOfDay)
        {
            int hours = (int)timeOfDay;
            int minutes = (int)((timeOfDay - hours) * 60);

            return $"--- WEATHER SIMULATOR ---\n" +
                   $"Time: {hours:D2}:{minutes:D2} | Season: {CurrentSeason} ()\n" +
                   $"Condition: {CurrentWeather}\n" +
                   $"Temp (Base): {CurrentClimate.TempC:F1}C | Temp (Effective): {EffectiveTemperature:F1}C\n" +
                   $"Humid: {CurrentClimate.Humidity:F1}% | Wind: {CurrentClimate.WindKph:F1} kph\n" +
                   $"Pressure: {CurrentClimate.PressureHpa:F1} hPa | CAPE: {CurrentClimate.Instability:F0}\n" +
                   $"--- VISUAL OUTPUTS ---\n" +
                   $"Rain: {Visuals.RainIntensity:F2} | Snow: {Visuals.SnowIntensity:F2} | Fog: {Visuals.FogDensity:F2}\n" +
                   $"Clouds: {Visuals.CloudCover:F2} | Drift Speed: {Visuals.WindVector.X:F1} pps";
        }
    }
}