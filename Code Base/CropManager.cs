using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Pixel_Simulations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pixel_Simulations
{
    public class CropManager
    {
        // Dependencies
        private readonly WorldMap _worldMap; // A reference to the world to check tile types
        private GraphicsDevice _gd;
        // Data
        public List<PlantingPlot> _plots = new();
        public Dictionary<Tool, CropData> CropData { get; private set; }
        private Texture2D _cropsGrowthTexture, _cropsGrowthNormal;
        private readonly Random _random = new();

        public CropManager(WorldMap worldMap, GraphicsDevice GD)
        {
            _gd = GD;
            _worldMap = worldMap;
        }

        public void LoadContent(ContentManager content)
        {
            _cropsGrowthTexture = content.Load<Texture2D>("crops_growth");
            _cropsGrowthNormal = content.Load<Texture2D>("crops_growth_n");
            LoadCropData();
        }

        private void LoadCropData()
        {
            CropData = new Dictionary<Tool, CropData>();
            string path = Path.Combine(AppContext.BaseDirectory, "Content", "crop_data.json");
            string jsonString = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            var data = JsonSerializer.Deserialize<List<CropData>>(jsonString, options);

            foreach (var crop in data) { CropData.Add(crop.SeedTool, crop); }
        }

        public void SubscribeToTimeManager(TimeManager timeManager)
        {
            timeManager.OnDayChanged += AdvanceDayForAllCrops;
        }

        private void AdvanceDayForAllCrops()
        {
            foreach (var plot in _plots)
                foreach (var crop in plot.Crops)
                    crop.AdvanceDay();
        }

        public IEnumerable<Crop> GetAllCrops()
        {
            return _plots.SelectMany(plot => plot.Crops);
        }

        // The core interaction logic, now living in its own manager
        public void InteractWithTile(int tileX, int tileY, CropData primaryCrop, bool isShiftHeld)
        {
            var existingPlot = _plots.FirstOrDefault(p => p.TileX == tileX && p.TileY == tileY);
            var randomOffset = new Vector2(_random.Next(-2, 3), _random.Next(-2, 3));
            int _tileSize = _worldMap._tileSize;
            if (existingPlot == null)
            {
                if (!IsTileValidForPlanting(tileX, tileY)) return;

                PlantingPlot newPlot;
                if (isShiftHeld)
                {
                    newPlot = new PlantingPlot(tileX, tileY, SowingMethod.InterplantHorizontal);
                    var leftPos = new Vector2(tileX * GameConstants.GridCellSize + GameConstants.GridCellSize * 0.25f, tileY * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f);
                    newPlot.Crops.Add(new Crop(primaryCrop, leftPos + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                }
                else
                {
                    newPlot = new PlantingPlot(tileX, tileY, SowingMethod.Normal);
                    var centerPos = new Vector2(tileX * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f, tileY * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f);
                    newPlot.Crops.Add(new Crop(primaryCrop, centerPos + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                }
                _plots.Add(newPlot);
            }
            else
            {
                if (!isShiftHeld) return;
                switch (existingPlot.SowingMethod)
                {
                    case SowingMethod.Normal:
                        // Convert a Normal planting to an Alternative/Corner one.
                        var originalCropData = existingPlot.Crops[0].Data;
                        if (primaryCrop.ID == originalCropData.ID) return; // Can't interplant with the same crop.

                        existingPlot.SowingMethod = SowingMethod.InterplantCorners;
                        existingPlot.Crops.Clear(); // Remove the old centered crop

                        // Re-add the original crop in the center
                        var centerPos = new Vector2(tileX * _tileSize + _tileSize / 2f, tileY * _tileSize + _tileSize / 2f);
                        existingPlot.Crops.Add(new Crop(originalCropData, centerPos + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));

                        // Add the new crop to the corners
                        float cornerOffset = _tileSize * 0.3f; // A bit tighter than before
                        existingPlot.Crops.Add(new Crop(primaryCrop, new Vector2(centerPos.X - cornerOffset, centerPos.Y - cornerOffset) + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                        existingPlot.Crops.Add(new Crop(primaryCrop, new Vector2(centerPos.X + cornerOffset, centerPos.Y - cornerOffset) + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                        existingPlot.Crops.Add(new Crop(primaryCrop, new Vector2(centerPos.X - cornerOffset, centerPos.Y + cornerOffset) + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                        existingPlot.Crops.Add(new Crop(primaryCrop, new Vector2(centerPos.X + cornerOffset, centerPos.Y + cornerOffset) + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                        break;

                    case SowingMethod.InterplantHorizontal:
                        // Complete a horizontal pair if it only has one plant (the left one).
                        if (existingPlot.Crops.Count == 1)
                        {
                            var leftCropData = existingPlot.Crops[0].Data;
                            if (primaryCrop.ID == leftCropData.ID) return; // Can't pair with the same crop.

                            var rightPos = new Vector2(tileX * _tileSize + _tileSize * 0.75f, tileY * _tileSize + _tileSize / 2f);
                            existingPlot.Crops.Add(new Crop(primaryCrop, rightPos + randomOffset, _cropsGrowthTexture, _cropsGrowthNormal, _gd));
                        }
                        break;
                }
            }
        }

        private bool IsTileValidForPlanting(int x, int y)
        {
            if (_worldMap.GetTileType(x, y) != TileType.Tilled) return false;

            // No need to check for existing crops here, as the logic flow checks for existing plots first.
            return true;
        }

        public void HarvestCrop(int x, int y)
        {
            var plotToHarvest = _plots.FirstOrDefault(p => p.TileX == x && p.TileY == y);
            if (plotToHarvest != null && plotToHarvest.Crops.Any(c => c.IsHarvestable))
            {
                _plots.Remove(plotToHarvest);
            }
        }
    }
}