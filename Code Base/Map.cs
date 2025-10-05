using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pixel_Simulations
{
    public enum TilesetType
    {
        Terrain, // 100-piece organic set
        Normal,  // 16-piece structured set
        Object,  // Chunk/prefab set
        Animated // Future use
    }
    public class Map
    {
        public int WidthInCells { get; private set; }
        public int HeightInCells { get; private set; }
        public List<Layer> Layers { get; private set; }

        public Map(int width, int height)
        {
            WidthInCells = width;
            HeightInCells = height;
            Layers = new List<Layer>();

            // Create a default ground layer to start with
            Layers.Add(new TileLayer("Ground"));
        }

        public void AddLayerAbove(int index)
        {
            // Insert at index + 1 to place it "above" the current one in the list.
            string name = $"Layer {Layers.Count + 1}";
            Layers.Insert(index + 1, new TileLayer(name));
        }

        public void AddLayerBelow(int index)
        {
            string name = $"Layer {Layers.Count + 1}";
            Layers.Insert(index, new TileLayer(name));
        }

        public void DeleteLayer(int index)
        {
            // Safety check: always keep at least one layer
            if (Layers.Count > 1 && index >= 0 && index < Layers.Count)
            {
                Layers.RemoveAt(index);
            }
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0 && index < Layers.Count)
            {
                // Simple swap with the layer above
                (Layers[index], Layers[index - 1]) = (Layers[index - 1], Layers[index]);
            }
        }

        public void MoveLayerDown(int index)
        {
            if (index >= 0 && index < Layers.Count - 1)
            {
                // Simple swap with the layer below
                (Layers[index], Layers[index + 1]) = (Layers[index + 1], Layers[index]);
            }
        }

    }

    public abstract class Layer
    {
        public string Name { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;

        protected Layer(string name)
        {
            Name = name;
        }
    }

    public class TileLayer : Layer
    {
        // Stores tile data sparsely. Only grid cells that have a tile are in the dictionary.
        public Dictionary<Point, TileInfo> Grid { get; private set; }

        public TileLayer(string name) : base(name)
        {
            Grid = new Dictionary<Point, TileInfo>();
        }

        public void PlaceTile(Point cell, TileInfo tileInfo)
        {
            if (IsLocked) return;
            Grid[cell] = tileInfo;
        }

        public void RemoveTile(Point cell)
        {
            if (IsLocked) return;
            if (Grid.ContainsKey(cell))
            {
                Grid.Remove(cell);
            }
        }
    }

    public class TileSet
    {
        public string Name { get; private set; }
        public TilesetType Type { get; private set; }
        public Dictionary<int, Texture2D> Atlas { get; private set; }
        public int TileSize { get; private set; }

        public TileSet(string name, TilesetType type, Texture2D sourceTexture, int tileSize, GraphicsDevice graphicsDevice, bool sliceVertically = false)
        {
            Name = name;
            Type = type;
            TileSize = tileSize;
            Atlas = new Dictionary<int, Texture2D>();
            SliceFromTexture(sourceTexture, graphicsDevice, sliceVertically);
        }

        private void SliceFromTexture(Texture2D sourceTexture, GraphicsDevice graphicsDevice, bool sliceVertically)
        {
            int id = 0;
            int columns = sourceTexture.Width / TileSize;
            int rows = sourceTexture.Height / TileSize;

            if (sliceVertically)
            {
                // *** SLICE COLUMN-FIRST ***
                // Outer loop iterates through columns (x), inner loop through rows (y).
                for (int x = 0; x < columns; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        ProcessTile(x, y, id++, sourceTexture, graphicsDevice);
                    }
                }
            }
            else
            {
                // *** SLICE ROW-FIRST (Default) ***
                // Outer loop iterates through rows (y), inner loop through columns (x).
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

            if (data.All(c => c.A == 0)) return;

            var tileTexture = new Texture2D(graphicsDevice, TileSize, TileSize);
            tileTexture.SetData(data);
            Atlas[id] = tileTexture;
        }

        public Texture2D GetTileTexture(int tileId)
        {
            return Atlas.TryGetValue(tileId, out var texture) ? texture : null;
        }
    }
    public struct TileInfo
    {
        // The unique name of the tileset this tile belongs to (e.g., "NaturalLandmass")
        public string TilesetName;

        // The local ID of the tile within its own tileset (e.g., 42)
        public int TileID;

        public TileInfo(string tilesetName, int tileId)
        {
            TilesetName = tilesetName;
            TileID = tileId;
        }
    }

    public class TilesetManager
    {
        // Stores all loaded tilesets, keyed by their unique name.
        public Dictionary<string, TileSet> TileSets { get; private set; }

        public TilesetManager()
        {
            TileSets = new Dictionary<string, TileSet>();
        }

        // Example method for loading. In a real app, this would scan folders.
        public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
        {
            // For now, we'll manually load our two types of tilesets.
            var terrainAtlas = content.Load<Texture2D>("TerrainTiles"); // Your 100-piece atlas
            var normalAtlas = content.Load<Texture2D>("NormalTiles");   // A standard 16-tile atlas

            var terrainSet = new TileSet("Terrain", TilesetType.Terrain, terrainAtlas, 16, graphicsDevice, sliceVertically: false);
            var normalSet = new TileSet("Normal", TilesetType.Normal, normalAtlas, 16, graphicsDevice);

            TileSets[terrainSet.Name] = terrainSet;
            TileSets[normalSet.Name] = normalSet;
        }

        public TileSet GetTileset(string name)
        {
            return TileSets.TryGetValue(name, out var tileset) ? tileset : null;
        }

        public Texture2D GetTileTexture(TileInfo tileInfo)
        {
            if (TileSets.TryGetValue(tileInfo.TilesetName, out var tileset))
            {
                return tileset.GetTileTexture(tileInfo.TileID);
            }
            return null;
        }
    }

}

