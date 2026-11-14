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
    public class EditorTaskProgress
    {
        public bool IsActive { get; set; } = false;
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";

        public float TargetProgress { get; private set; } = 0f;
        public float DisplayedProgress { get; private set; } = 0f; // Smoothly lerped for rendering
        public float FadeTimer { get; set; } = 0f;

        public void Start(string title, string initialStatus)
        {
            IsActive = true;
            Title = title;
            Status = initialStatus;
            TargetProgress = 0.05f;
            DisplayedProgress = 0.0f;
            FadeTimer = 0f;
        }

        public void SetProgress(float target, string status)
        {
            TargetProgress = MathHelper.Clamp(target, 0f, 1f);
            Status = status;
        }

        public void Complete(string completionStatus = "Completed Successfully!")
        {
            TargetProgress = 1.0f;
            Status = completionStatus;
            FadeTimer = 1.2f;
        }

        public void Update(float dt)
        {
            if (!IsActive) return;

            // Smoothly lerp the visual progress bar towards the target
            DisplayedProgress = MathHelper.Lerp(DisplayedProgress, TargetProgress, 14f * dt);

            if (FadeTimer > 0f)
            {
                FadeTimer -= dt;
                if (FadeTimer <= 0f)
                {
                    IsActive = false;
                    TargetProgress = 0f;
                    DisplayedProgress = 0f;
                    Title = "";
                    Status = "";
                }
            }
        }
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
                //new MoveTool(),
                new PointPlacerTool(),
                new SpawnPointerTool(),
                new LightShapeTool(),
                new ReflectionTool(),
                new WaterShapeTool(),
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
        public BuildingData ActiveBuildingBlueprint { get; set; } // Current Modular Blueprint
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
        [JsonIgnore] public List<TileSet> ActiveTileSets { get; } = new List<TileSet>();
        public AutoTileManager AutoTileManager { get; } = new AutoTileManager();
        public EditorTaskProgress TaskProgress { get; } = new EditorTaskProgress();


        // --- NEW ASSET CORE ---
        [JsonIgnore] public TilesetManager TilesetManager { get; }

        [JsonIgnore] public EditorLibrary AssetLibrary { get; set; }
        [JsonIgnore] public NoiseManager noiseManager = new NoiseManager();
        [JsonIgnore] public MaskDataManager MaskData { get; } = new MaskDataManager();
        [JsonIgnore] public PrefabManager PrefabManager { get; }
        public BuildingManager BuildingManager { get; } = new BuildingManager();
        [JsonIgnore] public TagManager TagManager { get; } = new TagManager();
        // Flags for UI state
        [JsonIgnore] public string ActiveAtlasForCreator { get; set; }
        [JsonIgnore] public bool ShowMaskRed { get; set; } = true;
        [JsonIgnore] public bool ShowMaskGreen { get; set; } = true;
        [JsonIgnore] public bool ShowMaskBlue { get; set; } = true;
        [JsonIgnore] public bool ShowMaskAlpha { get; set; } = false; // Usually keep alpha hidden in editor
        [JsonIgnore] public Effect normalShader { get; set; }
        [JsonIgnore] public WaterManager WaterSystem { get; private set; } // NEW
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
            WaterSystem = new WaterManager(_graphics, new RenderPipeline(_graphics, 1920, 1080)); // NEW
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
            normalShader = content.Load<Effect>("HeightToNormal");
            AssetLibrary.LoadCoreContent();


            PathHelper.LogConfiguration("Pixel Editor");

            string readData = PathHelper.GetReadDataPath();
            PrefabManager.Load(Path.Combine(readData, "objects.json"));
            TagManager.Load(Path.Combine(readData, "tags.json"));
            MaskData.Load(Path.Combine(readData, "mask_data.json"));
            AutoTileManager.Load(Path.Combine(readData, "autotiles.json"));

            // Load building blueprints from Runtime bin Assets/
            BuildingManager.Load(PathHelper.GetRuntimeAssetsPath());
            System.Diagnostics.Debug.WriteLine($"[EditorState] Loaded {BuildingManager.Buildings.Count} modular blueprints from: {PathHelper.GetRuntimeAssetsPath()}");


            AutoTileManager.Load(System.IO.Path.Combine(PathHelper.GetReadDataPath(), "Data", "autotiles.json"));
            var maskLayer = ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer;
            if (maskLayer != null)
            {
                WaterSystem.LoadContent(content);
                WaterSystem.InitializeSimulationGrids(maskLayer);
            }
        }
        public void refresh(GameTime gameTime)
        {
            Input.Update(gameTime);
            Input.Zoom = camera.Zoom;
            Layers.Layers = ActiveMap.Layers;
            UI.ActivePanelName = _layoutmanager.GetPanelAt(Input.MouseWindowPosition.ToPoint());
            if (WaterSystem.IsSimulating)
            {
                //                WaterSystem.UpdateGPUSimulation(, ActiveMap);
            }
        }

    }

}
