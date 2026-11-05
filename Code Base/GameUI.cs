using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Linq;

namespace Pixel_Simulations.UI
{
    public class GameUI : IUIContext
    {
        private readonly GameState _state;
        private readonly UITheme _theme;
        private readonly RenderPipeline _pipeline;

        private UIPanel _pauseRoot;
        private UIStackPanel _tabStack;
        private UIStackPanel _contentArea;
        private string _activeTab = "Status";

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
            int menuWidth = 820;
            int menuHeight = 520;
            Vector2 menuPos = new Vector2((_pipeline.FinalRect.Width - menuWidth) / 2f, (_pipeline.FinalRect.Height - menuHeight) / 2f);

            _pauseRoot = new UIPanel
            {
                LocalPosition = menuPos,
                Size = new Vector2(menuWidth, menuHeight),
                BackgroundColor = new Color(16, 18, 24, 240),
                BorderColor = new Color(50, 60, 80)
                
            };

            // 1. TOP HEADER BAR
            var header = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(16, 14),
                Size = new Vector2(menuWidth - 32, 36),
                AutoSize = false,
                PanelBackground = Color.Transparent,
                Spacing = 12f
            };
            header.AddChild(new UILabel { Text = "GAME PAUSED", TextColor = Color.Cyan });
            header.AddChild(new UIPanel { Size = new Vector2(2, 24), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

            _tabStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                Spacing = 6f,
                PanelBackground = Color.Transparent
            };

            string[] tabs = { "Status", "Environment", "Map / Warp", "Graphics", "Item Spawner" };
            foreach (var tab in tabs)
            {
                string tabName = tab;
                var btn = new UIButton
                {
                    Size = new Vector2(110, 28),
                    Text = tabName,
                    BackgroundColor = Color.Black * 0.4f
                };
                btn.OnClick = () =>
                {
                    _activeTab = tabName;
                    UpdateTabContent();
                };
                _tabStack.AddChild(btn);
            }
            header.AddChild(_tabStack);
            _pauseRoot.AddChild(header);

            // 2. MAIN CONTENT AREA
            _contentArea = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(16, 60),
                Size = new Vector2(menuWidth - 32, menuHeight - 125),
                PanelBackground = new Color(10, 12, 16, 200),
                BorderColor = new Color(35, 45, 60),
                Padding = 14f,
                Spacing = 10f,
                ClipToBounds = true,
                AutoSize = false
            };
            _pauseRoot.AddChild(_contentArea);

