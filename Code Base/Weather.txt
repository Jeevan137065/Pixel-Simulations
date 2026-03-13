using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Pixel_Simulations
{

    public class WeatherSimulator
    {
        public ClimateState CurrentClimate { get; private set; }
        private ClimateState _targetClimate;
        private float _transitionSpeed = 0.05f; // Adjust for slower/faster transitions
        public WeatherType CurrentWeather { get; private set; }
        public Season CurrentSeason { get; set; } = Season.Spring;
        public SeasonPhase Phase { get; set; } = SeasonPhase.Early;
        private Dictionary<Season, SeasonalProfile[]> _regionalClimateData;
        private Random _random = new Random();

        // Visual outputs for the Shader Manager
        public WeatherVisualParams Visuals { get; private set; }
        private int _weatherCycleIndex = 0;
        public WeatherSimulator()
        {
            InitializeClimateData();
            // Start the simulation with some default values
            CurrentClimate = new ClimateState { TempC = 20f, Humidity = 50f, WindKph = 10f, PressureHpa = 1015f, Instability = 20f };
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
        public void SetTargetWeather(ClimateState targetState)
        {
            _targetClimate = targetState;
        }
        public void CycleWeather()
        {
            _weatherCycleIndex = (_weatherCycleIndex + 1) % 6; // Cycles 0, 1, 2, 3,4,5
            switch (_weatherCycleIndex)
            {
                case 0: // Spring
                    CurrentSeason = Season.Spring;
                    GenerateNextClimate(CurrentSeason,SeasonPhase.Mid);
                    break;
                case 1: // Summer
                    CurrentSeason = Season.Summer;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid); 
                    break;
                case 2: // Monsoon
                    CurrentSeason = Season.Monsoon;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
                case 3: // Fall
                    CurrentSeason = Season.Fall;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
                case 4: // Autumn
                    CurrentSeason = Season.Autumn;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
                case 5: // Winter
                    CurrentSeason = Season.Winter;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
            }
        }
        public void CyclePhase()
        {
            _weatherCycleIndex = (_weatherCycleIndex + 1) % 3; // Cycles 0, 1, 2
            switch (_weatherCycleIndex)
            {
                case 0: // Spring
                    Phase = SeasonPhase.Early;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
                case 1: // Summer
                    Phase = SeasonPhase.Mid;
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
                case 2: // Monsoon
                    Phase = SeasonPhase.Late; 
                    GenerateNextClimate(CurrentSeason, SeasonPhase.Mid);
                    break;
            }
        }

        public void GenerateNextClimate(Season currentSeason, SeasonPhase currentPhase)
        {
            ClimateState target = new ClimateState();

            // 1. Get the baseline profile for this exact time of year
            SeasonalProfile profile = _regionalClimateData[currentSeason][(int)currentPhase];

            // 2. Generate base values within the profile's normal range
            target.TempC = GetRandomFloat(profile.TempRange.X, profile.TempRange.Y);
            target.Humidity = GetRandomFloat(profile.HumidityRange.X, profile.HumidityRange.Y);
            target.WindKph = GetRandomFloat(profile.WindRange.X, profile.WindRange.Y);
            target.PressureHpa = GetRandomFloat(profile.PressureRange.X, profile.PressureRange.Y);
            target.Instability = GetRandomFloat(profile.InstabilityRange.X, profile.InstabilityRange.Y);

            // 3. Apply Major Weather Events (Fronts)
            // If the random roll is less than the storm probability for this season...
            if (_random.NextDouble() < profile.StormProbability)
            {
                // Is it a Low Pressure system (Storm) or High Pressure system (Heatwave/Deep Freeze)?
                if (_random.NextDouble() < 0.7) // 70% chance storms are Low Pressure (wet/windy)
                {
                    // LOW PRESSURE FRONT ROLLS IN
                    target.PressureHpa -= _random.Next(10, 25); // Pressure drops significantly
                    target.Humidity += _random.Next(20, 40);    // Sucks in moisture
                    target.WindKph += _random.Next(15, 40);     // Winds pick up
                    target.Instability += _random.Next(10, 50); // Air becomes volatile (Thunderstorms likely)

                    // Low pressure usually cools things down slightly due to cloud cover
                    target.TempC -= _random.Next(2, 8);
                }
                else
                {
                    // HIGH PRESSURE FRONT ROLLS IN
                    target.PressureHpa += _random.Next(10, 20); // Pressure skyrockets
                    target.Humidity -= _random.Next(20, 50);    // Dries the air out completely (Clear skies)
                    target.Instability = 0;                     // Air becomes dead stable

                    // High pressure in summer = Heatwave. High pressure in winter = Deep Freeze.
                    if (currentSeason == Season.Summer || currentSeason == Season.Monsoon)
                        target.TempC += _random.Next(5, 12);
                    else if (currentSeason == Season.Winter || currentSeason == Season.Fall)
                        target.TempC -= _random.Next(5, 15);
                }
            }

            // 4. Final Sanity Clamps (Physics limits)
            target.TempC = MathHelper.Clamp(target.TempC, -40f, 55f);
            target.Humidity = MathHelper.Clamp(target.Humidity, 0f, 100f);
            target.WindKph = MathHelper.Clamp(target.WindKph, 0f, 100); // 200kph = Category 3 Hurricane
            target.PressureHpa = MathHelper.Clamp(target.PressureHpa, 950f, 1050f);
            target.Instability = MathHelper.Clamp(target.Instability, 0f, 100f);

            // 5. Tell the simulator to start smoothly lerping to this new reality
            SetTargetWeather(target);
        }
        private float GetRandomFloat(float min, float max)
        {
            return (float)(_random.NextDouble() * (max - min) + min);
        }
        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float lerpSpeed = 0.5f * dt; // Adjust for how fast weather rolls in

            // 1. Smoothly interpolate real-world values
            ClimateState c = CurrentClimate;
            c.TempC = MathHelper.Lerp(c.TempC, _targetClimate.TempC, lerpSpeed);
            c.Humidity = MathHelper.Lerp(c.Humidity, _targetClimate.Humidity, lerpSpeed);
            c.WindKph = MathHelper.Lerp(c.WindKph, _targetClimate.WindKph, lerpSpeed);
            c.PressureHpa = MathHelper.Lerp(c.PressureHpa, _targetClimate.PressureHpa, lerpSpeed);
            c.Instability = MathHelper.Lerp(c.Instability, _targetClimate.Instability, lerpSpeed);
            CurrentClimate = c;

            // 2. Evaluate the Meteorological Decision Tree
            CurrentWeather = EvaluateWeatherMatrix();

            // 3. Map to Shader Parameters
            UpdateVisualParameters();
        }

        /// <summary>
        /// The Core System of Equations. Follows a strict meteorological hierarchy.
        /// </summary>
        private WeatherType EvaluateWeatherMatrix()
        {
            // --- TIER 1: DUST STORMS (Desert / Monsoon Edge) ---
            // Requires very high wind, dry air, and hot temps.
            if (CurrentClimate.WindKph > 60 && CurrentClimate.Humidity < 30 && CurrentClimate.TempC > 25)
            {
                return WeatherType.DustStorm;
            }

            // --- TIER 2: PRECIPITATION EVENTS ---
            // Precipitation requires high humidity OR low atmospheric pressure sucking moisture up.
            bool isPrecipitating = CurrentClimate.Humidity > 85 || (CurrentClimate.PressureHpa < 1005 && CurrentClimate.Humidity > 70);

            if (isPrecipitating)
            {
                // COLD PRECIPITATION
                if (CurrentClimate.TempC <= 0f)
                {
                    if (CurrentClimate.WindKph > 55) return WeatherType.Blizzard;
                    return WeatherType.Snow;
                }

                // FREEZING LINE (0 to 3 degrees)
                if (CurrentClimate.TempC > 0f && CurrentClimate.TempC <= 3f)
                {
                    return WeatherType.Sleet;
                }

                // WARM PRECIPITATION
                if (CurrentClimate.Instability > 65) // Highly unstable air
                {
                    // Hail requires violent storms but a cold upper atmosphere (surface temp < 15C is a good proxy here)
                    if (CurrentClimate.TempC < 15f && CurrentClimate.Instability > 80) return WeatherType.Hail;
                    return WeatherType.Thunderstorm;
                }

                if (CurrentClimate.Instability < 30 && CurrentClimate.WindKph < 20)
                {
                    return WeatherType.Drizzle; // Flat, stable, calm rain
                }

                return WeatherType.Rain; // Standard rain
            }

            // --- TIER 3: OBSCUREMENTS (No Precipitation) ---
            // Fog needs high moisture, cool temps, and almost zero wind (or it blows away).
            if (CurrentClimate.Humidity > 90 && CurrentClimate.WindKph < 10 && CurrentClimate.TempC < 18)
            {
                return WeatherType.Fog;
            }

            // --- TIER 4: SKY COVERAGE ---
            // High pressure creates clear skies regardless of humidity. Low pressure creates clouds.
            if (CurrentClimate.PressureHpa >= 1020) return WeatherType.Clear;

            if (CurrentClimate.PressureHpa < 1008 || CurrentClimate.Humidity > 75) return WeatherType.Overcast;

            if (CurrentClimate.Humidity > 45) return WeatherType.PartlyCloudy;

            return WeatherType.Clear;
        }


        private void UpdateVisualParameters()
        {
            WeatherVisualParams v = new WeatherVisualParams();

            // Convert Kph to a 0-1 vector for shaders (Assume 100kph is max normal wind)
            Vector2 baseWindDir = new Vector2(0.15f, 1.0f);
            baseWindDir.Normalize();
            // 2. Convert WindKph to Pixels-Per-Second. 
            // 1 Kph = 10 pixels per second. 50kph = 500 pps.
            float speedPPS = CurrentClimate.WindKph * 8f;

            // 3. Gravity! Even if there is 0 wind, rain must fall. Minimum speed is 300 pps.
            speedPPS = Math.Max(speedPPS, 200f);
            // Calculate Cloud Cover based primarily on Atmospheric Pressure
            // High pressure (1020+) = Clear Skies. Low pressure (< 1005) = Fully Overcast.
            float pressureFactor = MathHelper.Clamp((1020f - CurrentClimate.PressureHpa) / 20f, 0f, 1f);

            // Humidity also contributes (can't have clouds without moisture)
            float humidityFactor = MathHelper.Clamp(CurrentClimate.Humidity / 80f, 0f, 1f);

            // Final Cloud Cover is a combination of both
            v.CloudCover = MathHelper.Clamp(pressureFactor * humidityFactor * 1.5f, 0f, 1f);
            v.WindVector = baseWindDir * speedPPS;
            // Calculate intensities based on how far past thresholds we are
            if (CurrentWeather == WeatherType.Rain || CurrentWeather == WeatherType.Thunderstorm)
                v.RainIntensity = MathHelper.Clamp((CurrentClimate.Humidity - 70f) / 30f, 0.1f, 1f);
            else if (CurrentWeather == WeatherType.Drizzle)
                v.RainIntensity = 0.1f;

            if (CurrentWeather == WeatherType.Snow || CurrentWeather == WeatherType.Blizzard)
                v.SnowIntensity = MathHelper.Clamp((CurrentClimate.Humidity - 70f) / 30f, 0.1f, 1f);

            if (CurrentWeather == WeatherType.Fog) v.FogDensity = 0.8f;
            if (CurrentWeather == WeatherType.DustStorm) v.FogDensity = 0.9f;

            if (CurrentWeather == WeatherType.Thunderstorm) v.StormIntensity = CurrentClimate.Instability / 100f;

            Visuals = v;
        }

        public string GetDebugInfo()
        {
            return $"--- WEATHER SIMULATOR ---\n" +
                   $"Current State: {CurrentWeather} Season {CurrentSeason} Phase {Phase}\n" +
                   $"Temp: {CurrentClimate.TempC:F2} | Humid: {CurrentClimate.Humidity:F2} | WindKph: {CurrentClimate.WindKph:F2}\n" +
                   $"PressureHpa: {CurrentClimate.PressureHpa:F2} | Instability: {CurrentClimate.Instability:F2}\n" +

                   $""+
                   $"--- VISUAL PARAMS ---\n" +
                   $"Rain Int: {Visuals.RainIntensity:F2} | Snow Int: {Visuals.SnowIntensity:F2}\n" +
                   $"Fog Den: {Visuals.FogDensity:F2} | Cloud: {Visuals.CloudCover:F2}\n" +
                   $"Wind Vec: {Visuals.WindVector.X:F1}, {Visuals.WindVector.Y:F1}";
        }
    }
}