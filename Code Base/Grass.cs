using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace Pixel_Simulations
{
    public class GrassSystem
    {
        // --- Inner Structures for Organization ---

        private GraphicsDevice _device;
        private Effect _effect;
        private VertexBuffer _vertexBuffer;
        public int _bladeCount = 0;
        private Texture2D _noiseMap;

        public GrassSettings Settings;
        public Vector2 PlayerPosition;
        public float Time;

        private Color[] noiseData;
        int noiseWidth, noiseHeight;
        public GrassSystem(GraphicsDevice device, GrassSettings initialSettings)
        {
            _device = device;
            Settings = initialSettings;
        }

        public void LoadContent(ContentManager content,  int mapWidth, int mapHeight)
        {
            _effect = content.Load<Effect>("grass");
            _noiseMap = content.Load<Texture2D>("noise");
            LoadNoiseMap(_noiseMap);
            InitializeField(mapWidth, mapHeight);
        }

        private void InitializeField(int mapWidth, int mapHeight)
        {
            List<BladeData> blades = new List<BladeData>();
            Random rand = new Random();

            // We use a fixed step but vary spawn chance based on settings
            float step = Settings.DensityStep;

            for (float x = 0; x < mapWidth; x += step)
            {
                for (float y = 0; y < mapHeight; y += step)
                {
                    float noise = GetNoiseValue(x, y);

                    // TIER 0: Empty
                    if (noise < Settings.MinThreshold) continue;

                    int spawnCount = 0;
                    float heightMod = 1.0f;

                    // TIER 1: Sparse
                    if (noise < Settings.MidThreshold)
                    {
                        if (rand.NextDouble() < Settings.SparseDensity)
                        {
                            spawnCount = 1;
                            heightMod = 0.2f; // Young/Short grass
                        }
                    }
                    // TIER 2: Mid
                    else if (noise < Settings.MaxThreshold)
                    {
                        spawnCount = 1;
                        heightMod = 0.6f;
                    }
                    // TIER 3: Lush
                    else
                    {
                        spawnCount = 2;
                        heightMod = 1.0f; // Overgrown
                    }

                    for (int i = 0; i < spawnCount; i++)
                    {
                        float jitter = step;
                        blades.Add(new BladeData
                        {
                            Pos = new Vector2(x + (float)(rand.NextDouble() - 0.5) * jitter,
                                            y + (float)(rand.NextDouble() - 0.5) * jitter),
                            Wind = (float)rand.NextDouble() * 10f,
                            Height = Settings.GlobalHeightBase * heightMod,
                            Var = (float)rand.NextDouble(),
                            Lean = (float)(rand.NextDouble() - 0.5) * Settings.RestingCurvature * 2.0f
                        });
                    }
                }
            }

            // Sort by Y for proper 2D Depth
            blades.Sort((a, b) => a.Pos.Y.CompareTo(b.Pos.Y));

            var vertices = new List<GrassVertex>();
            foreach (var b in blades)
            {
                AddBlade(vertices, b);
            }

            _bladeCount = blades.Count;
            _vertexBuffer = new VertexBuffer(_device, GrassVertex.VertexDeclaration, vertices.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(vertices.ToArray());
        }

        private void AddBlade(List<GrassVertex> vertices, BladeData b)
        {
            Random rand = new Random((int)(b.Pos.X * b.Pos.Y));
            int tuftSize = Settings.TuftSize;
            int segCount = Settings.Segments;

            for (int n = 0; n < tuftSize; n++)
            {
                // Unique personality for each blade in the clump
                float individualLean = b.Lean + (float)(rand.NextDouble() - 0.5) * Settings.TuftSpread;
                float individualHeight = b.Height * (0.8f + (float)rand.NextDouble() * 0.4f);
                float individualWind = b.Wind + (float)rand.NextDouble();
                float individualVar = (float)rand.NextDouble();

                for (int i = 0; i < segCount; i++)
                {
                    float t0 = i / (float)segCount;
                    float t1 = (i + 1) / (float)segCount;

                    // Goal C: Smoothly transition colors between segments
                    Color c0 = Color.Lerp(Settings.RootColor, Settings.TipColor, t0);
                    Color c1 = Color.Lerp(Settings.RootColor, Settings.TipColor, t1);

                    AddSegmentVertices(vertices, b.Pos, t0, t1, individualWind, individualHeight, individualVar, individualLean, c0, c1);
                }
            }
        }
        private void AddSegmentVertices(List<GrassVertex> verts, Vector2 root, float t0, float t1, float wind, float height, float var, float lean, Color c0, Color c1)
        {
            // A segment is a quad made of two triangles (6 vertices)
            // Triangle 1
            verts.Add(new GrassVertex(root, t0, -1.0f, wind, height, var, lean, c0));
            verts.Add(new GrassVertex(root, t1, -1.0f, wind, height, var, lean, c1));
            verts.Add(new GrassVertex(root, t0, 1.0f, wind, height, var, lean, c0));

            // Triangle 2
            verts.Add(new GrassVertex(root, t0, 1.0f, wind, height, var, lean, c0));
            verts.Add(new GrassVertex(root, t1, -1.0f, wind, height, var, lean, c1));
            verts.Add(new GrassVertex(root, t1, 1.0f, wind, height, var, lean, c1));
        }
        void LoadNoiseMap(Texture2D noiseTexture)
        {
            noiseWidth = noiseTexture.Width;
            noiseHeight = noiseTexture.Height;
            noiseData = new Color[noiseWidth * noiseHeight];
            noiseTexture.GetData(noiseData);
        }

        float GetNoiseValue(float x, float y)
        {
            // Scale world coords to noise texture coords (tiling)
            int nx = (int)(x / 2.0f) % noiseWidth;
            int ny = (int)(y / 2.0f) % noiseHeight;
            if (nx < 0) nx += noiseWidth;
            if (ny < 0) ny += noiseHeight;

            return noiseData[nx + ny * noiseWidth].R / 255.0f;
        }


        public void Update(GameTime gameTime, Vector2 playerWorldPos)
        {
            Time = (float)gameTime.TotalGameTime.TotalSeconds;
            PlayerPosition = playerWorldPos;
        }

        public void Draw(Matrix worldViewProjection)
        {
            _device.SetVertexBuffer(_vertexBuffer);
            _device.DepthStencilState = DepthStencilState.None;

            // 1. Core Transform & Time
            _effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
            _effect.Parameters["Time"]?.SetValue(Time);
            _effect.Parameters["PlayerPos"]?.SetValue(PlayerPosition);

            // 2. Physics Parameters from Settings
            _effect.Parameters["WindSpeed"]?.SetValue(Settings.WindSpeed);
            _effect.Parameters["WindIntensity"]?.SetValue(Settings.WindIntensity);
            _effect.Parameters["Stiffness"]?.SetValue(Settings.Stiffness);
            _effect.Parameters["PlayerPushRadius"]?.SetValue(Settings.PlayerPushRadius);
            _effect.Parameters["PlayerPushStrength"]?.SetValue(Settings.PlayerPushStrength);

            // 3. Artistic Volume Parameters (The "Look")
            _effect.Parameters["Segments"]?.SetValue((float)Settings.Segments);
            _effect.Parameters["RestingCurvature"]?.SetValue(Settings.RestingCurvature);
            _effect.Parameters["BladeThickness"]?.SetValue(Settings.BladeThickness);
            _effect.Parameters["BladeTaper"]?.SetValue(Settings.BladeTaper);
            // Pass 0: Shadows
            //_effect.Parameters["PassFlag"].SetValue(0.0f);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawPrimitives(PrimitiveType.TriangleList, 0, _vertexBuffer.VertexCount / 3);
            }

            // Pass 1: Blades
            //_effect.Parameters["PassFlag"].SetValue(1.0f);
            
        }
    }
}
