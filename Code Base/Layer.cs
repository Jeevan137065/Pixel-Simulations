using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations.Data
{
    public enum LayerType { Tile, Object, Control,Mask}
    public abstract class Layer
    {
        public string Name { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public bool IsExpanded { get; set; } = false;
        public abstract LayerType Type { get; }
        public abstract void Shift(Vector2 delta, int cellSize);
        protected Layer(string name) { Name = name; }
        protected Layer() { } // For deserialization
    }
    public class TileLayer : Layer
    {
        public override LayerType Type => LayerType.Tile;
        public Dictionary<Point, Chunk> Chunks { get; set; }
        public TileLayer(string name) : base(name)
        {
            Chunks = new Dictionary<Point, Chunk>();
        }
        public TileLayer() : base() { } // For deserialization
        public void PlaceTile(Point globalCell, TileInfo tileInfo)
        {
            if (IsLocked) return;

            Point chunkCoord = new Point(
                (int)Math.Floor((double)globalCell.X / Chunk.CHUNK_SIZE),
                (int)Math.Floor((double)globalCell.Y / Chunk.CHUNK_SIZE)
            );
            if (!Chunks.TryGetValue(chunkCoord, out var chunk))
            {
                // If the chunk doesn't exist, create it on-demand.
                chunk = new Chunk(chunkCoord);
                Chunks[chunkCoord] = chunk;
            }
            int localX = globalCell.X - chunkCoord.X * Chunk.CHUNK_SIZE;
            int localY = globalCell.Y - chunkCoord.Y * Chunk.CHUNK_SIZE;

            chunk.PlaceTile(localX, localY, tileInfo);
        }

        /// Removes a tile from a specific grid coordinate.
        public void RemoveTile(Point globalCell)
        {
            if (IsLocked) return;
            Point chunkCoord = new Point(
                (int)Math.Floor((double)globalCell.X / Chunk.CHUNK_SIZE),
                (int)Math.Floor((double)globalCell.Y / Chunk.CHUNK_SIZE)
            );
            int localX = globalCell.X - chunkCoord.X * Chunk.CHUNK_SIZE;
            int localY = globalCell.Y - chunkCoord.Y * Chunk.CHUNK_SIZE;
            var chunk = Chunks[chunkCoord];
            chunk.RemoveTile(localX, localY);
        }

        /// Gets the TileInfo at a specific cell, if one exists.
        public TileInfo GetTileAt(Point globalCell)
        {
            if (IsLocked) return null;

            // 1. Mathematically safe floor division for negatives
            Point chunkCoord = new Point(
                (int)Math.Floor((double)globalCell.X / Chunk.CHUNK_SIZE),
                (int)Math.Floor((double)globalCell.Y / Chunk.CHUNK_SIZE)
            );

            if (Chunks.TryGetValue(chunkCoord, out var chunk))
            {
                // 2. Mathematically safe local coordinate extraction
                int localX = globalCell.X - chunkCoord.X * Chunk.CHUNK_SIZE;
                int localY = globalCell.Y - chunkCoord.Y * Chunk.CHUNK_SIZE;

                return chunk.GetTileAt(localX, localY);
            }
            return null;
        }
        public override void Shift(Vector2 delta, int cellSize)
        {
            if (IsLocked) return;

            int dX = (int)Math.Round(delta.X / cellSize);
            int dY = (int)Math.Round(delta.Y / cellSize);
            if (dX == 0 && dY == 0) return;

            var newChunks = new Dictionary<Point, Chunk>();

            foreach (var chunkKvp in Chunks)
            {
                var oldChunk = chunkKvp.Value;
                int startGlobalX = oldChunk.ChunkCoordinate.X * Chunk.CHUNK_SIZE;
                int startGlobalY = oldChunk.ChunkCoordinate.Y * Chunk.CHUNK_SIZE;

                for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                {
                    for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
                    {
                        var tile = oldChunk.Tiles[x, y];
                        if (tile != null)
                        {
                            int newGlobalX = startGlobalX + x + dX;
                            int newGlobalY = startGlobalY + y + dY;

                            Point newChunkCoord = new Point(
                                (int)Math.Floor((double)newGlobalX / Chunk.CHUNK_SIZE),
                                (int)Math.Floor((double)newGlobalY / Chunk.CHUNK_SIZE)
                            );

                            if (!newChunks.TryGetValue(newChunkCoord, out var newChunk))
                            {
                                newChunk = new Chunk(newChunkCoord);
                                newChunks[newChunkCoord] = newChunk;
                            }

                            int localX = newGlobalX - newChunkCoord.X * Chunk.CHUNK_SIZE;
                            int localY = newGlobalY - newChunkCoord.Y * Chunk.CHUNK_SIZE;
                            newChunk.Tiles[localX, localY] = tile;
                        }
                    }
                }
            }
            Chunks = newChunks; // Replace old layout with shifted layout
        }

    }
    public class ObjectLayer : Layer
    {
        public override LayerType Type => LayerType.Object;
        public List<MapObject> Objects = new List<MapObject>();
        public ObjectLayer(string name) : base(name)
        {
        }
        public override void Shift(Vector2 delta, int cellSize)
        {
            if (IsLocked) return;
            foreach (var obj in Objects) obj.Position += delta;
        }
        public ObjectLayer() : base() { }
    }
    public class ControlLayer : ObjectLayer
    {
        public override LayerType Type => LayerType.Control;
        // Unified lists for all shape types
        public List<ShapeObject> Shapes { get; set; } = new List<ShapeObject>();
        public List<RectangleObject> Rectangles { get; set; } = new List<RectangleObject>();
        public List<PointObject> Points { get; set; } = new List<PointObject>();

        public ControlLayer(string name) : base(name) {}
        public ControlLayer() : base() { }
        public override void Shift(Vector2 delta, int cellSize)
        {
            base.Shift(delta, cellSize); // Shifts standard objects
            if (IsLocked) return;

            foreach (var rect in Rectangles) rect.Position += delta;
            foreach (var pt in Points) pt.Position += delta;
            foreach (var shape in Shapes)
            {
                shape.Shape.Offset(delta);
                shape.UpdateBoundsFromVertices();
            }
        }
    }
    public class MaskLayer : Layer
    {
        public override LayerType Type => LayerType.Mask;
        // Each chunk is a 256x256 pixel texture (16x16 standard tiles)
        public const int CHUNK_PIXEL_SIZE = 256;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;
        [JsonIgnore]
        public Dictionary<Point, RenderTarget2D> Chunks { get; set; } = new Dictionary<Point, RenderTarget2D>();

        public MaskLayer(string name) : base(name) { }
        public MaskLayer() : base() { }
        public override void Shift(Vector2 delta, int cellSize)
        {
            // Shifting textures across GPU boundaries is complex, 
            // so we'll leave Mask Layer static for now.
        }
        // Helper to get or create a chunk on the GPU
        public RenderTarget2D GetOrCreateChunk(Point coord, GraphicsDevice gd)
        {
            if (!Chunks.TryGetValue(coord, out var rt))
            {
                rt = new RenderTarget2D(gd, CHUNK_PIXEL_SIZE, CHUNK_PIXEL_SIZE, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

                var prevRt = gd.GetRenderTargets();
                gd.SetRenderTarget(rt);

                // FIX: Clear to Opaque Black so PNG loading doesn't destroy RGB data!
                gd.Clear(new Color(0, 0, 0, 255));

                gd.SetRenderTargets(prevRt);
                Chunks[coord] = rt;
            }
            return rt;
        }
    }
}
