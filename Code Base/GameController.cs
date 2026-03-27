using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace Pixel_Simulations
{
    public class GameController
    {
        private readonly GameState _state;
        private readonly EntityManager _entityManager;

        private bool _fKeyPressedLastFrame = false;

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

        private void HandleInteractions()
        {

            var kstate = Keyboard.GetState();
            // Handle Pause Toggle
            if (kstate.IsKeyDown(Keys.Escape) && !_fKeyPressedLastFrame)
            {
                _state.DebugPool[GameBool.IsPaused] = !_state.DebugPool[GameBool.IsPaused];
            }
            _fKeyPressedLastFrame = kstate.IsKeyDown(Keys.Escape);

            // If paused, halt all game simulation updates!
            if (_state.DebugPool[GameBool.IsPaused]) return;
            if (kstate.IsKeyDown(Keys.F) && !_fKeyPressedLastFrame)
            {
                var interactBox = _state.Player.GetInteractionBox();

                // Fast query! Get all objects that can be interacted with (e.g., #harvestable, #container)
                var interactables = _entityManager.GetByTag("#harvestable");

                foreach (var entity in interactables)
                {
                    if (entity.Prefab != null)
                    {
                        var bounds = new RectangleF(
                            entity.Position.X - entity.Prefab.Pivot.X,
                            entity.Position.Y - entity.Prefab.Pivot.Y,
                            entity.Prefab.SourceRect.Width,
                            entity.Prefab.SourceRect.Height);

                        if (bounds.Intersects(interactBox))
                        {
                            // Trigger logic, remove entity, resolve links!
                            foreach (var linkId in entity.BaseData.LinkedObjects)
                                _entityManager.RemoveEntity(linkId); // Kills the collision box instantly

                            _entityManager.RemoveEntity(entity.BaseData.ID);
                            _state.Physics.LoadMapData(_state.CurrentMap); // Rebuild physics
                            break;
                        }
                    }
                }
            }
            _fKeyPressedLastFrame = kstate.IsKeyDown(Keys.F);
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