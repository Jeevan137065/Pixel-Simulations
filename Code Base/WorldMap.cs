using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
        private Texture2D _grassTilesTexture;
        private Texture2D _cropsGrowthTexture;
        // Source Rectangles
        private readonly Rectangle[] _grassSources = new Rectangle[8]; // 2 varieties * 4 shades
        private readonly Rectangle[] _dirtSources = new Rectangle[2];
        private readonly Rectangle[] _tilledSources = new Rectangle[2];

        // Map Data
        private Tile[,] _tileGrid;
        private bool[,] _isTileOccupied;

        // Public lists of all dynamic, renderable objects on the map
        public List<Crop> Crops { get; } = new();
        public Dictionary<Tool, CropData> CropData { get; private set; }

        // World Properties
        private readonly int _worldWidth, _worldHeight, _tileSize;
        private readonly int _worldWidthInTiles, _worldHeightInTiles;
        private readonly Random _random = new();

        public WorldMap(int worldWidth, int worldHeight, int tileSize)
        {
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _tileSize = tileSize;
            _worldWidthInTiles = worldWidth / tileSize;
            _worldHeightInTiles = worldHeight / tileSize;
        }

        public void LoadContent(ContentManager content)
        {
            // Load textures
            _grassTilesTexture = content.Load<Texture2D>("Grass1");
            _cropsGrowthTexture = content.Load<Texture2D>("crops_growth");

            // Pre-calculate source rectangles
            for (int i = 0; i < 2; i++) for (int j = 0; j < 4; j++)
                { _grassSources[i * 4 + j] = new Rectangle(i * 16, (j) * 16, 16, 16); }
            _dirtSources[0] = new Rectangle(0, 4 * 16, 16, 16);
            _dirtSources[1] = new Rectangle(16, 4 * 16, 16, 16);
            _tilledSources[0] = new Rectangle(0, 5 * 16, 16, 16); // Alone
            _tilledSources[1] = new Rectangle(16, 5 * 16, 16, 16);
            LoadCropData();
            GenerateWorld();
        }

        private void LoadCropData()
        {
            CropData = new Dictionary<Tool, CropData>();
            string path = Path.Combine(AppContext.BaseDirectory, "Content", "crop_data.json");
            string jsonString = File.ReadAllText(path);

            // --- START OF FIX ---

            // 1. Create a new options object to configure the deserializer.
            var options = new JsonSerializerOptions
            {
                // This is a helpful option that makes your JSON case-insensitive.
                // For example, "seedTool" would work just as well as "SeedTool".
                PropertyNameCaseInsensitive = true
            };

            // 2. Add the crucial converter that allows strings to be parsed as enums.
            options.Converters.Add(new JsonStringEnumConverter());

            // 3. Pass the options object to the Deserialize method.
            var data = JsonSerializer.Deserialize<List<CropData>>(jsonString, options);

            // --- END OF FIX ---

            foreach (var crop in data)
            {
                CropData.Add(crop.SeedTool, crop);
            }
        }

        public void SubscribeToTimeManager(TimeManager timeManager)
        {
            timeManager.OnDayChanged -= HandleDayChanged;

            // Then, add it. Now it will only ever be subscribed once.
            timeManager.OnDayChanged += HandleDayChanged;
        }

        private void HandleDayChanged()
        {
            foreach (var crop in Crops)
            {
                crop.AdvanceDay();
            }
        }

        private void GenerateWorld()
        {
            _tileGrid = new Tile[_worldWidthInTiles, _worldHeightInTiles];
            _isTileOccupied = new bool[_worldWidthInTiles, _worldHeightInTiles];
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

        public void DrawGround(SpriteBatch spriteBatch, Rectangle cameraView)
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
                    spriteBatch.Draw(_grassTilesTexture, destRect, _tileGrid[x, y].SourceRect, Color.White);
                }
            }
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

        public void PlantCrop(int x, int y, CropData cropData)
        {
            if (IsTileValidForPlanting(x, y))
            {
                var position = new Vector2(x * _tileSize + _tileSize / 2f, y * _tileSize + _tileSize / 2f);
                var newCrop = new Crop(cropData, position, _cropsGrowthTexture);
                Crops.Add(newCrop);
            }
        }

        public void HarvestCrop(int x, int y)
        {
            // Find crop at this tile
            Crop cropToHarvest = null;
            foreach (var crop in Crops)
            {
                int cropTileX = (int)Math.Floor(crop.Position.X / _tileSize);
                int cropTileY = (int)Math.Floor(crop.Position.Y / _tileSize);
                if (cropTileX == x && cropTileY == y)
                {
                    cropToHarvest = crop;
                    break;
                }
            }

            if (cropToHarvest != null && cropToHarvest.IsHarvestable)
            {
                // For now, we just remove it. In the future, this would add to inventory.
                Crops.Remove(cropToHarvest);
            }
        }
        private bool IsTileValidForPlanting(int x, int y)
        {
            if (x < 0 || x >= _worldWidthInTiles || y < 0 || y >= _worldHeightInTiles)
                return false;

            // Rule 1: Must be a tilled tile.
            if (_tileGrid[x, y].Type != TileType.Tilled)
                return false;

            // Rule 2: Must not already have a crop on it.
            foreach (var crop in Crops)
            {
                int cropTileX = (int)Math.Floor(crop.Position.X / _tileSize);
                int cropTileY = (int)Math.Floor(crop.Position.Y / _tileSize);
                if (cropTileX == x && cropTileY == y)
                {
                    return false; // Found a crop here, so it's not valid.
                }
            }

            return true; // All checks passed.
        }
    }
}