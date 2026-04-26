using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;

namespace Pixel_Simulations
{
    public class GameController
    {
        private readonly GameState _state;
        private readonly EntityManager _entityManager;

        private bool _fKeyPressedLastFrame = false;
        private bool _iKeyPressedLastFrame = false;

        public GameController(GameState state, EntityManager entityManager)
        {
            _state = state;
            _entityManager = entityManager;
        }

        public void Update(GameTime gameTime)
        {
            _state.Update(gameTime);
            // 4. Handle Interactions
            HandleInteractions();

            // 5. Update Entity Logic (e.g., Swaying trees, moving NPCs)
            UpdateEntities(gameTime);
        }
        // Inside GameController.cs
        private void TryInteract()
        {
            RectangleF interactBox = _state.Player.GetInteractionBox();
            GameEntity targetEntity = null;

            // 1. Find targeted Entity
            foreach (var entity in _entityManager.AllEntities)
            {
                if (entity.Prefab != null)
                {
                    RectangleF bounds = new RectangleF(
                        entity.Position.X - entity.Prefab.Pivot.X,
                        entity.Position.Y - entity.Prefab.Pivot.Y,
                        entity.Prefab.SourceRect.Width,
                        entity.Prefab.SourceRect.Height);

                    if (bounds.Intersects(interactBox))
                    {
                        targetEntity = entity;
                        break;
                    }
                }
            }

            if (targetEntity == null) return;

            // 2. Identify Subject (Held Item)
            var heldStack = _state.Player.Inventory.GetSelectedItem();
            ItemDefinition heldItem = null;
            if (heldStack != null && heldStack.Count > 0)
            {
                heldItem = _state.ItemManager.GetItem(heldStack.ItemID);
            }

            // 3. Evaluate Rule
            var rule = _state.Interactions.Evaluate(targetEntity, heldItem);
            if (rule == null) return; // No interaction available

            // 4. APPLY RESULTS
            System.Diagnostics.Debug.WriteLine($"Interaction Executed: {rule.RuleID}");

            // Result A: Loot
            int lootID = rule.DefaultLootID;
            if (!string.IsNullOrEmpty(rule.LootPropertyKey))
            {
                if (int.TryParse(targetEntity.GetProperty(rule.LootPropertyKey, "-1"), out int id))
                    lootID = id;
            }

            if (lootID != -1)
            {
                int left = _state.Player.Inventory.AddItem(lootID, rule.LootAmount, _state.ItemManager);
                if (left > 0) System.Diagnostics.Debug.WriteLine("Inventory Full! Dropping items is not yet implemented.");
            }

            // Result B: Property Mutation & Visual State Changes
            bool propertiesChanged = false;
            foreach (var kvp in rule.SetProperties)
            {
                // Modify or add the property dynamically
                targetEntity.BaseData.Properties[kvp.Key] = new MapProperty(PropertyType.String, kvp.Value);
                propertiesChanged = true;
            }

            if (rule.SpriteOffset != Point.Zero)
            {
                targetEntity.BaseData.Properties["SpriteOffset_X"] = new MapProperty(PropertyType.Integer, rule.SpriteOffset.X.ToString());
                targetEntity.BaseData.Properties["SpriteOffset_Y"] = new MapProperty(PropertyType.Integer, rule.SpriteOffset.Y.ToString());
                propertiesChanged = true;
            }

            if (propertiesChanged)
            {
                _entityManager.RefreshEntityVisuals(targetEntity.BaseData.ID);
            }

            // Result C: Destruction
            if (rule.DestroyTarget)
            {
                // 1. Destroy Collision Links
                foreach (string linkID in targetEntity.BaseData.LinkedObjects)
                    _entityManager.RemoveEntity(linkID);

                // 2. Is this a Player-Placed item or a Base-Map item?
                if (targetEntity.BaseData is PlacedItemObject placedObj)
                {
                    // Remove from Save File
                    _state.ActiveSave.PlacedItems.Remove(placedObj);
                }
                else
                {
                    // Add to Destroyed list so it doesn't load next time
                    _state.ActiveSave.DestroyedBaseIDs.Add(targetEntity.BaseData.ID);
                }

                // 3. Remove from Engine and Refresh Physics
                _entityManager.RemoveEntity(targetEntity.BaseData.ID);
                // Note: You might need to update PhysicsManager to ignore destroyed IDs too, 
                // or just clear and rebuild it using the same logic as EntityManager.
            }
        }
        private void HandleInteractions()
        {

            var kstate = Keyboard.GetState();
            // Handle Pause Toggle
            if (kstate.IsKeyDown(Keys.Escape) && !_fKeyPressedLastFrame)
            {
                _state.DebugPool[GameBool.IsPaused] = !_state.DebugPool[GameBool.IsPaused];
            }
            if (kstate.IsKeyDown(Keys.F5) && !_fKeyPressedLastFrame)
            {
                _state.SaveGame();
            }
            _fKeyPressedLastFrame = kstate.IsKeyDown(Keys.Escape);
            if (kstate.IsKeyDown(Keys.I) && !_iKeyPressedLastFrame)
            {
                _state.IsInventoryOpen = !_state.IsInventoryOpen;
            }
            _iKeyPressedLastFrame = kstate.IsKeyDown(Keys.I);
            // If paused, halt all game simulation updates!
            if (_state.DebugPool[GameBool.IsPaused]) return;
            if (_state.input.NewLeftClick)
            {
                // Resolves 1920x1080 screen coordinates against inventory slots
                _state.Player.Inventory.HandleMouseInteraction(
                    _state.input.MouseScreenPosition.ToPoint(),
                    true,
                    new Vector2(1920, 1080), _state.IsInventoryOpen); // Pass your final screen resolution here
            }
            if (kstate.IsKeyDown(Keys.F) && !_fKeyPressedLastFrame)
            {
                TryInteract();
            }

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
                    if (physDef != null && physDef.IsPlaceable)
                    {
                        // 1. Snap mouse position to 16x16 grid (Bottom-Center Pivot)
                        Vector2 snapPos = new Vector2(
                            (float)Math.Floor(_state.input.MouseWorldPosition.X / 16) * 16 + 8,
                            (float)Math.Floor(_state.input.MouseWorldPosition.Y / 16) * 16 + 16
                        );

                        // 2. Check if a placed object is already there
                        GameEntity existingEntity = null;
                        foreach (var e in _entityManager.AllEntities)
                        {
                            if (e.BaseData is PlacedItemObject && Vector2.Distance(e.Position, snapPos) < 4f)
                            {
                                existingEntity = e; break;
                            }
                        }

                        bool itemPlaced = false;

                        // 3. PILE LOGIC (Stacking Wood)
                        if (physDef.Type == PlacementType.Pile)
                        {
                            if (existingEntity != null && existingEntity.BaseData is PlacedItemObject placed && placed.ItemID == heldStack.ItemID)
                            {
                                placed.Amount++;

                                // Check threshold for next visual stage
                                int nextStage = placed.CurrentStageIndex + 1;
                                if (nextStage < physDef.Stages.Count && placed.Amount >= physDef.Stages[nextStage].ThresholdInt1)
                                {
                                    placed.CurrentStageIndex = nextStage;
                                    _entityManager.RebuildStaticSprites(_state); // Refresh visuals
                                }
                                itemPlaced = true;
                            }
                            else if (existingEntity == null)
                            {
                                PlaceNewItem(heldStack.ItemID, snapPos, physDef);
                                itemPlaced = true;
                            }
                        }
                        // 4. CROP / PROP LOGIC
                        else if (existingEntity == null)
                        {
                            PlaceNewItem(heldStack.ItemID, snapPos, physDef);
                            itemPlaced = true;
                        }

                        // 5. Deduct from Inventory
                        if (itemPlaced)
                        {
                            float avgWeight = heldStack.TotalWeight / heldStack.Count;
                            heldStack.Count--;
                            heldStack.TotalWeight -= avgWeight;

                            if (heldStack.Count <= 0)
                                _state.Player.Inventory.Slots[_state.Player.Inventory.SelectedSlot] = null;
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
                DaysAlive = 0,
                CurrentStageIndex = 0
            };

            if (physDef.HasCollision)
                placedObj.Tags.Add(_state.tagManager.GetId("Solid"));

            // Add to Save Data
            _state.ActiveSave.PlacedItems.Add(placedObj);

            // Inject into Renderer instantly
            _entityManager.AddDynamicEntity(placedObj, _state);
        }
        private void UpdateEntities(GameTime gameTime)
        {
            // Example: Make things with #vegetation sway based on wind
            var plants = _entityManager.GetByTag(SystemTags.VEG);
            float windSpeed = _state.Weather.Visuals.WindVector.X;
            // Apply wind logic to those specific entities...
        }

    }
}