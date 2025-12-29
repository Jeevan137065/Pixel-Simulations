using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Newtonsoft.Json.Converters;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Editor
{

    public class EditorController
    {
        public readonly EditorState _editorState;
        private readonly EditorUI _uiManager;
        private readonly EventBus _eventBus;

        public readonly HistoryController _historyController;
        public readonly ToolController _toolController;
        public  LayerController _layerController { get; }
        public readonly TilesetController _tilesetController;

        public bool ShouldExit { get; private set; }

        public EditorController(EditorUI uiManager, EventBus eventBus)
        {
            _eventBus = eventBus;
            _editorState = uiManager._editorState;
            _uiManager = uiManager;

            _historyController = new HistoryController(_eventBus, _editorState.History);
            _toolController = new ToolController(_eventBus, _editorState.ToolState);
            _layerController = new LayerController(_eventBus, _editorState);
            _tilesetController = new TilesetController(_eventBus, _editorState, _uiManager.gd);
        }
        public void Update(GameTime gameTime, KeyboardState keyboardState, MouseState mouseState)
        {
            // --- 1. ACTIVATE AND UPDATE THE INPUTSTATE ---
            UpdateInputState(keyboardState, mouseState);
            var input = _editorState.Input;
             
            
            // --- 2. Handle Global Editor Controls ---
            if (input.CurrentKeyboard.IsKeyDown(Keys.Escape)) { ShouldExit = true; }
            if (input.CurrentKeyboard.IsKeyDown(Keys.L) && input.PreviousKeyboard.IsKeyUp(Keys.L)) { _editorState.ShowDebug = !_editorState.ShowDebug; }            
            if (input.CurrentKeyboard.IsKeyDown(Keys.I))    { }
            // --- 3. Handle Camera Updates ---
            _editorState.camera.Update(input, _editorState._layoutmanager.ViewportPanel,true);

            UpdateUI(input, _eventBus);
            //HandleKeyboardCameraMovement( input, gameTime);
            // --- 4. Process Viewport/Tool Interaction ---
            if (_editorState.UI.ActivePanelName == "Viewport")
            {
                var activeTool = _editorState.ToolState.ActiveTool;
                if (activeTool != null)
                {
                    // The tool receives the EventBus so it can publish undoable commands
                    activeTool.Update(GetCurrentToolInput(), _eventBus);
                }
            }

        }

        private void UpdateInputState(KeyboardState kbs, MouseState ms)
        {
            var input = _editorState.Input;
            var layout = _editorState._layoutmanager;

            input.PreviousKeyboard = input.CurrentKeyboard;
            input.PreviousMouse = input.CurrentMouse;
            input.CurrentKeyboard = kbs;
            input.CurrentMouse = ms;
            input.MouseWindowPosition = ms.Position.ToVector2();

            if (layout.ViewportPanel.Contains(ms.Position))
            {
                Vector2 mouseInViewport = input.MouseWindowPosition - layout.ViewportPanel.Location.ToVector2();
                // We need to divide by the upscale factor to get native coordinates for the camera
                Vector2 mouseNative = mouseInViewport / 2f; // Assuming 2x upscale factor

                input.MouseWorldPosition = _editorState.camera .ScreenToWorld(mouseNative);
                input.MouseGridCell = new Point(
                    (int)System.Math.Floor(input.MouseWorldPosition.X / 16),
                    (int)System.Math.Floor(input.MouseWorldPosition.Y / 16)
                );
                input.MouseChunkCell = new Point(
                    (int)System.Math.Floor((double)input.MouseGridCell.X / Chunk.CHUNK_SIZE),
                    (int)System.Math.Floor((double)input.MouseGridCell.Y / Chunk.CHUNK_SIZE)
                );
            }
            else
            {
                input.MouseWorldPosition = Vector2.Zero;
                input.MouseGridCell = Point.Zero;
                input.MouseChunkCell = Point.Zero;
            }
        }
        public ToolInput GetCurrentToolInput()
        {
            return new ToolInput
            {
                CurrentMouse = _editorState.Input.CurrentMouse,
                PreviousMouse = _editorState.Input.PreviousMouse,
                CurrentKeyboard = _editorState.Input.CurrentKeyboard,
                WorldPosition = _editorState.Input.MouseWorldPosition,
                ActiveLayer = _editorState.Layers.GetActiveLayer(),
                ActiveBrush = _editorState.Selection.ActiveTileBrush
            };
        }
        public void UpdateUI(InputState input, EventBus bus)
        {
            _uiManager.TopPanel.Update(input, bus);
            _uiManager.ToolPanel.Update(input, bus);
            _uiManager.TilesetPanel.Update(input, bus);
            _uiManager.LayerPanel.Update(input, bus);

        }
        private void HandleKeyboardCameraMovement(InputState input, GameTime gameTime)
        {
            // The amount to move, in world pixels per second.
            // We scale it by the zoom level so navigation feels consistent.
            float moveSpeed = 400f / _editorState.camera.Zoom;
            Vector2 moveDirection = Vector2.Zero;

            if (input.CurrentKeyboard.IsKeyDown(Keys.Up))
                moveDirection.Y -= 1;
            if (input.CurrentKeyboard.IsKeyDown(Keys.Down))
                moveDirection.Y += 1;
            if (input.CurrentKeyboard.IsKeyDown(Keys.Left))
                moveDirection.X -= 1;
            if (input.CurrentKeyboard.IsKeyDown(Keys.Right))
                moveDirection.X += 1;

            if (moveDirection != Vector2.Zero)
            {
                // Normalize to prevent faster diagonal movement
                moveDirection.Normalize();

                // Calculate the final movement vector based on speed and frame time
                Vector2 movement = moveDirection * moveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;

                // Tell the camera to pan. The camera's Pan method is additive.
                //_editorState.camera.Pan(-movement); // Pan is inverted, so we negate the movement
            }
        }

    }

    public class HistoryController
    {
        private readonly HistoryState _historyState;
        public int counter = 0;
        public HistoryController(EventBus eventBus, HistoryState historyState)
        {
            _historyState = historyState;

            // Subscribe to any command that is undoable
            eventBus.Subscribe<IUndoableCommand>(HandleUndoableCommand);


            // Also subscribe to specific menu actions
            eventBus.Subscribe<MenuActionCommand>(HandleMenuAction);
        }

        private void HandleUndoableCommand(IUndoableCommand command)
        {
            counter++;
            command.Execute();
            _historyState.UndoStack.Push(command);
            _historyState.RedoStack.Clear();
        }

        private void HandleMenuAction(MenuActionCommand command)
        {
            counter++;
            if (command.ActionName == "Undo")
            {
                if (_historyState.UndoStack.TryPop(out IUndoableCommand undoCommand))
                {
                    undoCommand.Undo();
                    _historyState.RedoStack.Push(undoCommand);
                }
            }
            else if (command.ActionName == "Redo")
            {
                if (_historyState.RedoStack.TryPop(out IUndoableCommand redoCommand))
                {
                    redoCommand.Execute();
                    _historyState.UndoStack.Push(redoCommand);
                }
            }
        }
    }
    public class ToolController
    {
        private readonly ToolState _toolState;
        public int counter = 0;
        public ToolController(EventBus eventBus, ToolState toolState)
        {
            _toolState = toolState;
            eventBus.Subscribe<ChangeToolCommand>(HandleToolChange);
        }

        private void HandleToolChange(ChangeToolCommand command)
        {
            counter++;
            var newTool = _toolState.Tools.FirstOrDefault(t => t.Name == command.ToolName);
            if (newTool != null)
            {
                _toolState.ActiveTool = newTool;
                _toolState.ActiveToolName = newTool.Name;
            }
        }
    }

    public class LayerController
    {
        private readonly EditorState _editorState;
        public int counter = 0;
        public LayerController(EventBus eventBus, EditorState editorState)
        {
            _editorState = editorState;
            eventBus.Subscribe<SelectLayerCommand>(HandleLayerSelection);
            eventBus.Subscribe<ToggleLayerVisibilityCommand>(HandleLayerVisibility);
            eventBus.Subscribe<ToggleLayerLockCommand>(HandleLayerLock);
            eventBus.Subscribe<MoveLayerCommand>(HandleMoveLayer);
            eventBus.Subscribe<AddLayerCommand>(HandleAddLayer);
            eventBus.Subscribe<DeleteActiveLayerCommand>(HandleDeleteLayer);
        }

        private void HandleLayerSelection(SelectLayerCommand cmd)
        {
            if (cmd.LayerIndex >= 0 && cmd.LayerIndex < _editorState.ActiveMap.Layers.Count)
            {
                _editorState.Layers.ActiveLayerIndex = cmd.LayerIndex;
            }
        }
        private void HandleLayerVisibility(ToggleLayerVisibilityCommand cmd)
        {
            counter++;
            if (cmd.LayerIndex >= 0 && cmd.LayerIndex < _editorState.ActiveMap.Layers.Count)
            {
                var layer = _editorState.ActiveMap.Layers[cmd.LayerIndex];
                layer.IsVisible = !layer.IsVisible;
            }
        }
        private void HandleLayerLock(ToggleLayerLockCommand cmd)
        {
            counter++;
            if (cmd.LayerIndex >= 0 && cmd.LayerIndex < _editorState.ActiveMap.Layers.Count)
            {
                var layer = _editorState.ActiveMap.Layers[cmd.LayerIndex];
                layer.IsLocked = !layer.IsLocked;
            }
        }
        private void HandleMoveLayer(MoveLayerCommand cmd)
        {
            var activeIndex = _editorState.Layers.ActiveLayerIndex;
            counter++;
            if (cmd.Direction) // Move Up
            {
                _editorState.ActiveMap.MoveLayerUp(cmd.LayerIndex);
                if (activeIndex == cmd.LayerIndex) _editorState.Layers.ActiveLayerIndex--;
            }
            else
            {
                _editorState.ActiveMap.MoveLayerDown(cmd.LayerIndex);
                if (activeIndex == cmd.LayerIndex) _editorState.Layers.ActiveLayerIndex++;
            }
        }
        private void HandleAddLayer(AddLayerCommand cmd)
        {
            counter++;
            var activeIndex = _editorState.Layers.ActiveLayerIndex;
            var newLayer = new TileLayer($"Layer {_editorState.ActiveMap.Layers.Count + 1}");

            if (cmd.Direction) // Add Above
            {
                _editorState.ActiveMap.AddLayerAbove(activeIndex, newLayer);
            }
            else // Add Below
            {
                _editorState.ActiveMap.AddLayerBelow(activeIndex, newLayer);
                _editorState.Layers.ActiveLayerIndex++; // Keep selection on the original layer
            }
        }
        private void HandleDeleteLayer(DeleteActiveLayerCommand cmd)
        {
            counter--;
            var activeIndex = _editorState.Layers.ActiveLayerIndex;
            _editorState.ActiveMap.DeleteLayer(activeIndex);
            _editorState.Layers.ActiveLayerIndex = MathHelper.Clamp(activeIndex, 0, _editorState.ActiveMap.Layers.Count - 1);
        }
    }

    public class TilesetController
    {
        private readonly EditorState _editorState;
        private readonly GraphicsDevice _graphicsDevice;
        public int counter = 0;
        public TilesetController(EventBus eventBus, EditorState editorState, GraphicsDevice graphicsDevice)
        {
            _editorState = editorState;
            _graphicsDevice = graphicsDevice;

            eventBus.Subscribe<CreateTilesetCommand>(HandleCreateTileset);
            eventBus.Subscribe<SelectTilesetCommand>(HandleSelectTileset);
            eventBus.Subscribe<SelectTileCommand>(HandleSelectTile);
        }

        private void HandleCreateTileset(CreateTilesetCommand cmd)
        {
            counter++;
            if (_editorState.AvailableAtlasTextures.TryGetValue(cmd.AtlasName, out var texture))
            {
                if (_editorState.ActiveTileSets.Any(ts => ts.Name == cmd.AtlasName)) return;

                // For now, assume 16x16 and row-first. This could be data-driven later.
                var newTileset = new TileSet(cmd.AtlasName, texture, 16, _graphicsDevice, SliceMode.RowFirst);
                _editorState.ActiveTileSets.Add(newTileset);

                // If this is the first tileset, make it active
                if (_editorState.ActiveTileSets.Count == 1)
                {
                    SelectInitialTileset(newTileset);
                }
            }
        }

        private void HandleSelectTileset(SelectTilesetCommand cmd)
        {
            var panelState = _editorState.TilesetPanel;
            if (panelState.ActiveTilesetName != cmd.TilesetName)
            {
                var tileset = _editorState.ActiveTileSets.FirstOrDefault(ts => ts.Name == cmd.TilesetName);
                if (tileset != null)
                {
                    SelectInitialTileset(tileset);
                }
            }
        }

        private void HandleSelectTile(SelectTileCommand cmd)
        {
            counter++;
            var panelState = _editorState.TilesetPanel;
            var selectionState = _editorState.Selection;

            panelState.ActiveTilesetName = cmd.TilesetName;
            panelState.SelectedTileID = cmd.TileID;
            selectionState.ActiveTileBrush = new TileInfo(cmd.TilesetName, cmd.TileID);
        }

        private void SelectInitialTileset(TileSet tileset)
        {
            var panelState = _editorState.TilesetPanel;
            var selectionState = _editorState.Selection;

            panelState.ActiveTilesetName = tileset.Name;
            int firstId = tileset.SlicedAtlas.Any() ? tileset.SlicedAtlas.Keys.First() : -1;
            panelState.SelectedTileID = firstId;

            if (firstId != -1)
            {
                selectionState.ActiveTileBrush = new TileInfo(tileset.Name, firstId);
            }
            else
            {
                selectionState.ActiveTileBrush = null;
            }
        }
    }
}
