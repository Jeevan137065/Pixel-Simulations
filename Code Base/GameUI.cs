using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;

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
                _contentArea.AddChild(new UILabel { Text = "Environment Simulation", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(500, 2), BackgroundColor = Color.Gray });

                // --- 1. CALENDAR CONTROLS ---
                var calRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };

                var seasonBtn = new UIButton { Size = new Vector2(160, 30), Text = $"Season: {_state.Calendar.CurrentSeason}", BackgroundColor = Color.DarkSlateGray };
                seasonBtn.OnClick = () => {
                    int nextS = ((int)_state.Calendar.CurrentSeason + 1) % 6;
                    _state.Calendar.DebugSetDate((Season)nextS, 1); // Jump to Day 1 of next season
                    UpdateTabContent();
                };

                var dayBtn = new UIButton { Size = new Vector2(160, 30), Text = $"Day: {_state.Calendar.Day} ({_state.Calendar.GetCurrentPhase()})", BackgroundColor = Color.DarkSlateGray };
                dayBtn.OnClick = () => {
                    int nextD = _state.Calendar.Day + 7; // Jump by a week
                    if (nextD > 28) nextD = 1;
                    _state.Calendar.DebugSetDate(_state.Calendar.CurrentSeason, nextD);
                    UpdateTabContent();
                };

                calRow.AddChild(seasonBtn);
                calRow.AddChild(dayBtn);
                _contentArea.AddChild(calRow);

                // --- 2. TIME SCRUBBER ---
                var timeRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 15f, PanelBackground = Color.Transparent, Padding = 10f };

                int hrs = (int)_state.TimeSystem.TimeOfDay;
                int mins = (int)((_state.TimeSystem.TimeOfDay - hrs) * 60);
                var timeLabel = new UILabel { Text = $"Time: {hrs:D2}:{mins:D2}", TextColor = Color.White };

                var timeSlider = new UISlider
                {
                    Size = new Vector2(300, 20),
                    Min = 0f,
                    Max = 23.99f,
                    Value = _state.TimeSystem.TimeOfDay
                };

                timeSlider.OnValueChanged = (val) => {
                    _state.TimeSystem.SetTime(val);
                    int h = (int)val; int m = (int)((val - h) * 60);
                    timeLabel.Text = $"Time: {h:D2}:{m:D2}";
                };

                timeRow.AddChild(timeSlider);
                timeRow.AddChild(timeLabel);
                _contentArea.AddChild(timeRow);

                // --- 3. WEATHER OVERRIDES ---
                var weatherRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };

                string wText = _state.Weather.ForcedWeather.HasValue ? _state.Weather.ForcedWeather.Value.ToString() : "Auto";
                var toggleWeatherBtn = new UIButton { Size = new Vector2(250, 30), Text = $"Force Weather: {wText}", BackgroundColor = _state.Weather.ForcedWeather.HasValue ? Color.DarkCyan : Color.DarkSlateGray };

                toggleWeatherBtn.OnClick = () => {
                    if (!_state.Weather.ForcedWeather.HasValue) _state.Weather.ForcedWeather = (WeatherType)0;
                    else
                    {
                        int next = (int)_state.Weather.ForcedWeather.Value + 1;
                        if (next >= Enum.GetValues(typeof(WeatherType)).Length) _state.Weather.ForcedWeather = null;
                        else _state.Weather.ForcedWeather = (WeatherType)next;
                    }
                    UpdateTabContent();
                };

                // Live Readout Label
                var wLiveLabel = new UILabel { Text = $"Live: {_state.Weather.CurrentWeather} ({_state.Weather.CurrentClimate.TempC:F0}C)", TextColor = Color.LimeGreen };

                weatherRow.AddChild(toggleWeatherBtn);
                weatherRow.AddChild(wLiveLabel);
                _contentArea.AddChild(weatherRow);

                // --- 4. DEBUG TOGGLES ---
                _contentArea.AddChild(new UIPanel { Size = new Vector2(500, 2), BackgroundColor = Color.Gray });
                _contentArea.AddChild(new UILabel { Text = "Visual Debug Toggles:", TextColor = Color.Cyan });

                var toggleColBtn = new UIButton { Size = new Vector2(200, 30), Text = $"Hitboxes: {_state.DebugPool[GameBool.ShowCollision]}", BackgroundColor = _state.DebugPool[GameBool.ShowCollision] ? Color.DarkGreen : Color.DarkRed };
                toggleColBtn.OnClick = () => { _state.DebugPool[GameBool.ShowCollision] = !_state.DebugPool[GameBool.ShowCollision]; UpdateTabContent(); };
                _contentArea.AddChild(toggleColBtn);

                var toggleParaBtn = new UIButton { Size = new Vector2(200, 30), Text = $"Parallax: {_state.DebugPool[GameBool.EnableParallax]}", BackgroundColor = _state.DebugPool[GameBool.EnableParallax] ? Color.DarkGreen : Color.DarkRed };
                toggleParaBtn.OnClick = () => { _state.DebugPool[GameBool.EnableParallax] = !_state.DebugPool[GameBool.EnableParallax]; UpdateTabContent(); };
                _contentArea.AddChild(toggleParaBtn);
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

                "--- WEATHER ---\n" +
                $"CurrentTime: {_state.TimeSystem.TimeOfDay:F3}, {_state.TimeSystem.GetDebugInfo()}\n" +
                //$"Weather Data: {_state.Weather.CurrentSeason}, {_state.Weather.CurrentClimate}, {_state.Weather.CurrentWeather}\n" +
                $"Shaders Loaded: {_state.Shaders.Effects.Count}\n" +
                $"Weather info: {_state.Weather.GetDebugInfo(_state.TimeSystem.TimeOfDay)}\n\n" +
                //"--- MATRICES ---\n" +
                //$"Native M11(ScaleX): {cam.NativeTransform.M11:F3}\n" +
                //$"Native M41(TransX): {cam.SimTransform.M41:F3}\n" +
                //$"Sim M11(ScaleX): {cam.SimTransform.M11:F3}\n" +
                //$"Sim M41(TransX): {cam.SimTransform.M41:F3}\n\n" +
                "--- PLAYER ---\n" +
                $"Pos: {player.Position.X:F1}, {player.Position.Y:F1}\n" +
                $"Has Texture: {player.Texture != null}\n\n" +
                $"Mouse in World: {_state.input.MouseWorldPosition}"+
                "--- ITEMS ---\n" +
                $"Items Loaded: {_state.ItemManager.Items.Count}\n" +
                $"Grass Count {_state.Grass._bladeVertCount}"+
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