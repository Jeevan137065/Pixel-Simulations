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
    public enum ControllableParameter
    {
        RainCount,
        WindSpeed,
        WindDirectionX,
        WindDirectionY,
        DistortionStrength,
        WindSpeedMultiplier,
        NoiseScale
    }

    public class WeatherManager
    {
        // Shaders
        private Effect _windDistortionEffect, _colorGradingEffect, _particleUpdateEffect, _proceduralRainEffect, _DebugEffect;

        // Textures and Buffers
        private Texture2D _particleAtlasTexture, _noiseTexture, _pixelTexture;
        private SpriteFont _debugFont;
        private VertexBuffer _particleRenderVB;
        private IndexBuffer _particleRenderIB;
        private RenderTarget2D _particleStateA;
        private RenderTarget2D _particleStateB; // For sending corner data
        private IndexBuffer _particleIndexBuffer;
        private int _maxParticles = 10000;
        private readonly int _stateTextureSize = 100;
        private VertexPositionTexture[] _quadVertices, _fullScreenQuad;
        private Vector4[] _particleData;
        private readonly Vector2 _simulationBounds = new Vector2(480 * 3, 270 * 3);
        private Random _random;
        public bool UseProceduralRain { get; private set; } = true;

        // Presets
        private WeatherSettings _weatherSettings;
        private Dictionary<Keys, string> _presetHotkeys;

        // Weather properties
        public int RainCount { get; set; }
        public Vector2 WindDirection { get; set; } = new Vector2(0.4f, 1f);
        public float WindSpeed { get; set; }
        public float DistortionStrength { get; set; }
        public float WindSpeedMultiplier { get; set; }
        public float Desaturation { get; set; }
        public Color TintColor { get; set; }
        public float NoiseScale { get; set; } = 1.5f;
        public bool IsRaining { get; set; } = true;

        // Wind Gusting Effect
        private float _gustTimer = 0f;
        private float _gustStrength = 1f;
        private float _timeToNextGust = 5f;


        // Input and Control State
        private ProceduralRainPreset _procRainPreset = new ProceduralRainPreset();
        private ControllableParameter _currentParameter;
        private KeyboardState _previousKeyboardState;

        public WeatherManager()
        {
            _random = new Random();
            _particleData = new Vector4[_maxParticles];
        }

        public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
        {
            _noiseTexture = content.Load<Texture2D>("noise");
            _debugFont = content.Load<SpriteFont>("DebugFont");
            _windDistortionEffect = content.Load<Effect>("WindDistortion");
            _colorGradingEffect = content.Load<Effect>("ColorGrading");
            _particleUpdateEffect = content.Load<Effect>("ParticleUpdate");
            _DebugEffect = content.Load<Effect>("Debug");
            _proceduralRainEffect = content.Load<Effect>("Rain");
            _particleAtlasTexture = content.Load<Texture2D>("particle_atlas");
            // Pass the noise texture to the shader once
            _windDistortionEffect.Parameters["NoiseTexture"].SetValue(_noiseTexture);
            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            _particleStateA = new RenderTarget2D(graphicsDevice, _stateTextureSize, _stateTextureSize, false, SurfaceFormat.Vector4, DepthFormat.None);
            _particleStateB = new RenderTarget2D(graphicsDevice, _stateTextureSize, _stateTextureSize, false, SurfaceFormat.Vector4, DepthFormat.None);
            _fullScreenQuad = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };
            LoadPresets("Content/weather_presets.json");
            InitializeParticleData(graphicsDevice);
            ApplyProfile("Clear");
        }

        private void LoadPresets(string path)
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);

                // --- THE FIX IS HERE ---
                // 1. Create a settings object.
                var settings = new JsonSerializerSettings();

                // 2. Add our custom Vector2Converter to its list of converters.
                settings.Converters.Add(new Vector2Converter());

                // 3. Pass the settings object to the deserializer.
                _weatherSettings = JsonConvert.DeserializeObject<WeatherSettings>(json, settings);

                // Set up hotkeys (this part is unchanged)
                _presetHotkeys = new Dictionary<Keys, string>
        {
            { Keys.D1, "Clear" },
            { Keys.D2, "Drizzle" },
            { Keys.D3, "HeavyRain" },
            { Keys.D4, "Thunderstorm" }
        };
            }
        }

        public void ApplyProfile(string profileName)
        {
            if (_weatherSettings?.Profiles.TryGetValue(profileName, out var profile) ?? false)
            {
                // Apply particle/post-processing settings
                RainCount = profile.Weather.RainCount;
                WindSpeed = profile.Weather.WindSpeed;
                DistortionStrength = profile.Weather.DistortionStrength;
                WindSpeedMultiplier = profile.Weather.WindSpeedMultiplier;
                Desaturation = profile.ColorGrading.Desaturation;
                TintColor = profile.ColorGrading.TintColor;

                // NEW: Apply the procedural rain settings
                if (profile.ProceduralRain != null)
                {
                    _procRainPreset = profile.ProceduralRain;
                }
            }
        }
        private void InitializeParticleData(GraphicsDevice graphicsDevice)
        {
            var initialData = new Vector4[_maxParticles];
            for (int i = 0; i < _maxParticles; i++)
            {
                // Reset particles to an initial "dead" state to be spawned
                initialData[i] = new Vector4(0, 0, -1, 0); // Position(0,0), Lifetime(-1), Type(rain)
            }
            _particleStateA.SetData(initialData);
            _particleStateB.SetData(initialData);

            // For drawing the quad
            _quadVertices = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };
        }
   
        private void ResetParticle(ref ParticleVertex p, Vector2 viewport)
        {
            var seed = new Vector2((float)_random.NextDouble(), (float)_random.NextDouble());
            float speed = WindSpeed * MathHelper.Lerp(0.4f, 1.2f, seed.X);
            float travelDistance = viewport.Y * (0.75f + seed.Y * 0.75f);

            p.Position = new Vector2((float)_random.NextDouble() * viewport.X, -10f);
            p.Velocity = Vector2.Normalize(WindDirection) * speed;
            p.Metadata.X = (speed > 0) ? travelDistance / speed : 0; // Lifetime
            p.Metadata.Y = 0; // Type = Rain
            p.Metadata.Z = seed.X;
            p.Metadata.W = seed.Y;
        }
        public void Update(GameTime gameTime, GraphicsDevice graphicsDevice, KeyboardState keyboardState, Rectangle cameraView)
        {

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            //UpdateGusting(deltaTime);
            HandleInputControls(keyboardState);

            if (UseProceduralRain)
            {
                UpdateProceduralRainParameters(gameTime, cameraView);
            }
            else
            {
                UpdateParticleSystem(gameTime, graphicsDevice);
            }
            UpdateShaderParameters(gameTime, graphicsDevice, cameraView);
            HandleInputControls(keyboardState);

        }

        private void HandleInputControls(KeyboardState keyboardState)
        {
            // Cycle through parameters with Tab
            if (keyboardState.IsKeyDown(Keys.Tab) && _previousKeyboardState.IsKeyUp(Keys.Tab))
            {
                _currentParameter = (ControllableParameter)(((int)_currentParameter + 1) % Enum.GetNames(typeof(ControllableParameter)).Length);
            }

            float increment = 0.01f;
            if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
                increment = 0.001f; // Finer control with Shift

            if (keyboardState.IsKeyDown(Keys.Right))
                ChangeParameter(_currentParameter, increment);
            if (keyboardState.IsKeyDown(Keys.Left))
                ChangeParameter(_currentParameter, -increment);
            if (keyboardState.IsKeyDown(Keys.P) && _previousKeyboardState.IsKeyUp(Keys.P))
            {
                UseProceduralRain = !UseProceduralRain;
            }

            foreach (var hotkey in _presetHotkeys)
            {
                if (keyboardState.IsKeyDown(hotkey.Key))
                {
                    ApplyProfile(hotkey.Value);
                    break;
                }
            }

            _previousKeyboardState = keyboardState;
        }

        private void ChangeParameter(ControllableParameter param, float amount)
        {
            switch (param)
            {
                case ControllableParameter.RainCount:
                    RainCount += (int)(amount * 1000); // Scale up for integer value
                    RainCount = Math.Max(0, RainCount);
                    break;
                case ControllableParameter.WindSpeed:
                    WindSpeed += amount * 100;
                    WindSpeed = Math.Max(0, WindSpeed);
                    break;
                case ControllableParameter.WindDirectionX:
                    WindDirection = new Vector2(WindDirection.X + amount, WindDirection.Y);
                    break;
                case ControllableParameter.WindDirectionY:
                    WindDirection = new Vector2(WindDirection.X, WindDirection.Y + amount);
                    break;
                case ControllableParameter.DistortionStrength:
                    DistortionStrength += amount;
                    DistortionStrength = Math.Max(0, DistortionStrength);
                    break;
                case ControllableParameter.WindSpeedMultiplier:
                    WindSpeedMultiplier += amount * 10;
                    WindSpeedMultiplier = Math.Max(0, WindSpeedMultiplier);
                    break;
                case ControllableParameter.NoiseScale:
                    NoiseScale += amount * 10;
                    NoiseScale = Math.Max(0.1f, NoiseScale);
                    break;
            }
        }

        private void UpdateProceduralRainParameters(GameTime gameTime, Rectangle cameraView)
        {
            var viewBounds = new Vector4(cameraView.Left, cameraView.Top, cameraView.Right, cameraView.Bottom);
            _proceduralRainEffect.Parameters["uViewBounds"]?.SetValue(viewBounds);
            _proceduralRainEffect.Parameters["uTime"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
            //_proceduralRainEffect.Parameters["MatrixTransform"].SetValue(Matrix.Identity);

            _proceduralRainEffect.Parameters["uCountPrimary"]?.SetValue(_procRainPreset.CountPrimary);
            _proceduralRainEffect.Parameters["uCountSecondary"]?.SetValue(_procRainPreset.CountSecondary);

            float slant = (WindDirection.X / WindDirection.Y) * _procRainPreset.SlantMultiplier;
            _proceduralRainEffect.Parameters["uSlantPrimary"]?.SetValue(slant);
            _proceduralRainEffect.Parameters["uSlantSecondary"]?.SetValue(slant);

            _proceduralRainEffect.Parameters["uSpeedPrimary"]?.SetValue(_procRainPreset.SpeedPrimary*-1);
            _proceduralRainEffect.Parameters["uSpeedSecondary"]?.SetValue(_procRainPreset.SpeedSecondary*-1);

            _proceduralRainEffect.Parameters["uBlurPrimary"]?.SetValue(_procRainPreset.BlurPrimary);
            _proceduralRainEffect.Parameters["uBlurSecondary"]?.SetValue(_procRainPreset.BlurSecondary);

            _proceduralRainEffect.Parameters["uSizePrimary"]?.SetValue(_procRainPreset.SizePrimary);
            _proceduralRainEffect.Parameters["uSizeSecondary"]?.SetValue(_procRainPreset.SizeSecondary);

            _proceduralRainEffect.Parameters["uRainColor"]?.SetValue(_procRainPreset.RainColor.ToVector3());
            _proceduralRainEffect.Parameters["uAlpha"]?.SetValue(_procRainPreset.Alpha);
        }

        private void UpdateParticleSystem(GameTime gameTime, GraphicsDevice graphicsDevice)
        {
            _particleUpdateEffect.Parameters["StateTexture"]?.SetValue(_particleStateA);
            _particleUpdateEffect.Parameters["DeltaTime"]?.SetValue((float)gameTime.ElapsedGameTime.TotalSeconds);
            _particleUpdateEffect.Parameters["Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
            _particleUpdateEffect.Parameters["WindDirection"]?.SetValue(WindDirection);
            _particleUpdateEffect.Parameters["WindSpeed"]?.SetValue(WindSpeed);
            _particleUpdateEffect.Parameters["SimulationBounds"]?.SetValue(_simulationBounds);

            graphicsDevice.SetRenderTarget(_particleStateB);
            _particleUpdateEffect.CurrentTechnique.Passes[0].Apply();
            graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fullScreenQuad, 0, 2);

            var temp = _particleStateA; _particleStateA = _particleStateB; _particleStateB = temp;
            _particleStateA.GetData(_particleData);
        }
        private void UpdateShaderParameters(GameTime gameTime, GraphicsDevice graphicsDevice, Rectangle cameraView)
        {


            _colorGradingEffect.Parameters["Desaturation"]?.SetValue(Desaturation);
            _colorGradingEffect.Parameters["TintColor255"]?.SetValue(new Vector3(TintColor.R, TintColor.G, TintColor.B));
            //_colorGradingEffect.Parameters["TintColor255"]?.SetValue(TintColor.ToVector3());

            // Update wind distortion shader
            _windDistortionEffect.Parameters["Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
            _windDistortionEffect.Parameters["WindDirection"]?.SetValue(Vector2.Normalize(WindDirection));
            _windDistortionEffect.Parameters["DistortionStrength"]?.SetValue(DistortionStrength * _gustStrength);
            _windDistortionEffect.Parameters["WindSpeedMultiplier"]?.SetValue(WindSpeedMultiplier * _gustStrength);
            _windDistortionEffect.Parameters["NoiseScale"]?.SetValue(NoiseScale);
        }
        private void UpdateGusting(float deltaTime)
        {
            _gustTimer += deltaTime;
            if (_gustTimer > _timeToNextGust)
            {
                _gustTimer = 0;
                _gustStrength = (float)(_random.NextDouble() * 1.5 + 1.5); // Sharp gust
                _timeToNextGust = (float)(_random.NextDouble() * 5 + 3); // Time until next gust
            }

            // Decay gust strength back to normal
            _gustStrength = MathHelper.Lerp(_gustStrength, 1f, deltaTime * 0.8f);
        }

        public void Draw(SpriteBatch spriteBatch,GraphicsDevice graphicsDevice, Matrix viewMatrix, Rectangle cameraView)
        {
            // The main draw call now decides which system to render
            if (UseProceduralRain)
            {
                DrawProceduralRain(graphicsDevice);
            }
            else
            {
                DrawParticleRain(spriteBatch, viewMatrix, cameraView);
            }
        }

        private void DrawParticleRain(SpriteBatch spriteBatch, Matrix viewMatrix, Rectangle cameraView)
        {
            if (!IsRaining || RainCount <= 0) return;

            spriteBatch.Begin(transformMatrix: viewMatrix, blendState: BlendState.AlphaBlend, samplerState: SamplerState.PointClamp);

            int particleCount = Math.Min(RainCount, _maxParticles);

            // Define our atlas properties
            Rectangle cullingBounds = new Rectangle(cameraView.X - 80, cameraView.Y - 80, cameraView.Width + 160, cameraView.Height + 160);
            particleCount = Math.Min(RainCount, _maxParticles);
            int atlasColumns = 4, atlasRows = 2;
            int spriteWidth = _particleAtlasTexture.Width / atlasColumns, spriteHeight = _particleAtlasTexture.Height / atlasRows;

            // Loop through the data we got from the GPU
            for (int i = 0; i < particleCount; i++)
            {
                Vector4 data = _particleData[i];
                Vector2 position = new Vector2(data.X, data.Y);

                if (!cullingBounds.Contains(position)) continue;

                float lifetime = data.Z;
                float type = data.W; // 0=rain, 0.5=splash

                if (lifetime < 0) continue; // Skip dead particles

                Rectangle sourceRect;
                Vector2 origin;
                float rotation = 0f;
                float scale = 1f;

                // Re-create a seed from the particle's index to pick a variant
                int variant = (i * 7 + 13) % atlasColumns; // A simple way to get a consistent random variant

                if (type == 0.0) // If Rain
                {
                    sourceRect = new Rectangle(variant * spriteWidth, 0, spriteWidth, spriteHeight); // Top row for rain
                    rotation = (float)Math.Atan2(WindDirection.Y, WindDirection.X) + MathHelper.PiOver2;
                }
                else // If Splash
                {
                    sourceRect = new Rectangle(variant * spriteWidth, spriteHeight, spriteWidth, spriteHeight); // Bottom row for splash
                    // Animate splash scale based on lifetime
                    float maxSplashLifetime = 0.3f;
                    scale = 1.5f - (lifetime / maxSplashLifetime);
                }

                origin = new Vector2(spriteWidth / 2f, spriteHeight / 2f);

                spriteBatch.Draw(_particleAtlasTexture, position, sourceRect, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        private void DrawProceduralRain(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            _proceduralRainEffect.CurrentTechnique.Passes[0].Apply();
            graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _fullScreenQuad, 0, 2);
            graphicsDevice.BlendState = BlendState.Opaque;
        }
        public void DrawDebugInfo(SpriteBatch spriteBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- Weather Controls (Tab to cycle) ---");
            sb.AppendLine($"{(ControllableParameter.RainCount == _currentParameter ? ">" : " ")} Rain Count: {RainCount}");
            sb.AppendLine($"{(ControllableParameter.WindSpeed == _currentParameter ? ">" : " ")} Wind Speed: {WindSpeed:F2}");
            sb.AppendLine($"{(ControllableParameter.WindDirectionX == _currentParameter ? ">" : " ")} Wind Dir X: {WindDirection.X:F2}");
            sb.AppendLine($"{(ControllableParameter.WindDirectionY == _currentParameter ? ">" : " ")} Wind Dir Y: {WindDirection.Y:F2}");
            sb.AppendLine($"{(ControllableParameter.DistortionStrength == _currentParameter ? ">" : " ")} Distort Str: {DistortionStrength:F4}");
            sb.AppendLine($"{(ControllableParameter.WindSpeedMultiplier == _currentParameter ? ">" : " ")} Distort Spd: {WindSpeedMultiplier:F2}");
            sb.AppendLine($"{(ControllableParameter.NoiseScale == _currentParameter ? ">" : " ")} Noise Scale: {NoiseScale:F2}");
            sb.AppendLine($"Gust Strength: {_gustStrength:F2}");

            spriteBatch.DrawString(_debugFont, sb.ToString(), new Vector2(10, 10), Color.White);
        }



        // Public accessors for shaders
        public Effect GetWindDistortionEffect() => IsRaining ? _windDistortionEffect : null;

        public Effect GetColorGradingEffect() => IsRaining ? _colorGradingEffect : null;
        // Constants to avoid magic numbers, assuming they are accessible
        private const int WorldWidth = 960;
        private const int WorldHeight = 540;
        private const int WindowHeight = 270;
    }
}