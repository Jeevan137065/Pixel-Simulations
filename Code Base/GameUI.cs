using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace Pixel_Simulations.UI
{
    public class GameUI : IUIContext
    {
        private readonly GameState _state;
        private readonly UITheme _theme;
        private readonly RenderPipeline _pipeline;
        // UI Framework Elements
        private UIPanel _pauseRoot;
        private UIStackPanel _contentArea;
        private string _activeTab = "Debug";

        // Input state required by UIFramework
        public EditorInputState UIInput { get; private set; }
        public UIElement FocusedElement { get; private set; }

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
            int menuWidth = 600;
            int menuHeight = 400;
            _pauseRoot = new UIPanel
            {
                LocalPosition = new Vector2((_pipeline.FinalRect.Width - menuWidth) / 2, (_pipeline.FinalRect.Height - menuHeight) / 2),
                Size = new Vector2(menuWidth, menuHeight),
                BackgroundColor = Color.Black * 0.9f,
                BorderColor = Color.White
            };

            // Top Navigation Tabs
            var tabStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(10, 10),
                Spacing = 10f,
                PanelBackground = Color.Transparent
            };

            string[] tabs = { "Map", "Player", "Debug", "Settings", "Resume" };
            foreach (var tab in tabs)
            {
                string tabName = tab;
                var btn = new UIButton
                {
                    Size = new Vector2(100, 30),
                    Text = tabName,
                    BackgroundColor = Color.DarkSlateGray
                };

                btn.OnClick = () =>
                {
                    if (tabName == "Resume") _state.DebugPool[GameBool.IsPaused] = false; // Or however you toggle pause
                    else
                    {
                        _activeTab = tabName;
                        UpdateTabContent();
                    }
                };
                tabStack.AddChild(btn);
            }

            // Content Area (Now a StackPanel to hold multiple UI Elements cleanly!)
            _contentArea = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(10, 50),
                Size = new Vector2(menuWidth - 20, menuHeight - 60),
                PanelBackground = Color.Black * 0.5f,
                BorderColor = Color.Gray,
                Padding = 15f,
                Spacing = 10f
            };

            _pauseRoot.AddChild(tabStack);
            _pauseRoot.AddChild(_contentArea);

            UpdateTabContent(); // Initial build
        }

        private void UpdateTabContent()
        {
            _contentArea.ClearChildren(); // Remove old tab content

            if (_activeTab == "Map")
            {
                _contentArea.AddChild(new UILabel { Text = $"Current Map: {_state.CurrentMap?.Layers.Count ?? 0} Layers", TextColor = Color.White });
            }
            else if (_activeTab == "Player")
            {
                _contentArea.AddChild(new UILabel { Text = $"Player Position: {_state.Player.Position.X:F0}, {_state.Player.Position.Y:F0}", TextColor = Color.White });
            }

            else if (_activeTab == "Debug")
            {
                _contentArea.AddChild(new UILabel { Text = "Game Debug Tools", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(500, 2), BackgroundColor = Color.Gray });

                // --- TOGGLE BUTTON HELPERS ---
                var toggleColBtn = new UIButton { Size = new Vector2(250, 35), Text = $"Show Collisions: {_state.DebugPool[GameBool.ShowCollision]}", BackgroundColor = _state.DebugPool[GameBool.ShowCollision] ? Color.DarkGreen : Color.DarkRed };
                toggleColBtn.OnClick = () =>
                {
                    _state.DebugPool[GameBool.ShowCollision] = !_state.DebugPool[GameBool.ShowCollision];
                    UpdateTabContent(); // Refresh button visuals
                };
                _contentArea.AddChild(toggleColBtn);

                var toggleLinkBtn = new UIButton { Size = new Vector2(250, 35), Text = $"Show Links: {_state.DebugPool[GameBool.ShowLinks]}", BackgroundColor = _state.DebugPool[GameBool.ShowLinks] ? Color.DarkGreen : Color.DarkRed };
                toggleLinkBtn.OnClick = () =>
                {
                    _state.DebugPool[GameBool.ShowLinks] = !_state.DebugPool[GameBool.ShowLinks];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleLinkBtn);

                var toggleShapesBtn = new UIButton { Size = new Vector2(250, 35), Text = $"Show All Shapes: {_state.DebugPool[GameBool.ShowShapes]}", BackgroundColor = _state.DebugPool[GameBool.ShowShapes] ? Color.DarkGreen : Color.DarkRed };
                toggleShapesBtn.OnClick = () =>
                {
                    _state.DebugPool[GameBool.ShowShapes] = !_state.DebugPool[GameBool.ShowShapes];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleShapesBtn);

                // Add some info about the Entity Manager
                _contentArea.AddChild(new UIPanel { Size = new Vector2(500, 2), BackgroundColor = Color.Gray });
                //_contentArea.AddChild(new UILabel { Text = $"Total Cached Entities: {_state..AllEntities.Count}", TextColor = Color.Cyan });
                // --- ITEM SPAWNER ROW ---
                _contentArea.AddChild(new UIPanel { Size = new Vector2(500, 2), BackgroundColor = Color.Gray });
                _contentArea.AddChild(new UILabel { Text = "Item Spawner:", TextColor = Color.Cyan });

                var row = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };
                var idInput = new UITextBox { Size = new Vector2(100, 30), Placeholder = "ID (e.g. 1)" };
                var countInput = new UITextBox { Size = new Vector2(100, 30), Placeholder = "Amount" };
                var spawnBtn = new UIButton { Size = new Vector2(120, 30), Text = "Give Item", BackgroundColor = Color.DarkGreen };

                spawnBtn.OnClick = () => {
                    if (int.TryParse(idInput.Text, out int id) && int.TryParse(countInput.Text, out int count))
                    {
                        int leftover = _state.Player.Inventory.AddItem(id, count, _state.ItemManager);
                        if (leftover > 0) System.Diagnostics.Debug.WriteLine($"Failed to add {leftover} items.");
                    }
                };

                row.AddChild(idInput);
                row.AddChild(countInput);
                row.AddChild(spawnBtn);
                _contentArea.AddChild(row);
            }
            else if (_activeTab == "Settings")
            {
                _contentArea.AddChild(new UILabel { Text = "Video Settings", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(500, 2), BackgroundColor = Color.Gray });

                // 1. Toggle Parallax
                var toggleParaBtn = new UIButton { Size = new Vector2(250, 35), Text = $"Enable Parallax: {_state.DebugPool[GameBool.EnableParallax]}", BackgroundColor = _state.DebugPool[GameBool.EnableParallax] ? Color.DarkGreen : Color.DarkRed };
                toggleParaBtn.OnClick = () =>
                {
                    _state.DebugPool[GameBool.EnableParallax] = !_state.DebugPool[GameBool.EnableParallax];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleParaBtn);

                // 2. Adjust Strength Slider (- / +)
                var strengthRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
                strengthRow.AddChild(new UILabel { Text = "Parallax Strength:", TextColor = Color.White });

                var minusBtn = new UIButton { Size = new Vector2(40, 30), Text = "-", BackgroundColor = Color.DarkSlateGray };
                // Clamps min to 0.0f
                minusBtn.OnClick = () => { _state.ParallaxStrength = System.Math.Max(0.0f, _state.ParallaxStrength - 0.05f); UpdateTabContent(); };

                var valueLbl = new UILabel { Text = $"{_state.ParallaxStrength:F2}", TextColor = Color.Cyan };

                var plusBtn = new UIButton { Size = new Vector2(40, 30), Text = "+", BackgroundColor = Color.DarkSlateGray };
                // Clamps max to 0.5f as requested!
                plusBtn.OnClick = () => { _state.ParallaxStrength = System.Math.Min(0.5f, _state.ParallaxStrength + 0.05f); UpdateTabContent(); };

                strengthRow.AddChild(minusBtn);
                strengthRow.AddChild(valueLbl);
                strengthRow.AddChild(plusBtn);

                _contentArea.AddChild(strengthRow);
            }
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
                if (UIInput.IsNewLeftClick) CheckFocusClick(_pauseRoot, UIInput);
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
                $"Cull View: {cam.SimViewBounds}\n\n" +

                "--- MATRICES ---\n" +
                $"Native M11(ScaleX): {cam.NativeTransform.M11:F3}\n" +
                $"Native M41(TransX): {cam.SimTransform.M41:F3}\n" +
                $"Sim M11(ScaleX): {cam.SimTransform.M11:F3}\n" +
                $"Sim M41(TransX): {cam.SimTransform.M41:F3}\n\n" +

                "--- PLAYER ---\n" +
                $"Pos: {player.Position.X:F1}, {player.Position.Y:F1}\n" +
                $"Has Texture: {player.Texture != null}\n\n" +

                "--- ITEMS ---\n" +
                $"Items Loaded: {_state.ItemManager.Items.Count}\n" +
                //$"Layers: {_state.CurrentMap?.Layers.Count ?? 0}" +
                //$"Mask Layer Chunks {_state.TerrainMaskChunks.Count}"+
                "--- MAP ---\n" +
                $"Loaded: {_state.CurrentMap != null}\n" +
                $"Layers: {_state.CurrentMap?.Layers.Count ?? 0}" +
                $"Mask Layer Chunks {_state.TerrainMaskChunks.Count}";

            // Draw black shadow for readability
            sb.DrawString(_theme.Font, debugText, new Vector2(11, 11), Color.Black);
            sb.DrawString(_theme.Font, debugText, new Vector2(10, 10), Color.LimeGreen);
        }
        public void SetFocus(UIElement element)
        {
            if (FocusedElement != null) FocusedElement.IsFocused = false;
            FocusedElement = element;
            if (FocusedElement != null) FocusedElement.IsFocused = true;
        }

        public void CheckFocusClick(UIElement root, EditorInputState input)
        {
            var hit = FindElementAt(root, input.MouseWindowPosition);
            if (hit is UITextBox) SetFocus(hit);
            else if (hit == null || !(hit is UIButton)) SetFocus(null);
        }
        private UIElement FindElementAt(UIElement element, Vector2 mousePos)
        {
            if (!element.IsVisible || !element.AbsoluteBounds.Contains(mousePos)) return null;
            for (int i = element.Children.Count - 1; i >= 0; i--)
            {
                var hit = FindElementAt(element.Children[i], mousePos);
                if (hit != null) return hit;
            }
            return element;
        }
        public void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color) { } // Ignored for GameUI
    }
}