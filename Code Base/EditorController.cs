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
        private readonly TilesetPanel _tilesetPanel;
        //private LayerPanel _layerPanel;
        private readonly ToolPanel _toolPanel;
        private readonly GraphicsDevice _graphicsDevice;
        public bool ShouldExit { get; private set; }

        public EditorController(EditorUI uiManager)
        {
            _editorState = uiManager._editorState;
            _graphicsDevice = uiManager.gd;
            _uiManager = uiManager;
            _toolPanel = uiManager.ToolPanel;
            _tilesetPanel = uiManager.TilesetPanel;
            UIEvents.MenuActionClicked += HandleMenuAction;
            UIEvents.ToolButtonClicked += HandleToolChange;
            UIEvents.CreateTilesetFromAtlas += HandleCreateTileset;
            UIEvents.TileSelected += HandleTileSelected;
            UIEvents.LayerSelected += HandleLayerSelection;
            UIEvents.LayerVisibilityToggled += HandleLayerVisibilityToggle;
            UIEvents.LayerLockToggled += HandleLayerLockToggle;
            UIEvents.LayerActionRequested += HandleLayerAction;
            UIEvents.CommandCreated += HandleCommandCreated;
            UIEvents.MenuActionClicked += HandleMenuAction;

        }
        private void HandleCommandCreated(ICommand command)
        {
            // Execute the command immediately
            command.Execute();
            // Add it to the undo stack
            _editorState.History.UndoStack.Push(command);
            // Clear the redo stack, since we've made a new action
            _editorState.History.RedoStack.Clear();
        }
        private void HandleMenuAction(string actionName)
        {
            // This is where the logic happens!
            System.Diagnostics.Debug.WriteLine($"Menu Action received: {actionName}");
            switch (actionName)
            {
                case "SaveMap": 
                    //_editorState.SaveMap("Content/Maps/level1.map"); break;
                case "LoadMap":
                //_editorState.LoadMap("Content/Maps/level1.map"); break;
                case "Undo":
                    Undo();
                    break;
                case "Redo":
                    Redo();
                    break;
            }
        }
        private void Undo()
        {
            var history = _editorState.History;
            if (history.UndoStack.TryPop(out ICommand command))
            {
                command.Undo();
                history.RedoStack.Push(command);
            }
        }
        private void Redo()
        {
            var history = _editorState.History;
            if (history.RedoStack.TryPop(out ICommand command))
            {
                command.Execute();
                history.UndoStack.Push(command);
            }
        }
        private void HandleToolChange(string toolName)
        {
            System.Diagnostics.Debug.WriteLine($"Tool change requested: {toolName}");
            //_editorState.Tools.SetActiveTool(toolName);
        }
        private void HandleCreateTileset(string atlasName)
        {
            // For now, assume all new tilesets are 16x16 and row-first.
            // This could be passed from the UI in the future.
            CreateNewTileset(atlasName, 16, SliceMode.RowFirst);
        }
        private void UpdateTilesetPanelState()
        {
            var state = _editorState.TilesetPanel;
            var activeNames = _editorState.ActiveTileSets.Select(ts => ts.Name).ToList();

            // Find atlases that haven't been used to create a tileset yet
            state.UnusedAtlasNames = _editorState.AvailableAtlasTextures.Keys
                .Where(name => !activeNames.Contains(name)).ToList();
        }
        public void CreateNewTileset(string atlasName, int tileSize, SliceMode sliceMode)
        {
            if (_editorState.AvailableAtlasTextures.TryGetValue(atlasName, out var texture))
            {
                // Check if a tileset with this name already exists
                if (_editorState.ActiveTileSets.Any(ts => ts.Name == atlasName)) return;

                var newTileset = new TileSet(atlasName, texture, tileSize, _graphicsDevice, sliceMode);
                _editorState.ActiveTileSets.Add(newTileset);

                // If this is the first tileset, make it active
                if (_editorState.ActiveTileSets.Count == 1)
                {
                    _editorState.TilesetPanel.ActiveTilesetName = atlasName;
                    // Also select the first tile
                    int firstId = newTileset.SlicedAtlas.Keys.FirstOrDefault(-1);
                    _editorState.TilesetPanel.SelectedTileID = firstId;
                    _editorState.Selection.ActiveTileBrush = new TileInfo(atlasName, firstId);
                }
            }
        }
        private void HandleTileSelected(string tilesetName, int tileId)
        {
            _editorState.TilesetPanel.ActiveTilesetName = tilesetName;
            _editorState.TilesetPanel.SelectedTileID = tileId;
            // Update the active brush for painting tools
            _editorState.Selection.ActiveTileBrush = new TileInfo(tilesetName, tileId);
        }
        public ToolInput GetCurrentToolInput()
        {
            var mouseState = Mouse.GetState();
            var keyboardState = Keyboard.GetState();
            var layoutManager = _editorState._layoutmanager; 
            // Calculate mouse positions relative to the world and the native canvas
            Vector2 mouseWindowPos = mouseState.Position.ToVector2();
            Vector2 mouseCanvasNative = (mouseWindowPos - layoutManager.ViewportPanel.Location.ToVector2()) / 2;
            Vector2 worldPos = _editorState.camera.ScreenToWorld(mouseCanvasNative);
            Point gridCell = new Point((int)System.Math.Floor(worldPos.X / 16), (int)System.Math.Floor(worldPos.Y / 16)); // Use CELL_SIZE from somewhere

            return new ToolInput
            {
                CurrentMouse = mouseState,
                //PreviousMouse = _previousMouseState, // Need to store this in Controller
                CurrentKeyboard = keyboardState,
                WorldPosition = worldPos,
                ActiveLayer = _editorState.Layers.GetActiveLayer(),
                ActiveBrush = _editorState.Selection.ActiveTileBrush,
                // Add other context like mouse button states, keyboard modifiers, etc.
            };
        }
        private void HandleLayerSelection(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < _editorState.Layers.Layers.Count)
            {
                _editorState.Layers.ActiveLayerIndex = layerIndex;
            }
        }
        private void HandleLayerVisibilityToggle(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < _editorState.Layers.Layers.Count)
            {
                var layer = _editorState.Layers.Layers[layerIndex];
                layer.IsVisible = !layer.IsVisible;
            }
        }
        private void HandleLayerAction(string actionName)
        {
            var activeIndex = _editorState.Layers.ActiveLayerIndex;

            // Handle actions that include an index, like MoveUp:5
            if (actionName.Contains(':'))
            {
                var parts = actionName.Split(':');
                var name = parts[0];
                var index = int.Parse(parts[1]);

                switch (name)
                {
                    case "MoveUp":
                        _editorState.ActiveMap.MoveLayerUp(index);
                        // If we moved the active layer, update its index
                        if (activeIndex == index && index > 0) _editorState.Layers.ActiveLayerIndex--;
                        break;
                    case "MoveDown":
                        _editorState.ActiveMap.MoveLayerDown(index);
                        if (activeIndex == index && index < _editorState.Layers.Layers.Count - 1) _editorState.Layers.ActiveLayerIndex++;
                        break;
                }
                return;
            }

            // Handle global actions
            switch (actionName)
            {
                case "AddUp":
                    _editorState.ActiveMap.AddLayerAbove(activeIndex, new TileLayer($"Layer {_editorState.ActiveMap.Layers.Count + 1}"));
                    break;
                case "AddDown":
                    _editorState.ActiveMap.AddLayerBelow(activeIndex, new TileLayer($"Layer {_editorState.ActiveMap.Layers.Count + 1}"));
                    // When adding below, the active layer's index increases
                    _editorState.Layers.ActiveLayerIndex++;
                    break;
                case "Delete":
                    _editorState.ActiveMap.DeleteLayer(activeIndex);
                    // Clamp active index after deletion
                    _editorState.Layers.ActiveLayerIndex = MathHelper.Clamp(activeIndex, 0, _editorState.ActiveMap.Layers.Count - 1);
                    break;
            }
        }
        private void HandleLayerLockToggle(int layerIndex)
        {
            if (layerIndex >= 0 && layerIndex < _editorState.Layers.Layers.Count)
            {
                var layer = _editorState.Layers.Layers[layerIndex];
                layer.IsLocked = !layer.IsLocked;
            }
        }
        public void Update(GameTime gameTime, KeyboardState keyboardState, MouseState mouseState)
        {
            // --- 1. ACTIVATE AND UPDATE THE INPUTSTATE ---
            UpdateInputState(keyboardState, mouseState);
            var input = _editorState.Input;
            var layout = _editorState._layoutmanager;

            // Update raw input states
            input.CurrentMouse = mouseState;
            input.CurrentKeyboard = keyboardState;
            input.MouseWindowPosition = mouseState.Position.ToVector2();
            if (layout.ViewportPanel.Contains(input.CurrentMouse.Position)) 
            {
                //_editorState.camera.ScreenToWorld(mouseState.Position.ToVector2() - _editorState._layoutmanager.ViewportPanel.Location.ToVector2() / _editorState._layoutmanager.UpscaleFactor);
                input.MouseWorldPosition =  _editorState.camera.ScreenToWorld(new Vector2((int)Math.Floor((input.MouseWindowPosition.X - layout.ViewportPanel.X)/layout.UpscaleFactor), (int)Math.Floor((input.MouseWindowPosition.Y - layout.ViewportPanel.Y) /layout.UpscaleFactor)));// Corrected to use camera and viewport offset
                input.MouseGridCell = new Point((int)Math.Floor(input.MouseWorldPosition.X / 16), (int)Math.Floor(input.MouseWorldPosition.Y / 16));
            }
            // --- 2. Handle Global Editor Controls ---
            if (input.CurrentKeyboard.IsKeyDown(Keys.Escape)) { ShouldExit = true; }
            if (input.CurrentKeyboard.IsKeyDown(Keys.L) && input.PreviousKeyboard.IsKeyUp(Keys.L))
            {
                _editorState.ShowDebug = !_editorState.ShowDebug;
            }
            if (input.CurrentKeyboard.IsKeyDown(Keys.I))    { _editorState.x++; }
            // --- 3. Handle Camera Updates ---
            _editorState.camera.Update(input, layout.ViewportPanel);
            // --- 4. Process Tool and UI Interactions ---
            // (This logic will be added in future steps, but the InputState is now ready for it)
            string activePanelName = layout.GetPanelAt(input.MouseWindowPosition.ToPoint());
            _editorState.UI.ActivePanelName = activePanelName;

            if (activePanelName == "Viewport")
            {
                var activeTool = _editorState.Tools.ActiveTool;
                var activeLayer = _editorState.Layers.GetActiveLayer();
                var activeBrush = _editorState.Selection.ActiveTileBrush;

                // Package the context into a ToolInput struct
                var toolInput = new ToolInput
                {
                    CurrentMouse = input.CurrentMouse,
                    PreviousMouse = input.PreviousMouse,
                    CurrentKeyboard = input.CurrentKeyboard,
                    WorldPosition = input.MouseWorldPosition,
                    ActiveLayer = activeLayer,
                    ActiveBrush = activeBrush
                };

                if (activeTool != null && activeLayer != null && !activeLayer.IsLocked)
                {
                    // The tool's Update method contains all its own logic for clicks, drags, etc.
                    activeTool.Update(toolInput);
                }
            }
            if (activePanelName == "Tileset")
            {
                _uiManager.TilesetPanel.Update(input);
            }
            if (activePanelName == "Top")
            {
                _uiManager.TopPanel.Update(input);
            }
            else if (activePanelName == "Tool")
            {
                _uiManager.ToolPanel.Update(input);
            }
            else if(activePanelName == "Layer")
            {
                _uiManager.LayerPanel.Update(input);
            }

            input.PreviousMouse = input.CurrentMouse;
            input.PreviousKeyboard = input.CurrentKeyboard;

        }
        private void UpdateInputState(KeyboardState kbs, MouseState ms)
        {
            // Store previous states before updating the new ones
            _editorState.Input.PreviousKeyboard = _editorState.Input.CurrentKeyboard;
            _editorState.Input.PreviousMouse = _editorState.Input.CurrentMouse;

            // Store current raw states
            _editorState.Input.CurrentKeyboard = kbs;
            _editorState.Input.CurrentMouse = ms;

            // --- Calculate Processed, Contextual Data ---

            _editorState.Input.MouseWindowPosition = ms.Position.ToVector2();

            // Calculate world position only if the mouse is inside the viewport
            if (_editorState._layoutmanager.ViewportPanel.Contains(ms.Position))
            {
                // Convert mouse position from window-space to viewport-relative screen-space
                Vector2 mouseInViewport = _editorState.Input.MouseWindowPosition - _editorState._layoutmanager.ViewportPanel.Location.ToVector2();

                // Use the camera to convert that to world-space
                _editorState.Input.MouseWorldPosition = _editorState.camera.ScreenToWorld(mouseInViewport);

                // Calculate the grid cell based on the world position
                _editorState.Input.MouseGridCell = new Point(
                    (int)System.Math.Floor(_editorState.Input.MouseWorldPosition.X / 16), // Assuming 16 is cell size
                    (int)System.Math.Floor(_editorState.Input.MouseWorldPosition.Y / 16)
                );
            }
            else
            {
                // If the mouse is outside the viewport, world/grid coordinates are not applicable
                _editorState.Input.MouseWorldPosition = Vector2.Zero;
                _editorState.Input.MouseGridCell = Point.Zero;
            }
        }


    }

}