            // 3. FOOTER CONTROL BAR
            var footer = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(16, menuHeight - 52),
                Size = new Vector2(menuWidth - 32, 40),
                AutoSize = false,
                PanelBackground = Color.Transparent,
                Spacing = 10f
            };

            var btnResume = new UIButton
            {
                Size = new Vector2(160, 34),
                Text = "Resume (ESC)",
                BackgroundColor = new Color(30, 100, 60)
            };
            btnResume.OnClick = () => _state.DebugPool[GameBool.IsPaused] = false;

            var btnSave = new UIButton
            {
                Size = new Vector2(140, 34),
                Text = "Save Progress",
                BackgroundColor = new Color(40, 80, 120)
            };
            btnSave.OnClick = () => {
                _state.SaveGame();
                UpdateTabContent();
            };

            footer.AddChild(btnResume);
            footer.AddChild(btnSave);
            _pauseRoot.AddChild(footer);

            UpdateTabContent();
        }

        private void UpdateTabContent()
        {
            _contentArea.ClearChildren();
            float contentW = _contentArea.Size.X - 28f;

            // Highlight active tab
            for (int i = 0; i < _tabStack.Children.Count; i++)
            {
                var tabBtn = (UIButton)_tabStack.Children[i];
                bool isSelected = tabBtn.Text == _activeTab;
                tabBtn.BackgroundColor = isSelected ? Color.DarkCyan : Color.Black * 0.4f;
                tabBtn.BorderColor = isSelected ? Color.Cyan : Color.Gray * 0.5f;
            }

            if (_activeTab == "Status")
            {
                _contentArea.AddChild(new UILabel { Text = "CHARACTER & WORLD METRICS", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

                _contentArea.AddChild(new UILabel { Text = $"Position: {_state.Player.Position.X:F0}, {_state.Player.Position.Y:F0} (Facing: {_state.Player.FacingDirection})", TextColor = Color.White });
                _contentArea.AddChild(new UILabel { Text = $"Date: Year {_state.Calendar.Year}, {_state.Calendar.CurrentSeason} (Day {_state.Calendar.Day}) - Phase: {_state.Calendar.GetCurrentPhase()}", TextColor = Color.LightGray });

                int hrs = (int)_state.TimeSystem.TimeOfDay;
                int mins = (int)((_state.TimeSystem.TimeOfDay - hrs) * 60);
                _contentArea.AddChild(new UILabel { Text = $"Time: {hrs:D2}:{mins:D2} | Ambient Temp: {_state.Weather.CurrentClimate.TempC:F1} C", TextColor = Color.LightGray });
                _contentArea.AddChild(new UILabel { Text = $"Live Weather: {_state.Weather.CurrentWeather} | Wind: {_state.Weather.CurrentClimate.WindKph:F0} km/h", TextColor = Color.LimeGreen });
                _contentArea.AddChild(new UILabel { Text = $"Inventory Carry Weight: {_state.Player.Inventory.CurrentWeight:F1} / {_state.Player.Inventory.MaxWeight:F1} kg", TextColor = Color.Cyan });
            }
            else if (_activeTab == "Environment")
            {
                _contentArea.AddChild(new UILabel { Text = "CALENDAR & CLIMATE CONTROL", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

                // Date stepper
                var calRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 8f, PanelBackground = Color.Transparent };
                var prevDayBtn = new UIButton { Size = new Vector2(36, 28), Text = "<", BackgroundColor = Color.DarkSlateGray };
                prevDayBtn.OnClick = () => { _state.Calendar.DebugSetDate(_state.Calendar.CurrentSeason, _state.Calendar.Day - 1); UpdateTabContent(); };

                var seasonBtn = new UIButton { Size = new Vector2(170, 28), Text = $"Season: {_state.Calendar.CurrentSeason}", BackgroundColor = Color.DarkSlateGray };
                seasonBtn.OnClick = () => {
                    int nextS = ((int)_state.Calendar.CurrentSeason + 1) % 6;
                    _state.Calendar.DebugSetDate((Season)nextS, 1);
                    UpdateTabContent();
                };

                var nextDayBtn = new UIButton { Size = new Vector2(36, 28), Text = ">", BackgroundColor = Color.DarkGreen };
                nextDayBtn.OnClick = () => { _state.Calendar.AdvanceDay(); UpdateTabContent(); };

                calRow.AddChild(prevDayBtn);
                calRow.AddChild(seasonBtn);
                calRow.AddChild(new UILabel { Text = $"Day {_state.Calendar.Day}", TextColor = Color.White });
                calRow.AddChild(nextDayBtn);
                _contentArea.AddChild(calRow);

                // Time slider
                var timeRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };
                int h = (int)_state.TimeSystem.TimeOfDay; int m = (int)((_state.TimeSystem.TimeOfDay - h) * 60);
                var timeLabel = new UILabel { Text = $"Time: {h:D2}:{m:D2}", TextColor = Color.White };
                var timeSlider = new UISlider { Size = new Vector2(280, 18), Min = 0f, Max = 23.99f, Value = _state.TimeSystem.TimeOfDay };
                timeSlider.OnValueChanged = (val) => {
                    _state.TimeSystem.SetTime(val);
                    int curH = (int)val; int curM = (int)((val - curH) * 60);
                    timeLabel.Text = $"Time: {curH:D2}:{curM:D2}";
                };
                timeRow.AddChild(timeSlider);
                timeRow.AddChild(timeLabel);
                _contentArea.AddChild(timeRow);

                // Weather override button
                string wText = _state.Weather.ForcedWeather.HasValue ? _state.Weather.ForcedWeather.Value.ToString() : "Dynamic (Forecast)";
                var weatherBtn = new UIButton { Size = new Vector2(260, 28), Text = $"Weather: {wText}", BackgroundColor = _state.Weather.ForcedWeather.HasValue ? Color.DarkCyan : Color.DarkSlateGray };
                weatherBtn.OnClick = () => {
                    if (!_state.Weather.ForcedWeather.HasValue) _state.Weather.ForcedWeather = (WeatherType)0;
                    else
                    {
                        int next = (int)_state.Weather.ForcedWeather.Value + 1;
                        if (next >= Enum.GetValues(typeof(WeatherType)).Length) _state.Weather.ForcedWeather = null;
                        else _state.Weather.ForcedWeather = (WeatherType)next;
                    }
                    UpdateTabContent();
                };
                _contentArea.AddChild(weatherBtn);
            }
            else if (_activeTab == "Map / Warp")
            {
                _contentArea.AddChild(new UILabel { Text = "FAST TRAVEL & WAYPOINTS", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

                // Gather all valid spawn anchors across the world
                var spawnList = _state._entityManager.AllEntities
                    .Where(e => e.BaseData.Tags.Contains(20) ||
                               (e.BaseData is PointObject pt && pt.Label.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (e.BaseData.Properties.ContainsKey("SpawnType")))
                    .ToList();

                if (spawnList.Count == 0)
                {
                    _contentArea.AddChild(new UILabel { Text = "No active spawn points found on current map.", TextColor = Color.Gray });
                }
                else
                {
                    _contentArea.AddChild(new UILabel { Text = $"Found {spawnList.Count} registered destination(s):", TextColor = Color.LightGray });

                    for (int i = 0; i < spawnList.Count; i++)
                    {
                        var spawnEntity = spawnList[i];
                        int index = i + 1;

                        string spawnName = !string.IsNullOrEmpty(spawnEntity.BaseData.Name)
                            ? spawnEntity.BaseData.Name
                            : $"Waypoint #{index}";

                        string spawnType = spawnEntity.GetProperty("SpawnType", "General");
                        Vector2 targetPos = spawnEntity.Position;

                        var row = new UIStackPanel
                        {
                            Direction = StackDirection.Horizontal,
                            Spacing = 10f,
                            PanelBackground = Color.Black * 0.3f,
                            Padding = 4f
                        };

                        var warpBtn = new UIButton
                        {
                            Size = new Vector2(180, 26),
                            Text = $"Teleport to #{index}",
                            BackgroundColor = Color.DarkMagenta
                        };

                        warpBtn.OnClick = () => {
                            _state.Player.Position = targetPos;
                            _state.Player.TargetWalkPos = null;
                            _state.Player.PendingInteraction = null;
                            _state.GameCamera.SnapTo(targetPos, _state.GameCamera.Zoom);
                            _state.DebugPool[GameBool.IsPaused] = false;
                        };

                        row.AddChild(warpBtn);
                        row.AddChild(new UILabel { Text = $"{spawnName} [{spawnType}] at ({targetPos.X:F0}, {targetPos.Y:F0})", TextColor = Color.White });

                        _contentArea.AddChild(row);
                    }
                }
            }
            else if (_activeTab == "Graphics")
            {
                _contentArea.AddChild(new UILabel { Text = "GRAPHICS & CAMERA SETTINGS", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

                var toggleParaBtn = new UIButton
                {
                    Size = new Vector2(220, 28),
                    Text = $"Parallax: {(_state.DebugPool[GameBool.EnableParallax] ? "ENABLED" : "DISABLED")}",
                    BackgroundColor = _state.DebugPool[GameBool.EnableParallax] ? Color.DarkGreen : Color.DarkRed
                };
                toggleParaBtn.OnClick = () => {
                    _state.DebugPool[GameBool.EnableParallax] = !_state.DebugPool[GameBool.EnableParallax];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleParaBtn);

                var toggleColBtn = new UIButton
                {
                    Size = new Vector2(220, 28),
                    Text = $"Hitboxes: {(_state.DebugPool[GameBool.ShowCollision] ? "VISIBLE" : "HIDDEN")}",
                    BackgroundColor = _state.DebugPool[GameBool.ShowCollision] ? Color.DarkGreen : Color.DarkRed
                };
                toggleColBtn.OnClick = () => {
                    _state.DebugPool[GameBool.ShowCollision] = !_state.DebugPool[GameBool.ShowCollision];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleColBtn);
                var toggleZoomBtn = new UIButton
                {
                    Size = new Vector2(240, 28),
                    Text = $"Dev Zoom Levels: {(_state.DebugPool[GameBool.ExtendedZoom] ? "ENABLED (0.5x-4x)" : "LOCKED (1x, 2x)")}",
                    BackgroundColor = _state.DebugPool[GameBool.ExtendedZoom] ? Color.DarkCyan : Color.DarkSlateGray
                };
                toggleZoomBtn.OnClick = () => {
                    _state.DebugPool[GameBool.ExtendedZoom] = !_state.DebugPool[GameBool.ExtendedZoom];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleZoomBtn);
            }
            else if (_activeTab == "Item Spawner")
            {
                _contentArea.AddChild(new UILabel { Text = "INVENTORY ITEM SPAWNER", TextColor = Color.Yellow });
                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

                var spawnRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 8f, PanelBackground = Color.Transparent };
                var idInput = new UITextBox { Size = new Vector2(60, 28), Text = "1", Placeholder = "ID" };
                idInput.OnTextChanged = (t) => idInput.Text = t;

                var amtInput = new UITextBox { Size = new Vector2(60, 28), Text = "5", Placeholder = "Amt" };
                amtInput.OnTextChanged = (t) => amtInput.Text = t;

                var giveBtn = new UIButton { Size = new Vector2(130, 28), Text = "Add to Inventory", BackgroundColor = Color.DarkGreen };
                giveBtn.OnClick = () => {
                    if (int.TryParse(idInput.Text, out int id) && int.TryParse(amtInput.Text, out int amt))
                    {
                        _state.Player.Inventory.AddItem(id, amt, _state.ItemManager);
                    }
                };

                spawnRow.AddChild(new UILabel { Text = "ID:", TextColor = Color.LightGray });
                spawnRow.AddChild(idInput);
                spawnRow.AddChild(new UILabel { Text = "Qty:", TextColor = Color.LightGray });
                spawnRow.AddChild(amtInput);
                spawnRow.AddChild(giveBtn);
                _contentArea.AddChild(spawnRow);
            }
            _contentArea.UpdateLayout();
        }

        public void Update(GameTime gameTime)
        {
            UIInput.CurrentMouse = Mouse.GetState();
            UIInput.CurrentKeyboard = Keyboard.GetState();
            UIInput.MouseWindowPosition = UIInput.CurrentMouse.Position.ToVector2();
            UIInput.Update(gameTime);

            if (_state.IsPaused)
            {
                if (UIInput.IsNewLeftClick) CheckFocusClick(_pauseRoot, UIInput);
                _pauseRoot.Update(UIInput, null);
            }

            UIInput.PreviousMouse = UIInput.CurrentMouse;
            UIInput.PreviousKeyboard = UIInput.CurrentKeyboard;
        }

        public void Draw(SpriteBatch sb)
        {
            // Always ensure the default font is globally accessible to labels
            if (UITheme.DefaultFont == null && _theme.Font != null)
            {
                UITheme.DefaultFont = _theme.Font;
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // 1. ALWAYS DRAW THE ON-SCREEN DEBUG READOUT (Left Screen Space)
            if (_state.DebugPool.ContainsKey(GameBool.ShowShapes) && _state.DebugPool[GameBool.ShowShapes])
            {
                DrawDebugInfo(sb);
            }
            else
            {
                // Even if debug shapes are toggled off, keep the core diagnostic telemetry
                DrawDebugInfo(sb);
            }

            // 2. DRAW PAUSE MENU MODAL (Only when paused)
            if (_state.IsPaused)
            {
                // Darken the background viewport with frosted tint
                sb.FillRectangle(_pipeline.FinalRect, Color.Black * 0.65f);

                // Render the Pause UI hierarchy
                _pauseRoot.Draw(sb, this, _theme);
            }

            sb.End();
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
        private void DrawDebugInfo(SpriteBatch sb)
        {
            if (_theme.Font == null) return;

            var cam = _state.GameCamera;
            var player = _state.Player;
            var heldStack = player?.Inventory?.GetSelectedItem();
            string heldStr = heldStack != null ? $"ID: {heldStack.ItemID} (Qty: {heldStack.Count})" : "Empty Hands";

            var tilledLayer = _state.CurrentMap?.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Tilled_Dirt");
            string tilledStr = tilledLayer != null ? $"Exists ({tilledLayer.Chunks.Count} chunks)" : "Not Created Yet";

            int hrs = (int)_state.TimeSystem.TimeOfDay;
            int mins = (int)((_state.TimeSystem.TimeOfDay - hrs) * 60);

            string debugText =
                "--- ENGINE PIPELINE ---\n" +
                $"Active RT View: {_pipeline._currentDebugView}\n" +
                $"Native Target: {_pipeline.NativeRect.Width}x{_pipeline.NativeRect.Height} (480p)\n" +
                $"Simulation RT: {_pipeline.SimRect.Width}x{_pipeline.SimRect.Height} (960p)\n" +
                $"Presentation : {_pipeline.FinalRect.Width}x{_pipeline.FinalRect.Height} (1080p)\n\n" +

                "--- CAMERA STATE ---\n" +
                $"Center World: {cam.Position.X:F1}, {cam.Position.Y:F1}\n" +
                $"Zoom Level  : {cam.Zoom:F2}x\n" +
                $"Sim Bounds  : {cam.SimViewBounds.X},{cam.SimViewBounds.Y} [{cam.SimViewBounds.Width}x{cam.SimViewBounds.Height}]\n\n" +

                "--- ENVIRONMENT & CLIMATE ---\n" +
                $"Time of Day : {hrs:D2}:{mins:D2} (Scale: {_state.TimeSystem.TimeScale:F3})\n" +
                $"Season/Date : Year {_state.Calendar.Year}, {_state.Calendar.CurrentSeason} (Day {_state.Calendar.Day})\n" +
                $"Weather Live: {_state.Weather.CurrentWeather} (Temp: {_state.Weather.CurrentClimate.TempC:F1}C)\n" +
                $"Atmosphere  : Humid: {_state.Weather.CurrentClimate.Humidity:F0}% | Wind: {_state.Weather.CurrentClimate.WindKph:F0} kph\n\n" +

                "--- PLAYER ENTITY ---\n" +
                $"World Foot  : {player.Foot.X:F1}, {player.Foot.Y:F1}\n" +
                $"Facing Dir  : {player.FacingDirection} | Moving: {player.IsMoving}\n" +
                $"Held Item   : {heldStr}\n" +
                $"Mouse World : {_state.input.MouseWorldPosition.X:F0}, {_state.input.MouseWorldPosition.Y:F0}\n\n" +

                "--- WORLD BUFFERS ---\n" +
                $"Map Layers  : {_state.CurrentMap?.Layers.Count ?? 0} | Tilled Soil: {tilledStr}\n" +
                $"Mask Chunks : {_state.TerrainMaskChunks.Count} Loaded\n" +
                $"Active Sprites: {_state._entityManager._drawList.Count}\n" +
                $"Composite Cache: {_state._entityManager._compositeCache.Count} Textures";

            // Drop shadow for legibility over bright backgrounds
            sb.DrawString(_theme.Font, debugText, new Vector2(16, 16), Color.Black * 0.9f);
            sb.DrawString(_theme.Font, debugText, new Vector2(15, 15), Color.LimeGreen);
        }
        public void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color) { }
    }
}