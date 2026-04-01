using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Tiled;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using Pixel_Simulations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;

namespace Pixel_Simulations.Editor
{
    // A collection of sub-states for organization

    public class UIState
    {
        public string ActivePanelName { get; set; } = "None";
        public Vector2 MouseInPanelPosition { get; set; }
        public bool IsLinkingMode { get; set; } = false; // Add this!
        public UIElement FocusedElement { get; set; }
    }
    public class ToolState
    {
        public ITool ActiveTool { get; set; }
        public string ActiveToolName { get; set; }
        public string HoveredButtonName { get; set; }
        public List<ITool> Tools { get; }
        public bool IsToolDrawing { get; set; } = false;
        public ShapeOperation ActiveBooleanOp { get; set; } = ShapeOperation.None;
        //public List<string> ButtonNames { get; set; } = new List<string> { "Brush", "Eraser", "Fill", "Rectangle", "CollisionBrush", "Line", "Eyedropper", "Path", "ObjectPlacer", "PointPlacer", "PointLight", "SpotLight", "SpriteLight", "ReflectionPlane" };
        public ToolState()
        {
            // Initialize with all available tools. This list can be expanded later.
            Tools = new List<ITool>
            {
                new BrushTool(),
                new EraserTool(),
                new ObjectPlacerTool(),
                new TerrainBrushTool(),
                new FreeRectangleTool(),
                new ShapeTool(),
                new SelectionTool(),
                new PointPlacerTool()
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
        public HandleType ActiveHandle { get; set; } = HandleType.None;
        public ObjectPrefab ActivePrefab { get; set; } // The currently selected blueprint
        public MapObject SelectedMapObject { get; set; } // The currently selected instance on the map
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
    public class TilesetState
    {
        // Tileset list will be initialized by TilesetManager
        public string ActiveTilesetName { get; set; }
        public int SelectedTileID { get; set; } = -1;
        public Point HoveredTileCell { get; set; } = new Point(-1, -1); // For the tile grid
        public string HoveredTilesetName { get; set; } // For the list of tilesets
        public bool IsAtlasPickerVisible { get; set; } = false;
    }
    public class PrefabCreatorState
    {
        public bool IsOpen = false;
        public string ActiveAtlasName { get; set; }
        public Rectangle SelectionRect { get; set; }
        public bool NeedsUIRebuild { get; set; } = false;
        public string TempName { get; set; } = "New_Object";

        // --- CHANGED to List and Dictionary! ---
        public List<string> TempTags { get; set; } = new List<string>();
        public Dictionary<string, MapProperty> TempProperties { get; set; } = new Dictionary<string, MapProperty>();

        public Vector2 DragStart { get; set; }
        public bool IsDragging { get; set; }

        // --- NEW: For panning large atlases ---
        public Vector2 AtlasPanOffset { get; set; } = Vector2.Zero;
    }
    public class LayerPanelState
    {
        public List<Layer> Layers { get; set; } = new List<Layer>();
        public int ActiveLayerIndex { get; set; } = -1;
        public int RenamingLayerIndex { get; set; } = -1;
        public string TextEditorString { get; set; }
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
        public LayerType NewLayerType { get; set; } = LayerType.Tile;
    }
    public class EditorState
    {
        // General Application State
        [JsonIgnore] public bool ShowDebug { get; set; } = true;
        [JsonIgnore] public bool ShowGrid { get; set; } = true;
        [JsonIgnore] public bool IsRunning { get; set; } = true;

        [JsonIgnore] public int CELL_SIZE = 16;
        [JsonIgnore] public GraphicsDevice _graphics;
        [JsonIgnore] public string CurrentMapFile { get; set; }
        [JsonIgnore] public Map ActiveMap { get; set; }
        [JsonIgnore] public readonly LayoutManager _layoutmanager;
        // Sub-States for Organization
        [JsonIgnore] public EditorCamera camera { get; set; }
        [JsonIgnore] public EditorInputState Input { get; }
        [JsonIgnore] public UIState UI { get; }
        [JsonIgnore] public TopPanelState TopState { get; }
        [JsonIgnore] public LayerPanelState Layers { get; }
        [JsonIgnore] public ToolState ToolState { get; }
        [JsonIgnore] public SelectionState Selection { get; }
        [JsonIgnore] public HistoryState History { get; }
        [JsonIgnore] public TilesetState TilesetPanel { get; } = new TilesetState();
        [JsonIgnore] public PrefabCreatorState PrefabCreator { get; } = new PrefabCreatorState();
        [JsonIgnore] public List<TileSet> ActiveTileSets { get; } = new List<TileSet>();
        // --- NEW ASSET CORE ---
        [JsonIgnore] public TilesetManager TilesetManager { get; }
        [JsonIgnore] public EditorLibrary AssetLibrary { get; set; }
        [JsonIgnore] public NoiseManager noiseManager = new NoiseManager();
        [JsonIgnore] public MaskDataManager MaskData { get; } = new MaskDataManager();
        [JsonIgnore] public PrefabManager PrefabManager { get; }
        [JsonIgnore] public TagManager TagManager { get; } = new TagManager();
        // Flags for UI state
        [JsonIgnore] public bool IsTagManagerOpen { get; set; } = false;
        [JsonIgnore] public string ActiveAtlasForCreator { get; set; }
        [JsonIgnore] public bool ShowMaskRed { get; set; } = true;
        [JsonIgnore] public bool ShowMaskGreen { get; set; } = true;
        [JsonIgnore] public bool ShowMaskBlue { get; set; } = true;
        [JsonIgnore] public bool ShowMaskAlpha { get; set; } = false; // Usually keep alpha hidden in editor

        public EditorState(LayoutManager layoutManager, GraphicsDevice graphicsDevice)
        {
            // Create a default map to start with
            ActiveMap = new Map();
            _graphics = graphicsDevice;
            // Initialize all state objects
            camera = new EditorCamera();
            Input = new EditorInputState();
            UI = new UIState();
            Layers = new LayerPanelState();
            ToolState = new ToolState();
            TopState = new TopPanelState();
            Selection = new SelectionState();
            History = new HistoryState();
            _layoutmanager = layoutManager;
            TilesetManager = new TilesetManager();
            PrefabManager = new PrefabManager();
            Layers.Layers = ActiveMap.Layers;
            if (Layers.Layers.Count > 0)
            {
                Layers.ActiveLayerIndex = 0;
            }
        }
        public void LoadContent(ContentManager content)
        {
            noiseManager.LoadContent(content);
            AssetLibrary = new EditorLibrary(content);
            //AssetLibrary.LoadAtlas("Basic",AtlasType.Tile);
            AssetLibrary.LoadAtlas("Base", AtlasType.Tile);
            AssetLibrary.LoadAtlas("Wild", AtlasType.Tile);

            AssetLibrary.LoadAtlas("Trees", AtlasType.Object);
            AssetLibrary.LoadAtlas("Building", AtlasType.Object);

            string prefabPath = Path.Combine(PathHelper.GetAssetsPath(), "Data", "objects.json");
            PrefabManager.Load(prefabPath);
            string tagsPath = Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json");
            TagManager.Load(tagsPath);
            string maskDataPath = Path.Combine(PathHelper.GetAssetsPath(), "Data", "mask_data.json");
            MaskData.Load(maskDataPath);
        }
        public void refresh(GameTime gameTime)
        {
            Input.Update(gameTime);
            Input.Zoom = camera.Zoom;
            Layers.Layers = ActiveMap.Layers;
            UI.ActivePanelName = _layoutmanager.GetPanelAt(Input.MouseWindowPosition.ToPoint());
        }

    }

}
