using Microsoft.Xna.Framework;
using Pixel_Simulations.Data;
using System;
using System.Linq;

namespace Pixel_Simulations
{
    public class InteractionEngine
    {
        private readonly GameState _state;
        private readonly EntityManager _entityManager;

        public InteractionEngine(GameState state, EntityManager entityManager)
        {
            _state = state;
            _entityManager = entityManager;
        }

        public void SendPayload(GameEntity targetEntity, InteractionPayload payload)
        {
            // ==========================================
            // 1. NATIVE HARDCODED LOGIC (Physical Items)
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
                        if (physDef.Type == PlacementType.Crop && placedObj.CurrentStageIndex < physDef.Stages.Count - 1)
                        {
                            placedObj.DaysInCurrentStage += payload.IntValue;
                            if (placedObj.DaysInCurrentStage >= stage.ThresholdInt1)
                            {
                                placedObj.DaysInCurrentStage = 0;
                                placedObj.CurrentStageIndex++;
                            }
                        }
                        else if (physDef.Type == PlacementType.Pile && stage.ThresholdBool)
                        {
                            placedObj.DaysInCurrentStage += payload.IntValue;
                            if (placedObj.DaysInCurrentStage >= stage.ThresholdInt2)
                            {
                                placedObj.Amount /= 2;
                                placedObj.DaysInCurrentStage = 0;

                                // Demote visual stage if pile shrunk below threshold
                                while (placedObj.CurrentStageIndex > 1 && placedObj.Amount < physDef.Stages[placedObj.CurrentStageIndex].ThresholdInt1)
                                {
                                    placedObj.CurrentStageIndex--;
                                }

                                if (placedObj.Amount <= 0) DestroyEntity(targetEntity);
                            }
                        }
                        return; // End of DayTick processing (No visual refresh here; handled by master call in GameController)
                    }

                    // --- PLAYER "USE" PAYLOAD (Harvesting) ---
                    if (payload.ActionType == "Use")
                    {
                        // A. Harvest Crop
                        if (physDef.Type == PlacementType.Crop && stage.ThresholdBool)
                        {
                            if (stage.ThresholdInt2 > 0) // Valid Produce ID Check
                            {
                                int remaining = _state.Player.Inventory.AddItem(stage.ThresholdInt2, 1, _state.ItemManager);

                                if (remaining == 0) // Item was successfully added to inventory
                                {
                                    // NEW: Multi-Harvest Check!
                                    if (physDef.IsMultiHarvest)
                                    {
                                        placedObj.CurrentStageIndex = physDef.HarvestRevertStage;
                                        placedObj.DaysInCurrentStage = 0; // Reset growth timer
                                        _entityManager.RebuildStaticSprites(_state); // Refresh visuals instantly
                                    }
                                    else
                                    {
                                        // Single harvest (e.g. Turnip), destroy crop
                                        DestroyEntity(targetEntity);
                                    }
                                }
                            }
                            return; // Action consumed
                        }

                        // B. Collect Machine
                        if (physDef.Type == PlacementType.Machine && stage.ThresholdBool)
                        {
                            if (stage.ThresholdInt2 > 0) // Valid Output ID Check
                            {
                                int remaining = _state.Player.Inventory.AddItem(stage.ThresholdInt2, 1, _state.ItemManager);
                                if (remaining == 0)
                                {
                                    placedObj.CurrentStageIndex = 1; // Reset to process again
                                    placedObj.ProcessingTimer = 0f;
                                    _entityManager.RebuildStaticSprites(_state);
                                }
                            }
                            return; // Action consumed
                        }
                    }
                }
            }

            // ==========================================
            // 2. DATA-DRIVEN LOGIC (JSON Rules like Axes, Tilling)
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

            if (visualsNeedsRefresh && targetEntity != null) _entityManager.RefreshEntityVisuals(targetEntity.BaseData.ID);
        }

        private void DestroyEntity(GameEntity entity)
        {
            foreach (string linkID in entity.BaseData.LinkedObjects) _entityManager.RemoveEntity(linkID);
            if (entity.BaseData is PlacedItemObject placed) _state.ActiveSave.PlacedItems.Remove(placed);
            else _state.ActiveSave.DestroyedBaseIDs.Add(entity.BaseData.ID);
            _entityManager.RemoveEntity(entity.BaseData.ID);
        }
    }
}