using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public abstract class Layer
    {
        public string Name { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsLocked { get; set; } = false;
        public abstract LayerType Type { get; }

        protected Layer(string name)
        {
            Name = name;
        }
    }

    public class TileLayer : Layer
    {
        // Stores tile data sparsely. Only grid cells that have a tile are in the dictionary.
        public Dictionary<Point, TileInfo> Grid { get; set; }

        public override LayerType Type => LayerType.Tile;
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

    public class ObjectLayer : Layer
    {
        public List<MapObject> Objects { get; set; } // <-- Change to public 'set'

        public override LayerType Type => LayerType.Tile;
        public ObjectLayer(string name) : base(name)
        {
            Objects = new List<MapObject>();
        }
    }
}
