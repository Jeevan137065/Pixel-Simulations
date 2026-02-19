using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Newtonsoft.Json.Converters;
using Pixel_Simulations.Data;
using Pixel_Simulations;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Pixel_Simulations.Editor
{
    public class EditorController
    {
        public readonly EditorState _editorState;
        private readonly EditorUI _uiManager;
        private readonly EventBus _eventBus;

        public readonly HistoryController _historyController;
        public readonly ToolController _toolController;
        public LayerController _layerController { get; }
        public readonly TilesetController _tilesetController;
        public MapController _mapController {get; }
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
            _mapController = new MapController(_eventBus, _editorState);
        }
        public void Update(GameTime gameTime, KeyboardState keyboardState, MouseState mouseState)
        {
            _editorState.refresh(gameTime);
            // --- 1. ACTIVATE AND UPDATE THE INPUTSTATE ---
            UpdateInputState(keyboardState, mouseState);
            var input = _editorState.Input;
            
            //_editorState.Input.Update(gameTime);

            // --- 2. Handle Global Editor Controls ---
            if (input.CurrentKeyboard.IsKeyDown(Keys.Escape)) { ShouldExit = true; }
            if (input.CurrentKeyboard.IsKeyDown(Keys.L) && input.PreviousKeyboard.IsKeyUp(Keys.L)) { _editorState.ShowDebug = !_editorState.ShowDebug; }
            if (input.CurrentKeyboard.IsKeyDown(Keys.K) && input.PreviousKeyboard.IsKeyUp(Keys.K)) { _editorState.ShowGrid = !_editorState.ShowGrid; }
            if (input.CurrentKeyboard.IsKeyDown(Keys.I)) { }
            
            // --- 3. Handle Camera Updates ---
            _editorState.camera.Update(input, _editorState._layoutmanager.ViewportPanel, true);

            UpdateUI(input, _eventBus);
            HandleGlobalShortcuts(input, _eventBus);
            HandleKeyboardCameraMovement( input, gameTime);
            // --- 4. Process Viewport/Tool Interaction ---
            if (_editorState.PrefabCreator.IsOpen)
            {
                _uiManager.PrefabPanel.Update(input, _eventBus);
                // Block keys from reaching the map/camera while typing name
                return;
            }
            if (_editorState.UI.ActivePanelName == "Viewport")
            {
                var activeTool = _editorState.ToolState.ActiveTool;

                if (activeTool != null)
                {
                    // The tool receives the EventBus so it can publish undoable commands
                    activeTool.Update(GetCurrentToolInput(), input, _eventBus,_editorState);
                }
            }

        }
        private void HandleGlobalShortcuts(InputState input, EventBus eventBus)
        {
            var kbs = input.CurrentKeyboard;
            var prevKbs = input.PreviousKeyboard;

            // --- Tool Selection Shortcuts ---
            // We check for IsKeyUp -> IsKeyDown to ensure the action only fires once per key press.
            if (kbs.IsKeyDown(Keys.B) && prevKbs.IsKeyUp(Keys.B))
                eventBus.Publish(new ChangeToolCommand { ToolName = "Brush" });

            if (kbs.IsKeyDown(Keys.E) && prevKbs.IsKeyUp(Keys.E))
                eventBus.Publish(new ChangeToolCommand { ToolName = "Eraser" });

            if (kbs.IsKeyDown(Keys.R) && prevKbs.IsKeyUp(Keys.R))
                eventBus.Publish(new ChangeToolCommand { ToolName = "FreeRectangle" });

            if (kbs.IsKeyDown(Keys.T) && prevKbs.IsKeyUp(Keys.T))
                eventBus.Publish(new ChangeToolCommand { ToolName = "GridRectangle" });

            if (kbs.IsKeyDown(Keys.P) && prevKbs.IsKeyUp(Keys.P))
                eventBus.Publish(new ChangeToolCommand { ToolName = "PointPlacer" });

            // --- Other Tool Shortcuts (Commented out for now) ---
            // if (kbs.IsKeyDown(Keys.F) && prevKbs.IsKeyUp(Keys.F)) eventBus.Publish(new ChangeToolCommand { ToolName = "Fill" });
            // if (kbs.IsKeyDown(Keys.L) && prevKbs.IsKeyUp(Keys.L)) eventBus.Publish(new ChangeToolCommand { ToolName = "Line" });
            // if (kbs.IsKeyDown(Keys.I) && prevKbs.IsKeyUp(Keys.I)) eventBus.Publish(new ChangeToolCommand { ToolName = "Eyedropper" });
            // if (kbs.IsKeyDown(Keys.M) && prevKbs.IsKeyUp(Keys.M)) eventBus.Publish(new ChangeToolCommand { ToolName = "Selection" });


            // --- File Menu Shortcuts ---
            bool isCtrlDown = kbs.IsKeyDown(Keys.LeftControl) || kbs.IsKeyDown(Keys.RightControl);
            bool isShiftDown = kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift);

            // Ctrl + S = Save
            if (isCtrlDown && kbs.IsKeyDown(Keys.S) && prevKbs.IsKeyUp(Keys.S))
            {
                eventBus.Publish(new MenuActionCommand { ActionName = "Save" });
            }

            // Shift + S = Export (using a different action name)
            if (isShiftDown && kbs.IsKeyDown(Keys.S) && prevKbs.IsKeyUp(Keys.S))
            {
                eventBus.Publish(new MenuActionCommand { ActionName = "Export" });
            }

            // Ctrl + O = Load (Using 'O' for Open is more standard than 'A')
            if (isCtrlDown && kbs.IsKeyDown(Keys.O) && prevKbs.IsKeyUp(Keys.O))
            {
                eventBus.Publish(new MenuActionCommand { ActionName = "Load" });
            }

            // Ctrl + Z = Undo
            if (isCtrlDown && kbs.IsKeyDown(Keys.Z) && prevKbs.IsKeyUp(Keys.Z))
            {
                eventBus.Publish(new MenuActionCommand { ActionName = "Undo" });
            }

            // Ctrl + Y = Redo
            if (isCtrlDown && kbs.IsKeyDown(Keys.Y) && prevKbs.IsKeyUp(Keys.Y))
            {
                eventBus.Publish(new MenuActionCommand { ActionName = "Redo" });
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

                input.MouseWorldPosition = _editorState.camera.ScreenToWorld(mouseNative);
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
                moveDirection.Y += 1;
            if (input.CurrentKeyboard.IsKeyDown(Keys.Down))
                moveDirection.Y -= 1;
            if (input.CurrentKeyboard.IsKeyDown(Keys.Left))
                moveDirection.X += 1;
            if (input.CurrentKeyboard.IsKeyDown(Keys.Right))
                moveDirection.X -= 1;

            if (moveDirection != Vector2.Zero)
            {
                // Normalize to prevent faster diagonal movement
                moveDirection.Normalize();

                // Calculate the final movement vector based on speed and frame time
                Vector2 movement = moveDirection * moveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;

                // Tell the camera to pan. The camera's Pan method is additive.
                _editorState.camera.Pan(-movement); // Pan is inverted, so we negate the movement
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
            eventBus.Subscribe<CycleNewLayerTypeCommand>(HandleCycleNewLayerType);
            eventBus.Subscribe<ToggleLayerExpansionCommand>(HandleLayerExpansion);
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
            var newLayerType = _editorState.Layers.NewLayerType;
            Layer newLayer;

            // Create the correct type of layer based on the state
            string name = $"{newLayerType}{_editorState.ActiveMap.Layers.Count + 1}";
            switch (newLayerType)
            {
                case LayerType.Tile:
                    newLayer = new TileLayer(name);
                    break;
                case LayerType.Object:
                    newLayer = new ObjectLayer(name);
                    break;
                case LayerType.Collision:
                    newLayer = new CollisionLayer(name);
                    break;
                case LayerType.Navigation:
                    newLayer = new NavigationLayer(name);
                    break;
                case LayerType.Trigger:
                    newLayer = new TriggerLayer(name);
                    break;
                default:
                    newLayer = new TileLayer(name);
                    break;
            }
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

        private void HandleCycleNewLayerType(CycleNewLayerTypeCommand cmd)
        {
            var panelState = _editorState.Layers;

            // Get all values of the LayerType enum
            var layerTypes = System.Enum.GetValues(typeof(LayerType)).Cast<LayerType>().ToList();

            // Find the index of the current type
            int currentIndex = layerTypes.IndexOf(panelState.NewLayerType);

            // Get the next index, wrapping around if necessary
            int nextIndex = (currentIndex + 1) % layerTypes.Count;

            // Update the state
            panelState.NewLayerType = layerTypes[nextIndex];
        }

        private void HandleLayerExpansion(ToggleLayerExpansionCommand cmd)
        {
            if (cmd.LayerIndex >= 0 && cmd.LayerIndex < _editorState.ActiveMap.Layers.Count)
            {
                var layer = _editorState.ActiveMap.Layers[cmd.LayerIndex];
                layer.IsExpanded = !layer.IsExpanded;
            }
        }

    }
    public class TilesetController
    {
        private readonly EditorState _editorState;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly EventBus _eventBus; // Added to publish side-effects
        public int counter = 0;

        public TilesetController(EventBus eventBus, EditorState editorState, GraphicsDevice graphicsDevice)
        {
            _editorState = editorState;
            _graphicsDevice = graphicsDevice;
            _eventBus = eventBus;

            eventBus.Subscribe<CreateTilesetCommand>(HandleCreateTileset);
            eventBus.Subscribe<SelectTilesetCommand>(HandleSelectTileset);
            eventBus.Subscribe<SelectTileCommand>(HandleSelectTile);
            eventBus.Subscribe<SelectPrefabCommand>(HandleSelectPrefab);
            eventBus.Subscribe<OpenPrefabCreatorCommand>(HandleOpenCreator);
            eventBus.Subscribe<OpenAtlasPickerCommand>(HandleOpenAtlasPicker);
            eventBus.Subscribe<SavePrefabCommand>(HandleSavePrefab);
            eventBus.Subscribe<ClosePrefabCreatorCommand>(HandleCloseCreator);
            eventBus.Subscribe<DeletePrefabCommand>(HandleDeletePrefab);
        }

        private void HandleCreateTileset(CreateTilesetCommand cmd)
        {
            var texture = _editorState.AssetLibrary.GetAtlas(cmd.AtlasName);
            if (texture != null)
            {
                if (_editorState.ActiveTileSets.Any(ts => ts.Name == cmd.AtlasName)) return;

                var newTileset = new TileSet(cmd.AtlasName, texture, 16, _graphicsDevice, SliceMode.RowFirst);
                _editorState.ActiveTileSets.Add(newTileset);
                _editorState.TilesetManager.RegisterTileSet(newTileset);

                // Set as active
                _editorState.TilesetPanel.ActiveTilesetName = cmd.AtlasName;
                SelectInitialTileset(newTileset);
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
        private void HandleOpenAtlasPicker(OpenAtlasPickerCommand cmd)
        {
            // Only get atlases marked for TILES
            var tileAtlases = _editorState.AssetLibrary.GetNamesByType(AtlasType.Tile);
            var activeNames = _editorState.ActiveTileSets.Select(ts => ts.Name).ToList();

            var next = tileAtlases.FirstOrDefault(n => !activeNames.Contains(n));
            if (next != null)
                _eventBus.Publish(new CreateTilesetCommand { AtlasName = next });
        }
        private void HandleSelectTile(SelectTileCommand cmd)
        {
            counter++;
            _editorState.TilesetPanel.ActiveTilesetName = cmd.TilesetName;
            _editorState.TilesetPanel.SelectedTileID = cmd.TileID;
            _editorState.Selection.ActiveTileBrush = new TileInfo(cmd.TilesetName, cmd.TileID);

            // Ensure we are using the Brush tool when a tile is selected
            _eventBus.Publish(new ChangeToolCommand { ToolName = "Brush" });
        }
        private void SelectInitialTileset(TileSet tileset)
        {
            var panelState = _editorState.TilesetPanel;
            panelState.ActiveTilesetName = tileset.Name;
            int firstId = tileset.SlicedAtlas.Any() ? tileset.SlicedAtlas.Keys.First() : -1;
            panelState.SelectedTileID = firstId;

            if (firstId != -1)
                _editorState.Selection.ActiveTileBrush = new TileInfo(tileset.Name, firstId);
        }

        private void HandleSelectPrefab(SelectPrefabCommand cmd)
        {
            var prefab = _editorState.PrefabManager.GetPrefab(cmd.PrefabID);
            if (prefab != null)
            {
                _editorState.Selection.ActivePrefab = prefab;

                // LOAD into Creator Context so we can Modify or Delete it
                var ctx = _editorState.PrefabCreator;
                ctx.TempName = prefab.ID;
                ctx.TempTags = string.Join(", ", prefab.Tags);
                ctx.SelectionRect = prefab.SourceRect;
                ctx.ActiveAtlasName = prefab.AtlasName;

                _eventBus.Publish(new ChangeToolCommand { ToolName = "ObjectPlacer" });
            }
        }

        private void HandleOpenCreator(OpenPrefabCreatorCommand cmd)
        {
            var ctx = _editorState.PrefabCreator;
            ctx.IsOpen = true;

            // RESET the context for a fresh object
            ctx.TempName = "New_Object_" + (_editorState.PrefabManager.Prefabs.Count + 1);
            ctx.TempTags = "tag";
            ctx.SelectionRect = new Rectangle(0, 0, 16, 16);
            ctx.ActiveAtlasName = cmd.AtlasName ?? "Basic";

            // Deselect active prefab so we don't accidentally overwrite it
            _editorState.Selection.ActivePrefab = null;
        }
        private void HandleSavePrefab(SavePrefabCommand cmd)
        {
            var ctx = _editorState.PrefabCreator;
            if (string.IsNullOrEmpty(ctx.TempName)) return;

            // Check for "New" mode conflicts
            if (cmd.Mode == "New" && _editorState.PrefabManager.Prefabs.ContainsKey(ctx.TempName))
            {
                ctx.TempName += "_Copy"; // Auto-rename to prevent block
            }

            var prefab = new ObjectPrefab
            {
                ID = ctx.TempName,
                AtlasName = ctx.ActiveAtlasName,
                SourceRect = ctx.SelectionRect,
                Tags = ctx.TempTags.Split(',').Select(t => t.Trim()).ToList(),
                Pivot = new Vector2(ctx.SelectionRect.Width / 2, ctx.SelectionRect.Height)
            };

            _editorState.PrefabManager.Prefabs[prefab.ID] = prefab;
            _editorState.PrefabManager.Save(Path.Combine(PathHelper.GetAssetsPath(), "Data", "objects.json"));
        }
        private void HandleDeletePrefab(DeletePrefabCommand cmd)
        {
            var ctx = _editorState.PrefabCreator;
            if (_editorState.PrefabManager.Prefabs.Remove(ctx.TempName))
            {
                _editorState.PrefabManager.Save(Path.Combine(PathHelper.GetAssetsPath(), "Data", "objects.json"));
                _editorState.Selection.ActivePrefab = null;

                // Reset to default name so the user can keep creating
                ctx.TempName = "New_Object_" + (_editorState.PrefabManager.Prefabs.Count + 1);
            }
        }
        private void HandleCloseCreator(ClosePrefabCreatorCommand cmd)
        {
            _editorState.PrefabCreator.IsOpen = false;
        }
    }
    public class MapController
    {
        private readonly EditorState _editorState;
        public string savedPath;
        public MapController(EventBus eventBus, EditorState editorState)
        {
            _editorState = editorState;
            eventBus.Subscribe<MenuActionCommand>(HandleMenuAction);
            savedPath = PathHelper.GetSolutionRoot();
        }

        private void HandleMenuAction(MenuActionCommand cmd)
        {
            string assetsPath = PathHelper.GetAssetsPath();
            if (string.IsNullOrEmpty(assetsPath)) return;

            // Define the paths once
            string jsonPath = Path.Combine(assetsPath, "Maps", "level1.json");
            string gameMapPath = Path.Combine(assetsPath, "Maps", "level1.map"); // Adjust

            switch (cmd.ActionName)
            {
                case "Save":
                    // "Save" now saves the .json working file
                    MapSerializer.Save(_editorState.ActiveMap, jsonPath);
                    _editorState.CurrentMapFile = jsonPath;
                    System.Diagnostics.Debug.WriteLine($"Working map SAVED to: {jsonPath}");
                    break;

                case "Load":
                    // "Load" now loads the .json working file
                    var loadedMap = MapSerializer.Load(jsonPath);
                    if (loadedMap != null)
                    {
                        LoadNewMap(loadedMap, jsonPath);
                    }
                    break;

                case "Export":
                    // "Export" creates the binary .map file for the game
                    MapSerializer.Export(_editorState.ActiveMap, gameMapPath);
                    System.Diagnostics.Debug.WriteLine($"Map EXPORTED for game to: {gameMapPath}");
                    break;
            }
        }
        private void LoadNewMap(Map newMap, string filePath)
        {
            // --- 1. REPLACE THE CORE DATA ---
            _editorState.ActiveMap = newMap;
            _editorState.CurrentMapFile = filePath;

            // --- 2. CLEAR ALL RUNTIME STATE ---
            string prefabPath = Path.Combine(PathHelper.GetAssetsPath(), "Data", "Objects.json");
            _editorState.PrefabManager.Load(prefabPath);
            // This is critical to prevent data from the old map from "leaking."
            _editorState.History.UndoStack.Clear();
            _editorState.History.RedoStack.Clear();
            _editorState.ToolState.IsToolDrawing = false; // Stop any active tool drawing

            // --- 3. RE-WIRE STATE DEPENDENCIES ---
            // Explicitly tell the LayerState to use the NEW list of layers from the NEW map.
            _editorState.Layers.Layers = newMap.Layers;

            // --- 4. RESET UI AND SELECTION TO SAFE DEFAULTS ---
            // If the new map has layers, select the first one. Otherwise, select nothing.
            _editorState.Layers.ActiveLayerIndex = (newMap.Layers != null && newMap.Layers.Count > 0) ? 0 : -1;

            // Deselect any active brush or object.
            _editorState.Selection.ActiveTileBrush = null;
            _editorState.Selection.SelectedMapObject = null;

            // Reset the TilesetPanel's view to the first available tileset and deselect any tile.
            _editorState.TilesetPanel.ActiveTilesetName = _editorState.ActiveTileSets.FirstOrDefault()?.Name;
            _editorState.TilesetPanel.SelectedTileID = -1;

            // Optional: Reset the camera to the origin to view the newly loaded map from the start.
            _editorState.camera.Position = Vector2.Zero;
            _editorState.camera.Zoom = 1.0f;

            System.Diagnostics.Debug.WriteLine($"Map LOADED into editor from: {filePath}");
        }
    }
}
