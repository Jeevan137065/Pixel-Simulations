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

        public bool IsNewLeftClick() => CurrentMouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
        public bool IsNewRightClick() => CurrentMouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released;
    }
    public class UIState
    {
        public string ActivePanelName { get; set; } = "None";
        public Vector2 MouseInPanelPosition { get; set; }

        // We can add more specific state here later
        // public List<MapObject> SelectedObjects { get; set; } = new List<MapObject>();
        // public bool IsPopupWindowVisible { get; set; } = false;
    }
    public class LayerPanelState
    {
        public List<Layer> Layers { get; set; } = new List<Layer>();
        public int ActiveLayerIndex { get; set; } = -1;
        public string HoveredButton { get; set; }
        public Layer GetActiveLayer()
        {
            if (ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count)
            {
                return Layers[ActiveLayerIndex];
            }
            return null;
        }
    }
    public class ToolState
    {
        public List<ITool> Tools { get; }
        public ITool ActiveTool { get; set; }

        public ToolState()
        {
            // Initialize with all available tools. This list can be expanded later.
            Tools = new List<ITool>
            {
                // We will create these tool classes in a later step
                // new BrushTool(),
                // new EraserTool(),
                // new RectangleTool(this) // Some tools might need a reference back to the state
            };

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
        public Stack<ICommand> UndoStack { get; } = new Stack<ICommand>();
        public Stack<ICommand> RedoStack { get; } = new Stack<ICommand>();
    }
    public class TopPanelState
    {
        public List<string> ButtonNames { get; } = new List<string> { "New", "Save", "Load", "Undo", "Redo", "Options" };
        public string HoveredButton { get; set; }
    }
    public class ToolPanelState
    {
        // Tools list will be initialized by ToolManager
        public List<string> ButtonNames { get; set; } = new List<string> { "Brush", "Eraser", "Fill", "Rectangle", "CollisionBrush", "Line", "Eyedropper", "Path", "ObjectPlacer", "PointPlacer", "PointLight", "SpotLight", "SpriteLight", "ReflectionPlane" };
        public string ActiveToolName { get; set; }
        public string HoveredButton { get; set; }
    }

    public class TilesetPanelState
    {
        // Tileset list will be initialized by TilesetManager
        public List<string> TilesetNames { get; set; } = new List<string>();
        public string ActiveTilesetName { get; set; }
        public int SelectedTileID { get; set; } = -1;
        public bool IsAtlasPickerVisible { get; set; } = false;
        public List<string> UnusedAtlasNames { get; set; } = new List<string>();
    }
    public class EditorState
    {
        // General Application State
        public bool ShowDebug { get; set; } = true;
        public bool IsRunning { get; set; } = true;

        public int CELL_SIZE = 16;
        public int x = 100; //temp variable for checking data sync among classes
        public string CurrentMapFile { get; set; }
        // Core Data
        public Map ActiveMap { get; set; }
        public readonly LayoutManager _layoutmanager;
        // Sub-States for Organization
        public EditorCamera camera { get; set; }
        public InputState Input { get; }
        public UIState UI { get; }
        public LayerPanelState Layers { get; }
        public ToolState Tools { get; }
        public SelectionState Selection { get; }
        public HistoryState History { get; }
        public TopPanelState TopPanel { get; } = new TopPanelState();
        public ToolPanelState ToolPanel { get; } = new ToolPanelState();
        public TilesetPanelState TilesetPanel { get; } = new TilesetPanelState();

        public Dictionary<string, Texture2D> AvailableAtlasTextures { get; } = new Dictionary<string, Texture2D>();

        // 2. Stores the active, user-created TileSet instances.
        public List<TileSet> ActiveTileSets { get; } = new List<TileSet>();

        public EditorState(LayoutManager layoutManager)
        {
            // Create a default map to start with
            ActiveMap = new Map(60, 60);

            // Initialize all state objects
            camera = new EditorCamera();
            Input = new InputState();
            UI = new UIState();
            Layers = new LayerPanelState();
            Tools = new ToolState();
            Selection = new SelectionState();
            History = new HistoryState();
            _layoutmanager = layoutManager;
            // Link the LayerState to the active map's layers
            Layers.Layers = ActiveMap.Layers;
            if (Layers.Layers.Count > 0)
            {
                Layers.ActiveLayerIndex = 0;
            }
        }

    }

    
}
