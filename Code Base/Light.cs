// File: Light.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pixel_Simulations
{
    public enum ShadingStyle
    {
        Smooth = 0,
        Pow = 1,
        Bands = 2,
        Rim = 3
    }

    public abstract class Light
    {
        public Vector2 Position { get; set; }
        public Color Color { get; set; } = Color.White;
        public float Radius { get; set; } // For PointLights: radius. For SpotLights: range/length.
        public float Intensity { get; set; } = 1.0f;
        public bool IsFlickering { get; set; } = false;

        // Shading Style Properties
        public ShadingStyle Style { get; set; } = ShadingStyle.Pow;
        public Color CoreColor { get; set; } = Color.White;
        public float BandSmoothness { get; set; } = 0.01f;
        public Color RimColor { get; set; } = Color.Aqua;
        public float RimIntensity { get; set; } = 2.0f;

        // Time for animations
        public float Time { get; set; }

        public abstract void Update(GameTime gameTime);
    }

    public class PointLight : Light
    {
        public float ConstantAttenuation { get; set; }
        public float LinearAttenuation { get; set; }
        public float QuadraticAttenuation { get; set; }
        public float FlickerIntensityMin { get; set; } = 0.8f;
        public float FlickerIntensityMax { get; set; } = 1.2f;
        public float FlickerRadiusMin { get; set; } = 0.9f;
        public float FlickerRadiusMax { get; set; } = 1.1f;
        public float FlickerSpeed { get; set; } = 10f; // Times per second

        private float _currentRadiusMultiplier = 1.0f;
        private float _currentIntensityMultiplier = 1.0f;
        private Random _random = new Random();
        private float _flickerTimer;

        public float CurrentIntensity => Intensity * _currentIntensityMultiplier;
        public float CurrentRadius => Radius * _currentRadiusMultiplier;

        public override void Update(GameTime gameTime)
        {
            if (IsFlickering)
            {
                _flickerTimer += (float)gameTime.ElapsedGameTime.TotalSeconds * FlickerSpeed;
                if (_flickerTimer > 1.0f)
                {
                    _flickerTimer = 0;
                    _currentIntensityMultiplier = (float)_random.NextDouble() * (FlickerIntensityMax - FlickerIntensityMin) + FlickerIntensityMin;
                    _currentRadiusMultiplier = (float)_random.NextDouble() * (FlickerRadiusMax - FlickerRadiusMin) + FlickerRadiusMin;
                }
            }
            else
            {
                _currentIntensityMultiplier = 1.0f;
                _currentRadiusMultiplier = 1.0f;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pos: {Position.X:F0}, {Position.Y:F0}");
            sb.AppendLine($"Style: {Style}");
            sb.AppendLine($"Color: {Color.R}, {Color.G}, {Color.B}");
            sb.AppendLine($"Radius: {Radius:F0} | Intensity: {CurrentIntensity:F2}");
            sb.AppendLine($"Atten (C,L,Q): {ConstantAttenuation:F2}, {LinearAttenuation:F2}, {QuadraticAttenuation:F2}");
            if (IsFlickering) sb.AppendLine($"Flicker: ON (I:{FlickerIntensityMin:F1}-{FlickerIntensityMax:F1} R:{FlickerRadiusMin:F1}-{FlickerRadiusMax:F1})");
            return sb.ToString();
        }
    }

    public class SpotLight : Light
    {
        // The height of the light source "above" the 2D plane.
        public float Height { get; set; } = 200f;

        // The 2D direction the light is pointing.
        // This is your 'alpha' angle, but stored as a normalized Vector2 for easier math.
        // (1, 0) points right. (0, 1) points down.
        public Vector2 Direction { get; set; } = new Vector2(0, 1);

        // The full angle of the light cone in degrees. This is your '2 * Beta'.
        public float ConeAngle { get; set; } = 90f;

        public override void Update(GameTime gameTime)
        {
            // Spotlights can have their own flicker/animation logic here if needed.
            // For now, it does nothing.
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pos: {Position.X:F0}, {Position.Y:F0}");
            sb.AppendLine($"Style: {Style}");
            sb.AppendLine($"Color: {Color.R}, {Color.G}, {Color.B}");
            sb.AppendLine($"Radius: {Radius:F0} | Intensity: {Intensity:F2}");
            return sb.ToString();
        }
    }


    public class LightManager
    {
        // The list of lights is now publicly accessible for the renderer to use.
        // We use AsReadOnly to prevent other classes from accidentally modifying the list itself (e.g., clearing it).
        public readonly List<Light> _lights = new List<Light>();
        public IReadOnlyList<Light> Lights => _lights.AsReadOnly();

        public void Add(Light light)
        {
            _lights.Add(light);
        }

        public void Remove(Light light)
        {
            _lights.Remove(light);
        }

        public void Clear()
        {
            _lights.Clear();
        }

        public void Update(GameTime gameTime)
        {
            foreach (var light in _lights)
            {
                light.Time = (float)gameTime.TotalGameTime.TotalSeconds;
                light.Update(gameTime);
            }
        }
    }
}