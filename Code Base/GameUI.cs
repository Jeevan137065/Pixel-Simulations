using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace Pixel_Simulations.UI
{
    public class GameUI
    {
        private readonly GameState _state;
        private readonly UITheme _theme;
        private readonly RenderPipeline _pipeline;
        // UI Framework Elements
        private UIPanel _pauseRoot;
        private UILabel _tabContentLabel;
        private string _activeTab = "Map";

        // Input state required by UIFramework
        public EditorInputState UIInput { get; private set; }
        public GameUI(GameState state, UITheme theme, RenderPipeline pipeline)
        {
            _state = state;
            _theme = theme;
            _pipeline = pipeline;
            UIInput = new EditorInputState();

            BuildPauseMenu();
        }
        private void BuildPauseMenu()
        {
            // 1. Root Container (Centered on screen)
            int menuWidth = 600;
            int menuHeight = 400;
            _pauseRoot = new UIPanel
            {
                LocalPosition = new Vector2((_pipeline.FinalRect.Width - menuWidth) / 2, (_pipeline.FinalRect.Height - menuHeight) / 2),
                Size = new Vector2(menuWidth, menuHeight),
                BackgroundColor = Color.Black * 0.9f,
                BorderColor = Color.White
            };

            // 2. Top Navigation Tabs
            var tabStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(10, 10),
                Spacing = 10f,
                PanelBackground = Color.Transparent
            };

            string[] tabs = { "Map", "Player", "Settings", "Resume" };
            foreach (var tab in tabs)
            {
                string tabName = tab; // Closure safety
                var btn = new UIButton
                {
                    Size = new Vector2(100, 30),
                    Text = tabName,
                    BackgroundColor = Color.DarkSlateGray
                };

                btn.OnClick = () =>
                {
                    if (tabName == "Resume") _state.DebugPool[GameBool.IsPaused] = false;
                    else
                    {
                        _activeTab = tabName;
                        UpdateTabContent();
                    }
                };
                tabStack.AddChild(btn);
            }

            // 3. Content Area
            var contentArea = new UIPanel
            {
                LocalPosition = new Vector2(10, 50),
                Size = new Vector2(menuWidth - 20, menuHeight - 60),
                BackgroundColor = Color.Black * 0.5f,
                BorderColor = Color.Gray
            };

            _tabContentLabel = new UILabel
            {
                LocalPosition = new Vector2(20, 20),
                Text = "Map Data Here",
                TextColor = Color.LightGray
            };

            contentArea.AddChild(_tabContentLabel);

            _pauseRoot.AddChild(tabStack);
            _pauseRoot.AddChild(contentArea);
        }

        private void UpdateTabContent()
        {
            if (_activeTab == "Map") _tabContentLabel.Text = $"Current Map: {_state.CurrentMap?.Layers.Count ?? 0} Layers";
            else if (_activeTab == "Player") _tabContentLabel.Text = $"Player Position: {_state.Player.Position.X:F0}, {_state.Player.Position.Y:F0}";
            else if (_activeTab == "Settings") _tabContentLabel.Text = "Settings Menu (Volume, Resolution, etc.)";
        }
        public void Update(GameTime gameTime)
        {
            // Feed real-time input data into the UI framework input wrapper
            UIInput.CurrentMouse = Mouse.GetState();
            UIInput.CurrentKeyboard = Keyboard.GetState();
            UIInput.MouseWindowPosition = UIInput.CurrentMouse.Position.ToVector2();
            UIInput.Update(gameTime); // Processes click timers

            if (_state.IsPaused)
            {
                // Pass null for EditorUI and EventBus since we don't use them in the game side
                _pauseRoot.Update(UIInput, null);
            }

            UIInput.PreviousMouse = UIInput.CurrentMouse;
            UIInput.PreviousKeyboard = UIInput.CurrentKeyboard;
        }

        public void Draw(SpriteBatch sb)
        {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Draw HUD (Always visible)
            DrawDebugInfo(sb);
            //sb.DrawString(_theme.Font, $"Time: {_state.TimeSystem.TimeOfDay:F1} | Weather: {_state.Weather.CurrentWeather}", new Vector2(10, 10), Color.White);
            //sb.DrawString(_theme.Font, "Press ESC to Pause", new Vector2(10, 35), Color.Yellow);

            // Draw Pause Menu (Only when paused)
            if (_state.IsPaused)
            {
                // Darken the background screen
                sb.FillRectangle(_pipeline.FinalRect, Color.Black * 0.5f);

                // Draw the UI Element hierarchy
                _pauseRoot.Draw(sb, null, _theme);
            }

            sb.End();
        }
        private void DrawDebugInfo(SpriteBatch sb)
        {
            if (_theme.Font == null) return;

            var cam = _state.GameCamera;
            var player = _state.Player;

            // Build the massive debug string
            string debugText =
                "--- PIPELINE STATE ---\n" +
                $"Current Debug RT: {_pipeline._currentDebugView}\n" +
                $"Native Rect: {_pipeline.NativeRect}\n" +
                $"Sim Rect: {_pipeline.SimRect}\n" +
                $"Final Rect: {_pipeline.FinalRect}\n\n" +

                "--- CAMERA STATE ---\n" +
                $"Pos: {cam.Position.X:F1}, {cam.Position.Y:F1}\n" +
                $"Zoom: {cam.Zoom:F2}\n" +
                $"Cull View: {cam.CameraView}\n\n" +

                "--- MATRICES ---\n" +
                $"Native M11(ScaleX): {cam.NativeFinal.M11:F3}\n" +
                $"Native M41(TransX): {cam.NativeFinal.M41:F3}\n" +
                $"Sim M11(ScaleX): {cam.SimFinal.M11:F3}\n" +
                $"Sim M41(TransX): {cam.SimFinal.M41:F3}\n\n" +

                "--- PLAYER ---\n" +
                $"Pos: {player.Position.X:F1}, {player.Position.Y:F1}\n" +
                $"Has Texture: {player.Texture != null}\n\n" +

                "--- MAP ---\n" +
                $"Loaded: {_state.CurrentMap != null}\n" +
                $"Layers: {_state.CurrentMap?.Layers.Count ?? 0}" +
                $"Mask Layer ";

            // Draw black shadow for readability
            sb.DrawString(_theme.Font, debugText, new Vector2(11, 11), Color.Black);
            sb.DrawString(_theme.Font, debugText, new Vector2(10, 10), Color.LimeGreen);
        }
        private void DrawHotbar(SpriteBatch sb)
        {
            // Standard loop rendering the _state.Player.Inventory...
        }
    }
}