using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pixel_Simulations
{
    public class GrassSystem
    {
        private GraphicsDevice _device;
        private Effect _effect;
        private VertexBuffer _bladeBuffer;
        private VertexBuffer _flowerBuffer;
        public int _bladeVertCount = 0;
        private int _flowerVertCount = 0;

        public Dictionary<string, GrassSettings> Presets { get; private set; } = new();
        public GrassSettings Settings { get; set; } = new();

        public Vector2 PlayerPosition;
        public float Time;

        // Cached noise data
        private Color[] _noisePixels;
        private int _noiseW, _noiseH;

        public GrassSystem(GraphicsDevice device)
        {
            _device = device;
        }

        public void LoadContent(ContentManager content, NoiseManager noiseManager, GameState state)
        {
            _effect = content.Load<Effect>("grass");

            // 1. CACHE EXTERNAL NOISE TEXTURE FOR CLUSTERING & GROWTH TIERS
            Texture2D noiseTex = noiseManager.GetActiveNoise() ?? noiseManager.Noises.Values.FirstOrDefault();
            if (noiseTex != null)
            {
                _noiseW = noiseTex.Width;
                _noiseH = noiseTex.Height;
                _noisePixels = new Color[_noiseW * _noiseH];
                noiseTex.GetData(_noisePixels);
            }

            // 2. LOAD PRESETS MASTER JSON
            string presetPath = Path.Combine(PathHelper.GetReadDataPath(), "GrassPresetsMaster.json");
            if (!File.Exists(presetPath))
            {
                presetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Data", "GrassPresetsMaster.json");
            }

            if (File.Exists(presetPath))
            {
                try
                {
                    string json = File.ReadAllText(presetPath);
                    Presets = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GrassSettings>>(json) ?? new();
                    Settings = Presets.Values.FirstOrDefault() ?? new GrassSettings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GrassSystem] Error loading presets: {ex.Message}");
                    Settings = new GrassSettings();
                }
            }

            // 3. GENERATE FIELD USING MASKS & NOISE
            RebuildFieldFromMap(state);
        }

        public void RebuildFieldFromMap(GameState state)
        {
            if (state.CurrentMap == null || state.TerrainMaskChunks == null || state.TerrainMaskChunks.Count == 0) return;

            var bladeVerts = new List<GrassVertex>();
            var flowerVerts = new List<GrassVertex>();

            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE; // 256
            float defaultStep = Settings.DensityStep > 0 ? Settings.DensityStep : 12f;

            foreach (var kvp in state.TerrainMaskChunks)
            {
                Point chunkCoord = kvp.Key;
                Texture2D maskTex = kvp.Value;
                Color[] maskPixels = new Color[chunkSize * chunkSize];
                maskTex.GetData(maskPixels);

                float chunkWorldX = chunkCoord.X * chunkSize;
                float chunkWorldY = chunkCoord.Y * chunkSize;

                for (float ly = 0; ly < chunkSize; ly += defaultStep)
                {
                    for (float lx = 0; lx < chunkSize; lx += defaultStep)
                    {
                        int px = Math.Clamp((int)lx, 0, chunkSize - 1);
                        int py = Math.Clamp((int)ly, 0, chunkSize - 1);
                        Color maskCol = maskPixels[py * chunkSize + px];

                        // ==============================================================
                        // FIX A: STRICT VOID CHECK & MULTI-BIOME PRESET LOOKUP
                        // ==============================================================
                        int redByte = maskCol.R;
                        if (redByte == 0) continue; // 0 is ALWAYS Barren / Void. Never spawn grass on 0!

                        // Find which loaded preset matches this Red-channel Biome ID
                        GrassSettings matchedPreset = null;
                        foreach (var p in Presets.Values)
                        {
                            if (p.BiomeMaskValue > 0 && Math.Abs(redByte - p.BiomeMaskValue) <= p.BiomeTolerance)
                            {
                                matchedPreset = p;
                                break;
                            }
                        }

                        // If no grass preset is assigned to this painted biome value, skip
                        if (matchedPreset == null) continue;

                        float worldX = chunkWorldX + lx;
                        float worldY = chunkWorldY + ly;

                        // --- 2. NOISE TEXTURE: MIN / MID / MAX GROWTH TIERS ---
                        float noiseVal = SampleNoise(worldX, worldY);
                        if (noiseVal < matchedPreset.MinThreshold) continue; // Bare clearing

                        int bladesInTuft = 1;
                        float heightTier = 0.55f;

                        if (noiseVal >= matchedPreset.MaxThreshold)
                        {
                            bladesInTuft = matchedPreset.TuftMaxBlades; // Lush tier
                            heightTier = 1.35f;
                        }
                        else if (noiseVal >= matchedPreset.MidThreshold)
                        {
                            bladesInTuft = Math.Max(2, (int)(matchedPreset.TuftMaxBlades * 0.65f)); // Standard tier
                            heightTier = 0.95f;
                        }
                        else
                        {
                            bladesInTuft = Math.Max(1, (int)(matchedPreset.TuftMaxBlades * 0.35f)); // Stunted tier
                            heightTier = 0.55f;
                        }

                        // --- 3. BLUE CHANNEL: ELEVATION OFFSET ---
                        float elev = maskCol.B / 255.0f;
                        float elevOffsetY = -(elev * matchedPreset.ElevationScale);

                        float tuftCenterX = worldX + (Hash(worldX, worldY) - 0.5f) * (matchedPreset.DensityStep * 0.6f);
                        float tuftCenterY = worldY + (Hash(worldY, worldX) - 0.5f) * (matchedPreset.DensityStep * 0.6f) + elevOffsetY;

                        int segs = Math.Clamp(matchedPreset.Segments, 2, 6);

                        for (int b = 0; b < bladesInTuft; b++)
                        {
                            float bSeed = b * 17.13f;
                            float rad = Hash(worldX + bSeed, worldY) * matchedPreset.TuftRadius;
                            float ang = Hash(worldY + bSeed, worldX) * MathHelper.TwoPi;

                            float rootX = tuftCenterX + (float)Math.Cos(ang) * rad;
                            float rootY = tuftCenterY + (float)Math.Sin(ang) * rad;

                            float hJitter = (Hash(rootX, rootY) - 0.5f) * 0.25f;
                            float hMod = Math.Max(0.2f, heightTier + hJitter);

                            float lean = (Hash(rootY, rootX) - 0.5f) * 8.0f;
                            float phase = Hash(rootX * 2.0f, rootY * 2.0f) * MathHelper.TwoPi;
                            float variation = Hash(rootX * rootY, b);

                            int fType = 0;
                            float fSize = 0f;
                            float fStage = 0f;
                            float rotJitter = 0f;

                            if (matchedPreset.FloraType > 0 && hMod > 0.70f)
                            {
                                if (Hash(rootX * 3.1f, rootY * 7.7f) < matchedPreset.FloraProbability)
                                {
                                    fType = matchedPreset.FloraType;
                                    fSize = MathHelper.Lerp(matchedPreset.FloraMinSize, matchedPreset.FloraMaxSize, Hash(rootY, b));

                                    if (matchedPreset.FloraStageMode == -1)
                                        fStage = (float)Math.Floor(Hash(rootX * 4.3f, rootY) * 4.0f);
                                    else
                                        fStage = matchedPreset.FloraStageMode;

                                    float maxRad = MathHelper.ToRadians(matchedPreset.FloraRotJitter);
                                    rotJitter = (Hash(rootX * 8.8f, rootY * 2.2f) - 0.5f) * 2.0f * maxRad;
                                }
                            }

                            Vector3 rootPos = new Vector3(rootX, rootY, lean);
                            Vector2 windHeight = new Vector2(phase, hMod);
                            Vector4 floraData = new Vector4(fType, fSize, fStage, rotJitter);

                            for (int s = 0; s < segs; s++)
                            {
                                float t0 = (float)s / segs;
                                float t1 = (float)(s + 1) / segs;

                                bladeVerts.Add(new GrassVertex(rootPos, new Vector2(t0, -1), windHeight, variation, Vector4.Zero));
                                bladeVerts.Add(new GrassVertex(rootPos, new Vector2(t1, -1), windHeight, variation, Vector4.Zero));
                                bladeVerts.Add(new GrassVertex(rootPos, new Vector2(t0, 1), windHeight, variation, Vector4.Zero));

                                bladeVerts.Add(new GrassVertex(rootPos, new Vector2(t0, 1), windHeight, variation, Vector4.Zero));
                                bladeVerts.Add(new GrassVertex(rootPos, new Vector2(t1, -1), windHeight, variation, Vector4.Zero));
                                bladeVerts.Add(new GrassVertex(rootPos, new Vector2(t1, 1), windHeight, variation, Vector4.Zero));
                            }

                            if (fType > 0)
                            {
                                flowerVerts.Add(new GrassVertex(rootPos, new Vector2(0f, -1), windHeight, variation, floraData));
                                flowerVerts.Add(new GrassVertex(rootPos, new Vector2(1f, -1), windHeight, variation, floraData));
                                flowerVerts.Add(new GrassVertex(rootPos, new Vector2(0f, 1), windHeight, variation, floraData));

                                flowerVerts.Add(new GrassVertex(rootPos, new Vector2(0f, 1), windHeight, variation, floraData));
                                flowerVerts.Add(new GrassVertex(rootPos, new Vector2(1f, -1), windHeight, variation, floraData));
                                flowerVerts.Add(new GrassVertex(rootPos, new Vector2(1f, 1), windHeight, variation, floraData));
                            }
                        }
                    }
                }
            }

            _bladeVertCount = bladeVerts.Count;
            if (_bladeVertCount > 0)
            {
                _bladeBuffer?.Dispose();
                _bladeBuffer = new VertexBuffer(_device, GrassVertex.VertexDeclaration, _bladeVertCount, BufferUsage.WriteOnly);
                _bladeBuffer.SetData(bladeVerts.ToArray());
            }

            _flowerVertCount = flowerVerts.Count;
            if (_flowerVertCount > 0)
            {
                _flowerBuffer?.Dispose();
                _flowerBuffer = new VertexBuffer(_device, GrassVertex.VertexDeclaration, _flowerVertCount, BufferUsage.WriteOnly);
                _flowerBuffer.SetData(flowerVerts.ToArray());
            }
        }

        private float SampleNoise(float wx, float wy)
        {
            if (_noisePixels == null || _noiseW == 0) return 0.5f;
            int tx = Math.Abs((int)(wx * 0.45f)) % _noiseW;
            int ty = Math.Abs((int)(wy * 0.45f)) % _noiseH;
            return _noisePixels[ty * _noiseW + tx].R / 255.0f;
        }

        private float Hash(float x, float y)
        {
            float n = (float)Math.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return n - (float)Math.Floor(n);
        }

        public void Update(GameTime gameTime, Vector2 playerWorldPos)
        {
            Time = (float)gameTime.TotalGameTime.TotalSeconds;
            PlayerPosition = playerWorldPos;
        }

        public void Draw(Camera camera, Texture2D zDepthTexture, WeatherSimulator weather = null)
        {
            if (_bladeVertCount == 0 || _effect == null || zDepthTexture == null) return;

            _device.DepthStencilState = DepthStencilState.None;

            _effect.Parameters["u_camViewTop"]?.SetValue((float)camera.SimViewBounds.Top);
            _effect.Parameters["u_camViewHeight"]?.SetValue((float)camera.SimViewBounds.Height);
            _effect.Parameters["WorldViewProjection"]?.SetValue(camera.SimWVP);
            _effect.Parameters["Time"]?.SetValue(Time);
            _effect.Parameters["PlayerPos"]?.SetValue(PlayerPosition);

            // FIX C: Pass Z-Depth Sorting Texture
            _effect.Parameters["ObjectDepthTexture"]?.SetValue(zDepthTexture);

            // FIX B: Pass Viewport Scaling so depth sampling aligns at any zoom level
            Vector2 vpScale = new Vector2(
                (float)camera.SimViewportRect.Width / RenderPipeline.MASTER_SIM_W,
                (float)camera.SimViewportRect.Height / RenderPipeline.MASTER_SIM_H
            );
            _effect.Parameters["u_viewportScale"]?.SetValue(vpScale);

            // Blade & Wind Parameters
            _effect.Parameters["u_baseH"]?.SetValue(Settings.BaseHeight);
            _effect.Parameters["u_baseW"]?.SetValue(Settings.BladeWidth);
            _effect.Parameters["u_taper"]?.SetValue(Settings.Taper);
            _effect.Parameters["u_curve"]?.SetValue(Settings.RestingCurvature);
            _effect.Parameters["u_stiff"]?.SetValue(Settings.Stiffness);

            Vector2 windDir = new Vector2(1f, 0f);
            float windSpeed = Settings.WindSpeed;

            if (weather != null)
            {
                if (weather.Visuals.WindVector != Vector2.Zero)
                    windDir = Vector2.Normalize(weather.Visuals.WindVector);
                windSpeed = (weather.CurrentClimate.WindKph / 100f) * Settings.WindSpeed * 2.5f;
            }
            else
            {
                float rad = MathHelper.ToRadians(Settings.WindAngle);
                windDir = new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad));
            }

            _effect.Parameters["u_windDir"]?.SetValue(windDir);
            _effect.Parameters["u_windSpeed"]?.SetValue(windSpeed);
            _effect.Parameters["u_waveLen"]?.SetValue(Settings.WaveLength);
            _effect.Parameters["u_gustStr"]?.SetValue(Settings.GustStrength);
            _effect.Parameters["u_flutterStr"]?.SetValue(Settings.FlutterStr);

            _effect.Parameters["u_pushRad"]?.SetValue(Settings.PlayerPushRadius);
            _effect.Parameters["u_pushStr"]?.SetValue(Settings.PlayerPushStrength);
            _effect.Parameters["u_animFPS"]?.SetValue(Settings.AnimFPS);
            _effect.Parameters["u_pixelSnap"]?.SetValue(Settings.PixelSnap ? 1 : 0);

            // Palettes
            _effect.Parameters["u_cRoot"]?.SetValue(Settings.RootColor.ToVector3());
            _effect.Parameters["u_cMid"]?.SetValue(Settings.MidColor.ToVector3());
            _effect.Parameters["u_cTip"]?.SetValue(Settings.TipColor.ToVector3());
            _effect.Parameters["u_cPetal"]?.SetValue(Settings.PetalColor.ToVector3());
            _effect.Parameters["u_cCore"]?.SetValue(Settings.CoreColor.ToVector3());
            _effect.Parameters["u_cCalyx"]?.SetValue(Settings.CalyxColor.ToVector3());

            // 1. Draw Blades
            _effect.Parameters["u_isFlower"]?.SetValue(0);
            _device.SetVertexBuffer(_bladeBuffer);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawPrimitives(PrimitiveType.TriangleList, 0, _bladeVertCount / 3);
            }

            // 2. Draw Flowers
            if (_flowerVertCount > 0)
            {
                _effect.Parameters["u_isFlower"]?.SetValue(1);
                _device.SetVertexBuffer(_flowerBuffer);
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawPrimitives(PrimitiveType.TriangleList, 0, _flowerVertCount / 3);
                }
            }
        }
    }
}