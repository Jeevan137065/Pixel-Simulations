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
    public enum WeatherType
    {
        Clear, PartlyCloudy, Overcast, Fog, Drizzle, Rain, Thunderstorm,
        Hail, Snow, Blizzard, Sleet, DustStorm
    }

    public struct AtmosphereState
    {
        public float Temperature; // 0.0 (Freeze) to 1.0 (Scorching)
        public float Humidity;    // 0.0 (Dry) to 1.0 (Saturated)
        public float WindEnergy;  // 0.0 (Calm) to 1.0 (Hurricane)
        public float Instability; // 0.0 (Stable) to 1.0 (Violent storms)
        public float Aerosols;    // 0.0 (Clear) to 1.0 (Dusty/Smoky)
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
    public class WeatherSimulator
    {
        private AtmosphereState _current;
        private AtmosphereState _target;
        private float _transitionSpeed = 0.05f; // Adjust for slower/faster transitions

        public WeatherType CurrentWeather { get; private set; }
        public WeatherVisualParams Visuals { get; private set; }

        public WeatherSimulator()
        {
            // Start Clear and Sunny
            _current = new AtmosphereState { Temperature = 0.6f, Humidity = 0.2f, WindEnergy = 0.1f, Instability = 0.1f, Aerosols = 0.0f };
            _target = _current;
        }

        public void SetTargetWeather(AtmosphereState targetState)
        {
            _target = targetState;
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float lerpAmount = _transitionSpeed * dt;

            // 1. Seamlessly transition the core variables
            _current.Temperature = MathHelper.Lerp(_current.Temperature, _target.Temperature, lerpAmount);
            _current.Humidity = MathHelper.Lerp(_current.Humidity, _target.Humidity, lerpAmount);
            _current.WindEnergy = MathHelper.Lerp(_current.WindEnergy, _target.WindEnergy, lerpAmount);
            _current.Instability = MathHelper.Lerp(_current.Instability, _target.Instability, lerpAmount);
            _current.Aerosols = MathHelper.Lerp(_current.Aerosols, _target.Aerosols, lerpAmount);

            // 2. Evaluate the matrix to find the current weather state
            CurrentWeather = EvaluateWeatherMatrix();

            // 3. Map the atmospheric data to visual shader parameters
            UpdateVisualParameters();
        }

        private WeatherType EvaluateWeatherMatrix()
        {
            if (_current.WindEnergy > 0.7f && _current.Humidity > 0.6f && _current.Temperature < 0.3f) return WeatherType.Blizzard;
            if (_current.Instability > 0.7f && _current.Humidity > 0.6f) return _current.Temperature < 0.5f ? WeatherType.Hail : WeatherType.Thunderstorm;
            if (_current.Humidity > 0.6f)
            {
                if (_current.Temperature < 0.28f) return WeatherType.Snow;
                if (_current.Temperature < 0.32f) return WeatherType.Sleet;
                if (_current.Instability < 0.3f) return WeatherType.Drizzle;
                return WeatherType.Rain;
            }
            if (_current.Aerosols > 0.6f && _current.Humidity < 0.4f) return WeatherType.DustStorm;
            if (_current.Humidity > 0.85f && _current.WindEnergy < 0.2f) return WeatherType.Fog;
            if (_current.Humidity > 0.5f) return WeatherType.Overcast;
            if (_current.Humidity > 0.3f) return WeatherType.PartlyCloudy;
            return WeatherType.Clear;
        }

        private void UpdateVisualParameters()
        {
            WeatherVisualParams v = new WeatherVisualParams();

            // Base Wind
            float windAngle = MathHelper.PiOver4; // Could be dynamic later
            v.WindVector = new Vector2((float)Math.Cos(windAngle), (float)Math.Sin(windAngle)) * (_current.WindEnergy * 500f);

            // Cloud Cover
            v.CloudCover = MathHelper.Clamp(_current.Humidity * 1.5f, 0f, 1f);

            // Rain Logic
            if (CurrentWeather == WeatherType.Rain || CurrentWeather == WeatherType.Thunderstorm)
                v.RainIntensity = MathHelper.Clamp((_current.Humidity - 0.6f) / 0.4f, 0.1f, 1f);
            else if (CurrentWeather == WeatherType.Drizzle)
                v.RainIntensity = 0.05f;

            // Snow Logic
            if (CurrentWeather == WeatherType.Snow || CurrentWeather == WeatherType.Blizzard)
                v.SnowIntensity = MathHelper.Clamp((_current.Humidity - 0.6f) / 0.4f, 0.1f, 1f);

            // Fog / Dust Logic
            if (CurrentWeather == WeatherType.Fog) v.FogDensity = _current.Humidity;
            if (CurrentWeather == WeatherType.DustStorm) v.FogDensity = _current.Aerosols;

            // Storms
            if (CurrentWeather == WeatherType.Thunderstorm) v.StormIntensity = _current.Instability;

            Visuals = v;
        }
    }
}