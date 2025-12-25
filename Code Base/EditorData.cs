using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Data
{
    public enum LayerType { Tile, Object, Collision, Pathing }
    public enum ObjectType { Prop, Rectangle, Point }
    public enum SliceMode { RowFirst, ColumnFirst }
    [JsonObject(MemberSerialization.OptIn)]
    public class TileInfo : IEquatable<TileInfo>
    {
        [JsonProperty]
        public string TilesetName { get; private set; }

        [JsonProperty]
        public int TileID { get; private set; }

        // Parameterless constructor for JSON deserialization
        private TileInfo() { }

        public TileInfo(string tilesetName, int tileId)
        {
            TilesetName = tilesetName;
            TileID = tileId;
        }

        // --- IEquatable Implementation for correct comparisons ---

        public bool Equals(TileInfo other)
        {
            if (other is null) return false;
            // Two TileInfo objects are the same if they come from the same tileset and have the same ID.
            return TilesetName == other.TilesetName && TileID == other.TileID;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TileInfo);
        }

        public override int GetHashCode()
        {
            // A good hash code combines the hash codes of its members.
            return HashCode.Combine(TilesetName, TileID);
        }

        public static bool operator ==(TileInfo left, TileInfo right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(TileInfo left, TileInfo right)
        {
            return !(left == right);
        }
    }

    public class TileSet
    {
        public string Name { get; }
        public int TileSize { get; }
        public SliceMode SlicingMode { get; }

        // The sliced textures, indexed by a simple integer ID (0, 1, 2...).
        public IReadOnlyDictionary<int, Texture2D> SlicedAtlas => _slicedAtlas;
        private readonly Dictionary<int, Texture2D> _slicedAtlas;

        public TileSet(string name, Texture2D sourceTexture, int tileSize, GraphicsDevice graphicsDevice, SliceMode sliceMode = SliceMode.RowFirst)
        {
            Name = name;
            TileSize = tileSize;
            SlicingMode = sliceMode;
            _slicedAtlas = new Dictionary<int, Texture2D>();

            SliceFromTexture(sourceTexture, graphicsDevice);
        }

        private void SliceFromTexture(Texture2D sourceTexture, GraphicsDevice graphicsDevice)
        {
            int id = 0;
            int columns = sourceTexture.Width / TileSize;
            int rows = sourceTexture.Height / TileSize;

            if (SlicingMode == SliceMode.ColumnFirst)
            {
                for (int x = 0; x < columns; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        ProcessTile(x, y, id++, sourceTexture, graphicsDevice);
                    }
                }
            }
            else // RowFirst
            {
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        ProcessTile(x, y, id++, sourceTexture, graphicsDevice);
                    }
                }
            }
        }

        private void ProcessTile(int x, int y, int id, Texture2D sourceTexture, GraphicsDevice graphicsDevice)
        {
            var rect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
            var data = new Color[TileSize * TileSize];
            sourceTexture.GetData(0, rect, data, 0, data.Length);

            if (data.All(c => c.A == 0)) return; // Skip empty tiles

            var tileTexture = new Texture2D(graphicsDevice, TileSize, TileSize);
            tileTexture.SetData(data);
            _slicedAtlas[id] = tileTexture;
        }

        public Texture2D GetTileTexture(int tileId)
        {
            return _slicedAtlas.TryGetValue(tileId, out var texture) ? texture : null;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Layer
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public bool IsVisible { get; set; } = true;

        [JsonProperty]
        public bool IsLocked { get; set; } = false;

        public abstract LayerType Type { get; }

        protected Layer(string name) { Name = name; }
        protected Layer() { } // For deserialization
    }

    public class TileLayer : Layer
    {
        public override LayerType Type => LayerType.Tile;

        [JsonProperty]
        public Dictionary<Point, TileInfo> Grid { get; set; }

        public TileLayer(string name) : base(name)
        {
            Grid = new Dictionary<Point, TileInfo>();
        }
        public TileLayer() : base() { } // For deserialization

        /// <summary>
        /// Places or replaces a tile at a specific grid coordinate.
        /// </summary>
        public void PlaceTile(Point cell, TileInfo tileInfo)
        {
            if (IsLocked) return;
            Grid[cell] = tileInfo;
        }

        /// <summary>
        /// Removes a tile from a specific grid coordinate.
        /// </summary>
        public void RemoveTile(Point cell)
        {
            if (IsLocked) return;
            Grid.Remove(cell);
        }

        /// <summary>
        /// Gets the TileInfo at a specific cell, if one exists.
        /// </summary>
        public TileInfo GetTileAt(Point cell)
        {
            return Grid.TryGetValue(cell, out var tileInfo) ? tileInfo : null;
        }
    }

    // Placeholder for future implementation
    public class ObjectLayer : Layer
    {
        public override LayerType Type => LayerType.Object;
        public ObjectLayer(string name) : base(name) { }
        public ObjectLayer() : base() { }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Map
    {
        [JsonProperty]
        public int WidthInCells { get; private set; }

        [JsonProperty]
        public int HeightInCells { get; private set; }

        [JsonProperty]
        public List<Layer> Layers { get; set; }

        public Map(int width, int height)
        {
            WidthInCells = width;
            HeightInCells = height;
            Layers = new List<Layer>();
            // Every new map starts with a default, unlocked Ground layer.
            Layers.Add(new TileLayer("Ground"));
        }

        // Json.NET requires a parameterless constructor for deserialization
        public Map() { }

        // Methods for manipulating layers. The EditorController will call these.
        public void AddLayerAbove(int index, Layer newLayer)
        {
            if (index < Layers.Count - 1)
                Layers.Insert(index + 1, newLayer);
            else
                Layers.Add(newLayer);
        }

        public void AddLayerBelow(int index, Layer newLayer)
        {
            if (index < Layers.Count - 1)
                Layers.Insert(index - 1, newLayer);
            else
                Layers.Add(newLayer);
        }

        public void DeleteLayer(int index)
        {
            if (Layers.Count > 1 && index >= 0 && index < Layers.Count)
                Layers.RemoveAt(index);
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0)
                (Layers[index], Layers[index - 1]) = (Layers[index - 1], Layers[index]);
        }

        public void MoveLayerDown(int index)
        {
            if (index < Layers.Count - 1)
                (Layers[index], Layers[index + 1]) = (Layers[index + 1], Layers[index]);
        }
    }

    public abstract class MapObject
    {
        public Vector2 Position { get; set; }
        public abstract ObjectType Type { get; }
    }

    public class PropObject : MapObject
    {
        public override ObjectType Type => ObjectType.Prop;
        public string AssetName { get; set; }
    }

    public class RectangleObject : MapObject
    {
        public override ObjectType Type => ObjectType.Rectangle;
        public Vector2 Size { get; set; }
        public string Tag { get; set; } // e.g., "Collision", "Trigger"
    }

}