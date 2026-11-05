using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using MonoGame.Extended.ECS;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Pixel_Simulations
{
    //Stores all game bools used
    public enum GameBool
    {
        IsPaused,
        ShowCollision,
        ShowPlayerBounds,
        ShowLinks,
        ShowShapes,
        EnableParallax,
        ExtendedZoom // Developer flag for arbitrary zoom levels
    }
    public class GameState
    {
        // --- Core Data Objects ---
        public Map CurrentMap { get; private set; }
        public EntityManager _entityManager;
        public GraphicsDevice _gd;
        public MapSaveData ActiveSave { get; private set; } = new MapSaveData();
        public EventBus Bus { get; private set; }
        public NewPlayer Player { get; private set; }
        // --- Managers and Services ---
        public Camera GameCamera { get; set; }
        public ItemManager ItemManager { get; }
        public bool IsInventoryOpen { get; set; } = false;
        public AssetLibrary Assets { get; }
        public Dictionary<Point, Texture2D> TerrainMaskChunks { get; private set; } = new Dictionary<Point, Texture2D>();
        public Dictionary<Point, Texture2D> TerrainNormalChunks { get; private set; } = new Dictionary<Point, Texture2D>();
        public Dictionary<Point, Texture2D> TerrainWaterChunks { get; private set; } = new Dictionary<Point, Texture2D>();
        public TilesetManager TilesetManager { get; }
        public AutoTileManager AutoTileManager { get; } = new AutoTileManager();
        public PrefabManager PrefabManager { get; }
        public WeatherSimulator Weather { get; set; }
        public WaterManager WaterSystem { get; private set; }
        public ShaderManager Shaders { get; private set; }
        public TimeManager Calendar { get; private set; }
        public DayTimeManager TimeSystem { get; private set; }
        public InteractionManager Interactions { get; }
        public TagManager tagManager { get; private set; }
        public GrassSystem Grass { get; private set; }
        public NoiseManager ns { get; set; }
        //useful objects
        public InputManager input { get; }
        public PhysicsManager Physics { get; }
        public string gameMapPath;
        public GameEntity HoveredEntity { get; set; }

        public bool _fKeyPressedLastFrame = false;
        public float ParallaxStrength { get; set; } = 0.15f;
        public Vector2 MaskOffset { get; set; }
        public int WorldSeed { get; set; } = 42;
        public string Debug_LastPayload { get; set; } = "None";
        public string Debug_FarmableStatus { get; set; } = "Unknown";
        public string Debug_LastCommand { get; set; } = "None";
        //Debug Bool
        public Dictionary<GameBool, bool> DebugPool = new Dictionary<GameBool, bool>();
        public GameState(EntityManager entityManager, EventBus eventBus)
        {
            GameCamera = new Camera();
            Assets = new AssetLibrary();
            ns = new NoiseManager();
            Bus = eventBus;
            TilesetManager = new TilesetManager();
            PrefabManager = new PrefabManager();
            Weather = new WeatherSimulator();
            Calendar = new TimeManager();
            TimeSystem = new DayTimeManager();
            input = new InputManager();
            Physics = new PhysicsManager();
            tagManager = new TagManager();
            ItemManager = new ItemManager();
            Interactions = new InteractionManager();
            _entityManager = entityManager;
        }

        public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice, RenderPipeline pipeline)
        {
            // Phase 1: Core System & Graphics Context Initialization
            InitializeCoreGraphicsAndHardware(graphicsDevice);

            // Phase 2: Data, Databases & Shader Program Loading
            LoadDataDatabasesAndShaders(content, pipeline);

            // Phase 3: Level, Environment Map & Asset Loading
            LoadLevelAndEnvironmentAssets(content);

            // Phase 4: Player, Simulation, & Interactive Runtime Setup
            InitializePlayerAndSimulationRuntime(content);
        }

        private void InitializeCoreGraphicsAndHardware(GraphicsDevice graphicsDevice)
        {
            _gd = graphicsDevice;

            // Core System flags
            DebugPool[GameBool.ShowCollision] = true;
            DebugPool[GameBool.ShowLinks] = false;
            DebugPool[GameBool.ShowShapes] = true;
            DebugPool[GameBool.ShowPlayerBounds] = false;
            DebugPool[GameBool.IsPaused] = false;
            DebugPool[GameBool.EnableParallax] = true;
            DebugPool[GameBool.ExtendedZoom] = true;
            GameCamera = new Camera();
            input.Update(new GameTime(), GameCamera); // Initialize inputs
        }

        private void LoadDataDatabasesAndShaders(ContentManager content, RenderPipeline pipeline)
        {
            // Load asset registries
            Assets.LoadCoreContent(content);
            ns.LoadContent(content);

            // Load data registries
            var parentDir = Directory.GetParent(content.RootDirectory)?.FullName ?? content.RootDirectory;
            string prefabPath = Path.Combine(parentDir, "Assets", "Data", "objects.json");
            PrefabManager.Load(prefabPath);
            System.Diagnostics.Debug.WriteLine($"[GameState] : Prefabs loaded. Prefab Count = {PrefabManager.Prefabs.Count}");
            string tagsPath = Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json");
            tagManager.Load(tagsPath);
            System.Diagnostics.Debug.WriteLine($"[GameState] : Tags loaded. Tags Count = { tagManager.Tags.Count}");
            string itemPath = Path.Combine(parentDir, "Assets", "Data", "items.json");
            string physicalPath = Path.Combine(parentDir, "Assets", "Data", "physical_items.json");
            ItemManager.Load(itemPath, physicalPath);
            System.Diagnostics.Debug.WriteLine($"[GameState] : ITEM MANAGER loaded. Items Count = {ItemManager.Items.Count},{ItemManager.PhysicalItems.Count}");
            string interactionsPath = Path.Combine(parentDir, "Assets", "Data", "interactions.json");
            Interactions.Load(interactionsPath);

            string timePath = Path.Combine(parentDir, "Assets", "Data", "TimeOfDay.json");
            TimeSystem.LoadContent(timePath);

            // Load shader programs
            Shaders = new ShaderManager(_gd);
            Shaders.PipelineReference = Shaders.PipelineReference ?? pipeline;
            Shaders.LoadContent(content, ns);
            WaterSystem = new WaterManager(_gd,pipeline);
            System.Diagnostics.Debug.WriteLine($"[GameState] : Shader loaded {Shaders.Effects.Count} Shaders.");

        }

        private void LoadLevelAndEnvironmentAssets(ContentManager content)
        {
            var parentDir = Directory.GetParent(content.RootDirectory)?.FullName ?? content.RootDirectory;
            //string mapName = "Level1";
            string mapName = "CoastTown";
            gameMapPath = Path.Combine(parentDir, "Assets", "Maps", $"{mapName}.map");

            CurrentMap = MapSerializer.Read(gameMapPath);
            System.Diagnostics.Debug.WriteLine($"[Map] : Map is Read at {gameMapPath}");

            var maskLayer = CurrentMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer;

            if (maskLayer != null)
            {
                string maskPath = Path.Combine(parentDir, "Assets", "Maps", $"{mapName}_mask.png");
                string normalPath = Path.Combine(parentDir, "Assets", "Maps", $"{mapName}_normals.png");
                string waterPath = Path.Combine(parentDir, "Assets", "Maps", $"{mapName}_water.png");
                // Terrain texture segments
                TerrainMaskChunks = MapSerializer.LoadGameChunks(maskPath, _gd, maskLayer.OffsetX, maskLayer.OffsetY);
                TerrainNormalChunks = MapSerializer.LoadGameChunks(normalPath, _gd, maskLayer.OffsetX, maskLayer.OffsetY);
                TerrainWaterChunks = MapSerializer.LoadGameChunks(waterPath, _gd, maskLayer.OffsetX, maskLayer.OffsetY); // NEW
                System.Diagnostics.Debug.WriteLine($"[Map] : Chunks are read at {maskPath}, {normalPath}. Available data [{TerrainMaskChunks.Count}] Terrain, and [{TerrainNormalChunks.Count} Normal Terrain] ");
                MaskOffset = new Vector2(maskLayer.OffsetX, maskLayer.OffsetY);
                WaterSystem.CacheTerrainElevationData(TerrainMaskChunks);
                WaterSystem.CacheWaterDepthData(TerrainWaterChunks);
            }

            // Save Game Profile
            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves", "CoastTown_Save.json");
            if (File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                ActiveSave = JsonConvert.DeserializeObject<MapSaveData>(json) ?? new MapSaveData();
            }
            else
            {
                ActiveSave = new MapSaveData();
            }
        }

        private void InitializePlayerAndSimulationRuntime(ContentManager content)
        {
            // Calendar clock bound to climate forecast
            Calendar.OnDayChanged += () => {
                Weather.GenerateDailyForecast(WorldSeed, Calendar.Year, Calendar.CurrentSeason, Calendar.Day);
                Bus.Publish(new DayAdvancedCommand { DaysPassed = 1 });
            };
            Weather.GenerateDailyForecast(WorldSeed, Calendar.Year, Calendar.CurrentSeason, Calendar.Day);

            // Standardize tiles
            InitializeTilesets(_gd);
            AutoTileManager.Load(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "autotiles.json"));

            // Grass & Wind Math
            Grass = new GrassSystem(_gd);
            var grassShapes = new List<RectangleF>();
            foreach (var layer in CurrentMap.Layers.OfType<ControlLayer>())
            {
                grassShapes.AddRange(layer.Shapes.Where(s => s.Tags.Contains(80)).Select(s => s.Shape.GetBounds()));
            }
            Grass.LoadContent(content, ns, grassShapes);


            // Physics Boundaries
            Physics.LoadMapData(CurrentMap, PrefabManager);

            // Dynamic Player entity
            Player = new NewPlayer("Hero", new Vector2(200, 200), _gd);
            Player.LoadContent(content, Physics);
        }
        private void InitializeTilesets(GraphicsDevice graphicsDevice)
        {
            // Clear any old tilesets first
            TilesetManager.Clear();

            // Create a TileSet instance for each atlas we have loaded.
            // In a more advanced system, this would be driven by a manifest file.
            var basicTexture = Assets.GetAtlas("Base");
            if (basicTexture != null)
                TilesetManager.RegisterTileSet(new TileSet("Base", basicTexture, 16, graphicsDevice));

            var basicWTexture = Assets.GetAtlas("BaseWorld");
            if (basicWTexture != null)
                TilesetManager.RegisterTileSet(new TileSet("BaseWorld", basicWTexture, 16, graphicsDevice));

            var wildTexture = Assets.GetAtlas("Wild");
            if (wildTexture != null)
                TilesetManager.RegisterTileSet(new TileSet("Wild", wildTexture, 16, graphicsDevice));
            var farmTexture = Assets.GetAtlas("Farm");
            if (farmTexture != null)
            {
                // Must match the exact string we used in InteractionEngine: "farm"
                TilesetManager.RegisterTileSet(new TileSet("Farm", farmTexture, 16, graphicsDevice));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("CRITICAL WARNING: 'farm' atlas not loaded! Tilled dirt will be invisible!");
            }
            // Add other tilesets here...
        }

        public void Update(GameTime gameTime)
        {
            input.Update(gameTime,GameCamera);
            Player.Update(gameTime);
            UpdateCameraInput(gameTime);

            // 3. Update the camera with the desired target
            // Assuming you don't have a single-fire trigger yet, this basic check works:
            var keyboardState = Keyboard.GetState();
            // Assuming you don't have a single-fire trigger yet, this basic check works:
            if (keyboardState.IsKeyDown(Keys.U))
            {
                // Simple debounce so it doesn't cycle 60 times a second
                if (!_fKeyPressedLastFrame)
                {
                    //Weather.GenerateDailyForecast(42, TimeSystem.Year, TimeSystem.CurrentSeason, TimeSystem.Day);
                }
                _fKeyPressedLastFrame = true;
            }

            if (keyboardState.IsKeyDown(Keys.F))
            {
                if (!_fKeyPressedLastFrame) TryInteract(); // Fire once per press
                _fKeyPressedLastFrame = true;
            }
            else
            {
                _fKeyPressedLastFrame = false;
            }
            if (WaterSystem.IsSimulating)
            {
                var maskLayer = CurrentMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer;
                if (maskLayer != null)
                {
                    //WaterSystem.UpdateSimulation(gameTime, CurrentMap, maskLayer);
                }
            }
            // Update the weather math
            float prevTime = TimeSystem.TimeOfDay;
            TimeSystem.Update(gameTime, Calendar.CurrentSeason, Calendar.GetCurrentPhase());
            if (prevTime > 23f && TimeSystem.TimeOfDay < 1f)
            {
                Calendar.AdvanceDay();
            }
            Weather.Update(gameTime, TimeSystem.TimeOfDay);
            Shaders.UpdateParticles(gameTime, Weather,GameCamera.Position,new Vector2(960,540));
            Shaders.UpdatePostProcessing(Weather,TimeSystem,gameTime);
            Grass.Update(gameTime, Player.Foot);
        }
        private void TryInteract()
        {
            RectangleF interactBox = Player.GetInteractionBox();

            PropObject targetProp = null;
            ObjectLayer targetLayer = null;

            // Search for a prop inside the interaction box
            foreach (var layer in CurrentMap.Layers.OfType<ObjectLayer>().Where(l => l.Type == LayerType.Object))
            {
                foreach (var obj in layer.Objects)
                {
                    if (obj is PropObject prop)
                    {
                        var prefab = PrefabManager.GetPrefab(prop.PrefabID);
                        if (prefab != null)
                        {
                            RectangleF bounds = new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, prefab.SourceRect.Width, prefab.SourceRect.Height);

                            if (bounds.Intersects(interactBox))
                            {
                                targetProp = prop;
                                targetLayer = layer;
                                break;
                            }
                        }
                    }
                }
                if (targetProp != null) break;
            }

            if (targetProp != null)
            {
                // 1. Remove the Tree visually
                targetLayer.Objects.Remove(targetProp);

                // 2. Destroy all Linked Objects (the collision boxes!)
                foreach (ObjectLink targetID in targetProp.Links)
                {
                    RemoveObjectFromMap(targetID.TargetID);
                }

                // 3. Refresh the Physics Engine so the collision actually disappears!
                Physics.LoadMapData(CurrentMap, PrefabManager);

                System.Diagnostics.Debug.WriteLine($"Chopped down: {targetProp.Name}");
            }
        }

        private void RemoveObjectFromMap(string id)
        {
            foreach (var layer in CurrentMap.Layers.OfType<ObjectLayer>())
            {
                // Handle standard PropObjects
                if (layer.Type == LayerType.Object)
                {
                    layer.Objects.RemoveAll(o => o.ID == id);
                }
                // Handle Collision/Triggers
                else if (layer is ControlLayer cl)
                {
                    cl.Rectangles.RemoveAll(r => r.ID == id);
                    cl.Shapes.RemoveAll(s => s.ID == id);
                    cl.Points.RemoveAll(p => p.ID == id);
                }
            }
        }
        public void UpdateCameraInput(GameTime gameTime)
        {
            // Sync developer flag to camera
            GameCamera.AllowExtendedZoom = DebugPool.TryGetValue(GameBool.ExtendedZoom, out bool allowExt) && allowExt;

            // Zoom In: Numpad+, '=', or Scroll Wheel Up
            if (input.IsKeyPressed(Keys.OemPlus) || input.IsKeyPressed(Keys.Add) || input.GetScrollDelta() > 0)
                GameCamera.ChangeZoomStep(true);

            // Zoom Out: Numpad-, '-', or Scroll Wheel Down
            if (input.IsKeyPressed(Keys.OemMinus) || input.IsKeyPressed(Keys.Subtract) || input.GetScrollDelta() < 0)
                GameCamera.ChangeZoomStep(false);

            if (input.IsKeyDown(Keys.Space))
            {
                GameCamera.SetFocusPoint(input.MouseWorldPosition, 0.7f);
            }
            else
            {
                GameCamera.SetFocusPoint(Vector2.Zero, 0f);
            }

            GameCamera.Update(gameTime, Player.Position);
        }
        public RectangleF GetStreamingBounds(int nativeWidth, int nativeHeight)
        {
            // 1. Start with a box slightly larger than the screen, centered on the player.
            // E.g., if screen is 480x270, maybe the base box is 600x400
            float padX = 60f;
            float padY = 60f;
            float width = nativeWidth + padX * 2;
            float height = nativeHeight + padY * 2;

            Vector2 center = Player.Position;
            RectangleF bounds = new RectangleF(center.X - width / 2, center.Y - height / 2, width, height);

            // 2. "Look Ahead" Bias: Extend the box in the direction the player is facing
            float biasAmount = 150f; // Look 150 pixels ahead

            switch (Player.FacingDirection)
            {
                case Direction.North:
                    bounds.Y -= biasAmount;
                    bounds.Height += biasAmount;
                    break;
                case Direction.South:
                    bounds.Height += biasAmount;
                    break;
                case Direction.West:
                    bounds.X -= biasAmount;
                    bounds.Width += biasAmount;
                    break;
                case Direction.East:
                    bounds.Width += biasAmount;
                    break;
            }

            return bounds;
        }
        public bool IsPaused => DebugPool[GameBool.IsPaused];
        public void SaveGame()
        {
            string savePath = System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Saves", "CoastTown_Save.json"); System.IO.Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            string json = JsonConvert.SerializeObject(ActiveSave, Formatting.Indented);
            System.IO.File.WriteAllText(savePath, json);
            System.Diagnostics.Debug.WriteLine("Game Progress Saved!");
        }

        public void AdvanceDailySimulation()
        {
            bool visualChanged = false;
            List<PlacedItemObject> itemsToDestroy = new List<PlacedItemObject>();

            foreach (var placed in ActiveSave.PlacedItems)
            {
                var physDef = ItemManager.GetPhysicalDef(placed.ItemID);
                if (physDef == null || placed.CurrentStageIndex >= physDef.Stages.Count) continue;

                var currentStage = physDef.Stages[placed.CurrentStageIndex];

                // --- CROP LOGIC ---
                if (physDef.Type == PlacementType.Crop)
                {
                    // If we are not in the final stage (Rotten/Harvestable)
                    if (placed.CurrentStageIndex < physDef.Stages.Count - 1)
                    {
                        placed.DaysInCurrentStage++;

                        // Int1 = Growth Days Required
                        if (placed.DaysInCurrentStage >= currentStage.ThresholdInt1)
                        {
                            placed.DaysInCurrentStage = 0;
                            placed.CurrentStageIndex++;
                            visualChanged = true;
                        }
                    }
                }
                // --- PILE DECAY LOGIC ---
                else if (physDef.Type == PlacementType.Pile)
                {
                    // Bool = Can Decay?
                    if (currentStage.ThresholdBool)
                    {
                        placed.DaysInCurrentStage++;

                        // Int2 = Half-Life Decay Days
                        if (placed.DaysInCurrentStage >= currentStage.ThresholdInt2)
                        {
                            placed.Amount = (int)Math.Floor(placed.Amount / 2f); // Halve the amount
                            placed.DaysInCurrentStage = 0;
                            visualChanged = true;

                            if (placed.Amount <= 0) itemsToDestroy.Add(placed);
                            else
                            {
                                // Demote visual stage if amount dropped below threshold
                                while (placed.CurrentStageIndex > 1 && placed.Amount < physDef.Stages[placed.CurrentStageIndex].ThresholdInt1)
                                {
                                    placed.CurrentStageIndex--;
                                }
                            }
                        }
                    }
                }
            }

            // Clean up destroyed piles
            foreach (var item in itemsToDestroy)
            {
                ActiveSave.PlacedItems.Remove(item);
                // We will rely on the EntityManager clearing it next frame or you can remove it directly
            }

            if (visualChanged) System.Diagnostics.Debug.WriteLine("Daily Simulation Complete. Visuals Updated.");
        }

    }
}
