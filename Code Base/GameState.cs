using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Pixel_Simulations
{
    public class GameState
    {
        // --- Core Data Objects ---
        public Map CurrentMap { get; private set; }
        public NewPlayer Player { get; private set; }

        // --- Managers and Services ---
        public Camera GameCamera { get; set; }
        public AssetLibrary Assets { get; }
        public TilesetManager TilesetManager { get; }
        public PrefabManager PrefabManager { get; }
        public WeatherSimulator Weather { get; set; }
        public ShaderManager Shaders { get; private set; }
        public DayTimeManager TimeSystem { get; private set; }
        //useful objects
        public InputManager input { get; }
        public PhysicsManager Physics { get; }
        public string gameMapPath;
        public int _uKeyPressed = 0;
        private bool _uKeyPressedLastFrame = false;
        public GameState()
        {
            GameCamera = new Camera();
            Assets = new AssetLibrary();
            TilesetManager = new TilesetManager();
            PrefabManager = new PrefabManager();
            Weather = new WeatherSimulator();
            TimeSystem = new DayTimeManager(); // NEW
            input = new InputManager();
        }

        /// <summary>
        /// Loads all game assets and initializes the game world from a map file.
        /// </summary>
        public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
        {
            // 1. Load all raw texture assets first.
            Assets.LoadCoreContent(content);

            // 2. Load the map data from the file.
            var parentDir = Directory.GetParent(content.RootDirectory)?.FullName ?? content.RootDirectory;


            string prefabPath = Path.Combine(parentDir,"Assets" ,"Data", "objects.json");
            PrefabManager.Load(prefabPath);

            // 3. MAP: Load the actual level data (Binary .map for the game).
            gameMapPath = Path.Combine(parentDir,"Assets", "Maps", "level1.map");
            System.Diagnostics.Debug.WriteLine($"Map being Read for game to: {gameMapPath}");
            CurrentMap = MapSerializer.Read(gameMapPath);
            
            // 3. Create all TileSet instances and register them with the TilesetManager.
            // The manager needs the raw textures from the AssetLibrary to do its job.
            InitializeTilesets(graphicsDevice);
            Shaders = new ShaderManager(graphicsDevice);
            Shaders.LoadContent(content);
            //Physics.LoadMapData(CurrentMap);
            // 4. Create the player object.
            Player = new NewPlayer("Hero", new Vector2(200, 200), graphicsDevice);
            Player.LoadContent(content,Physics); // Player loads its own specific content
        }

        private void InitializeTilesets(GraphicsDevice graphicsDevice)
        {
            // Clear any old tilesets first
            TilesetManager.Clear();

            // Create a TileSet instance for each atlas we have loaded.
            // In a more advanced system, this would be driven by a manifest file.
            var basicTexture = Assets.GetAtlas("BasiR");
            if (basicTexture != null)
                TilesetManager.RegisterTileSet(new TileSet("BasiR", basicTexture, 16, graphicsDevice));

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
                if (!_uKeyPressedLastFrame)
                {
                    Weather.CycleWeather();
                }
                _uKeyPressedLastFrame = true;
            }
            else if (keyboardState.IsKeyDown(Keys.Y))
            {
                // Simple debounce so it doesn't cycle 60 times a second
                if (!_uKeyPressedLastFrame)
                {
                    Weather.CyclePhase();
                }
                _uKeyPressedLastFrame = true;
            }
            else
            {
                _uKeyPressedLastFrame = false;
            }
            if(keyboardState.IsKeyDown(Keys.OemCloseBrackets))
                TimeSystem.AddHours(1f);

            // Press '[' to jump backwards 1 hour
            if (keyboardState.IsKeyDown(Keys.OemOpenBrackets))
                TimeSystem.AddHours(-1f);

            // Update the weather math
            TimeSystem.Update(gameTime, Weather.CurrentSeason, Weather.Phase);
            Weather.Update(gameTime, TimeSystem.TimeOfDay);
            Shaders.UpdateParticles(gameTime, Weather,GameCamera.Position,new Vector2(960,540));
            Shaders.UpdatePostProcessing(Weather,TimeSystem,gameTime);
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
    }
}
