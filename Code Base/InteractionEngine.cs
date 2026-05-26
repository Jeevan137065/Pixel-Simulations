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

        public const int TILLED_DIRT_ID = 16; // Change this to your exact ID
        public const int WATERED_DIRT_ID = 17;
        public InteractionEngine(GameState state, EntityManager entityManager, EventBus bus)
        {
            _state = state;
            _entityManager = entityManager;
            _bus = bus;
        }

        public void SendPayload(GameEntity targetEntity, InteractionPayload payload)
        {
            // ==========================================
            // 1. NATIVE HARD TOOL LOGIC (Farming)
            // ==========================================
            if (payload.ActionType == "Use" && payload.Medium != null)
            {
                Vector2 cellCenter = new Vector2(payload.TargetCell.X * 16 + 8, payload.TargetCell.Y * 16 + 8);

                if (payload.Medium.ItemTags.Contains("tilling") || payload.Medium.ItemTags.Contains("digging") || payload.Medium.ItemTags.Contains("watering"))
                {
                    var tilledLayer = GetOrCreateTilledLayer();
                    var existingTile = tilledLayer.GetTileAt(payload.TargetCell);
                    bool isFarmZone = IsFarmable(cellCenter);
                    if (payload.Medium.ItemTags.Contains("tilling") && isFarmZone)
                    {
                        if (existingTile != null && (existingTile.TileID == 2 || existingTile.TileID == 3)) return;
                        _state.Debug_LastCommand = "PlaceTileCommand (Tilled Dirt ID 2)";
                        // FIX: Capitalized "Farm"
                        _bus.Publish(new PlaceTileCommand(tilledLayer, payload.TargetCell, new TileInfo("Farm", 2)));
                        return;
                    }
                    else if (payload.Medium.ItemTags.Contains("digging") && IsFarmable(cellCenter))
                    {
                        var autoRule = _state.AutoTileManager.GetRuleForAtlas("Farm");
                        if (existingTile != null && IsLogicalDirt(tilledLayer, payload.TargetCell.X, payload.TargetCell.Y, autoRule)) return;

                        // Find the ID assigned to Mask 15 (Full Piece)
                        int fullPieceId = autoRule != null && autoRule.BitmaskMap.ContainsKey(15) ? autoRule.BitmaskMap[15] : 0;

                        tilledLayer.PlaceTile(payload.TargetCell, new TileInfo("Farm", fullPieceId));
                        RefreshAutoTiling(tilledLayer, payload.TargetCell);
                        return;
                    }

                    else if (payload.Medium.ItemTags.Contains("watering"))
                    {
                        if (existingTile != null && existingTile.TileID == 2)
                        {
                            _state.Debug_LastCommand = "PlaceTileCommand (Watered Dirt ID 3)";
                            // FIX: Capitalized "Farm"
                            _bus.Publish(new PlaceTileCommand(tilledLayer, payload.TargetCell, new TileInfo("Farm", 3)));
                            return;
                        }
                    }

                    _state.Debug_LastCommand = $"FAILED: FarmZone={isFarmZone}, Tags matched but logic rejected.";
                }
            }


            // ==========================================
            // 2. NATIVE ITEM LOGIC (Growth & Harvesting)
            // ==========================================
            if (targetEntity != null && targetEntity.BaseData is PlacedItemObject placedObj)
            {
                var physDef = _state.ItemManager.GetPhysicalDef(placedObj.ItemID);
                if (physDef != null && placedObj.CurrentStageIndex < physDef.Stages.Count)
                {
                    var stage = physDef.Stages[placedObj.CurrentStageIndex];

                    // --- TIME ADVANCEMENT PAYLOAD ---
                    if (payload.ActionType == "DayTick")
                    {
                        if (physDef.Type == PlacementType.Crop)
                        {
                            // Nudge position up 1 pixel to guarantee we check the tile it is standing on
                            Point cropCell = GridHelper.GetTileCell(new Vector2(placedObj.Position.X, placedObj.Position.Y - 1));
                            var tilledLayer = _state.CurrentMap.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Tilled_Dirt");
                            var soilTile = tilledLayer?.GetTileAt(cropCell);

                            // Only grow if the crop actually requires time AND the soil is watered (TileID 3)
                            bool isWatered = soilTile != null && soilTile.TileID == 3;

                            if (stage.ThresholdInt1 > 0 && isWatered)
                            {
                                placedObj.DaysInCurrentStage += payload.IntValue;
                                if (placedObj.DaysInCurrentStage >= stage.ThresholdInt1)
                                {
                                    placedObj.DaysInCurrentStage = 0;
                                    if (stage.NextStageIndex >= 0) placedObj.CurrentStageIndex = stage.NextStageIndex;
                                    else if (placedObj.CurrentStageIndex < physDef.Stages.Count - 1) placedObj.CurrentStageIndex++;
                                }
                            }
                        }
                        else if (physDef.Type == PlacementType.Pile && stage.ThresholdBool)
                        {
                            placedObj.DaysInCurrentStage += payload.IntValue;
                            if (placedObj.DaysInCurrentStage >= stage.ThresholdInt2)
                            {
                                placedObj.Amount /= 2;
                                placedObj.DaysInCurrentStage = 0;

                                while (placedObj.CurrentStageIndex > 1 && placedObj.Amount < physDef.Stages[placedObj.CurrentStageIndex].ThresholdInt1)
                                    placedObj.CurrentStageIndex--;

                                if (placedObj.Amount <= 0) DestroyEntity(targetEntity);
                            }
                        }
                        return;
                    }

                    // --- PLAYER "USE" PAYLOAD (Harvesting) ---
                    if (payload.ActionType == "Use")
                    {
                        if (physDef.Type == PlacementType.Crop && stage.ThresholdBool)
                        {
                            if (stage.ThresholdInt2 > 0)
                            {
                                int remaining = _state.Player.Inventory.AddItem(stage.ThresholdInt2, 1, _state.ItemManager);
                                if (remaining == 0)
                                {
                                    if (physDef.IsMultiHarvest)
                                    {
                                        placedObj.CurrentStageIndex = physDef.FinalHarvestStage;
                                        placedObj.DaysInCurrentStage = 0;
                                        _entityManager.RebuildStaticSprites(_state);
                                    }
                                    else DestroyEntity(targetEntity);
                                }
                            }
                            return;
                        }

                        if (physDef.Type == PlacementType.Machine && stage.ThresholdBool)
                        {
                            if (stage.ThresholdInt2 > 0)
                            {
                                int remaining = _state.Player.Inventory.AddItem(stage.ThresholdInt2, 1, _state.ItemManager);
                                if (remaining == 0)
                                {
                                    if (stage.NextStageIndex >= 0) placedObj.CurrentStageIndex = stage.NextStageIndex;
                                    else placedObj.CurrentStageIndex = 1;

                                    placedObj.ProcessingTimer = 0f;
                                    _entityManager.RebuildStaticSprites(_state);
                                }
                            }
                            return;
                        }
                    }
                }
            }

            // ==========================================
            // 3. DATA-DRIVEN LOGIC (JSON Rules)
            // ==========================================
            if (payload.ActionType == "Use")
            {
                var rule = _state.Interactions.Evaluate(targetEntity, payload.TargetTile, payload.Medium);
                if (rule != null) ExecuteRuleEffects(rule, targetEntity, payload);
            }
        }

        private void ExecuteRuleEffects(InteractionRule rule, GameEntity targetEntity, InteractionPayload payload)
        {
            bool visualsNeedsRefresh = false;
            TileLayer groundLayer = _state.CurrentMap.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Ground");

            foreach (var effect in rule.Effects)
            {
                switch (effect.Type)
                {
                    case EffectType.DropLoot:
                        _state.Player.Inventory.AddItem(effect.IntValue, string.IsNullOrEmpty(effect.StringValue) ? 1 : int.Parse(effect.StringValue), _state.ItemManager);
                        break;

                    case EffectType.ConsumeHeldItem:
                        if (payload.Medium != null)
                        {
                            var heldStack = _state.Player.Inventory.GetSelectedItem();
                            if (heldStack != null)
                            {
                                heldStack.Count -= effect.IntValue;
                                if (heldStack.Count <= 0) _state.Player.Inventory.Slots[_state.Player.Inventory.SelectedSlot] = null;
                            }
                        }
                        break;

                    case EffectType.SetProperty:
                        if (targetEntity != null)
                        {
                            targetEntity.BaseData.Properties[effect.StringValue] = new MapProperty(PropertyType.String, effect.SecondaryValue);
                            visualsNeedsRefresh = true;
                        }
                        break;

                    case EffectType.DestroyTarget:
                        if (targetEntity != null) DestroyEntity(targetEntity);
                        break;

                    case EffectType.ChangeTile:
                        if (groundLayer != null) groundLayer.PlaceTile(payload.TargetCell, new TileInfo(effect.StringValue, effect.IntValue));
                        break;

                    case EffectType.SpawnEntity:
                        Vector2 fallbackPivot = new Vector2(8, 16);
                        var spawnDef = _state.ItemManager.GetPhysicalDef(effect.IntValue);
                        if (spawnDef != null) fallbackPivot = new Vector2((spawnDef.CellWidth * 16) / 2f, spawnDef.CellHeight * 16);

                        Vector2 centerLookup = new Vector2(payload.TargetCell.X * 16 + 8, payload.TargetCell.Y * 16 + 8);
                        Vector2 spawnPos = GridHelper.SnapToSubCell(centerLookup, fallbackPivot);

                        var placedObj = new PlacedItemObject
                        {
                            ID = Guid.NewGuid().ToString().Substring(0, 8),
                            Name = $"Placed_{effect.IntValue}",
                            ItemID = effect.IntValue,
                            Position = spawnPos,
                            Amount = 1,
                            DaysInCurrentStage = 0,
                            CurrentStageIndex = 1
                        };

                        _state.ActiveSave.PlacedItems.Add(placedObj);
                        _entityManager.AddDynamicEntity(placedObj, _state);
                        break;
                }
            }

            if (visualsNeedsRefresh && targetEntity != null) _entityManager.RefreshEntityVisuals(targetEntity.BaseData.ID,_state);
        }

        private void DestroyEntity(GameEntity entity)
        {
            // FIX: Iterate through the ObjectLink struct and use TargetID
            foreach (var link in entity.BaseData.Links)
            {
                _entityManager.RemoveEntity(link.TargetID);
            }

            if (entity.BaseData is PlacedItemObject placed)
                _state.ActiveSave.PlacedItems.Remove(placed);
            else
                _state.ActiveSave.DestroyedBaseIDs.Add(entity.BaseData.ID);

            _entityManager.RemoveEntity(entity.BaseData.ID);
        }

        public void UpdateDebugState()
        {
            Vector2 lookPos = _state.Player.GetInteractionBox().Center;

            bool isFarmable = IsFarmable(lookPos);

            if (isFarmable)
                _state.Debug_FarmableStatus = $"TRUE (At {lookPos.X:F0}, {lookPos.Y:F0})";
            else
                _state.Debug_FarmableStatus = $"FALSE (At {lookPos.X:F0}, {lookPos.Y:F0})";
        }
        private bool IsFarmable(Vector2 worldPos)
        {
            foreach (var layer in _state.CurrentMap.Layers.OfType<ControlLayer>())
            {
                if (!layer.IsVisible || layer.IsLocked) continue;
                foreach (var shape in layer.Shapes)
                {
                    if (shape.Tags.Contains(90) && shape.Shape.Contains(worldPos)) return true;
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

            var updates = new System.Collections.Generic.Dictionary<Point, TileInfo>();
            var removals = new System.Collections.Generic.List<Point>();

            // Update the clicked cell and its 8 neighbors
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    Point targetCell = new Point(centerCell.X + x, centerCell.Y + y);
                    int bitmask = CalculateCornerBitmask(layer, targetCell, rule);

                    if (bitmask > 0)
                    {
                        if (rule.BitmaskMap.TryGetValue(bitmask, out int newTileId))
                        {
                            var existing = layer.GetTileAt(targetCell);
                            // Do not overwrite manual tilled/watered dirt
                            if (existing == null || (existing.TileID != TILLED_DIRT_ID && existing.TileID != WATERED_DIRT_ID && existing.TileID != newTileId))
                            {
                                updates[targetCell] = new TileInfo(rule.TargetAtlas, newTileId);
                            }
                        }
                    }
                    else
                    {
                        // Erase visual edges if they no longer connect to any logical dirt
                        var existing = layer.GetTileAt(targetCell);
                        if (existing != null && existing.TilesetName == rule.TargetAtlas && existing.TileID != TILLED_DIRT_ID && existing.TileID != WATERED_DIRT_ID)
                        {
                            removals.Add(targetCell);
                        }
                    }
                }
            }

            foreach (var kvp in updates) layer.PlaceTile(kvp.Key, kvp.Value);
            foreach (var pt in removals) layer.RemoveTile(pt);
        }

        private int CalculateCornerBitmask(TileLayer layer, Point cell, AutoTileRule rule)
        {
            int mask = 0; // TL=1, TR=2, BL=4, BR=8

            if (IsLogicalDirt(layer, cell.X, cell.Y, rule) || IsLogicalDirt(layer, cell.X - 1, cell.Y, rule) || IsLogicalDirt(layer, cell.X, cell.Y - 1, rule) || IsLogicalDirt(layer, cell.X - 1, cell.Y - 1, rule))
                mask += 1;

            if (IsLogicalDirt(layer, cell.X, cell.Y, rule) || IsLogicalDirt(layer, cell.X + 1, cell.Y, rule) || IsLogicalDirt(layer, cell.X, cell.Y - 1, rule) || IsLogicalDirt(layer, cell.X + 1, cell.Y - 1, rule))
                mask += 2;

            if (IsLogicalDirt(layer, cell.X, cell.Y, rule) || IsLogicalDirt(layer, cell.X - 1, cell.Y, rule) || IsLogicalDirt(layer, cell.X, cell.Y + 1, rule) || IsLogicalDirt(layer, cell.X - 1, cell.Y + 1, rule))
                mask += 4;

            if (IsLogicalDirt(layer, cell.X, cell.Y, rule) || IsLogicalDirt(layer, cell.X + 1, cell.Y, rule) || IsLogicalDirt(layer, cell.X, cell.Y + 1, rule) || IsLogicalDirt(layer, cell.X + 1, cell.Y + 1, rule))
                mask += 8;

            return mask;
        }


        public bool IsLogicalDirt(TileLayer layer, int x, int y, AutoTileRule rule)
        {
            var tile = layer.GetTileAt(new Point(x, y));
            if (tile == null || tile.TilesetName != rule.TargetAtlas) return false;

            // Tilled/Watered count as logic centers
            if (tile.TileID == TILLED_DIRT_ID || tile.TileID == WATERED_DIRT_ID) return true;

            // Mask 15 (Full piece) is the true logical center of Dug Dirt
            if (rule.BitmaskMap.TryGetValue(15, out int fullTileId))
            {
                return tile.TileID == fullTileId;
            }

            return false;
        }

    }
}