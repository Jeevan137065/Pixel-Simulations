
// File: ShaderManager.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Pixel_Simulations
{
    public class ShaderManager
    {
        private GraphicsDevice _graphicsDevice;
        private Texture2D _pixelTexture;
        private VertexPositionTexture[] _fullScreenQuad;

        // Future Shaders
        public Effect ColorGrading { get; private set; }
        public Effect Gusting { get; private set; }
        public Texture2D PerlinNoise { get; private set; }
        public Texture2D BlueNoise { get; private set; }
        // --- Future Weather Shaders ---
        public Effect MinecraftRain { get; private set; }
        public Effect ParticleWeather { get; private set; }
        public Texture2D ParticleAtlas { get; private set; }
        // Debug tracking
        private float _currentDistortion;
        private Vector3 _currentTint;
        private Effect _particleUpdateEffect;
        // --- Particle System (Internal GPU Simulation) ---
        private RenderTarget2D _particleStateA;
        private RenderTarget2D _particleStateB;
        private Vector4[] _particleData;

        private readonly int _maxParticles = 10000;
        private readonly int _stateTextureSize = 100; // sqrt(10000)
        private readonly Vector2 _simulationBounds = new Vector2(480 * 4, 270 * 4); // 3x3 
        private int debugger = 0;
        public ShaderManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            _fullScreenQuad = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };
            _particleData = new Vector4[_maxParticles];

            // Initialize the physics state targets (floating point texture for precision)
            _particleStateA = new RenderTarget2D(_graphicsDevice, _stateTextureSize, _stateTextureSize, false, SurfaceFormat.Vector4, DepthFormat.None);
            _particleStateB = new RenderTarget2D(_graphicsDevice, _stateTextureSize, _stateTextureSize, false, SurfaceFormat.Vector4, DepthFormat.None);

            InitializeParticleState();
        }
        private void InitializeParticleState()
        {
            var initialData = new Vector4[_maxParticles];
            for (int i = 0; i < _maxParticles; i++)
            {
                // Start all particles as "dead" (Lifetime < 0) so they spawn naturally
                initialData[i] = new Vector4(0, 0, -1, 0);
            }
            _particleStateA.SetData(initialData);
            _particleStateB.SetData(initialData);
        }
        public void LoadContent(ContentManager content)
        {
            ColorGrading = content.Load<Effect>("ColorGrading");
            Gusting = content.Load<Effect>("Gusting");
            PerlinNoise = content.Load<Texture2D>("PerlinA");
            // Load the new Rain Shader and its required noise texture
            MinecraftRain = content.Load<Effect>("RainA");
            BlueNoise = content.Load<Texture2D>("BlueA"); // You MUST have this texture
            // Set the noise texture once, it doesn't change
            Gusting.Parameters["NoiseTexture"].SetValue(PerlinNoise);

            //ParticleWeather = content.Load<Effect>("ParticleWeather");
            _particleUpdateEffect = content.Load<Effect>("ParticleUpdate");

            ParticleAtlas = content.Load<Texture2D>("Particle_atlas"); // Your 96x16 image
            //ParticleWeather.Parameters["AtlasTexture"]?.SetValue(ParticleAtlas);

        }
        /// <summary>
        /// Runs the GPU physics simulation for particles and pulls the data back to the CPU.
        /// Call this in GameState.Update().
        /// </summary>
        public void UpdateParticles(GameTime gameTime, WeatherSimulator weatherSim, Vector2 cameraPosition, Vector2 viewportSize)
        {
            var type = weatherSim.CurrentWeather;
            if (type == WeatherType.Clear || type == WeatherType.PartlyCloudy || type == WeatherType.Overcast)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            var visuals = weatherSim.Visuals;

            _particleUpdateEffect.Parameters["StateTexture"]?.SetValue(_particleStateA);
            _particleUpdateEffect.Parameters["DeltaTime"]?.SetValue(dt);
            _particleUpdateEffect.Parameters["Time"]?.SetValue(time);

            // --- NEW: Pass Camera Data ---
            _particleUpdateEffect.Parameters["CameraPosition"]?.SetValue(cameraPosition);
            _particleUpdateEffect.Parameters["ViewportSize"]?.SetValue(viewportSize);

            Vector2 windDir = visuals.WindVector != Vector2.Zero ? Vector2.Normalize(visuals.WindVector) : new Vector2(0.1f, 1f);
            _particleUpdateEffect.Parameters["WindDirection"]?.SetValue(windDir);
            _particleUpdateEffect.Parameters["WindSpeed"]?.SetValue(visuals.WindVector.Length());

            _graphicsDevice.SetRenderTarget(_particleStateB);
            _particleUpdateEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fullScreenQuad, 0, 2);

            var temp = _particleStateA; _particleStateA = _particleStateB; _particleStateB = temp;
            _particleStateA.GetData(_particleData);
        }

        /// <summary>
        /// Updates the parameters of the Post-Processing shaders before they are drawn.
        /// </summary>
        // Update the method signature to take the full simulator so we have access to Climate
        public void UpdatePostProcessing(WeatherSimulator weatherSim, GameTime gameTime)
        {
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            var climate = weatherSim.CurrentClimate;
            var visuals = weatherSim.Visuals;

            // --- 1. Update Gusting (Wind Distortion) ---
            Gusting.Parameters["Time"]?.SetValue(time);

            // Wind Direction based on the visual vector
            Vector2 windDir = visuals.WindVector != Vector2.Zero ? Vector2.Normalize(visuals.WindVector) : Vector2.Zero;
            Gusting.Parameters["WindVector"]?.SetValue(windDir);

            // Distortion is strictly tied to WindKph. (e.g., 100kph = 0.005 distortion)
            _currentDistortion = (climate.WindKph / 100f) * 0.05f;
            Gusting.Parameters["DistortionStrength"]?.SetValue(_currentDistortion);

            // --- 2. Update Color Grading (Temperature & Atmosphere) ---

            // Base Values
            float targetSat = 1.0f;
            float targetBright = 1.0f;
            float targetContrast = 1.0f;
            Vector3 targetTint = Vector3.One;

            // Temperature Tinting
            if (climate.TempC > 25f) // Hot: Push towards orange/yellow
            {
                float heatFactor = MathHelper.Clamp((climate.TempC - 25f) / 15f, 0f, 1f);
                targetTint = Vector3.Lerp(Vector3.One, new Vector3(1.0f, 0.9f, 0.7f), heatFactor);
            }
            else if (climate.TempC < 5f) // Cold: Push towards blue/cyan
            {
                float coldFactor = MathHelper.Clamp((5f - climate.TempC) / 15f, 0f, 1f);
                targetTint = Vector3.Lerp(Vector3.One, new Vector3(0.8f, 0.9f, 1.0f), coldFactor);
            }

            // Cloud Cover & Storms (Darkens and desaturates)
            if (weatherSim.CurrentWeather == WeatherType.Thunderstorm || weatherSim.CurrentWeather == WeatherType.Blizzard)
            {
                targetSat = 0.6f;
                targetBright = 0.6f;
                targetContrast = 1.2f; // High contrast for lightning/storms
                targetTint *= new Vector3(0.8f, 0.8f, 0.9f); // Darken further
            }
            else if (climate.Humidity > 60f) // Overcast / Raining
            {
                // Cloud cover dims the sun
                targetBright = MathHelper.Lerp(1.0f, 0.75f, visuals.CloudCover);
                targetSat = MathHelper.Lerp(1.0f, 0.8f, visuals.CloudCover);
            }

            // Fog & Dust Storms (Flattens contrast, tints to environment)
            if (weatherSim.CurrentWeather == WeatherType.DustStorm)
            {
                targetContrast = 0.8f;
                targetSat = 0.5f;
                targetTint = new Vector3(0.9f, 0.7f, 0.5f); // Brown/Orange dust
            }

            // Apply to Shader
            ColorGrading.Parameters["Saturation"]?.SetValue(targetSat);
            ColorGrading.Parameters["Brightness"]?.SetValue(targetBright);
            ColorGrading.Parameters["Contrast"]?.SetValue(targetContrast);
            ColorGrading.Parameters["TintColor"]?.SetValue(targetTint);

            _currentTint = targetTint; // For debug display
        }

        /// <summary>
        /// Draws shaders that exist IN THE WORLD (like Rain, Snow, Cloud Shadows).
        /// This should be called while the RenderPipeline is targeting RenderLayer.Weather.
        /// </summary>
        public void DrawWorldWeatherShaders(WeatherVisualParams weatherParams)
        {
            _graphicsDevice.BlendState = BlendState.AlphaBlend;

            // DRAW MINECRAFT RAIN
            if (weatherParams.RainIntensity > 0.01f && MinecraftRain != null)
            {
                // Set parameters based on current weather
                MinecraftRain.Parameters["Intensity"]?.SetValue(weatherParams.RainIntensity);
                MinecraftRain.Parameters["WindSlant"]?.SetValue(weatherParams.WindVector.X * 0.005f); // Slant based on wind X

                MinecraftRain.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fullScreenQuad, 0, 2);
            }

            // Future: Draw Snow here

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        // NEW METHOD: Configure and draw the particles
        public void DrawParticleWeather(SpriteBatch spriteBatch, WeatherSimulator weatherSim, Matrix cameraTransform)
        {
            var type = weatherSim.CurrentWeather;

            float intensity = 0f;
            if (type == WeatherType.Rain || type == WeatherType.Thunderstorm || type == WeatherType.Drizzle) intensity = weatherSim.Visuals.RainIntensity;
            else if (type == WeatherType.Snow || type == WeatherType.Blizzard) intensity = weatherSim.Visuals.SnowIntensity;
            else if (type == WeatherType.DustStorm) intensity = weatherSim.Visuals.FogDensity;

            if (intensity <= 0.01f) return;

            int particlesToDraw = (int)(_maxParticles * intensity);

            // Atlas Configuration Variables (Customize these bounds based on your exact 96x16 image)
            int startX = 0, startY = 0, width = 8, height = 8, frames = 1;
            bool rotateWithWind = true;

            switch (type)
            {
                case WeatherType.Rain:
                    startX = 0; startY = 0; width = 8; height = 16; frames = 4;
                    rotateWithWind = true;
                    break;
                case WeatherType.Snow:
                    startX = 32; startY = 0; width = 8; height = 8; frames = 4;
                    rotateWithWind = true;
                    break;
                case WeatherType.DustStorm:
                    startX = 64; startY = 0; width = 8; height = 8; frames = 4;
                    rotateWithWind = true;
                    break;
                case WeatherType.Drizzle:
                    startX = 32; startY = 8; width = 8; height = 8; frames = 4;
                    rotateWithWind = false;
                    break;
                case WeatherType.Thunderstorm:
                    startX = 64; startY = 0; width = 8; height = 16; frames = 2;
                    rotateWithWind = true;
                    break;
                case WeatherType.Blizzard:
                    startX = 80; startY = 0; width = 8; height = 8; frames = 2;
                    rotateWithWind = true;
                    break;
            }

            float rotation = 0f;
            if (rotateWithWind && weatherSim.Visuals.WindVector != Vector2.Zero)
            {
                rotation = (float)Math.Atan2(weatherSim.Visuals.WindVector.Y, weatherSim.Visuals.WindVector.X) + MathHelper.PiOver2;
            }

            Vector2 origin = new Vector2(width / 2f, height / 2f);
            //spriteBatch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, cameraTransform);
            for (int i = 0; i < particlesToDraw; i++)
            {
                Vector4 data = _particleData[i];
                float lifetime = data.Z;

                if (lifetime < 0) continue;

                Vector2 position = new Vector2(data.X, data.Y);

                // Pick a consistent frame
                int frame = (i * 7 + 13) % frames;
                Rectangle sourceRect = new Rectangle(startX + (frame * width), startY, width, height);

                // Fade out at end of life
                float alpha = MathHelper.Clamp(lifetime * 500f, 0f, 1f);
                Color color = Color.Blue * alpha;

                spriteBatch.Draw(ParticleAtlas, position, sourceRect, color, rotation, origin, 1.0f, SpriteEffects.None, 0f);
                debugger++;
            }

            spriteBatch.End();
        }
        /// <summary>
        /// Updates parameters for shaders that apply to the ENTIRE SCREEN (Color Grading, Gusting).
        /// Returns the array of active effects to be passed to the RenderPipeline's PostFinal method.
        /// </summary>
        public Effect[] GetActivePostProcessingEffects(WeatherVisualParams weatherParams, GameTime gameTime)
        {
            float time = (float)gameTime.TotalGameTime.TotalSeconds;

            // --- 1. Update Gusting ---
            Gusting.Parameters["Time"]?.SetValue(time);
            Vector2 windDir = weatherParams.WindVector != Vector2.Zero ? Vector2.Normalize(weatherParams.WindVector) : Vector2.Zero;
            Gusting.Parameters["WindVector"]?.SetValue(windDir);
            _currentDistortion = weatherParams.WindVector.Length() * 0.005f;
            Gusting.Parameters["DistortionStrength"]?.SetValue(_currentDistortion);

            // --- 2. Update Color Grading ---
            // (Your existing Color Grading logic here... _currentTint, Saturation, etc.)
            if (weatherParams.StormIntensity > 0.1f)
            {
                ColorGrading.Parameters["Saturation"]?.SetValue(0.5f);
                ColorGrading.Parameters["Brightness"]?.SetValue(0.7f);
                ColorGrading.Parameters["Contrast"]?.SetValue(1.1f);
                _currentTint = new Vector3(0.7f, 0.8f, 0.9f);
            }
            else if (weatherParams.RainIntensity > 0.1f || weatherParams.FogDensity > 0.3f)
            {
                ColorGrading.Parameters["Saturation"]?.SetValue(0.8f);
                ColorGrading.Parameters["Brightness"]?.SetValue(0.9f);
                ColorGrading.Parameters["Contrast"]?.SetValue(1.0f);
                _currentTint = new Vector3(0.9f, 0.95f, 1.0f);
            }
            else
            {
                ColorGrading.Parameters["Saturation"]?.SetValue(1.1f);
                ColorGrading.Parameters["Brightness"]?.SetValue(1.0f);
                ColorGrading.Parameters["Contrast"]?.SetValue(1.0f);
                _currentTint = Vector3.One;
            }
            ColorGrading.Parameters["TintColor"]?.SetValue(_currentTint);

            // Return the effects in the order they should be applied!
            // We distort the image first (Gusting), then color grade the distorted result.
            return new Effect[] {Gusting,ColorGrading };
        }
        public string GetDebugInfo()
        {
            return $"--- SHADER MANAGER ---\n" +
                   $"Gusting Distortion: {_currentDistortion:F5}\n" +
                   $"Color Tint (RGB): {_currentTint.X:F2}, {_currentTint.Y:F2}, {_currentTint.Z:F2}\n" +
                   $"Active Post-FX: ColorGrading, Gusting\n" +
                   $"Particles processing: {(_particleData != null ? "Yes" : "No")},Count {debugger}" +
                   $"Paticles {_particleData.Length}";
        }
    }
}
