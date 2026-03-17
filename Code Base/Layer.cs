using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations.Data
{
    public enum LayerType { Tile, Object, Control }
    public abstract class Layer
    {
        public string Name { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public bool IsExpanded { get; set; } = false;
        public abstract LayerType Type { get; }

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
            Point chunkCoord = new Point(globalCell.X / Chunk.CHUNK_SIZE, globalCell.Y / Chunk.CHUNK_SIZE);
            if (Chunks.TryGetValue(chunkCoord, out var chunk))
            {
                int localX = globalCell.X % Chunk.CHUNK_SIZE;
                int localY = globalCell.Y % Chunk.CHUNK_SIZE;
                return chunk.GetTileAt(localX, localY);
            }
            return null;
        }

    }
    public class ObjectLayer : Layer
    {
        public override LayerType Type => LayerType.Object;
        public List<MapObject> Objects = new List<MapObject>();
        public ObjectLayer(string name) : base(name)
        {
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
    }

}
