using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
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
        EnableParallax
    }
    public class GameState
    {
        // --- Core Data Objects ---
        public Map CurrentMap { get; private set; }
        public MapSaveData ActiveSave { get; private set; } = new MapSaveData();
        public NewPlayer Player { get; private set; }
        // --- Managers and Services ---
        public Camera GameCamera { get; set; }
        public ItemManager ItemManager { get; }
        public bool IsInventoryOpen { get; set; } = false;
        public AssetLibrary Assets { get; }
        public Dictionary<Point, Texture2D> TerrainMaskChunks { get; private set; } = new Dictionary<Point, Texture2D>();
        public Dictionary<Point, Texture2D> TerrainNormalChunks { get; private set; } = new Dictionary<Point, Texture2D>();
        public TilesetManager TilesetManager { get; }
        public PrefabManager PrefabManager { get; }
        public WeatherSimulator Weather { get; set; }
        public ShaderManager Shaders { get; private set; }
        public DayTimeManager TimeSystem { get; private set; }
        public InteractionManager Interactions { get; }
        public TagManager tagManager { get; private set; }
        public GrassSystem Grass { get; private set; }
        //useful objects
        public InputManager input { get; }
        public PhysicsManager Physics { get; }
        public string gameMapPath;
        public GameEntity HoveredEntity { get; set; }

        public bool _fKeyPressedLastFrame = false;
        public float ParallaxStrength { get; set; } = 0.15f;
        public Vector2 MaskOffset { get; set; }
        //Debug Bool
        public Dictionary<GameBool, bool> DebugPool = new Dictionary<GameBool, bool>();
        public GameState()
        {
            GameCamera = new Camera();
            Assets = new AssetLibrary();
            TilesetManager = new TilesetManager();
            PrefabManager = new PrefabManager();
            Weather = new WeatherSimulator();
            TimeSystem = new DayTimeManager();
            input = new InputManager();
            Physics = new PhysicsManager();
            tagManager = new TagManager();
            ItemManager = new ItemManager();
            Interactions = new InteractionManager();
        }

        /// <summary>
        /// Loads all game assets and initializes the game world from a map file.
        /// </summary>
        public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
        {
            DebugPool[GameBool.ShowCollision] = false;
            DebugPool[GameBool.ShowLinks] = false;
            DebugPool[GameBool.ShowShapes] = false;
            DebugPool[GameBool.ShowPlayerBounds] = false;
            DebugPool[GameBool.IsPaused] = false;
            DebugPool[GameBool.EnableParallax] = true;
            // 1. Load all raw texture assets first.
            Assets.LoadCoreContent(content);

            // 2. Load the map data from the file.
            var parentDir = Directory.GetParent(content.RootDirectory)?.FullName ?? content.RootDirectory;


            string prefabPath = Path.Combine(parentDir,"Assets" ,"Data", "objects.json");
            PrefabManager.Load(prefabPath);

            // 3. MAP: Load the actual level data (Binary .map for the game).
            string mapName = "CoastTown";
            gameMapPath = Path.Combine(parentDir,"Assets", "Maps", $"{mapName}.map");
            System.Diagnostics.Debug.WriteLine($"Map being Read for game to: {gameMapPath}");
            CurrentMap = MapSerializer.Read(gameMapPath);
            var maskLayer = CurrentMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer;

            if (maskLayer != null)
            {
                string maskPath = Path.Combine(parentDir, "Assets", "Maps", $"{mapName}_mask.png");
                string normalPath = Path.Combine(parentDir, "Assets", "Maps", $"{mapName}_normals.png");

                // Load the Blue/Red/Green Data map
                TerrainMaskChunks = MapSerializer.LoadGameChunks(maskPath, graphicsDevice, maskLayer.OffsetX, maskLayer.OffsetY);

                // Load the generated Normal Map
                TerrainNormalChunks = MapSerializer.LoadGameChunks(normalPath, graphicsDevice, maskLayer.OffsetX, maskLayer.OffsetY);
                int maskOffsetX = maskLayer.OffsetX;
                int maskOffsetY = maskLayer.OffsetY;
                MaskOffset = new Vector2(maskOffsetX, maskOffsetY);
                System.Diagnostics.Debug.WriteLine($"Mask offset as: {MaskOffset}");
            }
            if (CurrentMap == null) {
                System.Diagnostics.Debug.WriteLine($"Map Read at: {gameMapPath} is NULL");
            }
            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves", "CoastTown_Save.json");
            if (File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                ActiveSave = JsonConvert.DeserializeObject<MapSaveData>(json) ?? new MapSaveData();
            }
            else
            {
                ActiveSave = new MapSaveData(); // Fresh save
            }
            string tagsPath = Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json");
            tagManager.Load(tagsPath);
            // 3. Create all TileSet instances and register them with the TilesetManager.
            // The manager needs the raw textures from the AssetLibrary to do its job.
            InitializeTilesets(graphicsDevice);
            Grass = new GrassSystem(graphicsDevice,GrassLibrary.GetPreset(GrassPreset.WheatField));
            var grassShapes = new List<RectangleF>();
            foreach (var layer in CurrentMap.Layers.OfType<ControlLayer>())
            {
                foreach (var sh in layer.Shapes.Where(s => s.Tags.Contains(40)))
                {
                    grassShapes.Add(sh.Shape.GetBounds());
                }
            }
            Grass.LoadContent(content,grassShapes);
            Shaders = new ShaderManager(graphicsDevice);
            Shaders.LoadContent(content);
            Physics.LoadMapData(CurrentMap);
            
            // ---------------------------------
            // 4. Create the player object.
            Player = new NewPlayer("Hero", new Vector2(200, 200), graphicsDevice);
            string itemPath = Path.Combine(parentDir, "Assets", "Data", "items.json");
            ItemManager.Load(itemPath, itemPath);
            string interactionsPath = Path.Combine(parentDir, "Assets", "Data", "interactions.json");
            Interactions.Load(interactionsPath);
            Player.LoadContent(content,Physics); // Player loads its own specific content
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

            var wildTexture = Assets.GetAtlas("Wild");
            if (wildTexture != null)
                TilesetManager.RegisterTileSet(new TileSet("Wild", wildTexture, 16, graphicsDevice));

            // Add other tilesets here...
        }

        public void Update(GameTime gameTime)
        {
            input.Update(gameTime,GameCamera);
            Player.Update(gameTime);
            UpdateCameraInput(gameTime);
            //GameCamera.Follow(Player, 1.0f);

            // 3. Update the camera with the desired target
            // Assuming you don't have a single-fire trigger yet, this basic check works:
            var keyboardState = Keyboard.GetState();
            // Assuming you don't have a single-fire trigger yet, this basic check works:
            if (keyboardState.IsKeyDown(Keys.U))
            {
                // Simple debounce so it doesn't cycle 60 times a second
                if (!_fKeyPressedLastFrame)
                {
                    Weather.CycleWeather();
                }
                _fKeyPressedLastFrame = true;
            }
            else if (keyboardState.IsKeyDown(Keys.Y))
            {
                // Simple debounce so it doesn't cycle 60 times a second
                if (!_fKeyPressedLastFrame)
                {
                    Weather.CyclePhase();
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
            if(keyboardState.IsKeyDown(Keys.OemCloseBrackets))
                TimeSystem.AddHours(1f);

            // Press '[' to jump backwards 1 hour
            if (keyboardState.IsKeyDown(Keys.OemOpenBrackets))
                TimeSystem.AddHours(-1f);

            // Update the weather math
            TimeSystem.Update(gameTime, Weather.CurrentSeason, Weather.Phase);
            Grass.Update(gameTime, Player.Foot);
            Weather.Update(gameTime, TimeSystem.TimeOfDay);
            Shaders.UpdateParticles(gameTime, Weather,GameCamera.Position,new Vector2(960,540));
            Shaders.UpdatePostProcessing(Weather,TimeSystem,gameTime);
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
                foreach (string targetID in targetProp.LinkedObjects)
                {
                    RemoveObjectFromMap(targetID);
                }

                // 3. Refresh the Physics Engine so the collision actually disappears!
                Physics.LoadMapData(CurrentMap);

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
            float targetZoom = GameCamera.Zoom;

            // 1. Handle Zoom Steps via InputManager
            if (input.IsKeyPressed(Keys.OemPlus) || input.GetScrollDelta() > 0)
                GameCamera.ChangeZoomStep(true);

            if (input.IsKeyPressed(Keys.OemMinus) || input.GetScrollDelta() < 0)
                GameCamera.ChangeZoomStep(false);

            // 2. Focus Logic
            if (input.IsKeyDown(Keys.Space))
            {
                // When Space is held, pull camera 70% toward the mouse
                GameCamera.SetFocusPoint(input.MouseWorldPosition, 0.7f);
            }
            else
            {
                // Release focus (smoothly slides back to player)
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
        public void GrowCrops()
        {
            bool visualChanged = false;

            // We no longer loop over the map layers! We only loop over the player's placed items.
            foreach (var placed in ActiveSave.PlacedItems)
            {
                var physDef = ItemManager.GetPhysicalDef(placed.ItemID);
                if (physDef != null && physDef.Type == PlacementType.Crop)
                {
                    placed.DaysAlive++;

                    int nextStage = placed.CurrentStageIndex + 1;
                    if (nextStage < physDef.Stages.Count)
                    {
                        if (placed.DaysAlive >= physDef.Stages[nextStage].ThresholdInt1)
                        {
                            placed.CurrentStageIndex = nextStage;
                            visualChanged = true;
                        }
                    }
                }
            }

            if (visualChanged) System.Diagnostics.Debug.WriteLine("Crops Grew! (Needs Visual Refresh)");
        }
    }
}
