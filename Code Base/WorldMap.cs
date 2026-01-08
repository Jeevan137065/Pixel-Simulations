using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Timers;
using Pixel_Simulations.Code_Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixel_Simulations
{
    

    public enum TileType { Grass, Dirt, Tilled }

    public struct Tile
    {
        public TileType Type;
        public int Variant;
        public Rectangle SourceRect;
    }
    public class WorldMap
    {
        // Textures
        private Texture2D _grassTilesTexture1, _grassTilesTexture2;
        private readonly Rectangle[] _grassSources = new Rectangle[8];
        private readonly Rectangle[] _dirtSources = new Rectangle[2];
        private readonly Rectangle[] _tilledSources = new Rectangle[2];
        private Texture2D _grassBladeTexture, _grassBladeNormal;
        // Data
        private Tile[,] _tileGrid;
        private readonly Random _random = new();
        GraphicsDevice _graphics;
        // World Properties & Child Systems
        public int _worldWidth, _worldHeight, _tileSize;
        private readonly int _worldWidthInTiles, _worldHeightInTiles;
        public CropManager CropManager { get; } // The WorldMap OWNS the CropManager
        public List<OldGrass> Grasses { get; } = new();

        public WorldMap(int worldWidth, int worldHeight, int tileSize, GraphicsDevice graphics)
        {
            _worldWidth = worldWidth; _worldHeight = worldHeight; _tileSize = tileSize;
            _worldWidthInTiles = worldWidth / tileSize; _worldHeightInTiles = worldHeight / tileSize;
            _graphics = graphics;

            // Create the child manager and give it a reference to this WorldMap
            CropManager = new CropManager(this, _graphics);
        }

        public void LoadContent(ContentManager content)
        {
            // Load textures
            _grassTilesTexture1 = content.Load<Texture2D>("Grass1");
            _grassTilesTexture2 = content.Load<Texture2D>("Grass2");
            _grassBladeTexture = content.Load<Texture2D>("GrassBlade");
            _grassBladeNormal = content.Load<Texture2D>("GrassBlade_n");
            // Pre-calculate source rectangles
            for (int i = 0; i < 2; i++) for (int j = 0; j < 4; j++)
                { _grassSources[i * 4 + j] = new Rectangle(i * 16, (j) * 16, 16, 16); }
            _dirtSources[0] = new Rectangle(0, 4 * 16, 16, 16);
            _dirtSources[1] = new Rectangle(16, 4 * 16, 16, 16);
            _tilledSources[0] = new Rectangle(0, 5 * 16, 16, 16); // Alone
            _tilledSources[1] = new Rectangle(16, 5 * 16, 16, 16);
            //CropManager.LoadContent(content);


            GenerateWorld();
        }


        private void GenerateWorld()
        {
            _tileGrid = new Tile[_worldWidthInTiles, _worldHeightInTiles];
            const float noiseScale = 0.1f;


            for (int y = 0; y < _worldHeightInTiles; y++)
            {
                for (int x = 0; x < _worldWidthInTiles; x++)
                {
                    // Place ground tile everywhere
                    double noiseValue = Perlin.Noise(x * noiseScale, y * noiseScale);
                    int greennessIndex = Math.Min(3, (int)(noiseValue * 4));
                    _tileGrid[x, y].Type = TileType.Grass;
                    _tileGrid[x, y].Variant = _random.Next(0, 2); // Variety, not greenness
                    UpdateTileSourceRect(x, y);

                    if (_tileGrid[x, y].Type == TileType.Grass)
                    {
                        // 30% chance to spawn grass on a grass tile
                        if (_random.NextDouble() < 0.70)
                        {
                            var tilePos = new Vector2(x * _tileSize, y * _tileSize);
                            // Create the grass object
                            var grass = new OldGrass(tilePos, _grassBladeTexture, _grassBladeNormal, _graphics, _random);
                            Grasses.Add(grass);
                        }
                    }
                }
            }
        }

        private void UpdateTileSourceRect(int x, int y)
        {
            if (x < 0 || x >= _worldWidthInTiles || y < 0 || y >= _worldHeightInTiles) return;

            switch (_tileGrid[x, y].Type)
            {
                case TileType.Grass:
                    double noiseValue = Perlin.Noise(x * 0.1f, y * 0.1f);
                    int greenness = Math.Min(3, (int)(noiseValue * 4));
                    _tileGrid[x, y].SourceRect = _grassSources[_tileGrid[x, y].Variant * 4 + greenness];
                    break;
                case TileType.Dirt:
                    _tileGrid[x, y].SourceRect = _dirtSources[_tileGrid[x, y].Variant];
                    break;
                case TileType.Tilled:
                    bool leftIsTilled = (x > 0 && _tileGrid[x - 1, y].Type == TileType.Tilled);
                    bool rightIsTilled = (x < _worldWidthInTiles - 1 && _tileGrid[x + 1, y].Type == TileType.Tilled);
                    int tilledVariant = (leftIsTilled || rightIsTilled) ? 1 : 0;
                    _tileGrid[x, y].SourceRect = _tilledSources[tilledVariant];
                    break;
            }
        }

        public void DrawGround(SpriteBatch spriteBatch, Rectangle cameraView, bool rain)
        {
            int startX = Math.Max(0, cameraView.X / _tileSize);
            int startY = Math.Max(0, cameraView.Y / _tileSize);
            int endX = Math.Min(_worldWidthInTiles - 1, (cameraView.Right / _tileSize) + 1);
            int endY = Math.Min(_worldHeightInTiles - 1, (cameraView.Bottom / _tileSize) + 1);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var destRect = new Rectangle(x * _tileSize, y * _tileSize, _tileSize, _tileSize);
                    if (rain) { spriteBatch.Draw(_grassTilesTexture1, destRect, _tileGrid[x, y].SourceRect, Color.White); }
                    else { spriteBatch.Draw(_grassTilesTexture2, destRect, _tileGrid[x, y].SourceRect, Color.White); }
                    
                }
            }
        }
        public void DrawGrass( Matrix SimulationMatrix, GameTime gameTime)
        {

        }

        public void DigTile(int x, int y)
        {
            if (x < 0 || x >= _worldWidthInTiles || y < 0 || y >= _worldHeightInTiles) return;
            if (_tileGrid[x, y].Type == TileType.Grass)
            {
                _tileGrid[x, y].Type = TileType.Dirt;
                _tileGrid[x, y].Variant = _random.Next(0, 2);
                UpdateTileSourceRect(x, y);

            }
        }

        public void TillTile(int x, int y)
        {
            if (x < 0 || x >= _worldWidthInTiles || y < 0 || y >= _worldHeightInTiles) return;
            if (_tileGrid[x, y].Type == TileType.Dirt)
            {
                _tileGrid[x, y].Type = TileType.Tilled;
                _tileGrid[x, y].Variant = 0;
                UpdateTileSourceRect(x, y);

                // Update neighbors to connect to this new tilled tile
                UpdateTileSourceRect(x - 1, y);
                UpdateTileSourceRect(x + 1, y);
            }
        }

        public TileType GetTileType(int x, int y)
        {
            if (x < 0 || x >= _worldWidthInTiles || y < 0 || y >= _worldHeightInTiles)
            {
                return TileType.Grass; // Return a default, non-plantable type for out-of-bounds
            }
            return _tileGrid[x, y].Type;
        }

    }
}