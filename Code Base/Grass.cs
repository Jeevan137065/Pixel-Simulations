using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;


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

        private Texture2D _noiseA;
        private Texture2D _noiseB;
        private Color[] _dataA, _dataB;

        public GrassSettings Settings;
        public Vector2 PlayerPosition;
        public float Time;

        public GrassSystem(GraphicsDevice device) { _device = device; }

        public void LoadContent(ContentManager content,NoiseManager noiseManager, List<RectangleF> validAreas)
        {
            _effect = content.Load<Effect>("grass");

            // --- USE THE NOISE MANAGER! ---
            // Make sure these names match the keys you load in NoiseManager.cs!
            _noiseA = noiseManager.Noises.ContainsKey("Perlin") ? noiseManager.Noises["Perlin"] : null;
            _noiseB = noiseManager.Noises.ContainsKey("Streak") ? noiseManager.Noises["Streak"] : null;

            if (_noiseA != null) { _dataA = new Color[_noiseA.Width * _noiseA.Height]; _noiseA.GetData(_dataA); }
            if (_noiseB != null) { _dataB = new Color[_noiseB.Width * _noiseB.Height]; _noiseB.GetData(_dataB); }

            // --- LOAD JSON DICTIONARY ---
            string presetPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "Data", "GrassPresetsMaster.json");
            if (System.IO.File.Exists(presetPath))
            {
                string json = System.IO.File.ReadAllText(presetPath);

                // Deserialize into a Dictionary
                var allPresets = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, GrassSettings>>(json);

                if (allPresets != null)
                {
                    // Pick the biome you want to spawn! 
                    // In the future, this string could be passed in dynamically per-map.
                    string targetBiome = "Spring Grass";

                    if (allPresets.TryGetValue(targetBiome, out var loadedSettings))
                    {
                        Settings = loadedSettings;
                        System.Diagnostics.Debug.WriteLine($"Successfully loaded Grass Biome: {targetBiome}");
                    }
                    else
                    {
                        // Fallback to the first one in the list if the name is misspelled
                        Settings = System.Linq.Enumerable.FirstOrDefault(allPresets.Values) ?? new GrassSettings();
                        System.Diagnostics.Debug.WriteLine($"Target biome not found. Fell back to default.");
                    }
                }
            }
            else
            {
                Settings = new GrassSettings(); // Absolute Fallback
            }

            InitializeField(validAreas);
        }

        private float SampleNoise(Color[] data, int w, int h, float x, float y)
        {
            int nx = (int)Math.Floor(x) % w;
            int ny = (int)Math.Floor(y) % h;
            if (nx < 0) nx += w; if (ny < 0) ny += h;
            return data[ny * w + nx].R / 255.0f;
        }

        private float GetDualNoise(float x, float y)
        {
            float sA = SampleNoise(_dataA, _noiseA.Width, _noiseA.Height, x, y);
            float sB = SampleNoise(_dataB, _noiseB.Width, _noiseB.Height, x * 1.618f + 50f, y * 1.618f + 50f);
            return (sA + sB) * 0.5f;
        }

        private float Hash(float x, float y)
        {
            float h = (float)Math.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            return h - (float)Math.Floor(h);
        }

        private void InitializeField(List<RectangleF> validAreas)
        {
            var bladeVerts = new List<GrassVertex>();
            var flowerVerts = new List<GrassVertex>();

            foreach (var rect in validAreas)
            {
                for (float y = rect.Top; y < rect.Bottom; y += Settings.DensityStep)
                {
                    for (float x = rect.Left; x < rect.Right; x += Settings.DensityStep)
                    {
                        float nVal = GetDualNoise(x * 0.2f, y * 0.2f);
                        if (nVal < Settings.MinThreshold) continue;

                        int count = nVal > Settings.MaxThreshold ? 3 : nVal > Settings.MidThreshold ? 2 : 1;
                        float heightMod = nVal > Settings.MaxThreshold ? 1.4f : nVal > Settings.MidThreshold ? 0.8f : 0.4f;

                        for (int i = 0; i < count; i++)
                        {
                            float h = Hash(x + i, y);
                            float px = x + (h - 0.5f) * Settings.DensityStep;
                            float py = y + (Hash(x, y + i) - 0.5f) * Settings.DensityStep;

                            float hMod = heightMod * (0.8f + h * 0.4f);
                            float wOff = h * MathHelper.TwoPi;
                            float lean = (Hash(x, y * 2) - 0.5f) * 20f;
                            float var = Hash(x * y, i);

                            int fType = (Settings.FlowerType > 0 && hMod > 0.8f && Hash(x * 3, y * 3) < Settings.FlowerProbability) ? Settings.FlowerType : 0;
                            float fSize = Settings.FlowerMinSize + Hash(x * 4, y * 4) * (Settings.FlowerMaxSize - Settings.FlowerMinSize);

                            Vector2 root = new Vector2(px, py);

                            // Generate Blade Quads
                            for (int s = 0; s < Settings.Segments; s++)
                            {
                                float t0 = (float)s / Settings.Segments;
                                float t1 = (float)(s + 1) / Settings.Segments;

                                // Tri 1
                                bladeVerts.Add(new GrassVertex(root, t0, -1f, wOff, hMod, var, lean, Color.White, 0, 0));
                                bladeVerts.Add(new GrassVertex(root, t1, -1f, wOff, hMod, var, lean, Color.White, 0, 0));
                                bladeVerts.Add(new GrassVertex(root, t0, 1f, wOff, hMod, var, lean, Color.White, 0, 0));
                                // Tri 2
                                bladeVerts.Add(new GrassVertex(root, t0, 1f, wOff, hMod, var, lean, Color.White, 0, 0));
                                bladeVerts.Add(new GrassVertex(root, t1, -1f, wOff, hMod, var, lean, Color.White, 0, 0));
                                bladeVerts.Add(new GrassVertex(root, t1, 1f, wOff, hMod, var, lean, Color.White, 0, 0));
                            }

                            // Generate Flower Quad (Only if it has one)
                            if (fType > 0)
                            {
                                flowerVerts.Add(new GrassVertex(root, 0, -1f, wOff, hMod, var, lean, Color.White, fType, fSize)); // TL
                                flowerVerts.Add(new GrassVertex(root, 0, 1f, wOff, hMod, var, lean, Color.White, fType, fSize));  // TR
                                flowerVerts.Add(new GrassVertex(root, 1f, -1f, wOff, hMod, var, lean, Color.White, fType, fSize)); // BL

                                flowerVerts.Add(new GrassVertex(root, 1f, -1f, wOff, hMod, var, lean, Color.White, fType, fSize)); // BL
                                flowerVerts.Add(new GrassVertex(root, 0, 1f, wOff, hMod, var, lean, Color.White, fType, fSize));  // TR
                                flowerVerts.Add(new GrassVertex(root, 1f, 1f, wOff, hMod, var, lean, Color.White, fType, fSize));  // BR
                            }
                        }
                    }
                }

                _bladeVertCount = bladeVerts.Count;
                if (_bladeVertCount > 0)
                {
                    _bladeBuffer = new VertexBuffer(_device, GrassVertex.VertexDeclaration, _bladeVertCount, BufferUsage.WriteOnly);
                    _bladeBuffer.SetData(bladeVerts.ToArray());
                }

                _flowerVertCount = flowerVerts.Count;
                if (_flowerVertCount > 0)
                {
                    _flowerBuffer = new VertexBuffer(_device, GrassVertex.VertexDeclaration, _flowerVertCount, BufferUsage.WriteOnly);
                    _flowerBuffer.SetData(flowerVerts.ToArray());
                }
            }
        }

        public void Update(GameTime gameTime, Vector2 playerWorldPos)
        {
            Time = (float)gameTime.TotalGameTime.TotalSeconds;
            PlayerPosition = playerWorldPos;
        }

        public void Draw(Matrix worldViewProjection, Texture2D depthTexture)
        {
            if (_bladeVertCount == 0) return;

            _device.DepthStencilState = DepthStencilState.None;

            _effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
            _effect.Parameters["Time"]?.SetValue(Time);
            _effect.Parameters["PlayerPos"]?.SetValue(PlayerPosition);
            _effect.Parameters["ObjectDepthTexture"]?.SetValue(depthTexture);

            _effect.Parameters["u_baseH"]?.SetValue(Settings.GlobalHeightBase * 0.15f);
            _effect.Parameters["u_baseW"]?.SetValue(Settings.BladeThickness * 0.15f);
            _effect.Parameters["u_taper"]?.SetValue(Settings.Taper);
            _effect.Parameters["u_curve"]?.SetValue(Settings.RestingCurvature);

            _effect.Parameters["u_wSpd"]?.SetValue(Settings.WindSpeed);
            _effect.Parameters["u_wInt"]?.SetValue(Settings.WindIntensity);
            _effect.Parameters["u_stiff"]?.SetValue(Settings.Stiffness);
            _effect.Parameters["u_pushRad"]?.SetValue(Settings.PlayerPushRadius);
            _effect.Parameters["u_pushStr"]?.SetValue(Settings.PlayerPushStrength);

            _effect.Parameters["u_cRoot"]?.SetValue(Settings.RootColor.ToVector3());
            _effect.Parameters["u_cTip"]?.SetValue(Settings.TipColor.ToVector3());
            _effect.Parameters["u_cFlower"]?.SetValue(Settings.FlowerColor.ToVector3());
            _effect.Parameters["u_cFlower2"]?.SetValue(Settings.FlowerColor2.ToVector3());

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
