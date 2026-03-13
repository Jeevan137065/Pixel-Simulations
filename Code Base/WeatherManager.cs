using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public class WeatherSimulator
    {
        public ClimateState CurrentClimate { get; private set; }
        public float EffectiveTemperature { get; private set; } // Temp WITH Day/Night shift

        private ClimateState _targetClimate;
        private float _transitionSpeed = 0.05f;

        public WeatherType CurrentWeather { get; private set; }
        public Season CurrentSeason { get; set; } = Season.Spring;
        public SeasonPhase Phase { get; set; } = SeasonPhase.Early;

        private Dictionary<Season, SeasonalProfile[]> _regionalClimateData;
        private Random _random = new Random();
        private int _weatherCycleIndex = 0;

        public WeatherVisualParams Visuals { get; private set; }

        public WeatherSimulator()
        {
            InitializeClimateData();
            CurrentClimate = new ClimateState { TempC = 15f, Humidity = 50f, WindKph = 5f, PressureHpa = 1015f, Instability = 20f };
            _targetClimate = CurrentClimate;
        }

        private void InitializeClimateData()
        {
            _regionalClimateData = new Dictionary<Season, SeasonalProfile[]>();

            // --- Example: Western North American Climate (Mountains to High Desert) ---

            // WINTER: Cold, wet (snowy), highly variable pressure (storms), very stable air (no thunderstorms).
            _regionalClimateData[Season.Winter] = new SeasonalProfile[]
            {
                // Early Winter (Transitioning from Fall)
                new SeasonalProfile { TempRange = new Vector2(-5, 10), HumidityRange = new Vector2(40, 80), WindRange = new Vector2(10, 40), PressureRange = new Vector2(995, 1025), InstabilityRange = new Vector2(0, 20), StormProbability = 0.3f },
                // Mid Winter (Deep Freeze)
                new SeasonalProfile { TempRange = new Vector2(-15, 2), HumidityRange = new Vector2(60, 95), WindRange = new Vector2(15, 60), PressureRange = new Vector2(985, 1030), InstabilityRange = new Vector2(0, 10), StormProbability = 0.4f },
                // Late Winter (Starting to thaw)
                new SeasonalProfile { TempRange = new Vector2(-8, 8),  HumidityRange = new Vector2(50, 85), WindRange = new Vector2(10, 50), PressureRange = new Vector2(990, 1020), InstabilityRange = new Vector2(0, 30), StormProbability = 0.25f }
            };

            // SPRING: Warming up, very unstable air (thunderstorms/tornadoes), windy.
            _regionalClimateData[Season.Spring] = new SeasonalProfile[]
            {
                // Early Spring (Volatile)
                new SeasonalProfile { TempRange = new Vector2(0, 15),  HumidityRange = new Vector2(40, 90), WindRange = new Vector2(20, 70), PressureRange = new Vector2(990, 1015), InstabilityRange = new Vector2(20, 60), StormProbability = 0.4f },
                // Mid Spring (Tornado Season)
                new SeasonalProfile { TempRange = new Vector2(10, 25), HumidityRange = new Vector2(50, 95), WindRange = new Vector2(15, 60), PressureRange = new Vector2(995, 1015), InstabilityRange = new Vector2(40, 90), StormProbability = 0.5f },
                // Late Spring (Warming, stabilizing slightly)
                new SeasonalProfile { TempRange = new Vector2(15, 30), HumidityRange = new Vector2(30, 80), WindRange = new Vector2(10, 40), PressureRange = new Vector2(1000, 1020), InstabilityRange = new Vector2(30, 70), StormProbability = 0.3f }
            };

            // SUMMER: Hot, mostly dry, stable high pressure (clear skies), occasional afternoon heat-storms.
            _regionalClimateData[Season.Summer] = new SeasonalProfile[]
            {
                new SeasonalProfile { TempRange = new Vector2(20, 35), HumidityRange = new Vector2(20, 60), WindRange = new Vector2(5,  25), PressureRange = new Vector2(1010, 1025), InstabilityRange = new Vector2(10, 50), StormProbability = 0.1f },
                new SeasonalProfile { TempRange = new Vector2(25, 42), HumidityRange = new Vector2(10, 40), WindRange = new Vector2(0,  20), PressureRange = new Vector2(1015, 1030), InstabilityRange = new Vector2(10, 60), StormProbability = 0.05f },
                new SeasonalProfile { TempRange = new Vector2(22, 38), HumidityRange = new Vector2(15, 50), WindRange = new Vector2(5,  30), PressureRange = new Vector2(1010, 1025), InstabilityRange = new Vector2(20, 70), StormProbability = 0.15f }
            };

            // MONSOON: (Late Summer/Early Fall specific to Western US). Very hot, sudden massive spikes in humidity and extreme instability.
            _regionalClimateData[Season.Monsoon] = new SeasonalProfile[]
            {
                new SeasonalProfile { TempRange = new Vector2(25, 40), HumidityRange = new Vector2(30, 85), WindRange = new Vector2(10, 50), PressureRange = new Vector2(1005, 1015), InstabilityRange = new Vector2(60, 100), StormProbability = 0.6f },
                new SeasonalProfile { TempRange = new Vector2(22, 35), HumidityRange = new Vector2(40, 95), WindRange = new Vector2(15, 65), PressureRange = new Vector2(1000, 1010), InstabilityRange = new Vector2(70, 100), StormProbability = 0.8f },
                new SeasonalProfile { TempRange = new Vector2(18, 30), HumidityRange = new Vector2(30, 80), WindRange = new Vector2(10, 40), PressureRange = new Vector2(1005, 1015), InstabilityRange = new Vector2(40, 80),  StormProbability = 0.4f }
            };

            // AUTUMN (Early Fall): Cooling down, drying out, stable air, beautiful clear days.
            _regionalClimateData[Season.Autumn] = new SeasonalProfile[]
            {
                new SeasonalProfile { TempRange = new Vector2(15, 28), HumidityRange = new Vector2(20, 60), WindRange = new Vector2(5,  30), PressureRange = new Vector2(1010, 1025), InstabilityRange = new Vector2(10, 40), StormProbability = 0.1f },
                new SeasonalProfile { TempRange = new Vector2(10, 22), HumidityRange = new Vector2(25, 65), WindRange = new Vector2(10, 35), PressureRange = new Vector2(1005, 1020), InstabilityRange = new Vector2(10, 30), StormProbability = 0.2f },
                new SeasonalProfile { TempRange = new Vector2(5,  18), HumidityRange = new Vector2(30, 70), WindRange = new Vector2(10, 40), PressureRange = new Vector2(1000, 1020), InstabilityRange = new Vector2(0,  20), StormProbability = 0.25f }
            };

            // FALL (Late Fall/Pre-Winter): Cold snaps, increasing moisture, wind picks up.
            _regionalClimateData[Season.Fall] = new SeasonalProfile[]
            {
                new SeasonalProfile { TempRange = new Vector2(0,  15), HumidityRange = new Vector2(35, 75), WindRange = new Vector2(15, 45), PressureRange = new Vector2(995, 1020),  InstabilityRange = new Vector2(0,  20), StormProbability = 0.3f },
                new SeasonalProfile { TempRange = new Vector2(-5, 10), HumidityRange = new Vector2(40, 85), WindRange = new Vector2(15, 55), PressureRange = new Vector2(990, 1025),  InstabilityRange = new Vector2(0,  15), StormProbability = 0.4f },
                new SeasonalProfile { TempRange = new Vector2(-10, 5), HumidityRange = new Vector2(50, 90), WindRange = new Vector2(20, 60), PressureRange = new Vector2(985, 1025),  InstabilityRange = new Vector2(0,  10), StormProbability = 0.5f }
            };
        }
        public void SetTargetWeather(ClimateState targetState) { _targetClimate = targetState; }

        public void CycleWeather()
        {
            _weatherCycleIndex = (_weatherCycleIndex + 1) % 6;
            CurrentSeason = (Season)_weatherCycleIndex;
            GenerateNextClimate(CurrentSeason, Phase);
        }

        public void CyclePhase()
        {
            Phase = (SeasonPhase)(((int)Phase + 1) % 3);
            GenerateNextClimate(CurrentSeason, Phase);
        }

        public void GenerateNextClimate(Season currentSeason, SeasonPhase currentPhase)
        {
            ClimateState target = new ClimateState();
            SeasonalProfile profile = _regionalClimateData[currentSeason][(int)currentPhase];

            target.TempC = GetRandomFloat(profile.TempRange.X, profile.TempRange.Y);
            target.Humidity = GetRandomFloat(profile.HumidityRange.X, profile.HumidityRange.Y);
            target.WindKph = GetRandomFloat(profile.WindRange.X, profile.WindRange.Y);
            target.PressureHpa = GetRandomFloat(profile.PressureRange.X, profile.PressureRange.Y);
            target.Instability = GetRandomFloat(profile.InstabilityRange.X, profile.InstabilityRange.Y);

            if (_random.NextDouble() < profile.StormProbability)
            {
                if (_random.NextDouble() < 0.7) // Low Pressure Front (Storms)
                {
                    target.PressureHpa -= _random.Next(10, 25);
                    target.Humidity += _random.Next(20, 40);
                    target.WindKph += _random.Next(15, 30); // Capped wind jumps
                    target.Instability += _random.Next(20, 50);
                    target.TempC -= _random.Next(2, 6);
                }
                else // High Pressure Front (Clear/Dry)
                {
                    target.PressureHpa += _random.Next(10, 20);
                    target.Humidity -= _random.Next(20, 50);
                    target.Instability = 0;
                    if (currentSeason == Season.Summer || currentSeason == Season.Monsoon) target.TempC += _random.Next(5, 10);
                    else if (currentSeason == Season.Winter || currentSeason == Season.Fall) target.TempC -= _random.Next(5, 10);
                }
            }

            // Realistic Sanity Clamps
            target.TempC = MathHelper.Clamp(target.TempC, -30f, 45f);
            target.Humidity = MathHelper.Clamp(target.Humidity, 0f, 100f);
            target.WindKph = MathHelper.Clamp(target.WindKph, 0f, 100f); // Max 100kph prevents physics breaking
            target.PressureHpa = MathHelper.Clamp(target.PressureHpa, 960f, 1040f);
            target.Instability = MathHelper.Clamp(target.Instability, 0f, 100f);

            SetTargetWeather(target);
        }

        private float GetRandomFloat(float min, float max) => (float)(_random.NextDouble() * (max - min) + min);

        // --- NEW: Added timeOfDay parameter ---
        public void Update(GameTime gameTime, float timeOfDay)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float lerpSpeed = 0.5f * dt;

            ClimateState c = CurrentClimate;
            c.TempC = MathHelper.Lerp(c.TempC, _targetClimate.TempC, lerpSpeed);
            c.Humidity = MathHelper.Lerp(c.Humidity, _targetClimate.Humidity, lerpSpeed);
            c.WindKph = MathHelper.Lerp(c.WindKph, _targetClimate.WindKph, lerpSpeed);
            c.PressureHpa = MathHelper.Lerp(c.PressureHpa, _targetClimate.PressureHpa, lerpSpeed);
            c.Instability = MathHelper.Lerp(c.Instability, _targetClimate.Instability, lerpSpeed);
            CurrentClimate = c;

            // --- DIURNAL CYCLE (Sun affects temperature!) ---
            // Base temp is coldest at 4 AM, hottest at 4 PM (16:00). Shifts temp by +/- 4 degrees.
            float diurnalShift = (float)Math.Sin((timeOfDay - 10f) * (Math.PI / 12f)) * 4.0f;
            EffectiveTemperature = CurrentClimate.TempC + diurnalShift;

            CurrentWeather = EvaluateWeatherMatrix();
            UpdateVisualParameters();
        }

        private WeatherType EvaluateWeatherMatrix()
        {
            if (CurrentClimate.WindKph > 50 && CurrentClimate.Humidity < 30 && EffectiveTemperature > 25) return WeatherType.DustStorm;

            bool isPrecipitating = CurrentClimate.Humidity > 85 || (CurrentClimate.PressureHpa < 1005 && CurrentClimate.Humidity > 65);

            if (isPrecipitating)
            {
                if (EffectiveTemperature <= 0f) return CurrentClimate.WindKph > 45 ? WeatherType.Blizzard : WeatherType.Snow;
                if (EffectiveTemperature > 0f && EffectiveTemperature <= 3f) return WeatherType.Sleet;
                if (CurrentClimate.Instability > 60) return (EffectiveTemperature < 15f && CurrentClimate.Instability > 80) ? WeatherType.Hail : WeatherType.Thunderstorm;
                if (CurrentClimate.Instability < 30 && CurrentClimate.WindKph < 15) return WeatherType.Drizzle;
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

            // --- WIND FIX ---
            // Visual wind vector is now purely horizontal drift. 
            // 1 Kph = 2.5 pixels per second visual drift. 100kph = 250pps (Fast, but readable).
            float driftPPS = CurrentClimate.WindKph * 2.5f;
            v.WindVector = new Vector2(1f, 0f) * driftPPS;

            // Cloud Cover
            float pressureFactor = MathHelper.Clamp((1020f - CurrentClimate.PressureHpa) / 20f, 0f, 1f);
            float humidityFactor = MathHelper.Clamp(CurrentClimate.Humidity / 80f, 0f, 1f);
            v.CloudCover = MathHelper.Clamp(pressureFactor * humidityFactor * 1.5f, 0f, 1f);

            // Intensities
            if (CurrentWeather == WeatherType.Rain || CurrentWeather == WeatherType.Thunderstorm)
                v.RainIntensity = MathHelper.Clamp((CurrentClimate.Humidity - 60f) / 40f, 0.1f, 1f);
            else if (CurrentWeather == WeatherType.Drizzle) v.RainIntensity = 0.15f;

            if (CurrentWeather == WeatherType.Snow || CurrentWeather == WeatherType.Blizzard)
                v.SnowIntensity = MathHelper.Clamp((CurrentClimate.Humidity - 60f) / 40f, 0.1f, 1f);

            if (CurrentWeather == WeatherType.Fog) v.FogDensity = MathHelper.Clamp((CurrentClimate.Humidity - 80f) / 20f, 0.3f, 1.0f);
            if (CurrentWeather == WeatherType.DustStorm) v.FogDensity = 0.8f;
            if (CurrentWeather == WeatherType.Thunderstorm) v.StormIntensity = CurrentClimate.Instability / 100f;

            Visuals = v;
        }

        public string GetDebugInfo(float timeOfDay)
        {
            int hours = (int)timeOfDay;
            int minutes = (int)((timeOfDay - hours) * 60);

            return $"--- WEATHER SIMULATOR ---\n" +
                   $"Time: {hours:D2}:{minutes:D2} | Season: {CurrentSeason} ({Phase})\n" +
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