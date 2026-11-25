using Microsoft.Xna.Framework;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class InteractionEngine
    {
        private readonly GameState _state;
        private readonly EntityManager _entityManager;
        private readonly EventBus _bus;

        public const int TILE_TILLED_DIRT = 2;
        public const int TILE_WATERED_DIRT = 3;

        public InteractionEngine(GameState state, EntityManager entityManager, EventBus bus)
        {
            _state = state;
            _entityManager = entityManager;
            _bus = bus;
        }

        public void SendPayload(GameEntity targetEntity, InteractionPayload payload)
        {
            Vector2 cellCenter = new Vector2(payload.TargetCell.X * 16 + 8, payload.TargetCell.Y * 16 + 8);
            var heldItem = payload.Medium;

            // ==============================================================
            // 1. FORAGE & CROP HARVESTING (Right-Click or Left-Click)
            // ==============================================================
            var hitPlant = _state.ActiveSave.ActivePlants.FirstOrDefault(p =>
                p.SoilCell == payload.TargetCell || Vector2.Distance(p.Position, cellCenter) < 14f);

            if (hitPlant != null)
            {
                var plantDef = _state.plantManager.GetPlant(hitPlant.PlantID);
                if (plantDef != null && hitPlant.CurrentStageIndex < plantDef.Stages.Count)
                {
                    var curStage = plantDef.Stages[hitPlant.CurrentStageIndex];

                    if (curStage.IsHarvestable)
                    {
                        HarvestPlant(hitPlant, plantDef, curStage);
                        return; // Interaction successfully consumed!
                    }
                }
            }

            // ==============================================================
            // 2. SEED SOWING (Holding Seed -> Target Tilled/Watered Dirt)
            // ==============================================================
            if (payload.ActionType == "Use" && heldItem != null && (heldItem.Category == "Seed" || heldItem.ItemTags.Contains("seed")))
            {
                var tilledLayer = _state.CurrentMap.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Tilled_Dirt");
                var soilTile = tilledLayer?.GetTileAt(payload.TargetCell);

                // Seeds require Tilled (2) or Watered (3) soil
                if (soilTile != null && (soilTile.TileID == TILE_TILLED_DIRT || soilTile.TileID == TILE_WATERED_DIRT))
                {
                    if (hitPlant == null && IsFarmZone(cellCenter))
                    {
                        var plantDef = _state.plantManager.FindPlantBySeed(heldItem);
                        if (plantDef != null)
                        {
                            SowSeed(payload.TargetCell, cellCenter, plantDef, soilTile.TileID == TILE_WATERED_DIRT);

                            // Consume 1 seed from player inventory
                            var heldStack = _state.Player.Inventory.GetSelectedItem();
                            if (heldStack != null)
                            {
                                heldStack.Count--;
                                if (heldStack.Count <= 0)
                                    _state.Player.Inventory.Slots[_state.Player.Inventory.SelectedSlot] = null;
                            }
                            return;
                        }
                    }
                }
            }

            // ==============================================================
            // 3. HARD TOOL LOGIC (Tilling, Digging, Watering)
            // ==============================================================
            if (payload.ActionType == "Use" && heldItem != null)
            {
                var tilledLayer = GetOrCreateTilledLayer();
                var existingTile = tilledLayer.GetTileAt(payload.TargetCell);
                bool inFarmZone = IsFarmZone(cellCenter);

                // --- TILLING (Hoe) ---
                if (heldItem.ItemTags.Contains("tilling") && inFarmZone)
                {
                    if (existingTile != null && (existingTile.TileID == TILE_TILLED_DIRT || existingTile.TileID == TILE_WATERED_DIRT)) return;

                    _state.Debug_LastCommand = "Tilled Soil (ID 2)";
                    _bus.Publish(new PlaceTileCommand(tilledLayer, payload.TargetCell, new TileInfo("Farm", TILE_TILLED_DIRT)));
                    return;
                }

                // --- WATERING CAN ---
                if (heldItem.ItemTags.Contains("watering"))
                {
                    if (existingTile != null && existingTile.TileID == TILE_TILLED_DIRT)
                    {
                        _state.Debug_LastCommand = "Watered Soil (ID 3)";
                        _bus.Publish(new PlaceTileCommand(tilledLayer, payload.TargetCell, new TileInfo("Farm", TILE_WATERED_DIRT)));

                        // Mark any crop on this soil as watered for the day
                        if (hitPlant != null) hitPlant.IsWateredToday = true;
                        return;
                    }
                }

                // --- DIGGING (Shovel Auto-Tile Edge) ---
                if (heldItem.ItemTags.Contains("digging") && inFarmZone)
                {
                    var autoRule = _state.AutoTileManager.GetRuleForAtlas("Farm");
                    int fullPieceId = autoRule != null && autoRule.BitmaskMap.ContainsKey(15) ? autoRule.BitmaskMap[15] : 0;

                    tilledLayer.PlaceTile(payload.TargetCell, new TileInfo("Farm", fullPieceId));
                    RefreshAutoTiling(tilledLayer, payload.TargetCell);
                    return;
                }
            }
        }

        // ==============================================================
        // HARVESTING & FORAGING HANDLER
        // ==============================================================
        private void HarvestPlant(PlantInstance instance, PlantDefinition def, PlantStage stage)
        {
            Random rand = new Random();
            int itemsAwarded = 0;

            // 1. Roll Drops
            foreach (var drop in stage.Drops)
            {
                if (rand.NextDouble() <= drop.Chance)
                {
                    int count = rand.Next(drop.MinCount, drop.MaxCount + 1);
                    int remaining = _state.Player.Inventory.AddItem(drop.ItemID, count, _state.ItemManager);
                    itemsAwarded += (count - remaining);

                    var itemDef = _state.ItemManager.GetItem(drop.ItemID);
                    if (itemDef != null)
                        System.Diagnostics.Debug.WriteLine($"[Harvest] Harvested {count}x {itemDef.Name}");
                }
            }

            // 2. Lifecycle Transition
            if (stage.HarvestRevertIndex >= 0 && def.HarvestBehavior == HarvestDynamic.RegrowToStage)
            {
                // Multi-Harvest Regrowth Loop (e.g. Berry bush / Vine)
                instance.CurrentStageIndex = stage.HarvestRevertIndex;
                instance.DaysInStage = 0;
            }
            else
            {
                // Single Harvest (Uproot root crop, plucking mushroom)
                _state.ActiveSave.ActivePlants.Remove(instance);
            }

            _entityManager.RebuildStaticSprites(_state);
        }

        // ==============================================================
        // SOW SEED HANDLER
        // ==============================================================
        private void SowSeed(Point cell, Vector2 center, PlantDefinition def, bool isAlreadyWatered)
        {
            Vector2 snapPos = GridHelper.GetPlacementPosition(center, false);

            var instance = new PlantInstance
            {
                PlantID = def.ID,
                Position = snapPos,
                CurrentStageIndex = 0, // Starts at Seed/Spore
                DaysInStage = 0,
                IsWildForage = false,
                IsWateredToday = isAlreadyWatered,
                SoilCell = cell
            };

            _state.ActiveSave.ActivePlants.Add(instance);
            _entityManager.RebuildStaticSprites(_state);
            System.Diagnostics.Debug.WriteLine($"[Sow] Planted {def.Name} at ({cell.X}, {cell.Y})");
        }

        // ==============================================================
        // FARM ZONE DETECTOR (Red Channel = 90 or Farmable Shape)
        // ==============================================================
        public bool IsFarmZone(Vector2 worldPos)
        {
            // 1. Check Mask Red Channel (Biome 90 = Farmable Soil)
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;
            int chunkX = (int)Math.Floor(worldPos.X / chunkSize);
            int chunkY = (int)Math.Floor(worldPos.Y / chunkSize);
            Point chunkCoord = new Point(chunkX, chunkY);

            if (_state.TerrainMaskChunks.TryGetValue(chunkCoord, out var maskTex))
            {
                int lx = Math.Clamp((int)(worldPos.X - chunkX * chunkSize), 0, chunkSize - 1);
                int ly = Math.Clamp((int)(worldPos.Y - chunkY * chunkSize), 0, chunkSize - 1);

                Color[] singlePixel = new Color[1];
                maskTex.GetData(0, new Rectangle(lx, ly, 1, 1), singlePixel, 0, 1);

                // If within tolerance of Farm Zone (Value 90)
                if (Math.Abs(singlePixel[0].R - SystemTags.BIOME_FARM_ZONE_VALUE) <= 5)
                    return true;
            }

            // 2. Fallback to ControlLayer shape tagged FARMABLE
            foreach (var layer in _state.CurrentMap.Layers.OfType<ControlLayer>())
            {
                if (!layer.IsVisible) continue;
                foreach (var shape in layer.Shapes)
                {
                    if (shape.HasTag(SystemTags.FARMABLE) && shape.Shape.Contains(worldPos))
                        return true;
                }
            }

            return false;
        }

        private TileLayer GetOrCreateTilledLayer()
        {
            var layer = _state.CurrentMap.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Tilled_Dirt");
            if (layer == null)
            {
                layer = new TileLayer("Tilled_Dirt");
                _state.CurrentMap.Layers.Insert(1, layer);
            }
            return layer;
        }

        public void RefreshAutoTiling(TileLayer layer, Point centerCell)
        {
            var rule = _state.AutoTileManager.GetRuleForAtlas("Farm");
            if (rule == null) return;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Point targetCell = new Point(centerCell.X + x, centerCell.Y + y);
                    int bitmask = CalculateCornerBitmask(layer, targetCell, rule);

                    if (bitmask > 0 && rule.BitmaskMap.TryGetValue(bitmask, out int newTileId))
                    {
                        var existing = layer.GetTileAt(targetCell);
                        if (existing == null || (existing.TileID != TILE_TILLED_DIRT && existing.TileID != TILE_WATERED_DIRT && existing.TileID != newTileId))
                        {
                            layer.PlaceTile(targetCell, new TileInfo(rule.TargetAtlas, newTileId));
                        }
                    }
                }
            }
        }

        private int CalculateCornerBitmask(TileLayer layer, Point cell, AutoTileRule rule)
        {
            int mask = 0;
            if (IsDirt(layer, cell.X, cell.Y, rule) || IsDirt(layer, cell.X - 1, cell.Y, rule) || IsDirt(layer, cell.X, cell.Y - 1, rule) || IsDirt(layer, cell.X - 1, cell.Y - 1, rule)) mask += 1;
            if (IsDirt(layer, cell.X, cell.Y, rule) || IsDirt(layer, cell.X + 1, cell.Y, rule) || IsDirt(layer, cell.X, cell.Y - 1, rule) || IsDirt(layer, cell.X + 1, cell.Y - 1, rule)) mask += 2;
            if (IsDirt(layer, cell.X, cell.Y, rule) || IsDirt(layer, cell.X - 1, cell.Y, rule) || IsDirt(layer, cell.X, cell.Y + 1, rule) || IsDirt(layer, cell.X - 1, cell.Y + 1, rule)) mask += 4;
            if (IsDirt(layer, cell.X, cell.Y, rule) || IsDirt(layer, cell.X + 1, cell.Y, rule) || IsDirt(layer, cell.X, cell.Y + 1, rule) || IsDirt(layer, cell.X + 1, cell.Y + 1, rule)) mask += 8;
            return mask;
        }

        private bool IsDirt(TileLayer layer, int x, int y, AutoTileRule rule)
        {
            var t = layer.GetTileAt(new Point(x, y));
            return t != null && t.TilesetName == rule.TargetAtlas;
        }

        public void UpdateDebugState()
        {
            Vector2 lookPos = _state.Player.GetInteractionBox().Center;
            _state.Debug_FarmableStatus = IsFarmZone(lookPos) ? $"FARM ZONE: YES" : $"FARM ZONE: NO";
        }
    }
}