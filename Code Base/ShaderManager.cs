using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations
{
    /// <summary>
    /// Helper extension to safely set shader parameters without crashing if they don't exist.
    /// </summary>
    public static class EffectExtensions
    {
        public static void SetSafe(this Effect effect, string paramName, object value)
        {
            var param = effect?.Parameters[paramName];
            if (param == null) return;

            if (value is float f) param.SetValue(f);
            else if (value is Vector2 v2) param.SetValue(v2);
            else if (value is Vector3 v3) param.SetValue(v3);
            else if (value is Vector4 v4) param.SetValue(v4);
            else if (value is Texture2D t) param.SetValue(t);
        }
    }

    // ========================================================================
    // MAIN SHADER MANAGER (The Facade)
    // ========================================================================
    public class ShaderManager
    {
        public GraphicsDevice GraphicsDevice { get; private set; }
        public Dictionary<string, Effect> Effects { get; private set; }
        public Dictionary<string, Texture2D> Textures { get; private set; }
        public Texture2D PixelTexture { get; private set; }

        // --- The Three Sub-Systems ---
        public GPUParticleSystem Particles { get; private set; }
        public WeatherOverlayRenderer Overlays { get; private set; }
        public PostProcessController PostFX { get; private set; }
        public VertexPositionTexture[] FullScreenQuad { get; private set; }
        public ShaderManager(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
            Effects = new Dictionary<string, Effect>();
            Textures = new Dictionary<string, Texture2D>();

            PixelTexture = new Texture2D(graphicsDevice, 1, 1);
            PixelTexture.SetData(new[] { Color.Transparent });

            // Initialize sub-systems
            Particles = new GPUParticleSystem(this);
            Overlays = new WeatherOverlayRenderer(this);
            PostFX = new PostProcessController(this);

            FullScreenQuad = new VertexPositionTexture[] {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };
        }

        public void LoadContent(ContentManager content)
        {
            // Load all Effects
            Effects["ParallaxSprite"] = content.Load<Effect>("ParallaxSprite");
            Effects["ColorGrading"] = content.Load<Effect>("ColorGrading");
            Effects["Gusting"] = content.Load<Effect>("Gusting");
            Effects["RollingFog"] = content.Load<Effect>("RollingFog");
            Effects["ParticleUpdate"] = content.Load<Effect>("ParticleUpdate");
            Effects["CloudShadows"] = content.Load<Effect>("CloudShadows");
            Effects["AmbientLighting"] = content.Load<Effect>("AmbientLight");
            Effects["TestWeather"] = content.Load<Effect>("Test");
            Effects["WaterFlood"] = content.Load<Effect>("WaterFlood");
            Effects["ReflectionShader"] = content.Load<Effect>("ReflectionShader");
            // Load all Textures
            Textures["Noise_Perlin"] = content.Load<Texture2D>("WhiteA");
            Textures["Noise_Blue"] = content.Load<Texture2D>("BlueA");
            Textures["Atlas_Weather"] = content.Load<Texture2D>("Particle_atlas");
            Effects["WaterFlood"].SetSafe("WaterLevel", 50f / 255f);
            Effects["WaterFlood"].SetSafe("WaterColor", new Vector4(0.1f, 0.4f, 0.8f, 0.6f));
            // Assign static textures to effects immediately
            Effects["Gusting"].SetSafe("NoiseTexture", Textures["Noise_Perlin"]);
            Effects["RollingFog"].SetSafe("NoiseTexture", Textures["Noise_Perlin"]);
            Effects["CloudShadows"].SetSafe("NoiseTexture", Textures["Noise_Perlin"]);
            Effects["TestWeather"].SetSafe("NoiseTexture", Textures["Noise_Perlin"]);
        }

        // --- Routing methods to keep Game1.cs unchanged ---
        public void UpdateParticles(GameTime gameTime, WeatherSimulator weatherSim, Vector2 cameraPos, Vector2 viewport)
            => Particles.Update(gameTime, weatherSim, cameraPos, viewport);

        public void DrawParticleWeather(SpriteBatch spriteBatch, WeatherSimulator weatherSim, Matrix cameraTransform, float totalSecs)
            => Particles.Draw(spriteBatch, weatherSim, cameraTransform, totalSecs);

        public void DrawWeatherOverlays(SpriteBatch spriteBatch, Vector2 cameraPos, Vector2 viewport, WeatherSimulator weatherSim, RenderTarget2D volumeDepth)
            => Overlays.Draw(spriteBatch, cameraPos, viewport, weatherSim, volumeDepth);

        public void UpdatePostProcessing(WeatherSimulator weatherSim, DayTimeManager timeManager, GameTime gameTime)
            => PostFX.Update(weatherSim, timeManager, gameTime);

        public Effect[] GetActivePostProcessingEffects()
            => PostFX.GetActiveEffects();

        public string GetDebugInfo() => PostFX.GetDebugInfo() + "\n" + Particles.GetDebugInfo();
    }

    // ========================================================================
    // SUBSYSTEM 1: GPU PARTICLE PHYSICS & DRAWING
    // ========================================================================
    public class GPUParticleSystem
    {
        private ShaderManager _manager;
        private RenderTarget2D _stateA, _stateB;
        private Vector4[] _particleData;
        private VertexPositionTexture[] _quad;
        private readonly int _maxParticles = 10000;

        public GPUParticleSystem(ShaderManager manager)
        {
            _manager = manager;
            _particleData = new Vector4[_maxParticles];
            int texSize = 100; // sqrt(10000)

            _stateA = new RenderTarget2D(_manager.GraphicsDevice, texSize, texSize, false, SurfaceFormat.Vector4, DepthFormat.None);
            _stateB = new RenderTarget2D(_manager.GraphicsDevice, texSize, texSize, false, SurfaceFormat.Vector4, DepthFormat.None);

            for (int i = 0; i < _maxParticles; i++) _particleData[i] = new Vector4(0, 0, -1, 0);
            _stateA.SetData(_particleData);
            _stateB.SetData(_particleData);

            _quad = new VertexPositionTexture[] {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1))
            };
        }

        public void Update(GameTime gameTime, WeatherSimulator weatherSim, Vector2 cameraPos, Vector2 viewportSize)
        {
            var type = weatherSim.CurrentWeather;
            if (type == WeatherType.Clear || type == WeatherType.PartlyCloudy || type == WeatherType.Overcast) return;

            var effect = _manager.Effects["ParticleUpdate"];
            effect.SetSafe("StateTexture", _stateA);
            effect.SetSafe("DeltaTime", (float)gameTime.ElapsedGameTime.TotalSeconds);
            effect.SetSafe("Time", (float)gameTime.TotalGameTime.TotalSeconds);
            effect.SetSafe("CameraPosition", cameraPos);
            effect.SetSafe("ViewportSize", viewportSize);

            float visualWindSpeed = weatherSim.Visuals.WindVector.X; // Drift speed in pixels per second
            Vector2 windDir = new Vector2(1f, 0f); // Wind blows horizontally across the screen

            effect.SetSafe("WindDirection", windDir);
            effect.SetSafe("WindSpeed", visualWindSpeed);


            float fallSpeed = 400f; float splashDuration = 0.15f;
            if (type == WeatherType.Snow || type == WeatherType.Blizzard) { fallSpeed = type == WeatherType.Blizzard ? 200f : 80f; splashDuration = 2.0f; }
            else if (type == WeatherType.Thunderstorm) { fallSpeed = 700f; splashDuration = 0.2f; }
            else if (type == WeatherType.Drizzle) { fallSpeed = 200f; splashDuration = 0.1f; }
            else if (type == WeatherType.DustStorm) { fallSpeed = 20f; splashDuration = 0.5f; }

            effect.SetSafe("FallSpeed", fallSpeed);
            effect.SetSafe("SplashDuration", splashDuration);

            _manager.GraphicsDevice.SetRenderTarget(_stateB);
            effect.CurrentTechnique.Passes[0].Apply();
            _manager.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _quad, 0, 2);

            var temp = _stateA; _stateA = _stateB; _stateB = temp;
            _stateA.GetData(_particleData);
        }

        public void Draw(SpriteBatch spriteBatch, WeatherSimulator weatherSim, Matrix cameraTransform, float totalSeconds)
        {
            float intensity = weatherSim.CurrentWeather == WeatherType.DustStorm ? weatherSim.Visuals.FogDensity :
                              weatherSim.CurrentWeather == WeatherType.Snow || weatherSim.CurrentWeather == WeatherType.Blizzard ? weatherSim.Visuals.SnowIntensity :
                              weatherSim.Visuals.RainIntensity;

            if (intensity <= 0.01f) return;

            int particlesToDraw = (int)(_maxParticles * intensity);
            float visualWindSpeed = (weatherSim.CurrentClimate.WindKph / 150f) * 200f;
            float rotation = (float)Math.Atan2(visualWindSpeed, 400f);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, cameraTransform);

            var atlas = _manager.Textures["Atlas_Weather"];
            var type = weatherSim.CurrentWeather;

            for (int i = 0; i < particlesToDraw; i++)
            {
                Vector4 data = _particleData[i];
                if (data.W < 0.5f && data.Z < 0) continue;

                int baseFrame = (i * 7 + 13) % 4;
                int animFrame = (baseFrame + (int)(totalSeconds * 12f)) % 4;
                Rectangle src = default;
                Vector2 origin = new Vector2(4, 4);
                float rot = rotation;
                Vector2 pos = new Vector2(data.X, data.Y);

                if (data.W < 0.5f) // Falling
                {
                    pos.Y -= data.Z;
                    switch (type)
                    {
                        case WeatherType.Rain: src = new Rectangle(animFrame * 8, 0, 8, 16); origin = new Vector2(4, 8); break;
                        case WeatherType.Snow: src = new Rectangle(32 + (animFrame % 2) * 8, (animFrame / 2) * 8, 8, 8); rot += totalSeconds * (i % 2 == 0 ? 1 : -1); break;
                        case WeatherType.DustStorm: src = new Rectangle(48 + (animFrame % 2) * 8, (animFrame / 2) * 8, 8, 8); rot += totalSeconds * 2f; break;
                        case WeatherType.Drizzle: src = new Rectangle(64 + (animFrame % 2) * 8, (animFrame / 2) * 8, 8, 8); rot = 0f; break;
                        case WeatherType.Thunderstorm:
                            if (i % 5 == 0) src = new Rectangle(80 + (animFrame % 2) * 8, 0, 8, 8);
                            else { src = new Rectangle(animFrame * 8, 0, 8, 16); origin = new Vector2(4, 8); }
                            break;
                        case WeatherType.Blizzard:
                            src = i % 5 == 0 ? new Rectangle(80 + (animFrame % 2) * 8, 8, 8, 8) : new Rectangle(32 + (animFrame % 2) * 8, (animFrame / 2) * 8, 8, 8);
                            rot += totalSeconds * 2f;
                            break;
                    }
                }
                else // Splashing
                {
                    float splashProgress = MathHelper.Clamp(-data.Z, 0f, 1f);
                    rot = 0f; pos = new Vector2(data.X, data.Y);
                    if (type == WeatherType.Rain || type == WeatherType.Thunderstorm || type == WeatherType.Drizzle)
                    {
                        int sAnim = Math.Min((int)(splashProgress * 4f), 3);
                        src = new Rectangle(64 + (sAnim % 2) * 8, (sAnim / 2) * 8, 8, 8);
                    }
                    else
                    {
                        src = new Rectangle((type == WeatherType.DustStorm ? 48 : 32) + (baseFrame % 2) * 8, (baseFrame / 2) * 8, 8, 8);
                    }
                }
                spriteBatch.Draw(atlas, pos, src, Color.White, rot, origin, 1.0f, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        public string GetDebugInfo() => $"Particles Processing: Yes | Count: {_maxParticles}";
    }

    // ========================================================================
    // SUBSYSTEM 2: ENVIRONMENT OVERLAYS (Fog, Clouds)
    // ========================================================================
    public class WeatherOverlayRenderer
    {
        private ShaderManager _manager;

        public WeatherOverlayRenderer(ShaderManager manager) { _manager = manager; }

        public void Draw(SpriteBatch spriteBatch, Vector2 cameraPos, Vector2 viewport, WeatherSimulator weatherSim, RenderTarget2D volumeDepth)
        {
            float time = (float)DateTime.Now.TimeOfDay.TotalSeconds;
            Vector2 windDir = weatherSim.Visuals.WindVector != Vector2.Zero ? Vector2.Normalize(weatherSim.Visuals.WindVector) : new Vector2(1, 0);
            Rectangle bounds = new Rectangle(0, 0, (int)viewport.X, (int)viewport.Y);

            // 1. CLOUD SHADOWS
            if (weatherSim.Visuals.CloudCover > 0.05f && _manager.Effects.TryGetValue("CloudShadows", out Effect shadows))
            {
                shadows.SetSafe("CameraPosition", cameraPos);
                shadows.SetSafe("ViewportSize", viewport);
                shadows.SetSafe("Time", time);
                shadows.SetSafe("WindDirection", windDir);
                shadows.SetSafe("CloudCover", weatherSim.Visuals.CloudCover);

                _manager.GraphicsDevice.BlendState = BlendState.Opaque;

                // 2. Apply the shader
                shadows.CurrentTechnique.Passes[0].Apply();

                // 3. Draw the Quad! No SpriteBatch needed.
                _manager.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    _manager.FullScreenQuad,
                    0,
                    2
                );
            }

            // 2. FOG
            if (_manager.Effects.TryGetValue("RollingFog", out Effect fog))
            {
                fog.SetSafe("CameraPosition", cameraPos);
                fog.SetSafe("ViewportSize", viewport);
                fog.SetSafe("VolumeDepthTexture", volumeDepth);
                fog.SetSafe("MaxAltitude", 150f); // Must match Volumetric Depth pass!
                fog.SetSafe("FogTopAltitude", 50f);

                float density = Math.Max(weatherSim.Visuals.FogDensity, weatherSim.Visuals.RainIntensity);
                fog.SetSafe("FogDensity", 1.5f);

                Vector3 color = weatherSim.CurrentWeather == WeatherType.DustStorm ? new Vector3(0.6f, 0.4f, 0.2f) : new Vector3(0.5f, 0.55f, 0.6f);
                fog.SetSafe("FogColor", color);

                _manager.GraphicsDevice.BlendState = BlendState.Opaque;

                // 2. Apply the shader
                fog.CurrentTechnique.Passes[0].Apply();

                // 3. Draw the Quad! No SpriteBatch needed.
                _manager.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    _manager.FullScreenQuad,
                    0,
                    2
                );
                //spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.PointClamp, null, null, fog);
                //spriteBatch.Draw(_manager.PixelTexture, bounds, Color.White);
                //spriteBatch.End();
            }
            if (_manager.Effects.TryGetValue("TestWeather", out Effect test))
            {

                //_manager.GraphicsDevice.BlendState = BlendState.Opaque;

                //// 2. Apply the shader
                ////_manager.Effects["TestWeather"].CurrentTechnique.Passes[0].Apply();

                //// 3. Draw the Quad! No SpriteBatch needed.
                ////_manager.GraphicsDevice.DrawUserPrimitives(
                //    PrimitiveType.TriangleStrip,
                //    _manager.FullScreenQuad,
                //    0,
                //    2
                //);
            }

        }
    }

    // ========================================================================
    // SUBSYSTEM 3: POST PROCESSING (Color, Gusting, Lighting)
    // ========================================================================
    public class PostProcessController
    {
        private ShaderManager _manager;
        private float _lightningFlashAmount = 0f;
        private float _lightningTimer = 0f;
        private Random _random = new Random();

        public float CurrentDistortion { get; private set; }
        public Vector3 CurrentTint { get; private set; }

        public PostProcessController(ShaderManager manager) { _manager = manager; }

        public void Update(WeatherSimulator weatherSim, DayTimeManager timeManager, GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            var climate = weatherSim.CurrentClimate;

            // --- Gusting ---
            Vector2 gustingDir = new Vector2(1, 0) * (climate.WindKph / 100f);
            CurrentDistortion = (climate.WindKph / 150f) * 0.015f;

            _manager.Effects["Gusting"].SetSafe("Time", time);
            _manager.Effects["Gusting"].SetSafe("WindVector", gustingDir);
            _manager.Effects["Gusting"].SetSafe("DistortionStrength", CurrentDistortion);

            // --- Lightning Logic ---
            if (_lightningFlashAmount > 0) _lightningFlashAmount = Math.Max(0, _lightningFlashAmount - (dt * 3.0f));
            if (weatherSim.CurrentWeather == WeatherType.Thunderstorm)
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0)
                {
                    _lightningFlashAmount = 1.0f;
                    _lightningTimer = (float)(_random.NextDouble() * 5.0 + 1.0);
                }
            }

            // --- Color Grading ---
            float tSat = 1.0f, tBright = 1.0f, tCont = 1.0f;
            Vector3 tTint = Vector3.One;

            if (climate.TempC > 25f) tTint = Vector3.Lerp(Vector3.One, new Vector3(1.0f, 0.9f, 0.7f), MathHelper.Clamp((climate.TempC - 25f) / 15f, 0f, 1f));
            else if (climate.TempC < 5f) tTint = Vector3.Lerp(Vector3.One, new Vector3(0.8f, 0.9f, 1.0f), MathHelper.Clamp((5f - climate.TempC) / 15f, 0f, 1f));

            if (weatherSim.CurrentWeather == WeatherType.Thunderstorm || weatherSim.CurrentWeather == WeatherType.Blizzard)
            {
                tSat = 0.5f; tBright = 0.6f; tCont = 1.1f; tTint = new Vector3(0.6f, 0.7f, 0.8f);
            }
            else if (weatherSim.Visuals.CloudCover > 0.1f)
            {
                tBright = MathHelper.Lerp(1.0f, 0.8f, weatherSim.Visuals.CloudCover);
                tSat = MathHelper.Lerp(1.0f, 0.85f, weatherSim.Visuals.CloudCover);
                tCont = MathHelper.Lerp(1.0f, 0.85f, weatherSim.Visuals.CloudCover);
            }

            if (weatherSim.CurrentWeather == WeatherType.DustStorm) { tCont = 0.8f; tSat = 0.5f; tTint = new Vector3(0.9f, 0.7f, 0.5f); }

            if (_lightningFlashAmount > 0)
            {
                tBright = MathHelper.Lerp(tBright, 3.0f, _lightningFlashAmount);
                tCont = MathHelper.Lerp(tCont, 0.8f, _lightningFlashAmount);
                tTint = Vector3.Lerp(tTint, new Vector3(0.9f, 0.95f, 1.0f), _lightningFlashAmount);
            }

            _manager.Effects["ColorGrading"].SetSafe("Saturation", tSat);
            _manager.Effects["ColorGrading"].SetSafe("Brightness", tBright);
            _manager.Effects["ColorGrading"].SetSafe("Contrast", tCont);
            _manager.Effects["ColorGrading"].SetSafe("TintColor", tTint);
            CurrentTint = tTint;

            // --- Ambient Lighting (Day/Night) ---
            Vector3 ambientLight = timeManager.CurrentAmbientColor;
            if (weatherSim.CurrentWeather == WeatherType.Thunderstorm) ambientLight *= new Vector3(0.6f, 0.6f, 0.7f);
            else if (climate.Humidity > 60f) ambientLight *= new Vector3(MathHelper.Lerp(1.0f, 0.7f, weatherSim.Visuals.CloudCover));
            if (_lightningFlashAmount > 0) ambientLight = Vector3.Lerp(ambientLight, new Vector3(2.0f, 2.0f, 2.5f), _lightningFlashAmount);

            _manager.Effects["AmbientLighting"].SetSafe("AmbientColor", ambientLight);
        }

        public Effect[] GetActiveEffects()
        {
            return new Effect[] {
                _manager.Effects["Gusting"],
                _manager.Effects["AmbientLighting"],
                _manager.Effects["ColorGrading"]
            };
        }

        public string GetDebugInfo() => $"Gusting: {CurrentDistortion:F5} | Tint: {CurrentTint.X:F2},{CurrentTint.Y:F2},{CurrentTint.Z:F2}";
    }
}