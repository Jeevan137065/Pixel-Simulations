using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TileInfo : IEquatable<TileInfo>
    {
        [JsonProperty]
        public string TilesetName { get; private set; }

        [JsonProperty]
        public int TileID { get; private set; }
        [JsonProperty]
        public byte Rotation { get; set; } // 0=0, 1=90, 2=180, 3=270

        // Parameterless constructor for JSON deserialization
        private TileInfo() { }

        public TileInfo(string tilesetName, int tileId, byte rotation = 0)
        {
            TilesetName = tilesetName;
            TileID = tileId;
            Rotation = rotation;
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

    public class TilesetManager
    {
        private readonly Dictionary<string, TileSet> _tileSets;

        public TilesetManager()
        {
            _tileSets = new Dictionary<string, TileSet>();
        }


        /// Registers a new TileSet instance with the manager.
        public void RegisterTileSet(TileSet tileSet)
        {
            if (tileSet != null && !_tileSets.ContainsKey(tileSet.Name))
            {
                _tileSets[tileSet.Name] = tileSet;
            }
        }

        /// Clears all registered tilesets. Useful when loading a new map/project.
        public void Clear()
        {
            _tileSets.Clear();
        }

        /// The primary function: gets the correct Texture2D for a given TileInfo.
        public Texture2D GetTileTexture(TileInfo tileInfo)
        {
            if (tileInfo == null) return null;

            if (_tileSets.TryGetValue(tileInfo.TilesetName, out var tileSet))
            {
                return tileSet.GetTileTexture(tileInfo.TileID);
            }

            // Return null (or a default "missing" texture) if the tileset isn't found.
            return null;
        }
    }
}
