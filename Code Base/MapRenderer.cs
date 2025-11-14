using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public class MapRenderer
    {
        private Map _map;
        private TilesetManager _tilesetManager;
        private int _cellSize;

        public MapRenderer(Map map, TilesetManager tilesetManager, int cellSize)
        {
            _map = map;
            _tilesetManager = tilesetManager;
            _cellSize = cellSize;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // We can add logic here to only draw visible layers later
            foreach (var layer in _map.Layers)
            {
                if (layer is TileLayer tileLayer)
                {
                    DrawTileLayer(spriteBatch, tileLayer);
                }
            }

        }

        private void DrawTileLayer(SpriteBatch spriteBatch, TileLayer layer)
        {
            // In the future, we will add culling to only draw tiles visible by the camera.
            foreach (var tileEntry in layer.Grid)
            {
                var texture = _tilesetManager.GetTileTexture(tileEntry.Value);
                if (texture != null)
                {
                    var position = new Vector2(tileEntry.Key.X * _cellSize, tileEntry.Key.Y * _cellSize);
                    spriteBatch.Draw(texture, position, Color.White);
                }
            }
        }



    }


}