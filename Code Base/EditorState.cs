using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Tiled;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Editor
{
    // A collection of sub-states for organization
    public class InputState
    {
        // Raw States
        public MouseState CurrentMouse { get; set; }
        public MouseState PreviousMouse { get; set; }
        public KeyboardState CurrentKeyboard { get; set; }
        public KeyboardState PreviousKeyboard { get; set; }

        // Processed, Contextual Data
        public Vector2 MouseWindowPosition { get; set; }
        public Vector2 MouseWorldPosition { get; set; }
        public Point MouseGridCell { get; set; }
        public Point MouseChunkCell { get; set; }
        public int ClickCounter = 0;
        public bool IsNewLeftClick()
        {
            ClickCounter++; 
            return CurrentMouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
        }
        public bool IsNewRightClick()
        {
            ClickCounter++;
            return CurrentMouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released;
        }
    }
    public class UIState
    {
        public string ActivePanelName { get; set; } = "None";
        public Vector2 MouseInPanelPosition { get; set; }

        // We can add more specific state here later
        // public List<MapObject> SelectedObjects { get; set; } = new List<MapObject>();
        // public bool IsPopupWindowVisible { get; set; } = false;
    }
    public class ToolState
    {
        public ITool ActiveTool { get; set; }
        public string ActiveToolName { get; set; }
        public string HoveredButtonName { get; set; }
        public List<ITool> Tools { get; }
        //public List<string> ButtonNames { get; set; } = new List<string> { "Brush", "Eraser", "Fill", "Rectangle", "CollisionBrush", "Line", "Eyedropper", "Path", "ObjectPlacer", "PointPlacer", "PointLight", "SpotLight", "SpriteLight", "ReflectionPlane" };
        public ToolState()
        {
            // Initialize with all available tools. This list can be expanded later.
            Tools = new List<ITool>
            {
                new BrushTool(),
                new EraserTool(),
                // new RectangleTool(this) // Some tools might need a reference back to the state
            };
            //ActiveTool = null;
            ActiveTool = Tools.FirstOrDefault();
        }
    }
    public class SelectionState
    {
        // For tile-based tools
        public TileInfo ActiveTileBrush { get; set; }

        // For object-based tools
        public string ActiveObjectAssetName { get; set; }
    }
    public class HistoryState
    {
        public Stack<IUndoableCommand> UndoStack { get; } = new Stack<IUndoableCommand>();
        public Stack<IUndoableCommand> RedoStack { get; } = new Stack<IUndoableCommand>();
    }
    public class TopPanelState
    {
        public List<string> ButtonNames { get; } = new List<string> { "New", "Save", "Load", "Undo", "Redo", "Options" };
        public string HoveredButtonName { get; set; }
    }
    public class TilesetPanelState
    {
        // Tileset list will be initialized by TilesetManager
        public string ActiveTilesetName { get; set; }
        public int SelectedTileID { get; set; } = -1;
        public Point HoveredTileCell { get; set; } = new Point(-1, -1); // For the tile grid
        public string HoveredTilesetName { get; set; } // For the list of tilesets
        public bool IsAtlasPickerVisible { get; set; } = false;
    }
    public class LayerPanelState
    {
        public List<Layer> Layers { get; set; } = new List<Layer>();
        public int ActiveLayerIndex { get; set; } = -1;
        public string HoveredButtonName { get; set; } // For the bottom control bar
        public int HoveredLayerIndex { get; set; } = -1; // Which layer row is hovered
        public string HoveredLayerIconName { get; set; } // "Visible", "Lock", "MoveUp", etc.
        public Layer GetActiveLayer()
        {
            if (ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count)
            {
                return Layers[ActiveLayerIndex];
            }
            return null;
        }
    }
    public class EditorState
    {
        // General Application State
        public bool ShowDebug { get; set; } = true;
        public bool IsRunning { get; set; } = true;

        public int CELL_SIZE = 16;
        public GraphicsDevice _graphics;
        public string CurrentMapFile { get; set; }
        // Core Data
        public Map ActiveMap { get; set; }
        public readonly LayoutManager _layoutmanager;
        // Sub-States for Organization
        public EditorCamera camera { get; set; }
        public InputState Input { get; }
        public UIState UI { get; }
        public TopPanelState TopState { get; }
        public LayerPanelState Layers { get; }
        public ToolState ToolState { get; }
        public SelectionState Selection { get; }
        public HistoryState History { get; }
        //public ToolPanelState ToolStack { get; } = new ToolPanelState();
        public TilesetPanelState TilesetPanel { get; } = new TilesetPanelState();

        public Dictionary<string, Texture2D> AvailableAtlasTextures { get; } = new Dictionary<string, Texture2D>();

        // 2. Stores the active, user-created TileSet instances.
        public List<TileSet> ActiveTileSets { get; } = new List<TileSet>();
        public TilesetManager TilesetManager { get; }

        public EditorState(LayoutManager layoutManager, GraphicsDevice graphicsDevice)
        {
            // Create a default map to start with
            ActiveMap = new Map();
            _graphics = graphicsDevice;
            // Initialize all state objects
            camera = new EditorCamera();
            Input = new InputState();
            UI = new UIState();
            Layers = new LayerPanelState();
            ToolState = new ToolState();
            TopState = new TopPanelState();
            Selection = new SelectionState();
            History = new HistoryState();
            _layoutmanager = layoutManager;
            TilesetManager = new TilesetManager();
            Layers.Layers = ActiveMap.Layers;
            if (Layers.Layers.Count > 0)
            {
                Layers.ActiveLayerIndex = 0;
            }
        }

        public void CreateNewTileset(string atlasName, int tileSize, SliceMode sliceMode)
        {
            if (AvailableAtlasTextures.TryGetValue(atlasName, out var texture))
            {
                // Check if a tileset with this name already exists
                if (ActiveTileSets.Any(ts => ts.Name == atlasName)) return;

                var newTileset = new TileSet(atlasName, texture, tileSize,_graphics, sliceMode);
                ActiveTileSets.Add(newTileset);
                TilesetManager.RegisterTileSet(newTileset);
                // If this is the first tileset, make it active
                if (ActiveTileSets.Count == 1)
                {
                    TilesetPanel.ActiveTilesetName = atlasName;
                    // Also select the first tile
                    int firstId = newTileset.SlicedAtlas.Keys.FirstOrDefault(-1);
                    TilesetPanel.SelectedTileID = firstId;
                    Selection.ActiveTileBrush = new TileInfo(atlasName, firstId);
                }
            }
        }

        public void refresh()
        {
            Layers.Layers = ActiveMap.Layers;
            UI.ActivePanelName = _layoutmanager.GetPanelAt(Input.MouseWindowPosition.ToPoint());
        }

    }

    
}
