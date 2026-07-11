// File: WaterManager.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pixel_Simulations
{
    public class WaterManager : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly RenderPipeline _pipeline;

        private float[] _waterGrid;
        private float[] _terrainGrid;
        private int _gridWidth;
        private int _gridHeight;
        private bool _isSimulating = false;

        public float WaterLevel { get; set; } = 130f;
        public float InflowVolume { get; set; } = 15f;
        public float SeepageDecayRate { get; set; } = 0.0f;
        public float SimulationSpeed { get; set; } = 2.0f;
        public float FoamWidth { get; set; } = 1.5f;

        private readonly Dictionary<Point, byte[]> _terrainElevationCache = new Dictionary<Point, byte[]>();
        private readonly Dictionary<Point, byte[]> _waterDepthCache = new Dictionary<Point, byte[]>();

        public bool IsSimulating => _isSimulating;

        public WaterManager(GraphicsDevice graphicsDevice, RenderPipeline renderPipeline)
        {
            _graphicsDevice = graphicsDevice;
            _pipeline = renderPipeline;
        }

        public void LoadContent(ContentManager content) { }

        // --- STABLE CONTROL HOOKS ---
        public void ToggleSimulation(bool run, GraphicsDevice gd, EditorUpscaler upscaler, Map activeMap)
        {
            _isSimulating = run;
            if (_isSimulating && activeMap != null)
            {
                var maskLayer = activeMap.Layers.OfType<MaskLayer>().FirstOrDefault();
                if (maskLayer != null)
                {
                    InitializeSimulationGrids(maskLayer);
                }
            }
        }

        public void UpdateGPUSimulation(EditorUpscaler upscaler, Map activeMap, MaskLayer mask)
        {
            // Stable stub for the future shader-based solver passes
        }

        public void SaveBakedWaterMap(string path, MaskLayer mask, EditorUpscaler upscaler)
        {
            if (mask == null) return;

            // Generate clean, placeholder water data png to prevent runtime load exceptions
            int width = MaskLayer.CHUNK_PIXEL_SIZE;
            int height = MaskLayer.CHUNK_PIXEL_SIZE;

            using (var rt = new RenderTarget2D(_graphicsDevice, width, height))
            {
                _graphicsDevice.SetRenderTarget(rt);
                _graphicsDevice.Clear(Color.Transparent);
                _graphicsDevice.SetRenderTarget(null);

                using (var stream = File.OpenWrite(path))
                {
                    rt.SaveAsPng(stream, width, height);
                }
            }
            System.Diagnostics.Debug.WriteLine($"[Bake] Placeholder water map saved to: {path}");
        }

        public void InitializeSimulationGrids(MaskLayer mask)
        {
            if (mask == null || mask.Chunks.Count == 0) return;

            int minChunkX = mask.Chunks.Keys.Min(p => p.X);
            int minChunkY = mask.Chunks.Keys.Min(p => p.Y);
            int maxChunkX = mask.Chunks.Keys.Max(p => p.X);
            int maxChunkY = mask.Chunks.Keys.Max(p => p.Y);

            _gridWidth = (maxChunkX - minChunkX + 1) * MaskLayer.CHUNK_PIXEL_SIZE;
            _gridHeight = (maxChunkY - minChunkY + 1) * MaskLayer.CHUNK_PIXEL_SIZE;

            _waterGrid = new float[_gridWidth * _gridHeight];
            _terrainGrid = new float[_gridWidth * _gridHeight];

            for (int y = 0; y < _gridHeight; y++)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    int globalX = x + minChunkX * MaskLayer.CHUNK_PIXEL_SIZE;
                    int globalY = y + minChunkY * MaskLayer.CHUNK_PIXEL_SIZE;
                    _terrainGrid[y * _gridWidth + x] = GetTerrainHeightAt(new Vector2(globalX, globalY)) * 255f;
                }
            }
        }

        public void CacheWaterDepthData(Dictionary<Point, Texture2D> waterChunks)
        {
            _waterDepthCache.Clear();
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;

            foreach (var kvp in waterChunks)
            {
                Color[] colors = new Color[chunkSize * chunkSize];
                kvp.Value.GetData(colors);

                byte[] depthBytes = new byte[chunkSize * chunkSize];
                for (int i = 0; i < colors.Length; i++)
                {
                    depthBytes[i] = colors[i].B;
                }

                _waterDepthCache[kvp.Key] = depthBytes;
            }
        }

        public float GetWaterDepthAt(Vector2 worldPos, Vector2 maskOffset)
        {
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;
            float localX = worldPos.X - (maskOffset.X * chunkSize);
            float localY = worldPos.Y - (maskOffset.Y * chunkSize);

            int chunkX = (int)Math.Floor(localX / chunkSize);
            int chunkY = (int)Math.Floor(localY / chunkSize);
            Point chunkCoord = new Point(chunkX, chunkY);

            if (_waterDepthCache.TryGetValue(chunkCoord, out var bytes))
            {
                int lx = (int)(localX - chunkX * chunkSize);
                int ly = (int)(localY - chunkY * chunkSize);

                if (lx >= 0 && lx < chunkSize && ly >= 0 && ly < chunkSize)
                {
                    return bytes[ly * chunkSize + lx] / 255f;
                }
            }
            return 0f;
        }

        public float GetTerrainHeightAt(Vector2 worldPos)
        {
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;
            int chunkX = (int)Math.Floor(worldPos.X / chunkSize);
            int chunkY = (int)Math.Floor(worldPos.Y / chunkSize);
            Point chunkCoord = new Point(chunkX, chunkY);

            if (_terrainElevationCache.TryGetValue(chunkCoord, out var bytes))
            {
                int lx = (int)(worldPos.X - chunkX * chunkSize);
                int ly = (int)(worldPos.Y - chunkY * chunkSize);

                if (lx >= 0 && lx < chunkSize && ly >= 0 && ly < chunkSize)
                {
                    return bytes[ly * chunkSize + lx] / 255f;
                }
            }
            return 1.0f;
        }

        public void CacheTerrainElevationData(Dictionary<Point, Texture2D> maskChunks)
        {
            _terrainElevationCache.Clear();
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;

            foreach (var kvp in maskChunks)
            {
                Color[] colors = new Color[chunkSize * chunkSize];
                kvp.Value.GetData(colors);

                byte[] elevBytes = new byte[chunkSize * chunkSize];
                for (int i = 0; i < colors.Length; i++)
                {
                    elevBytes[i] = colors[i].B;
                }
                _terrainElevationCache[kvp.Key] = elevBytes;
            }
        }

        public void DrawSimpleWater(GraphicsDevice gd, Effect simpleWaterShader) { }

        public void Dispose() { }
    }
}