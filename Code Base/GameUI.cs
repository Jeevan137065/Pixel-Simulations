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

        // --- Performance Profiler Metrics ---
        private float _fps = 0f;
        private float _frameTimeMs = 0f;
        private float _fpsTimer = 0f;
        private int _frameCount = 0;
        private long _processRamMb = 0;
        private long _gcAllocMb = 0;
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
                        string title = !string.IsNullOrEmpty(spawnEntity.BaseData.Name) ? spawnEntity.BaseData.Name : $"Point_{i + 1}";
                        string subtitle = "";

                        if (spawnEntity.BaseData is PointObject pt && !string.IsNullOrEmpty(pt.Label))
                            subtitle = $" - \"{pt.Label}\"";


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
                            Text = $"Teleport to #{title}",
                            BackgroundColor = Color.DarkMagenta
                        };

                        Vector2 targetPos = spawnEntity.Position;
                        warpBtn.OnClick = () => {
                            _state.Player.Position = targetPos;
                            _state.Player.TargetWalkPos = null;
                            _state.Player.PendingInteraction = null;
                            _state.GameCamera.SnapTo(targetPos, _state.GameCamera.Zoom);
                            _state.DebugPool[GameBool.IsPaused] = false;
                        };

                        row.AddChild(warpBtn);
                        row.AddChild(new UILabel { Text = $"{title}{subtitle} at ({targetPos.X:F0}, {targetPos.Y:F0})", TextColor = Color.White });
                        _contentArea.AddChild(row);
                    }
                }
            }
            else if (_activeTab == "Graphics")
            {
                var toggleParaBtn = new UIButton
                {
                    Size = new Vector2(260, 28),
                    Text = $"Parallax Effect: {(_state.DebugPool[GameBool.EnableParallax] ? "ENABLED" : "DISABLED")}",
                    BackgroundColor = _state.DebugPool[GameBool.EnableParallax] ? Color.DarkGreen : Color.DarkRed
                };
                toggleParaBtn.OnClick = () => {
                    _state.DebugPool[GameBool.EnableParallax] = !_state.DebugPool[GameBool.EnableParallax];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleParaBtn);

                // Parallax Strength Stepper & Slider Row
                var strengthRow = new UIStackPanel
                {
                    Direction = StackDirection.Horizontal,
                    Spacing = 8f,
                    PanelBackground = Color.Black * 0.35f,
                    Padding = 6f
                };
                strengthRow.AddChild(new UILabel { Text = "Strength:", TextColor = Color.LightGray });

                var lblStrength = new UILabel { Text = $"{_state.ParallaxStrength:F2}", TextColor = Color.Cyan, Size = new Vector2(40, 20) };

                var sldParallax = new UISlider
                {
                    Size = new Vector2(160, 18),
                    Min = 0.0f,
                    Max = 0.5f,
                    Value = _state.ParallaxStrength
                };
                sldParallax.OnValueChanged = (val) => {
                    _state.ParallaxStrength = (float)Math.Round(val, 2);
                    lblStrength.Text = $"{_state.ParallaxStrength:F2}";
                };

                var minusBtn = new UIButton { Size = new Vector2(26, 22), Text = "-", BackgroundColor = Color.DarkSlateGray };
                minusBtn.OnClick = () => {
                    _state.ParallaxStrength = Math.Max(0.0f, (float)Math.Round(_state.ParallaxStrength - 0.05f, 2));
                    sldParallax.Value = _state.ParallaxStrength;
                    lblStrength.Text = $"{_state.ParallaxStrength:F2}";
                };

                var plusBtn = new UIButton { Size = new Vector2(26, 22), Text = "+", BackgroundColor = Color.DarkSlateGray };
                plusBtn.OnClick = () => {
                    _state.ParallaxStrength = Math.Min(0.5f, (float)Math.Round(_state.ParallaxStrength + 0.05f, 2));
                    sldParallax.Value = _state.ParallaxStrength;
                    lblStrength.Text = $"{_state.ParallaxStrength:F2}";
                };

                strengthRow.AddChild(minusBtn);
                strengthRow.AddChild(sldParallax);
                strengthRow.AddChild(plusBtn);
                strengthRow.AddChild(lblStrength);
                _contentArea.AddChild(strengthRow);

                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray * 0.3f, BorderColor = Color.Transparent });

                // 2. DEBUG VISIBILITY TOGGLES
                var toggleColBtn = new UIButton
                {
                    Size = new Vector2(260, 28),
                    Text = $"Physical Collisions: {(_state.DebugPool[GameBool.ShowCollision] ? "SHOWN" : "HIDDEN")}",
                    BackgroundColor = _state.DebugPool[GameBool.ShowCollision] ? Color.DarkGreen : Color.DarkRed
                };
                toggleColBtn.OnClick = () => {
                    _state.DebugPool[GameBool.ShowCollision] = !_state.DebugPool[GameBool.ShowCollision];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleColBtn);

                var toggleCtrlBtn = new UIButton
                {
                    Size = new Vector2(260, 28),
                    Text = $"Control Triggers & Spawns: {(_state.DebugPool[GameBool.ShowControlNodes] ? "SHOWN" : "HIDDEN")}",
                    BackgroundColor = _state.DebugPool[GameBool.ShowControlNodes] ? Color.DarkCyan : Color.DarkSlateGray
                };
                toggleCtrlBtn.OnClick = () => {
                    _state.DebugPool[GameBool.ShowControlNodes] = !_state.DebugPool[GameBool.ShowControlNodes];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleCtrlBtn);

                // 3. EXTENDED DEVELOPER ZOOM TOGGLE
                var toggleZoomBtn = new UIButton
                {
                    Size = new Vector2(260, 28),
                    Text = $"Dev Zoom Levels: {(_state.DebugPool[GameBool.ExtendedZoom] ? "ENABLED (0.5x-4x)" : "LOCKED (1x, 2x)")}",
                    BackgroundColor = _state.DebugPool[GameBool.ExtendedZoom] ? Color.DarkCyan : Color.DarkSlateGray
                };
                toggleZoomBtn.OnClick = () => {
                    _state.DebugPool[GameBool.ExtendedZoom] = !_state.DebugPool[GameBool.ExtendedZoom];
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleZoomBtn);

                _contentArea.AddChild(new UILabel { Text = "PIXEL-ART GRASS STABILIZATION", TextColor = Color.Cyan });

                // 1. Pixel Grid Snapping Toggle
                var toggleSnapBtn = new UIButton
                {
                    Size = new Vector2(280, 28),
                    Text = $"Pixel-Grid Snapping: {(_state.Grass.Settings.PixelSnap ? "LOCKED (480p Grid)" : "SMOOTH (Floating)")}",
                    BackgroundColor = _state.Grass.Settings.PixelSnap ? Color.DarkGreen : Color.DarkSlateGray
                };
                toggleSnapBtn.OnClick = () => {
                    _state.Grass.Settings.PixelSnap = !_state.Grass.Settings.PixelSnap;
                    UpdateTabContent();
                };
                _contentArea.AddChild(toggleSnapBtn);

                // 2. Cel Animation FPS Stepper
                var fpsRow = new UIStackPanel
                {
                    Direction = StackDirection.Horizontal,
                    Spacing = 8f,
                    PanelBackground = Color.Black * 0.35f,
                    Padding = 6f
                };
                fpsRow.AddChild(new UILabel { Text = "Animation Cel FPS:", TextColor = Color.LightGray });

                var lblFPS = new UILabel { Text = $"{_state.Grass.Settings.AnimFPS:F0} FPS", TextColor = Color.Yellow, Size = new Vector2(60, 20) };

                var sldFPS = new UISlider
                {
                    Size = new Vector2(160, 18),
                    Min = 4.0f,
                    Max = 60.0f,
                    Value = _state.Grass.Settings.AnimFPS
                };
                sldFPS.OnValueChanged = (val) => {
                    _state.Grass.Settings.AnimFPS = (float)Math.Round(val);
                    lblFPS.Text = $"{_state.Grass.Settings.AnimFPS:F0} FPS";
                };

                var fpsMinus = new UIButton { Size = new Vector2(26, 22), Text = "-", BackgroundColor = Color.DarkSlateGray };
                fpsMinus.OnClick = () => {
                    _state.Grass.Settings.AnimFPS = Math.Max(4f, _state.Grass.Settings.AnimFPS - 2f);
                    sldFPS.Value = _state.Grass.Settings.AnimFPS;
                    lblFPS.Text = $"{_state.Grass.Settings.AnimFPS:F0} FPS";
                };

                var fpsPlus = new UIButton { Size = new Vector2(26, 22), Text = "+", BackgroundColor = Color.DarkSlateGray };
                fpsPlus.OnClick = () => {
                    _state.Grass.Settings.AnimFPS = Math.Min(60f, _state.Grass.Settings.AnimFPS + 2f);
                    sldFPS.Value = _state.Grass.Settings.AnimFPS;
                    lblFPS.Text = $"{_state.Grass.Settings.AnimFPS:F0} FPS";
                };

                fpsRow.AddChild(fpsMinus);
                fpsRow.AddChild(sldFPS);
                fpsRow.AddChild(fpsPlus);
                fpsRow.AddChild(lblFPS);
                _contentArea.AddChild(fpsRow);

                // 3. Wind Wave Gust Intensity Slider
                var gustRow = new UIStackPanel
                {
                    Direction = StackDirection.Horizontal,
                    Spacing = 8f,
                    PanelBackground = Color.Black * 0.35f,
                    Padding = 6f
                };
                gustRow.AddChild(new UILabel { Text = "Wave Force:", TextColor = Color.LightGray });

                var lblGust = new UILabel { Text = $"{_state.Grass.Settings.GustStrength:F2}", TextColor = Color.LimeGreen, Size = new Vector2(40, 20) };

                var sldGust = new UISlider
                {
                    Size = new Vector2(160, 18),
                    Min = 0.0f,
                    Max = 1.5f,
                    Value = _state.Grass.Settings.GustStrength
                };
                sldGust.OnValueChanged = (val) => {
                    _state.Grass.Settings.GustStrength = (float)Math.Round(val, 2);
                    lblGust.Text = $"{_state.Grass.Settings.GustStrength:F2}";
                };

                gustRow.AddChild(sldGust);
                gustRow.AddChild(lblGust);
                _contentArea.AddChild(gustRow);

                _contentArea.AddChild(new UIPanel { Size = new Vector2(contentW, 1), BackgroundColor = Color.DarkGray * 0.3f, BorderColor = Color.Transparent });
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
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Frame time and FPS calculations
            _frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
            _fpsTimer += dt;
            _frameCount++;
            if (_fpsTimer >= 0.35f)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;

                // Sample memory metrics periodically (avoids per-frame OS calls)
                _gcAllocMb = GC.GetTotalMemory(false) / (1024 * 1024);
                using (var proc = System.Diagnostics.Process.GetCurrentProcess())
                {
                    _processRamMb = proc.WorkingSet64 / (1024 * 1024);
                }
            }

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
            // 1. Master Toggle for Debug Readout ('L' key)
            if (_state.IsDebug(GameBool.ShowDebugText))
            {
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
            var font = _theme.Font ?? UITheme.DefaultFont;
            if (font == null) return;

            var cam = _state.GameCamera;
            var player = _state.Player;
            var mouseWorld = _state.input.MouseWorldPosition;
            Point mouseCell = GridHelper.GetTileCell(mouseWorld);

            // 1. SAMPLE MASKS BENEATH PLAYER & MOUSE
            Color playerMask = _state.GetMaskColorAt(player.Foot);
            Color mouseMask = _state.GetMaskColorAt(mouseWorld);

            // Lookups
            string pBiome = _state.MaskData.GetBiome(playerMask.R)?.Name ?? "Unassigned";
            string pSpawn = _state.MaskData.GetSpawn(playerMask.G)?.Name ?? "None";
            bool pFarmZone = _state.IsFarmZone(player.Foot);

            string mBiome = _state.MaskData.GetBiome(mouseMask.R)?.Name ?? "Unassigned";
            string mSpawn = _state.MaskData.GetSpawn(mouseMask.G)?.Name ?? "None";
            bool mFarmZone = _state.IsFarmZone(mouseWorld);

            // Inspect hovered soil / crop
            var tilledLayer = _state.CurrentMap?.Layers.OfType<TileLayer>().FirstOrDefault(l => l.Name == "Tilled_Dirt");
            var soilTile = tilledLayer?.GetTileAt(mouseCell);
            string soilStatus = soilTile == null ? "Untilled" : (soilTile.TileID == 3 ? "Watered Dirt (ID 3)" : "Dry Tilled Dirt (ID 2)");

            var hoveredPlant = _state.ActiveSave.ActivePlants.FirstOrDefault(p => p.SoilCell == mouseCell);
            string plantStatus = "None";
            if (hoveredPlant != null)
            {
                var def = _state.plantManager.GetPlant(hoveredPlant.PlantID);
                var st = (def != null && hoveredPlant.CurrentStageIndex < def.Stages.Count) ? def.Stages[hoveredPlant.CurrentStageIndex] : null;
                plantStatus = $"{def?.Name ?? hoveredPlant.PlantID} [Stage {hoveredPlant.CurrentStageIndex}: {st?.Name}] (Watered: {hoveredPlant.IsWateredToday})";
            }

            var heldStack = player?.Inventory?.GetSelectedItem();
            string heldStr = heldStack != null ? $"#{heldStack.ItemID} (Qty: {heldStack.Count})" : "Empty Hands";

            int hrs = (int)_state.TimeSystem.TimeOfDay;
            int mins = (int)((_state.TimeSystem.TimeOfDay - hrs) * 60);

            var sbText = new System.Text.StringBuilder();

            // ==============================================================
            // HEADER & HOTKEY TOGGLE STATUS BAR
            // ==============================================================
            sbText.AppendLine("=== SYSTEM TELEMETRY MONITOR ===");
            sbText.Append($"[Alt+M] Mem: {(_state.IsDebug(GameBool.ShowMemoryDebug) ? "ON" : "OFF")} | ");
            sbText.Append($"[Alt+G] Grass: {(_state.IsDebug(GameBool.ShowGrassDebug) ? "ON" : "OFF")} | ");
            sbText.Append($"[Alt+W] Weather: {(_state.IsDebug(GameBool.ShowWeatherDebug) ? "ON" : "OFF")} | ");
            sbText.Append($"[Alt+P] Phys: {(_state.IsDebug(GameBool.ShowPhysicsDebug) ? "ON" : "OFF")} | ");
            sbText.AppendLine($"[Alt+E] Entities: {(_state.IsDebug(GameBool.ShowEntityDebug) ? "ON" : "OFF")}");
            sbText.AppendLine($"[Alt+N] Mask Probe: {(_state.IsDebug(GameBool.ShowMaskDebug) ? "ON" : "OFF")} | [P] Cycle RT: {_pipeline._currentDebugView}");
            sbText.AppendLine("--------------------------------------------------");

            // ==============================================================
            // CORE ENGINE, CAMERA & TARGET TELEMETRY
            // ==============================================================
            sbText.AppendLine($"FPS: {_fps:F0} ({_frameTimeMs:F1} ms) | Active RT: {_pipeline._currentDebugView}");
            sbText.AppendLine($"Pipelines: Native={_pipeline.NativeRect.Width}x{_pipeline.NativeRect.Height} | Sim={_pipeline.SimRect.Width}x{_pipeline.SimRect.Height} | Screen={_pipeline.FinalRect.Width}x{_pipeline.FinalRect.Height}");
            sbText.AppendLine($"Camera: Zoom={cam.Zoom:F2}x | Pos=({cam.Position.X:F0}, {cam.Position.Y:F0}) | Frustum=[{cam.SimViewBounds.X},{cam.SimViewBounds.Y} {cam.SimViewBounds.Width}x{cam.SimViewBounds.Height}]");
            sbText.AppendLine($"Player: World=({player.Foot.X:F0}, {player.Foot.Y:F0}) | Facing={player.FacingDirection} | Moving={player.IsMoving} | Indoors={_state.IsInsideBuilding}");
            sbText.AppendLine($"Held Item: {heldStr} | Inventory Weight: {_state.Player.Inventory.CurrentWeight:F1}/{_state.Player.Inventory.MaxWeight:F1} kg");

            // ==============================================================
            // [ALT+N] MASK, TERRAIN & BOTANICAL PROBE (FIXED!)
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowMaskDebug))
            {
                sbText.AppendLine("\n--- [ALT+N: MASK PROBE & BOTANICALS] ---");
                sbText.AppendLine($"[PLAYER PROBE] World: ({player.Foot.X:F0}, {player.Foot.Y:F0})");
                sbText.AppendLine($"   Biome  (R) : [{playerMask.R}] {pBiome}");
                sbText.AppendLine($"   Spawns (G) : [{playerMask.G}] {pSpawn}");
                sbText.AppendLine($"   Elev   (B) : [{playerMask.B}] (Alt Offset: -{Math.Round(playerMask.B * (45f / 255f))}px)");
                sbText.AppendLine($"   Farm Zone  : {(pFarmZone ? "YES (Permitted Soil)" : "NO (Protected)")}");

                sbText.AppendLine($"[MOUSE PROBE] World: ({mouseWorld.X:F0}, {mouseWorld.Y:F0}) | Cell: ({mouseCell.X}, {mouseCell.Y})");
                sbText.AppendLine($"   Biome  (R) : [{mouseMask.R}] {mBiome}");
                sbText.AppendLine($"   Spawns (G) : [{mouseMask.G}] {mSpawn}");
                sbText.AppendLine($"   Elev   (B) : [{mouseMask.B}]");
                sbText.AppendLine($"   Farm Zone  : {(mFarmZone ? "YES" : "NO")}");
                sbText.AppendLine($"   Soil State : {soilStatus}");
                sbText.AppendLine($"   Plant/Crop : {plantStatus}");

                sbText.AppendLine($"[BOTANICAL POOL]");
                sbText.AppendLine($"   Active World Plants : {_state.ActiveSave.ActivePlants.Count} instances");
                sbText.AppendLine($"   Registered Plant Def: {_state.plantManager.Plants.Count} species");
                sbText.AppendLine($"   Last Action Status  : {_state.Debug_LastCommand}");
                sbText.AppendLine($"   [HOTKEY] Press 'K' to Force-Spawn forages across map");
            }

            // ==============================================================
            // [ALT+M] MEMORY, GPU & PERFORMANCE
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowMemoryDebug))
            {
                sbText.AppendLine("\n--- [ALT+M: MEMORY & GPU PERFORMANCE] ---");
                sbText.AppendLine($"Process Working Set: {_processRamMb} MB");
                sbText.AppendLine($"GC Managed Heap:     {_gcAllocMb} MB (Gen 0/1/2: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)})");
                sbText.AppendLine($"GPU Adapter:         {_state._gd.Adapter.Description}");
                sbText.AppendLine($"Composite Textures:  {_state._entityManager._compositeCache.Count} active");
            }

            // ==============================================================
            // [ALT+G] GRASS SYSTEM
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowGrassDebug))
            {
                sbText.AppendLine("\n--- [ALT+G: PROCEDURAL GRASS] ---");
                sbText.AppendLine($"Blade Vertices:  {_state.Grass._bladeVertCount:N0} (~{_state.Grass._bladeVertCount / 18:N0} blades)");
                sbText.AppendLine($"Base Height:     {_state.Grass.Settings.BaseHeight:F1} | Width: {_state.Grass.Settings.BladeWidth:F1}");
                sbText.AppendLine($"Wind Physics:    Gust={_state.Grass.Settings.GustStrength:F2} | WaveLen={_state.Grass.Settings.WaveLength:F0} | Spd={_state.Grass.Settings.WindSpeed:F1}");
                sbText.AppendLine($"Cel Stepping:    FPS={_state.Grass.Settings.AnimFPS:F0} | PixelSnap={_state.Grass.Settings.PixelSnap}");
                sbText.AppendLine($"Loaded Presets:  {_state.Grass.Presets.Count} biomes");
            }

            // ==============================================================
            // [ALT+W] WEATHER & CLIMATE
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowWeatherDebug))
            {
                sbText.AppendLine("\n--- [ALT+W: WEATHER & CLIMATE] ---");
                sbText.AppendLine($"Time of Day:   {hrs:D2}:{mins:D2} (Scale: {_state.TimeSystem.TimeScale:F3})");
                sbText.AppendLine($"Date/Season:   Year {_state.Calendar.Year}, {_state.Calendar.CurrentSeason} (Day {_state.Calendar.Day}, Phase: {_state.Calendar.GetCurrentPhase()})");
                sbText.AppendLine($"Condition:     {_state.Weather.CurrentWeather} (Forced: {_state.Weather.ForcedWeather?.ToString() ?? "None"})");
                sbText.AppendLine($"Climate:       {_state.Weather.CurrentClimate.TempC:F1} C | Humid: {_state.Weather.CurrentClimate.Humidity:F0}% | Pressure: {_state.Weather.CurrentClimate.PressureHpa:F0} hPa");
                sbText.AppendLine($"Wind/Precip:   {_state.Weather.CurrentClimate.WindKph:F0} km/h | Rain: {_state.Weather.Visuals.RainIntensity:F2} | Cloud: {_state.Weather.Visuals.CloudCover:F2}");
            }

            // ==============================================================
            // [ALT+P] PHYSICS & BARRIERS
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowPhysicsDebug))
            {
                sbText.AppendLine("\n--- [ALT+P: PHYSICS & BARRIERS] ---");
                sbText.AppendLine($"Total Barriers:   {_state.Physics._barriers.Count}");
                sbText.AppendLine($"Vaulting Fences:  {_state.Physics._barriers.Count(b => b.IsOneWay)}");
                sbText.AppendLine($"Static Blockers:  {_state.Physics._barriers.Count(b => !b.IsOneWay)}");
                sbText.AppendLine($"Player Footbox:   [{player.FootBounds.X:F0}, {player.FootBounds.Y:F0}, {player.FootBounds.Width:F0}x{player.FootBounds.Height:F0}]");
            }

            // ==============================================================
            // [ALT+E] ENTITIES & SCENE
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowEntityDebug))
            {
                sbText.AppendLine("\n--- [ALT+E: ENTITIES & SCENE] ---");
                sbText.AppendLine($"Total Registered: {_state._entityManager.AllEntities.Count}");
                sbText.AppendLine($"Draw List Size:   {_state._entityManager._drawList.Count} ({_state._entityManager._staticSprites.Count} static, {_state._entityManager._simulationSprites.Count} sim)");
                sbText.AppendLine($"Placed Items:     {_state.ActiveSave.PlacedItems.Count}");
                sbText.AppendLine($"Destroyed Base:   {_state.ActiveSave.DestroyedBaseIDs.Count}");
            }

            // ==============================================================
            // [ALT+B] MODULAR BUILDINGS
            // ==============================================================
            if (_state.IsDebug(GameBool.ShowBuildingDebug))
            {
                sbText.AppendLine("\n--- [ALT+B: MODULAR BUILDINGS] ---");
                var buildings = _state.CurrentMap?.Layers.OfType<ObjectLayer>()
                    .SelectMany(l => l.Objects.OfType<ModularBuildingInstance>())
                    .ToList();

                sbText.AppendLine($"Building Instances: {buildings?.Count ?? 0}");
                if (buildings != null)
                {
                    foreach (var b in buildings)
                    {
                        sbText.AppendLine($" - {b.Name} ({b.BuildingId}): Inside={b.IsPlayerInside}, RoofAlpha={b.RoofAlpha:F2}");
                    }
                }
            }

            string fullText = sbText.ToString();
            Vector2 textSize = font.MeasureString(fullText);

            // Draw frosted dark plate
            Rectangle bgBox = new Rectangle(12, 12, (int)textSize.X + 16, (int)textSize.Y + 16);
            sb.FillRectangle(bgBox, Color.Black * 0.82f);
            sb.DrawRectangle(bgBox, Color.DarkSlateGray * 0.8f, 1f);

            // Render legible shadow and foreground text
            sb.DrawString(font, fullText, new Vector2(21, 21), Color.Black * 0.95f);
            sb.DrawString(font, fullText, new Vector2(20, 20), Color.LimeGreen);
        }
        public void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color) { }
    }
}