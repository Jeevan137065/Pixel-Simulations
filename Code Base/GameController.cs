using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Linq;

namespace Pixel_Simulations
{
    public class GameController
    {
        private readonly GameState _state;
        private readonly EntityManager _entityManager;
        private readonly InteractionEngine _interactionEngine;
        private bool _fKeyPressedLastFrame = false;
        private bool _iKeyPressedLastFrame = false;

        public GameController(GameState state, EntityManager entityManager, EventBus bus)
        {
            _state = state;
            _entityManager = entityManager;
            _interactionEngine = new InteractionEngine(state, entityManager);

            // SUBSCRIBER: Listen for the New Day!
            bus.Subscribe<DayAdvancedCommand>(OnNewDay);
        }

        public void Update(GameTime gameTime)
        {
            _state.Update(gameTime);
            HandleInteractions();
            UpdateEntities(gameTime);
        }

        // Inside GameController.cs
        private void TryInteract(bool isRightClick)
        {
            // 1. Where are we looking?
            RectangleF interactBox = _state.Player.GetInteractionBox();
            Vector2 lookPos = interactBox.Center;

            // 1. FIND TARGET ENTITY
            GameEntity targetEntity = _entityManager.AllEntities.FirstOrDefault(e => {
                if (e.Prefab != null)
                {
                    RectangleF bounds = new RectangleF(e.Position.X - e.Prefab.Pivot.X, e.Position.Y - e.Prefab.Pivot.Y, e.Prefab.SourceRect.Width, e.Prefab.SourceRect.Height);
                    return bounds.Intersects(interactBox);
                }
                if (e.BaseData is PlacedItemObject placed)
                {
                    var pDef = _state.ItemManager.GetPhysicalDef(placed.ItemID);
                    if (pDef != null)
                    {
                        Vector2 pivot = new Vector2((pDef.CellWidth * 16) / 2f, pDef.CellHeight * 16);
                        RectangleF pBounds = new RectangleF(e.Position.X - pivot.X, e.Position.Y - pivot.Y, pDef.CellWidth * 16, pDef.CellHeight * 16);
                        return pBounds.Intersects(interactBox);
                    }
                }
                return false;
            });

            var heldStack = _state.Player.Inventory.GetSelectedItem();
            ItemDefinition heldItem = (heldStack != null && heldStack.Count > 0) ? _state.ItemManager.GetItem(heldStack.ItemID) : null;

            // ==========================================
            // A. HARVESTING & COLLECTING (Works for both F and Right-Click)
            // ==========================================
            if (targetEntity != null && targetEntity.BaseData is PlacedItemObject placedObj)
            {
                var physDef = _state.ItemManager.GetPhysicalDef(placedObj.ItemID);
                if (physDef != null && placedObj.CurrentStageIndex < physDef.Stages.Count)
                {
                    var stage = physDef.Stages[placedObj.CurrentStageIndex];

                    // Crop Harvesting
                    if (physDef.Type == PlacementType.Crop && stage.ThresholdBool)
                    {
                        if (stage.ThresholdInt2 > 0) // Ensure it doesn't give item 0!
                        {
                            int remaining = _state.Player.Inventory.AddItem(stage.ThresholdInt2, 1, _state.ItemManager);
                            if (remaining > 0) return; // Inventory full
                        }

                        // NEW: Multi-Harvest Logic
                        if (physDef.IsMultiHarvest)
                        {
                            placedObj.CurrentStageIndex = physDef.HarvestRevertStage;
                            placedObj.DaysInCurrentStage = 0; // Reset growth timer!
                            _entityManager.RebuildStaticSprites(_state);
                        }
                        else
                        {
                            // Single Harvest (e.g., Turnips, Carrots) -> Destroy
                            _state.ActiveSave.PlacedItems.Remove(placedObj);
                            _entityManager.RemoveEntity(placedObj.ID);
                            _entityManager.RebuildStaticSprites(_state);
                        }
                        return; // Stop here, action consumed!
                    }
                    // Machine Collection
                    if (physDef.Type == PlacementType.Machine && stage.ThresholdBool)
                    {
                        if (stage.ThresholdInt2 > 0)
                        {
                            int remaining = _state.Player.Inventory.AddItem(stage.ThresholdInt2, 1, _state.ItemManager);
                            if (remaining > 0) return; // Inventory full
                        }

                        placedObj.CurrentStageIndex = 1;
                        placedObj.ProcessingTimer = 0f;
                        _entityManager.RebuildStaticSprites(_state);
                        return; // Stop here, action consumed!
                    }
                }
            }

            // ==========================================
            // B. RIGHT CLICK: PLACEMENT & PILE ADDITION
            // ==========================================
            if (isRightClick)
            {
                if (heldStack != null && heldStack.Count > 0)
                {
                    var physDef = _state.ItemManager.GetPhysicalDef(heldStack.ItemID);
                    if (physDef != null && physDef.IsPlaceable && physDef.Stages.Count > 1)
                    {
                        Vector2 snapPos = GridHelper.GetPlacementPosition(lookPos, physDef.PrecisionPlacement);
                        GameEntity exactEntity = _entityManager.AllEntities.FirstOrDefault(e => e.BaseData is PlacedItemObject && Vector2.Distance(e.Position, snapPos) < 1f);
                        bool itemPlaced = false;

                        // Pile Logic
                        if (physDef.Type == PlacementType.Pile)
                        {
                            if (exactEntity != null && exactEntity.BaseData is PlacedItemObject placedPile && placedPile.ItemID == heldStack.ItemID)
                            {
                                int maxCapacity = physDef.Stages[physDef.Stages.Count - 1].ThresholdInt1;
                                if (placedPile.Amount < maxCapacity)
                                {
                                    placedPile.Amount++;
                                    placedPile.DaysInCurrentStage = 0;

                                    int nextStage = placedPile.CurrentStageIndex + 1;
                                    if (nextStage < physDef.Stages.Count && placedPile.Amount >= physDef.Stages[nextStage].ThresholdInt1)
                                    {
                                        placedPile.CurrentStageIndex = nextStage;
                                        _entityManager.RebuildStaticSprites(_state);
                                    }
                                    itemPlaced = true;
                                }
                            }
                            else if (exactEntity == null && targetEntity == null)
                            {
                                PlaceNewItem(heldStack.ItemID, snapPos, physDef);
                                itemPlaced = true;
                            }
                        }
                        // Standard Placement (Props/Crops/Machines)
                        else if (exactEntity == null && targetEntity == null)
                        {
                            PlaceNewItem(heldStack.ItemID, snapPos, physDef);
                            itemPlaced = true;
                        }

                        if (itemPlaced)
                        {
                            heldStack.Count--;
                            if (heldStack.Count <= 0) _state.Player.Inventory.Slots[_state.Player.Inventory.SelectedSlot] = null;
                        }
                    }
                }
                return; // Stop here, Right Click consumed!
            }

            // ==========================================
            // C. LEFT CLICK / F: JSON RULES (Trees, Dirt, etc)
            // ==========================================
            Point targetGridCell = GridHelper.GetTileCell(lookPos);
            TileLayer groundLayer = _state.CurrentMap.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Ground");
            TileInfo targetTile = groundLayer?.GetTileAt(targetGridCell);

            var payload = new InteractionPayload
            {
                ActionType = "Use",
                Medium = heldItem,
                TargetCell = targetGridCell,
                TargetTile = targetTile
            };
            _interactionEngine.SendPayload(targetEntity, payload);
        }
        private void HandleInteractions()
        {
            var kstate = Keyboard.GetState();

            if (kstate.IsKeyDown(Keys.Escape) && !_fKeyPressedLastFrame) _state.DebugPool[GameBool.IsPaused] = !_state.DebugPool[GameBool.IsPaused];
            if (kstate.IsKeyDown(Keys.F5) && !_fKeyPressedLastFrame) _state.SaveGame();
            if (kstate.IsKeyDown(Keys.I) && !_iKeyPressedLastFrame) _state.IsInventoryOpen = !_state.IsInventoryOpen;

            _fKeyPressedLastFrame = kstate.IsKeyDown(Keys.Escape);
            _iKeyPressedLastFrame = kstate.IsKeyDown(Keys.I);

            if (_state.DebugPool[GameBool.IsPaused]) return;

            if (_state.input.NewLeftClick)
                _state.Player.Inventory.HandleMouseInteraction(_state.input.MouseScreenPosition.ToPoint(), true, new Vector2(1920, 1080), _state.IsInventoryOpen);

            if (kstate.IsKeyDown(Keys.F) && !_fKeyPressedLastFrame) TryInteract(false);
            _fKeyPressedLastFrame = kstate.IsKeyDown(Keys.F);

            // Right Click
            if (_state.input.NewRightClick && !_state.IsInventoryOpen) TryInteract(true);

            _fKeyPressedLastFrame = kstate.IsKeyDown(Keys.F);

            _state.HoveredEntity = null;
            if (_state.DebugPool[GameBool.ShowCollision] || _state.DebugPool[GameBool.ShowShapes] || _state.DebugPool[GameBool.ShowLinks])
            {
                Vector2 mouseWorld = _state.input.MouseWorldPosition;

                // Search backwards (top to bottom) to find the top-most entity
                for (int i = _entityManager.AllEntities.Count - 1; i >= 0; i--)
                {
                    var entity = _entityManager.AllEntities[i];
                    if (entity.Prefab != null)
                    {
                        RectangleF bounds = new RectangleF(
                            entity.Position.X - entity.Prefab.Pivot.X,
                            entity.Position.Y - entity.Prefab.Pivot.Y,
                            entity.Prefab.SourceRect.Width,
                            entity.Prefab.SourceRect.Height);

                        if (bounds.Contains(mouseWorld))
                        {
                            _state.HoveredEntity = entity;
                            break;
                        }
                    }
                    else if (entity.BaseData is RectangleObject r && new RectangleF(r.Position, r.Size).Contains(mouseWorld))
                    {
                        _state.HoveredEntity = entity;
                        break;
                    }
                    else if (entity.BaseData is ShapeObject s && s.Shape.GetBounds().Contains(mouseWorld))
                    {
                        _state.HoveredEntity = entity;
                        break;
                    }
                }
            }

            if (_state.input.NewRightClick && !_state.IsInventoryOpen)
            {
                var heldStack = _state.Player.Inventory.GetSelectedItem();
                if (heldStack != null && heldStack.Count > 0)
                {
                    var physDef = _state.ItemManager.GetPhysicalDef(heldStack.ItemID);
                    if (physDef != null && physDef.IsPlaceable && physDef.Stages.Count > 1)
                    {
                        Vector2 pivot = new Vector2((physDef.CellWidth * 16) / 2f, physDef.CellHeight * 16);

                        // FIX: Use the Player's Interaction Center, NOT the Mouse Position!
                        Vector2 lookPos = _state.Player.GetInteractionBox().Center;
                        Vector2 snapPos = GridHelper.GetPlacementPosition(lookPos, physDef.PrecisionPlacement);

                        // FIX: Bounding Box Intersection to prevent crops overlapping
                        RectangleF placementBounds = new RectangleF(snapPos.X - pivot.X, snapPos.Y - pivot.Y, physDef.CellWidth * 16, physDef.CellHeight * 16);

                        GameEntity existingEntity = _entityManager.AllEntities.FirstOrDefault(e => {
                            if (e.BaseData is PlacedItemObject placed)
                            {
                                var otherDef = _state.ItemManager.GetPhysicalDef(placed.ItemID);
                                if (otherDef != null)
                                {
                                    Vector2 otherPivot = new Vector2((otherDef.CellWidth * 16) / 2f, otherDef.CellHeight * 16);
                                    RectangleF otherBounds = new RectangleF(e.Position.X - otherPivot.X, e.Position.Y - otherPivot.Y, otherDef.CellWidth * 16, otherDef.CellHeight * 16);
                                    return placementBounds.Intersects(otherBounds);
                                }
                            }
                            return false;
                        });

                        bool itemPlaced = false;

                        if (physDef.Type == PlacementType.Pile)
                        {
                            if (existingEntity != null && existingEntity.BaseData is PlacedItemObject placed && placed.ItemID == heldStack.ItemID)
                            {
                                int maxCapacity = physDef.Stages[physDef.Stages.Count - 1].ThresholdInt1;
                                if (placed.Amount < maxCapacity)
                                {
                                    placed.Amount++;
                                    placed.DaysInCurrentStage = 0;

                                    int nextStage = placed.CurrentStageIndex + 1;
                                    if (nextStage < physDef.Stages.Count && placed.Amount >= physDef.Stages[nextStage].ThresholdInt1)
                                    {
                                        placed.CurrentStageIndex = nextStage;
                                        _entityManager.RefreshEntityVisuals(placed.ID);
                                    }
                                    itemPlaced = true;
                                }
                            }
                            else if (existingEntity == null) { PlaceNewItem(heldStack.ItemID, snapPos, physDef); itemPlaced = true; }
                        }
                        else if (existingEntity == null)
                        {
                            PlaceNewItem(heldStack.ItemID, snapPos, physDef);
                            itemPlaced = true;
                        }

                        if (itemPlaced)
                        {
                            heldStack.Count--;
                            if (heldStack.Count <= 0) _state.Player.Inventory.Slots[_state.Player.Inventory.SelectedSlot] = null;
                        }
                    }
                }
            }
        }
        private void PlaceNewItem(int itemID, Vector2 pos, PhysicalItemDefinition physDef)
        {
            var placedObj = new PlacedItemObject
            {
                ID = Guid.NewGuid().ToString().Substring(0, 8),
                Name = $"Placed_{itemID}",
                ItemID = itemID,
                Position = pos,
                Amount = 1,
                DaysInCurrentStage = 0,
                CurrentStageIndex = 1
            };

            if (physDef.HasCollision)
                placedObj.Tags.Add(_state.tagManager.GetId("Solid"));

            // Add to Save Data
            _state.ActiveSave.PlacedItems.Add(placedObj);

            // Inject into Renderer instantly
            _entityManager.AddDynamicEntity(placedObj, _state);
        }
        private void SimulateMachines(GameTime gameTime)
        {
            float inGameHoursPassed = (float)gameTime.ElapsedGameTime.TotalSeconds * _state.TimeSystem.TimeScale;
            bool visualsNeedsRefresh = false;

            foreach (var placed in _state.ActiveSave.PlacedItems)
            {
                var physDef = _state.ItemManager.GetPhysicalDef(placed.ItemID);
                if (physDef != null && physDef.Type == PlacementType.Machine)
                {
                    // If not in the final stage (Finished state)
                    if (placed.CurrentStageIndex < physDef.Stages.Count - 1)
                    {
                        var stage = physDef.Stages[placed.CurrentStageIndex];

                        // Int1 = Processing Time (In-Game Hours)
                        if (stage.ThresholdInt1 > 0)
                        {
                            placed.ProcessingTimer += inGameHoursPassed;

                            if (placed.ProcessingTimer >= stage.ThresholdInt1)
                            {
                                placed.ProcessingTimer = 0f;
                                placed.CurrentStageIndex++;
                                visualsNeedsRefresh = true;
                                _entityManager.RefreshEntityVisuals(placed.ID);
                            }
                        }
                    }
                }
            }
        }

        private void OnNewDay(DayAdvancedCommand cmd)
        {
            var tickPayload = new InteractionPayload { ActionType = "DayTick", IntValue = cmd.DaysPassed };

            // Broadcast the day change to every entity in the world
            foreach (var entity in _entityManager.AllEntities.ToList())
            {
                _interactionEngine.SendPayload(entity, tickPayload);
            }
            _entityManager.RebuildStaticSprites(_state);

            System.Diagnostics.Debug.WriteLine($"Day Advanced! All Simulation Sprites Rebuilt.");
        }

        private void UpdateEntities(GameTime gameTime)
        {
            // Example: Make things with #vegetation sway based on wind
            var plants = _entityManager.GetByTag(SystemTags.VEG);
            float windSpeed = _state.Weather.Visuals.WindVector.X;
            SimulateMachines(gameTime);
        }

    }
}