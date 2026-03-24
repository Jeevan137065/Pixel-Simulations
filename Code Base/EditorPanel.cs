// Add the correct namespace for ImGui.NET if you are using the ImGui.NET library
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.UI;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;
//using MonoGame.ImGuiNet;
using System.IO;
using System.Linq;
using System.Text;

namespace Pixel_Simulations.Editor
{
    public class TopPanel : BasePanel // BasePanel holds Area, EditorUI, EditorState
    {
        private readonly UIStackPanel _rootStack;
        private readonly UITheme _theme;
        private readonly UITextBox _mapNameInput;
        public TopPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            var state = editorState.TopState;
            _theme = new UITheme { Font = editorUI.DebugFont };

            // 1. Create the container (Horizontal Stack)
            _rootStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(area.X +10, area.Y+10),
                Size = new Vector2(area.Width, area.Width),
                AutoSize = true,
                Spacing = 6f,
                Padding = 0f,
                PanelBackground = Color.Transparent,
                BorderColor = Color.Transparent // Hide layout box!
            };
            _rootStack.AddChild(new UILabel { Text = "Map File:", TextColor = Color.Cyan });

            _mapNameInput = new UITextBox { Size = new Vector2(150, 24), Text = "level1" };
            // Pass the typed name down to the MapController via the EditorState or directly
            _mapNameInput.OnTextChanged = (txt) => { _editorState.CurrentMapFile = txt; };
            _rootStack.AddChild(_mapNameInput);
            // 2. Add Buttons automatically
            string[] actions = { "New", "Save", "Load", "Undo", "Redo", "Capture", "Export" };
            string[] icons = { "New", "Save", "Load", "Undo", "Redo", "Capture", "Export" };
            for (int i = 0; i < actions.Length; i++)
            {
                var btn = new UIButton
                {
                    Size = new Vector2(32, 32),
                    IconName = icons[i],
                    Command = new MenuActionCommand { ActionName = actions[i] }
                };
                _rootStack.AddChild(btn);
            }
            var newBtn = (UIButton)_rootStack.Children[2]; // Assuming "New" is the first button you added
            newBtn.OnClick = () => {
                _mapNameInput.Text = ""; // Clear it for a new name
                _editorUI.SetFocus(_mapNameInput); // Instantly activate the typing box!
            };
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            if (!Area.Contains(input.MouseWindowPosition)) return;

            // The framework handles the math, hovering, and event publishing automatically!
            _rootStack.Update(input, bus);
        }

        public override void Draw(SpriteBatch sb)
        {
            _rootStack.Draw(sb, _editorUI, _theme);
        }

        public override string GetDebugInfo()
        {
            return $"Top Panel Active | Buttons: {_rootStack.Children.Count}";
        }
    }
    public enum Tab { Tiles, Objects }
    public class TilesetPanel : BasePanel
    {
        public Tab _activeTab = Tab.Tiles;

        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _tabStack;
        private readonly UIStackPanel _footerStack;
        private readonly UIStackPanel _dynamicTilesetStack;
        private readonly UIGridView _contentGrid; // Custom element for optimized grid drawing
        private readonly UITheme _theme;
        public TilesetPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont };

            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            // 1. TABS (Top)
            _tabStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = Vector2.Zero,
                Size = new Vector2(area.Width, 30),
                AutoSize = false,
                Spacing = 0,
                Padding = 0
            };

            var btnTiles = new UIButton { Size = new Vector2(area.Width / 2, 30), Text = "Tiles" };
            btnTiles.OnClick = () => _activeTab = Tab.Tiles;

            var btnObjects = new UIButton { Size = new Vector2(area.Width / 2, 30), Text = "Objects" };
            btnObjects.OnClick = () => _activeTab = Tab.Objects;

            _tabStack.AddChild(btnTiles);
            _tabStack.AddChild(btnObjects);

            // 2. FOOTER (Bottom)
            _footerStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(0, area.Height - 40),
                Size = new Vector2(area.Width, 40),
                AutoSize = false,
                PanelBackground = Color.Black * 0.4f
            };

            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewTileSet", Command = new OpenAtlasPickerCommand() });
            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewObject", Command = new TogglePrefabCreatorCommand { DefaultAtlasName = "Basic" } });
            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewTags",Text = "Tags",Command = new ToggleTagManagerCommand()});
            // Container for dynamically generated active tileset tabs
            _dynamicTilesetStack = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent };
            _footerStack.AddChild(_dynamicTilesetStack);

            // 3. SCROLLABLE CONTENT (Middle)
            _contentGrid = new UIGridView(_editorState, this)
            {
                LocalPosition = new Vector2(0, 30),
                Size = new Vector2(area.Width, area.Height - 70),
                ClipToBounds = true // Crucial for scrolling
            };

            // Assemble
            _rootPanel.AddChild(_tabStack);
            _rootPanel.AddChild(_contentGrid);
            _rootPanel.AddChild(_footerStack);
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            RefreshDynamicFooter();

            // Sync Tab visual state
            ((UIButton)_tabStack.Children[0]).IsSelected = (_activeTab == Tab.Tiles);
            ((UIButton)_tabStack.Children[1]).IsSelected = (_activeTab == Tab.Objects);

            if (Area.Contains(input.MouseWindowPosition))
            {
                _rootPanel.Update(input, bus);
            }
        }

        private void RefreshDynamicFooter()
        {
            // Only rebuild if count changes to save performance
            if (_dynamicTilesetStack.Children.Count != _editorState.ActiveTileSets.Count)
            {
                _dynamicTilesetStack.ClearChildren();
                foreach (var ts in _editorState.ActiveTileSets)
                {
                    _dynamicTilesetStack.AddChild(new UIButton
                    {
                        Size = new Vector2(80, 30),
                        Text = ts.Name,
                        Command = new SelectTilesetCommand { TilesetName = ts.Name }
                    });
                }
            }

            // Sync selected visual state
            foreach (UIButton btn in _dynamicTilesetStack.Children)
            {
                btn.IsSelected = (btn.Text == _editorState.TilesetPanel.ActiveTilesetName);
            }
        }

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, _editorUI, _theme);
        public override string GetDebugInfo()
        {
            return $"Active Tab: {_activeTab} | Selected TS: {_editorState.TilesetPanel.ActiveTilesetName}\n" +
                   $"Grid Scroll Y: {_contentGrid.ScrollOffset.Y:F0}";
        }

    }
    public class PrefabCreatorPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;

        // Stacks
        private readonly UIStackPanel _middleStack;
        private readonly UIStackPanel _rightStack;

        // Elements
        private readonly UITextBox _nameInput;
        private readonly UIFlowPanel _activeTagsFlow;
        private readonly UIStackPanel _tagLibraryScroll;
        private readonly UIStackPanel _propListScroll;

        // Atlas Canvas
        private Rectangle _canvasArea;
        private Rectangle _previewArea;
        private readonly UITheme _theme;
        // Property Temp State
        private string _newPropKey = "";
        private bool _isTagPickerOpen = false;
        private PropertyType _newPropType = PropertyType.String;
        public PrefabCreatorPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.Black * 0.95f };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height), BackgroundColor = _theme.PanelBackground };

            // 1. CANVAS AREA (Left 50%)
            _canvasArea = new Rectangle(10, 10, (int)(area.Width * 0.5f) - 20, area.Height - 20);

            int midX = _canvasArea.Right + 10;
            int midWidth = (int)(area.Width * 0.25f) - 10;
            int rightX = midX + midWidth + 10;
            int rightWidth = (int)(area.Width * 0.25f) - 20;

            // 2. MIDDLE STACK (Name, Tags, Properties)
            _middleStack = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(midX, 10),
                Size = new Vector2(midWidth, area.Height - 20),
                PanelBackground = Color.Transparent,
                Spacing = 20f // Much more breathing room
            };
            _middleStack.AddChild(new UILabel { Text = "Object ID:", TextColor = Color.Yellow });
            _nameInput = new UITextBox { Size = new Vector2(midWidth, 24), Placeholder = "e.g. Tree_Oak" };
            _middleStack.AddChild(_nameInput);

            // Tags Section
            _middleStack.AddChild(new UIPanel { Size = new Vector2(midWidth, 2), BackgroundColor = Color.Gray });

            // Header Row for Tags
            var tagHeaderRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
            tagHeaderRow.AddChild(new UILabel { Text = "Active Tags:", TextColor = Color.Cyan });

            // The "+" Button
            var addTagBtn = new UIButton { Size = new Vector2(30, 20), Text = "+", BackgroundColor = Color.DarkCyan };
            addTagBtn.OnClick = () => { _isTagPickerOpen = !_isTagPickerOpen; RebuildUI(); };
            tagHeaderRow.AddChild(addTagBtn);
            _middleStack.AddChild(tagHeaderRow);

            // Flow panel for assigned tags (Pills)
            _activeTagsFlow = new UIFlowPanel { Size = new Vector2(midWidth, 30), BackgroundColor = Color.Black * 0.2f, SpacingX = 10f, SpacingY = 10f };
            _middleStack.AddChild(_activeTagsFlow);

            // The Tag Library (Hidden by default)
            _tagLibraryScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(midWidth, 140), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f }; _middleStack.AddChild(_tagLibraryScroll);
            _middleStack.AddChild(_tagLibraryScroll);

            // Properties Section
            _middleStack.AddChild(new UIPanel { Size = new Vector2(midWidth, 2), BackgroundColor = Color.Gray });
            _middleStack.AddChild(new UILabel { Text = "Default Properties:", TextColor = Color.Cyan });

            _propListScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(midWidth, 140), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            _middleStack.AddChild(_propListScroll);

            // Add Property Form
            var addPropRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 5f };
            var newKeyInput = new UITextBox { Size = new Vector2(80, 24), Placeholder = "Key...", Text = _newPropKey, DebugName = "PrefabPropKey" };
            newKeyInput.OnTextChanged = (t) => _newPropKey = t;
            addPropRow.AddChild(newKeyInput);

            var typeBtn = new UIButton { Size = new Vector2(40, 24), Text = _newPropType.ToString().Substring(0, 3), BackgroundColor = Color.DarkGoldenrod };
            typeBtn.OnClick = () => { _newPropType = (PropertyType)(((int)_newPropType + 1) % 4); RebuildUI(); };
            addPropRow.AddChild(typeBtn);

            var addPropBtn = new UIButton { Size = new Vector2(40, 24), Text = "Add", BackgroundColor = Color.DarkGreen };
            addPropBtn.OnClick = () => {
                var ctx = _editorState.PrefabCreator;
                if (!string.IsNullOrWhiteSpace(_newPropKey) && !ctx.TempProperties.ContainsKey(_newPropKey))
                {
                    string defaultVal = _newPropType == PropertyType.Boolean ? "False" : (_newPropType == PropertyType.String ? "" : "0");
                    ctx.TempProperties.Add(_newPropKey.Trim(), new MapProperty(_newPropType, defaultVal));
                    _newPropKey = ""; RebuildUI();
                }
            };
            addPropRow.AddChild(addPropBtn);
            _middleStack.AddChild(addPropRow);

            // 3. RIGHT STACK (Atlas Select, Preview, Save)
            _rightStack = new UIStackPanel { Direction = StackDirection.Vertical, LocalPosition = new Vector2(rightX, 10), Size = new Vector2(rightWidth, area.Height - 20), PanelBackground = Color.Transparent, Spacing = 10f };

            _previewArea = new Rectangle(rightX, 10, rightWidth, 300);
            _rightStack.AddChild(new UIPanel { Size = new Vector2(rightWidth, 300), BackgroundColor = Color.Transparent }); // Spacer for drawing preview

            _rightStack.AddChild(new UILabel { Text = "Select Atlas:", TextColor = Color.Yellow });

            // Generate Atlas Buttons
            var atlasScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(rightWidth, 150), AutoSize = false, PanelBackground = Color.Black * 0.3f, ClipToBounds = true, Spacing = 5f, Padding = 5f };
            foreach (var atlasName in _editorState.AssetLibrary.GetNamesByType(AtlasType.Object))
            {
                var aBtn = new UIButton { Size = new Vector2(rightWidth - 20, 25), Text = atlasName, BackgroundColor = Color.Black * 0.6f }; // Added Background Color!
                aBtn.OnClick = () => { _editorState.PrefabCreator.ActiveAtlasName = atlasName; _editorState.PrefabCreator.AtlasPanOffset = Vector2.Zero; };
                atlasScroll.AddChild(aBtn);
            }
            _rightStack.AddChild(atlasScroll);

            // Save Row
            var saveRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
            saveRow.AddChild(new UIButton { Size = new Vector2(60, 30), Text = "Save", Command = new SavePrefabCommand { Mode = "New" }, BackgroundColor = Color.DarkGreen });
            saveRow.AddChild(new UIButton { Size = new Vector2(60, 30), Text = "Overwrt", Command = new SavePrefabCommand { Mode = "Replace" }, BackgroundColor = Color.DarkGoldenrod });
            saveRow.AddChild(new UIButton { Size = new Vector2(60, 30), Text = "Delete", Command = new DeletePrefabCommand(), BackgroundColor = Color.DarkRed });
            saveRow.AddChild(new UIButton { Size = new Vector2(60, 30), Text = "Exit", Command = new TogglePrefabCreatorCommand(), BackgroundColor = Color.Black * 0.6f });
            _rightStack.AddChild(saveRow);


            _rootPanel.AddChild(_middleStack);
            _rootPanel.AddChild(_rightStack);
        }
        private void RebuildUI()
        {
            var ctx = _editorState.PrefabCreator;

            // 1. Rebuild Active Tags
            _activeTagsFlow.ClearChildren();
            foreach (var tag in ctx.TempTags.ToList())
            {
                string safeTag = tag;
                var def = _editorState.TagManager.GetTag(tag);

                // Dynamic Pill Size
                Vector2 tSize = UITheme.DefaultFont != null ? UITheme.DefaultFont.MeasureString(tag) : new Vector2(50, 20);

                var btn = new UIButton { Size = new Vector2(tSize.X + 16, 24), Text = tag, BackgroundColor = def?.TagColor ?? Color.Gray };
                btn.OnClick = () => { ctx.TempTags.Remove(safeTag); RebuildUI(); }; // Remove on click
                _activeTagsFlow.AddChild(btn);
            }

            // 2. Rebuild Tag Library (Only if the expander is open!)
            _tagLibraryScroll.IsVisible = _isTagPickerOpen;
            if (_isTagPickerOpen)
            {
                _tagLibraryScroll.ClearChildren();
                foreach (var tag in _editorState.TagManager.Tags.Values)
                {
                    if (ctx.TempTags.Contains(tag.HashID)) continue; // Don't show tags we already have!

                    string safeHash = tag.HashID;
                    var btn = new UIButton { Size = new Vector2(_tagLibraryScroll.Size.X - 20, 24), Text = $"{tag.HashID} ({tag.Name})", BackgroundColor = tag.TagColor * 0.4f };

                    btn.OnClick = () => {
                        ctx.TempTags.Add(safeHash);
                        _isTagPickerOpen = false; // Auto-close library after picking
                        RebuildUI();
                    };
                    _tagLibraryScroll.AddChild(btn);
                }
                if (_tagLibraryScroll.Children.Count == 0)
                    _tagLibraryScroll.AddChild(new UILabel { Text = "All tags applied.", TextColor = Color.Gray });
            }

            // 3. Rebuild Properties
            _propListScroll.ClearChildren();
            foreach (var kvp in ctx.TempProperties)
            {
                string safeKey = kvp.Key;
                var pRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 5f };

                string typeInd = kvp.Value.Type.ToString().Substring(0, 3);
                pRow.AddChild(new UILabel { Text = $"[{typeInd}] {safeKey}:", TextColor = Color.LightGray });

                if (kvp.Value.Type == PropertyType.Boolean)
                {
                    var toggleBtn = new UIButton { Size = new Vector2(50, 20), Text = kvp.Value.Value, BackgroundColor = kvp.Value.Value == "True" ? Color.DarkGreen : Color.DarkRed };
                    toggleBtn.OnClick = () => { kvp.Value.Value = kvp.Value.Value == "True" ? "False" : "True"; RebuildUI(); };
                    pRow.AddChild(toggleBtn);
                }
                else
                {
                    var valInput = new UITextBox { Size = new Vector2(80, 20), Text = kvp.Value.Value, DebugName = $"PreProp_{safeKey}" };
                    valInput.OnTextChanged = (txt) => kvp.Value.Value = txt;
                    pRow.AddChild(valInput);
                }

                var delBtn = new UIButton { Size = new Vector2(25, 20), Text = "X", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => { ctx.TempProperties.Remove(safeKey); RebuildUI(); };
                pRow.AddChild(delBtn);

                _propListScroll.AddChild(pRow);
            }
            if (ctx.TempProperties.Count == 0) _propListScroll.AddChild(new UILabel { Text = "No default properties.", TextColor = Color.Gray });
            _activeTagsFlow.UpdateLayout();
            _middleStack.UpdateLayout();
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            var ctx = _editorState.PrefabCreator;
            if (!ctx.IsOpen) return;
            if (ctx.NeedsUIRebuild)
            {
                RebuildUI();
                ctx.NeedsUIRebuild = false;
            }
            // Sync Text Input
            if (!_nameInput.IsFocused) _nameInput.Text = ctx.TempName;
            _nameInput.OnTextChanged = (t) => ctx.TempName = t;

            // Initialize Rebuild if first frame
            if (_activeTagsFlow.Children.Count == 0 && ctx.TempTags.Count > 0) RebuildUI();

            _editorUI.CheckFocusClick(_rootPanel, input);
            _rootPanel.Update(input, bus);

            // Handle Global Scrolling for UI elements inside this panel
            ApplyGlobalScrolling(_rootPanel, input);

            // --- CANVAS PANNING (Arrow Keys) ---
            if (_editorState.UI.FocusedElement == null) // Only pan if not typing!
            {
                var kbs = input.CurrentKeyboard;
                float panSpeed = 5f;
                if (kbs.IsKeyDown(Keys.Left)) ctx.AtlasPanOffset += new Vector2(panSpeed, 0);
                if (kbs.IsKeyDown(Keys.Right)) ctx.AtlasPanOffset -= new Vector2(panSpeed, 0);
                if (kbs.IsKeyDown(Keys.Up)) ctx.AtlasPanOffset += new Vector2(0, panSpeed);
                if (kbs.IsKeyDown(Keys.Down)) ctx.AtlasPanOffset -= new Vector2(0, panSpeed);
            }

            // --- CANVAS DRAG SELECTION ---
            Rectangle absCanvas = new Rectangle(Area.X + _canvasArea.X, Area.Y + _canvasArea.Y, _canvasArea.Width, _canvasArea.Height);

            if (absCanvas.Contains(input.MouseWindowPosition))
            {
                // Calculate mouse position relative to the PANNED texture
                Vector2 mouseLocal = input.MouseWindowPosition - absCanvas.Location.ToVector2() - ctx.AtlasPanOffset;
                Vector2 snapped = new Vector2((float)System.Math.Floor(mouseLocal.X / 16) * 16, (float)System.Math.Floor(mouseLocal.Y / 16) * 16);

                if (input.IsNewLeftClick)
                {
                    ctx.DragStart = snapped;
                    ctx.IsDragging = true;
                    _editorUI.SetFocus(null);
                }

                if (ctx.IsDragging)
                {
                    ctx.SelectionRect = new Rectangle(
                        (int)System.Math.Min(ctx.DragStart.X, snapped.X),
                        (int)System.Math.Min(ctx.DragStart.Y, snapped.Y),
                        (int)System.Math.Abs(ctx.DragStart.X - snapped.X) + 16,
                        (int)System.Math.Abs(ctx.DragStart.Y - snapped.Y) + 16);

                    if (!input.LeftHold) ctx.IsDragging = false;
                }
            }
        }

        private void ApplyGlobalScrolling(UIElement element, EditorInputState input)
        {
            if (element is UIStackPanel stack && stack.ClipToBounds && stack.AbsoluteBounds.Contains(input.MouseWindowPosition))
            {
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) stack.ScrollOffset = new Vector2(0, System.Math.Max(0, stack.ScrollOffset.Y - scrollDelta * 0.5f));
            }
            foreach (var child in element.Children) ApplyGlobalScrolling(child, input);
        }

        public override void Draw(SpriteBatch sb)
        {
            var ctx = _editorState.PrefabCreator;
            if (!ctx.IsOpen) return;

            _rootPanel.Draw(sb, _editorUI, _theme);

            Rectangle absCanvas = new Rectangle(Area.X + _canvasArea.X, Area.Y + _canvasArea.Y, _canvasArea.Width, _canvasArea.Height);
            Rectangle absPreview = new Rectangle(Area.X + _previewArea.X, Area.Y + _previewArea.Y, _previewArea.Width, _previewArea.Height);

            // 1. Draw Canvas (With Clipping!)
            sb.FillRectangle(absCanvas, Color.Black * 0.8f);
            sb.DrawRectangle(absCanvas, Color.White, 1);

            var tex = _editorState.AssetLibrary.GetAtlas(ctx.ActiveAtlasName);
            if (tex != null)
            {
                // Clip the rendering so the panned image doesn't bleed out of the box
                Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
                sb.End();
                sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
                sb.GraphicsDevice.ScissorRectangle = absCanvas;

                // Draw Texture with Pan Offset
                Vector2 drawPos = absCanvas.Location.ToVector2() + ctx.AtlasPanOffset;
                sb.Draw(tex, drawPos, Color.White);

                // Draw Grid (Optional, but helpful)
                for (int x = 0; x < tex.Width; x += 16) sb.DrawLine(drawPos.X + x, drawPos.Y, drawPos.X + x, drawPos.Y + tex.Height, Color.White * 0.1f);
                for (int y = 0; y < tex.Height; y += 16) sb.DrawLine(drawPos.X, drawPos.Y + y, drawPos.X + tex.Width, drawPos.Y + y, Color.White * 0.1f);

                // Draw Selection Box with Pan Offset
                Rectangle drawSelect = new Rectangle((int)drawPos.X + ctx.SelectionRect.X, (int)drawPos.Y + ctx.SelectionRect.Y, ctx.SelectionRect.Width, ctx.SelectionRect.Height);
                sb.DrawRectangle(drawSelect, Color.Yellow, 2);
                sb.FillRectangle(drawSelect, Color.Yellow * 0.2f);

                sb.End();
                sb.GraphicsDevice.ScissorRectangle = prevScissor;
                sb.Begin();
            }

            // 2. Draw Preview Box
            sb.FillRectangle(absPreview, Color.Black * 0.5f);
            sb.DrawRectangle(absPreview, Color.White, 1);
            sb.DrawString(_editorUI.DebugFont, "PREVIEW", new Vector2(absPreview.X + 5, absPreview.Y + 5), Color.Gray);

            if (tex != null && !ctx.SelectionRect.IsEmpty)
            {
                float scale = System.Math.Min((absPreview.Width - 40f) / ctx.SelectionRect.Width, (absPreview.Height - 40f) / ctx.SelectionRect.Height);
                Vector2 centerPos = new Vector2(
                    absPreview.Center.X - (ctx.SelectionRect.Width * scale) / 2,
                    absPreview.Center.Y - (ctx.SelectionRect.Height * scale) / 2
                );

                sb.Draw(tex, centerPos, ctx.SelectionRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
        public override string GetDebugInfo()
        {
            var ctx = _editorState.PrefabCreator;
            return $"Atlas: {ctx.ActiveAtlasName}\n" +
                   $"Selection: {ctx.SelectionRect}\n" +
                   $"Dragging: {ctx.IsDragging}";
        }
    }
    public class TagManagerPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _listStack;
        private readonly UIStackPanel _formStack;
        private readonly UITheme _theme;

        // Form fields
        private UITextBox _idInput, _nameInput, _descInput;
        private UIButton _colorBtn;
        // Simple color cycle (Click to change color)
        private Color[] _presetColors = { Color.Gray, Color.Crimson, Color.SeaGreen, Color.RoyalBlue, Color.Goldenrod, Color.Purple, Color.Teal };
        private int _colorIndex = 0;
        private int _cachedTagCount = -1; // Used to prevent rebuilding list every frame

        public TagManagerPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont };

            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height), BackgroundColor = _theme.PanelBackground, BorderColor = Color.White };

            // TOP: Tag List (Fixed Height, no AutoSize)
            int listHeight = 300;
            _listStack = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(10, 10),
                Size = new Vector2(area.Width - 20, listHeight),
                AutoSize = false, // FIX: Prevent spilling
                ClipToBounds = true,
                PanelBackground = Color.Black * 0.5f,
                Spacing = 15f,
                Padding = 5f
            };

            // BOTTOM: Creation Form
            _formStack = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(10, listHeight + 20),
                Size = new Vector2(area.Width - 20, area.Height - listHeight - 30),
                AutoSize = false, // FIX: Prevent spilling
                PanelBackground = Color.DarkSlateGray,
                Padding = 15f,
                Spacing = 10f
            };

            _formStack.AddChild(new UILabel { Text = "Create / Edit Tag:", TextColor = Color.Yellow });

            _idInput = new UITextBox { Size = new Vector2(_formStack.Size.X - 30, 30), Placeholder = "Hash ID (e.g. #tree)" };
            _nameInput = new UITextBox { Size = new Vector2(_formStack.Size.X - 30, 30), Placeholder = "Display Name (e.g. Tree)" };
            _descInput = new UITextBox { Size = new Vector2(_formStack.Size.X - 30, 30), Placeholder = "Dev Notes (e.g. Harvestable Wood)" };

            var btnRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };

            _colorBtn = new UIButton { Size = new Vector2(80, 30), Text = "Color", BackgroundColor = _presetColors[0] };
            _colorBtn.OnClick = () => {
                _colorIndex = (_colorIndex + 1) % _presetColors.Length;
                _colorBtn.BackgroundColor = _presetColors[_colorIndex];
            };

            var addBtn = new UIButton { Size = new Vector2(100, 30), Text = "Save Tag", BackgroundColor = Color.DarkGreen };
            addBtn.OnClick = SaveTagLogic;

            var exitBtn = new UIButton { Size = new Vector2(100, 30), Text = "Close", Command = new ToggleTagManagerCommand(), BackgroundColor = Color.DarkRed };

            btnRow.AddChild(_colorBtn);
            btnRow.AddChild(addBtn);
            btnRow.AddChild(exitBtn);

            _formStack.AddChild(_idInput);
            _formStack.AddChild(_nameInput);
            _formStack.AddChild(_descInput);
            _formStack.AddChild(btnRow);

            _rootPanel.AddChild(_listStack);
            _rootPanel.AddChild(_formStack);
        }
        private void SaveTagLogic()
        {
            if (string.IsNullOrWhiteSpace(_idInput.Text)) return;
            string id = _idInput.Text.StartsWith("#") ? _idInput.Text : "#" + _idInput.Text;

            var newTag = new TagDefinition
            {
                HashID = id,
                Name = string.IsNullOrWhiteSpace(_nameInput.Text) ? id : _nameInput.Text,
                Description = _descInput.Text,
                TagColor = _presetColors[_colorIndex]
            };

            _editorState.TagManager.Tags[id] = newTag;
            _editorState.TagManager.Save(Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json"));

            // Clear Form & Force Rebuild
            _idInput.Text = ""; _nameInput.Text = ""; _descInput.Text = "";
            _editorUI.SetFocus(null);
            _cachedTagCount = -1;
        }
        public override void Update(EditorInputState input, EventBus bus)
        {
            if (!_editorState.IsTagManagerOpen) return;

            // Only rebuild visual list if tag count changed
            if (_cachedTagCount != _editorState.TagManager.Tags.Count)
            {
                BuildTagList();
                _cachedTagCount = _editorState.TagManager.Tags.Count;
            }

            if (Area.Contains(input.MouseWindowPosition))
            {
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) _listStack.ScrollOffset = new Vector2(0, System.Math.Max(0, _listStack.ScrollOffset.Y - scrollDelta * 0.5f));

                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
            }
        }

        private void BuildTagList()
        {
            _listStack.ClearChildren();

            foreach (var tag in _editorState.TagManager.Tags.Values)
            {
                string localId = tag.HashID; // closure safe copy

                var row = new UIButton
                {
                    Size = new Vector2(_listStack.Size.X - 20, 50),
                    BackgroundColor = Color.Black * 0.4f,
                    BorderColor = Color.Transparent
                }; 
                row.OnClick = () => {
                    _idInput.Text = tag.HashID;
                    _nameInput.Text = tag.Name;
                    _descInput.Text = tag.Description;

                    // Find matching color index to sync the color button
                    _colorIndex = System.Array.IndexOf(_presetColors, tag.TagColor);
                    if (_colorIndex == -1) _colorIndex = 0;
                    _colorBtn.BackgroundColor = _presetColors[_colorIndex];
                };
                var delBtn = new UIButton
                {
                    Size = new Vector2(30, 30),
                    LocalPosition = new Vector2(row.Size.X - 40, 10),
                    IconName = "Delete",
                    BackgroundColor = Color.DarkRed
                };

                delBtn.OnClick = () => {
                    _editorState.TagManager.Tags.Remove(localId);
                    _editorState.TagManager.Save(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json"));

                    // Clear inputs if we deleted the currently editing tag
                    if (_idInput.Text == localId) { _idInput.Text = ""; _nameInput.Text = ""; _descInput.Text = ""; }

                    _cachedTagCount = -1;
                };

                row.AddChild(delBtn);
                _listStack.AddChild(row);
            }
        }

        public void ProcessSaveCommand()
        {
            if (string.IsNullOrWhiteSpace(_idInput.Text)) return;
            string id = _idInput.Text.StartsWith("#") ? _idInput.Text : "#" + _idInput.Text;

            var newTag = new TagDefinition
            {
                HashID = id,
                Name = string.IsNullOrWhiteSpace(_nameInput.Text) ? id : _nameInput.Text,
                Description = _descInput.Text,
                TagColor = _presetColors[_colorIndex]
            };

            _editorState.TagManager.Tags[id] = newTag;
            _editorState.TagManager.Save(Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json"));

            // Clear form
            _idInput.Text = ""; _nameInput.Text = ""; _descInput.Text = "";
            _editorUI.SetFocus(null);
        }

        public void ProcessDeleteCommand(string hashId)
        {
            if (_editorState.TagManager.Tags.Remove(hashId))
                _editorState.TagManager.Save(Path.Combine(PathHelper.GetAssetsPath(), "Data", "tags.json"));
        }

        public override void Draw(SpriteBatch sb)
        {
            if (!_editorState.IsTagManagerOpen) return;
            _rootPanel.Draw(sb, _editorUI, _theme);

            // CUSTOM DRAWING: Draw the pills and text over the rows
            int i = 0;
            foreach (var tag in _editorState.TagManager.Tags.Values)
            {
                if (i >= _listStack.Children.Count) break;
                var row = _listStack.Children[i];

                Vector2 pos = row.AbsoluteBounds.Location.ToVector2() + new Vector2(10, 5);

                // Ensure drawing stays within bounds if scrolled
                if (row.AbsoluteBounds.Bottom > _listStack.AbsoluteBounds.Top && row.AbsoluteBounds.Top < _listStack.AbsoluteBounds.Bottom)
                {
                    Rectangle pillBounds = new Rectangle((int)pos.X, (int)pos.Y + 10, 100, 20);
                    UIDrawExtensions.DrawPill(sb, _theme.Font, pillBounds, tag.HashID, tag.TagColor, Color.White);

                    sb.DrawString(_theme.Font, $"{tag.Name}", pos + new Vector2(120, 5), Color.White);
                    sb.DrawString(_theme.Font, tag.Description, pos + new Vector2(120, 25), Color.Gray);
                }
                i++;
            }
        }


        public override string GetDebugInfo()
        {
            return $"Total Tags: {_editorState.TagManager.Tags.Count}\n" +
                   $"Color Idx: {_colorIndex} ({_presetColors[_colorIndex].ToString()})\n" +
                   $"Scroll Y: {_listStack.ScrollOffset.Y:F0}";
        }
    }
    public class ToolPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _toolStack;
        private readonly UILabel _hintLabel;
        private readonly UITheme _theme;

        private readonly Dictionary<ITool, UIButton> _toolButtons = new Dictionary<ITool, UIButton>();

        public ToolPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont };
            _rootPanel = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(area.X + 10, area.Y + 10), // Centered vertically
                Spacing = 10f,
                Padding = 0f,
                PanelBackground = Color.Transparent,
                BorderColor = Color.Transparent
            };
            // Tools (Left)
            _toolStack = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = new Vector2(10, 10), Spacing = 5f, PanelBackground = Color.Transparent };
            foreach (var tool in editorState.ToolState.Tools)
            {
                var btn = new UIButton { Size = new Vector2(32, 32), IconName = tool.IconName, Command = new ChangeToolCommand { ToolName = tool.Name } };
                _toolButtons.Add(tool, btn);
                _toolStack.AddChild(btn);
            }
            // Spacer between tools and hints
            _toolStack.AddChild(new UIPanel { Size = new Vector2(20, 32), BackgroundColor = Color.Transparent, BorderColor = Color.Transparent });
            // Hints (Bottom Left)
            _hintLabel = new UILabel { LocalPosition = new Vector2(area.Width - 100, area.Height - 25), ColorOverride = Color.Yellow };

            
            _rootPanel.AddChild(_toolStack);
            //_rootPanel.AddChild(_hintLabel);
        }
        public override void Update(EditorInputState input, EventBus bus)
        {
            var activeTool = _editorState.ToolState.ActiveTool;
            foreach (var kvp in _toolButtons)
            {
                kvp.Value.IsSelected = (kvp.Key == activeTool);
                kvp.Value.IconName = kvp.Key.IconName;
            }
            if (activeTool != null) _hintLabel.Text = activeTool.GetShortcutHints();

            if (Area.Contains(input.MouseWindowPosition))
            {
                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
            }
        }

        public override void Draw(SpriteBatch sb)
        {
            _rootPanel.Draw(sb, _editorUI, _theme);
        }

        public override string GetDebugInfo() => "";
    }
    public class InspectorPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _inspectorStack;
        private readonly UILabel _linkingNotice;
        private readonly UITheme _theme;

        private MapObject _cachedSelectedObject;
        private bool _needsInspectorRebuild = false;
        private UILabel _posLabel;
        private UILabel _extraDataLabel;
        private UIStackPanel _tagLibraryScroll; // Keep reference to scroll boxes
        private UIStackPanel _propListScroll;
        private string _newPropKey = "";
        private PropertyType _newPropType = PropertyType.String;
        private bool _isTagPickerOpen = false;
        public InspectorPanel(Rectangle area, EditorUI editorUI, EditorState editorState) : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont, PanelBackground = Color.Black * 0.8f, BorderColor = Color.DarkGray };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            _inspectorStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(15, 10),
                Size = new Vector2(area.Width - 30, area.Height - 20),
                PanelBackground = Color.Transparent,
                Spacing = 25f
            };

            _linkingNotice = new UILabel { LocalPosition = new Vector2(15, 10), Text = " LINKING MODE: Click a target on the map. (ESC to Cancel)", ColorOverride = Color.Cyan, IsVisible = false };

            _rootPanel.AddChild(_inspectorStack);
            _rootPanel.AddChild(_linkingNotice);
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            var selected = _editorState.Selection.SelectedMapObject;
            if (_cachedSelectedObject != selected)
            {
                _cachedSelectedObject = selected;
                _needsInspectorRebuild = true;
            }

            if (_needsInspectorRebuild)
            {
                BuildInspector(selected, bus);
                _needsInspectorRebuild = false;
            }

            // Handle Link Picking Mode
            if (_editorState.UI.IsLinkingMode)
            {
                if (input.IsNewRightClick || input.CurrentKeyboard.IsKeyDown(Keys.Escape))
                {
                    _editorState.UI.IsLinkingMode = false;
                    _needsInspectorRebuild = true;
                }
                else if (input.IsNewLeftClick && _editorState._layoutmanager.ViewportPanel.Contains(input.MouseWindowPosition))
                {
                    var target = FindTopObjectUnderMouse(input.MouseWorldPosition);

                    // CRITICAL FIX: Ensure the source is not null! (e.g. user deselected it while in link mode)
                    var source = _editorState.Selection.SelectedMapObject;

                    if (source != null && target != null && target != source)
                    {
                        bus.Publish(new LinkObjectsCommand(source, target));
                    }

                    _editorState.UI.IsLinkingMode = false;
                    _needsInspectorRebuild = true;
                }
                return; // BLOCK UI clicks while linking
            }

            if (Area.Contains(input.MouseWindowPosition))
            {
                ApplyGlobalScrolling(_rootPanel, input);
                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
            }
        }

        private void ApplyGlobalScrolling(UIElement element, EditorInputState input)
        {
            if (element is UIStackPanel stack && stack.ClipToBounds && stack.AbsoluteBounds.Contains(input.MouseWindowPosition))
            {
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) stack.ScrollOffset = new Vector2(0, System.Math.Max(0, stack.ScrollOffset.Y - scrollDelta * 0.5f));
            }
            foreach (var child in element.Children) ApplyGlobalScrolling(child, input);
        }

        private void BuildInspector(MapObject obj, EventBus bus)
        {
            _inspectorStack.ClearChildren();
            _linkingNotice.IsVisible = _editorState.UI.IsLinkingMode;

            if (_editorState.UI.IsLinkingMode || obj == null) return;
            // CRASH FIX: Ensure collections are initialized (fixes old save files)
            obj.Tags ??= new System.Collections.Generic.HashSet<string>();
            obj.Properties ??= new System.Collections.Generic.Dictionary<string, MapProperty>();
            // --- COL 1: BASE INFO (200px) ---
            var col1 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 20f, AutoSize = false, Size = new Vector2(200, 160) };
            col1.AddChild(new UILabel { Text = $"Name: {obj.Name}", TextColor = Color.White });
            col1.AddChild(new UILabel { Text = $"Type: {obj.Type}", TextColor = Color.LightGray });
            col1.AddChild(new UILabel { Text = $"Pos: {obj.Position.X:F0}, {obj.Position.Y:F0}", TextColor = Color.Gray });
            var delObjBtn = new UIButton { Size = new Vector2(180, 24), Text = "Delete Object", BackgroundColor = Color.DarkRed };
            delObjBtn.OnClick = () => {
                // Find the layer this object belongs to and delete it
                var layer = _editorState.ActiveMap.Layers.FirstOrDefault(l =>
                    (l is ObjectLayer ol && ol.Objects.Contains(obj)) ||
                    (l is ControlLayer cl && (cl.Rectangles.Contains(obj) || cl.Shapes.Contains(obj) || cl.Points.Contains(obj)))
                );

                if (layer is ObjectLayer targetLayer)
                {
                    bus.Publish(new RemoveObjectCommand(targetLayer, obj));
                    _editorState.Selection.SelectedMapObject = null;
                    _needsInspectorRebuild = true;
                }
            };
            col1.AddChild(delObjBtn);
            _inspectorStack.AddChild(col1);
            _inspectorStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });
            // --- COL 2: LINKING (250px) ---
            var col2 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f, AutoSize = false, Size = new Vector2(250, 160) };
            var linkHeaderRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 15f };
            var pickBtn = new UIButton { Size = new Vector2(90, 24), Text = "+ Target", BackgroundColor = Color.DarkCyan };
            linkHeaderRow.AddChild(pickBtn);
            pickBtn.OnClick = () => { _editorState.UI.IsLinkingMode = true; _needsInspectorRebuild = true; };

            linkHeaderRow.AddChild(new UILabel { Text = " Connections", TextColor = Color.Cyan });
            col2.AddChild(linkHeaderRow);

            var linkListScrollBox = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.3f, BorderColor = Color.Transparent, Spacing = 8f, Padding = 5f, AutoSize = false, Size = new Vector2(250, 100), ClipToBounds = true };
            foreach (string targetId in obj.LinkedObjects)
            {
                string safeTargetId = targetId;
                var row = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 15f };
                var delBtn = new UIButton { Size = new Vector2(40, 20), Text = "Del", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => { bus.Publish(new UnlinkObjectCommand(obj, safeTargetId)); _needsInspectorRebuild = true; };
                row.AddChild(delBtn);
                row.AddChild(new UILabel { Text = $"> {targetId.Substring(0, 8)}", TextColor = Color.LightGray });
                linkListScrollBox.AddChild(row);
            }
            if (obj.LinkedObjects.Count == 0) linkListScrollBox.AddChild(new UILabel { Text = "Not linked.", TextColor = Color.Gray });
            col2.AddChild(linkListScrollBox);
            _inspectorStack.AddChild(col2);
            _inspectorStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });

            // --- COL 3: TAGS (250px) ---
            var col3 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f, AutoSize = false, Size = new Vector2(250, 160) };

            var tagHeaderRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
            tagHeaderRow.AddChild(new UILabel { Text = "Tags:", TextColor = Color.Yellow });

            var addTagBtn = new UIButton { Size = new Vector2(30, 20), Text = "+", BackgroundColor = Color.DarkCyan };
            addTagBtn.OnClick = () => { _isTagPickerOpen = !_isTagPickerOpen; _needsInspectorRebuild = true; };
            tagHeaderRow.AddChild(addTagBtn);
            col3.AddChild(tagHeaderRow);

            var tagFlow = new UIFlowPanel { Size = new Vector2(250, 24), BackgroundColor = Color.Black * 0.2f, BorderColor = Color.Transparent, SpacingX = 8f, SpacingY = 8f, Padding = 5f };

            // Inherited Tags
            if (obj is PropObject prop)
            {
                var prefab = _editorState.PrefabManager.GetPrefab(prop.PrefabID);
                if (prefab != null)
                {
                    foreach (var pTag in prefab.Tags)
                    {
                        var def = _editorState.TagManager.GetTag(pTag);
                        // SAFE MEASUREMENT
                        Vector2 tSize = (UITheme.DefaultFont != null && !string.IsNullOrEmpty(pTag)) ? UITheme.DefaultFont.MeasureString(pTag) : new Vector2(50, 20);
                        tagFlow.AddChild(new UIButton { Size = new Vector2(tSize.X + 16, 24), Text = pTag, BackgroundColor = (def?.TagColor ?? Color.Gray) * 0.5f, TextColor = Color.LightGray });
                    }
                }
            }

            // Instance Tags
            foreach (var iTag in obj.Tags)
            {
                string localTag = iTag;
                var def = _editorState.TagManager.GetTag(iTag);
                // SAFE MEASUREMENT
                Vector2 tSize = (UITheme.DefaultFont != null && !string.IsNullOrEmpty(iTag)) ? UITheme.DefaultFont.MeasureString(iTag) : new Vector2(50, 20);
                var btn = new UIButton { Size = new Vector2(tSize.X + 16, 24), Text = iTag, BackgroundColor = def?.TagColor ?? Color.Gray };
                btn.OnClick = () => { obj.Tags.Remove(localTag); _needsInspectorRebuild = true; };
                tagFlow.AddChild(btn);
            }
            col3.AddChild(tagFlow);

            // Tag Library Expander
            var tagLibraryScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(250, 100), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            tagLibraryScroll.IsVisible = _isTagPickerOpen;

            if (_isTagPickerOpen)
            {
                foreach (var tag in _editorState.TagManager.Tags.Values)
                {
                    if (obj.Tags.Contains(tag.HashID)) continue;

                    string safeHash = tag.HashID;
                    var btn = new UIButton { Size = new Vector2(230, 24), Text = $"{tag.HashID} ({tag.Name})", BackgroundColor = tag.TagColor * 0.4f };
                    btn.OnClick = () => {
                        obj.Tags.Add(safeHash);
                        _isTagPickerOpen = false;
                        _needsInspectorRebuild = true;
                    };
                    tagLibraryScroll.AddChild(btn);
                }
                if (tagLibraryScroll.Children.Count == 0) tagLibraryScroll.AddChild(new UILabel { Text = "All tags applied.", TextColor = Color.Gray });
            }
            col3.AddChild(tagLibraryScroll);
            _inspectorStack.AddChild(col3);
            _inspectorStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });

            // CRITICAL: Call UpdateLayout so the heights calculate correctly!
            //tagFlow.UpdateLayout();
            //col3.UpdateLayout();// --- COL 4: PROPERTIES (350px) ---
            var col4 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f, AutoSize = false, Size = new Vector2(350, 160) };
            col4.AddChild(new UILabel { Text = " Custom Properties:", TextColor = Color.Cyan });

            var propListScroll = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.3f, BorderColor = Color.Transparent, Spacing = 8f, Padding = 5f, AutoSize = false, Size = new Vector2(350, 90), ClipToBounds = true };
            foreach (var kvp in obj.Properties)
            {
                string safeKey = kvp.Key;
                var pRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 8f };
                string typeIndicator = kvp.Value.Type.ToString().Substring(0, 3);

                // Fixed widths to prevent overlap
                pRow.AddChild(new UILabel { Text = $"[{typeIndicator}] {safeKey}:", TextColor = Color.LightGray });

                if (kvp.Value.Type == PropertyType.Boolean)
                {
                    var toggleBtn = new UIButton { Size = new Vector2(60, 24), Text = kvp.Value.Value, BackgroundColor = kvp.Value.Value == "True" ? Color.DarkGreen : Color.DarkRed };
                    toggleBtn.OnClick = () => { kvp.Value.Value = kvp.Value.Value == "True" ? "False" : "True"; _needsInspectorRebuild = true; };
                    pRow.AddChild(toggleBtn);
                }
                else
                {
                    var valInput = new UITextBox { Size = new Vector2(100, 24), Text = kvp.Value.Value, DebugName = $"Prop_{safeKey}" };
                    valInput.OnTextChanged = (txt) => kvp.Value.Value = txt;
                    pRow.AddChild(valInput);
                }

                var delBtn = new UIButton { Size = new Vector2(30, 24), Text = "X", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => { obj.Properties.Remove(safeKey); _needsInspectorRebuild = true; };
                pRow.AddChild(delBtn);
                propListScroll.AddChild(pRow);
            }
            if (obj.Properties.Count == 0) propListScroll.AddChild(new UILabel { Text = "No properties.", TextColor = Color.Gray });
            col4.AddChild(propListScroll);

            var addRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 8f };
            var newKeyInput = new UITextBox { Size = new Vector2(120, 24), Placeholder = "Key...", Text = _newPropKey, DebugName = "NewPropKey" };
            newKeyInput.OnTextChanged = (txt) => _newPropKey = txt;
            addRow.AddChild(newKeyInput);

            var typeBtn = new UIButton { Size = new Vector2(50, 24), Text = _newPropType.ToString().Substring(0, 3), BackgroundColor = Color.DarkGoldenrod };
            typeBtn.OnClick = () => { _newPropType = (PropertyType)(((int)_newPropType + 1) % 4); _needsInspectorRebuild = true; };
            addRow.AddChild(typeBtn);

            var addPropBtn = new UIButton { Size = new Vector2(60, 24), Text = "Add", BackgroundColor = Color.DarkGreen };
            addPropBtn.OnClick = () => {
                if (!string.IsNullOrWhiteSpace(_newPropKey) && !obj.Properties.ContainsKey(_newPropKey))
                {
                    string defaultVal = _newPropType == PropertyType.Boolean ? "False" : (_newPropType == PropertyType.String ? "" : "0");
                    obj.Properties.Add(_newPropKey.Trim(), new MapProperty(_newPropType, defaultVal));
                    _newPropKey = ""; _needsInspectorRebuild = true;
                }
            };
            addRow.AddChild(addPropBtn);
            col4.AddChild(addRow);

            _inspectorStack.AddChild(col4);
        }

        private MapObject FindTopObjectUnderMouse(Vector2 mouseWorld)
        {
            foreach (var layer in _editorState.ActiveMap.Layers.OfType<ControlLayer>().Reverse())
            {
                if (!layer.IsVisible || layer.IsLocked) continue;
                var rect = layer.Rectangles.LastOrDefault(r => new RectangleF(r.Position, r.Size).Contains(mouseWorld));
                if (rect != null) return rect;
                var pt = layer.Points.LastOrDefault(p => Vector2.Distance(p.Position, mouseWorld) <= p.Radius);
                if (pt != null) return pt;
                var shape = layer.Shapes.LastOrDefault(s => s.Shape.GetBounds().Contains(mouseWorld));
                if (shape != null) return shape;
            }
            foreach (var layer in _editorState.ActiveMap.Layers.OfType<ObjectLayer>().Where(l => l.Type == LayerType.Object).Reverse())
            {
                if (!layer.IsVisible || layer.IsLocked) continue;
                for (int i = layer.Objects.Count - 1; i >= 0; i--)
                {
                    if (layer.Objects[i] is PropObject prop)
                    {
                        var prefab = _editorState.PrefabManager.GetPrefab(prop.PrefabID);
                        if (prefab != null && new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, prefab.SourceRect.Width, prefab.SourceRect.Height).Contains(mouseWorld))
                            return prop;
                    }
                }
            }
            return null;
        }

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, _editorUI, _theme);
        public override string GetDebugInfo() => $"Linking Mode: {_editorState.UI.IsLinkingMode}";
    }
    public class MaskEditorPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _mainStack;
        private readonly UITheme _theme;

        private UILabel _radiusLabel;
        private UILabel _elevationLabel;
        private UIStackPanel _noiseListScroll;

        public MaskEditorPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.Black * 0.8f, BorderColor = Color.DarkGray };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            // MAIN HORIZONTAL STACK(Invisible Layout)
            _mainStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(15, 10),
                Size = new Vector2(area.Width - 30, area.Height - 20),
                PanelBackground = Color.Transparent,
                BorderColor = Color.Transparent,
                Spacing = 40f // Wide gaps between columns
            };

            // --- COL 1: BRUSH STATS & VIEW TOGGLES (300px) ---
            var col1 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(300, 140), Spacing = 7f };
            col1.AddChild(new UILabel { Text = "Terrain Brush Tools", TextColor = Color.Cyan });

            _radiusLabel = new UILabel { Text = "Radius: --", TextColor = Color.White };
            _elevationLabel = new UILabel { Text = "Value: --", TextColor = Color.White };

            col1.AddChild(_radiusLabel);
            col1.AddChild(_elevationLabel);
            col1.AddChild(new UILabel { Text = "[ / ] Adjust | SHIFT [ / ] Radius | CTRL Erase", TextColor = Color.Gray });

            col1.AddChild(new UIPanel { Size = new Vector2(300, 2), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });
            col1.AddChild(new UILabel { Text = "View Toggles:", TextColor = Color.Yellow });

            var toggleRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };
            toggleRow.AddChild(CreateViewToggleBtn("Red", Color.Red, () => state.ShowMaskRed = !state.ShowMaskRed, () => state.ShowMaskRed));
            toggleRow.AddChild(CreateViewToggleBtn("Grn", Color.LimeGreen, () => state.ShowMaskGreen = !state.ShowMaskGreen, () => state.ShowMaskGreen));
            toggleRow.AddChild(CreateViewToggleBtn("Blu", Color.DeepSkyBlue, () => state.ShowMaskBlue = !state.ShowMaskBlue, () => state.ShowMaskBlue));

            col1.AddChild(toggleRow);


            // --- COL 2: CHANNEL & ELEVATION (350px) ---
            var col2 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(350, 160), Spacing = 10f };
            col2.AddChild(new UILabel { Text = "Active Channel:", TextColor = Color.Yellow });

            var channelRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };
            channelRow.AddChild(CreateChannelBtn("Biome", PaintChannel.R_Biome, Color.Red));
            channelRow.AddChild(CreateChannelBtn("Spawn", PaintChannel.G_SpawnType, Color.LimeGreen));
            channelRow.AddChild(CreateChannelBtn("Elev", PaintChannel.B_Elevation, Color.DeepSkyBlue));
            col2.AddChild(channelRow);

            col2.AddChild(new UIPanel { Size = new Vector2(350, 2), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

            col2.AddChild(new UILabel { Text = "Quick Elevations (Blue):", TextColor = Color.Cyan });
            var btnRow1 = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f };
            btnRow1.AddChild(CreateElevBtn("Sea (0)", 0, Color.DarkBlue));
            btnRow1.AddChild(CreateElevBtn("Ground (128)", 128, Color.SeaGreen));

            var btnRow2 = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f };
            btnRow2.AddChild(CreateElevBtn("Hill (192)", 192, Color.SaddleBrown));
            btnRow2.AddChild(CreateElevBtn("Peak (255)", 255, Color.White));

            col2.AddChild(btnRow1);
            col2.AddChild(btnRow2);


            // --- COL 3: NOISE MANAGER (350px) ---
            var col3 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(350, 160), Spacing = 10f };
            col3.AddChild(new UILabel { Text = "Active Noise:", TextColor = Color.Cyan });

            _noiseListScroll = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.4f, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(350, 120), ClipToBounds = true, Spacing = 5f, Padding = 5f };
            col3.AddChild(_noiseListScroll);

            // Assemble
            _mainStack.AddChild(col1);
            _mainStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });
            _mainStack.AddChild(col2);
            _mainStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });
            _mainStack.AddChild(col3);
            _rootPanel.AddChild(_mainStack);
        }

        // ----------------------------------------------------
        // Add this helper method to MaskEditorPanel class:
        private UIButton CreateViewToggleBtn(string text, Color baseColor, System.Action onClick, System.Func<bool> getState)
        {
            var btn = new UIButton { Size = new Vector2(60, 24), Text = text, BackgroundColor = baseColor * 0.5f, TextColor = Color.White };
            btn.OnClick = () => {
                onClick();
                btn.BorderColor = getState() ? Color.Yellow : Color.Transparent;
                btn.BackgroundColor = getState() ? baseColor : baseColor * 0.2f;
            };
            // Initial state
            btn.BorderColor = getState() ? Color.Yellow : Color.Transparent;
            btn.BackgroundColor = getState() ? baseColor : baseColor * 0.2f;
            return btn;
        }
        private UIButton CreateChannelBtn(string text, PaintChannel channel, Color c)
        {
            var btn = new UIButton { Size = new Vector2(65, 24), Text = text, BackgroundColor = c * 0.4f, TextColor = Color.White };
            btn.OnClick = () => {
                var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                if (tool != null) tool.ActiveChannel = channel;
            };
            return btn;
        }
        private UIButton CreateElevBtn(string text, int val, Color c)
        {
            var btn = new UIButton { Size = new Vector2(120, 30), Text = text, BackgroundColor = c * 0.6f, TextColor = Color.White };
            btn.OnClick = () => {
                var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                if (tool != null) tool.TargetValue = val;
            };
            return btn;
        }
        public void PopulateNoises()
        {
            _noiseListScroll.ClearChildren();

            var noneBtn = new UIButton { Size = new Vector2(280, 24), Text = "None (Solid Brush)", BackgroundColor = Color.DarkSlateGray };
            noneBtn.OnClick = () => _editorState.noiseManager.ActiveNoiseName = "None";
            _noiseListScroll.AddChild(noneBtn);

            foreach (var noiseName in _editorState.noiseManager.Noises.Keys)
            {
                string safeName = noiseName;
                var btn = new UIButton { Size = new Vector2(280, 24), Text = safeName, BackgroundColor = Color.DarkSlateGray };
                btn.OnClick = () => _editorState.noiseManager.ActiveNoiseName = safeName;
                _noiseListScroll.AddChild(btn);
            }
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
            if (tool != null)
            {
                _radiusLabel.Text = $"Radius: {tool.BrushRadius:F0} px";
                _elevationLabel.Text = $"Elevation ({tool.ActiveChannel}): {tool.TargetValue}";
            }

            if (_noiseListScroll.Children.Count <= 1 && _editorState.noiseManager.Noises.Count > 0) PopulateNoises();

            foreach (UIButton btn in _noiseListScroll.Children)
            {
                bool isActive = (btn.Text == _editorState.noiseManager.ActiveNoiseName) || (btn.Text.StartsWith("None") && _editorState.noiseManager.ActiveNoiseName == "None");
                btn.BorderColor = isActive ? Color.Yellow : Color.Transparent;
                btn.BackgroundColor = isActive ? Color.DarkCyan : Color.DarkSlateGray;
            }

            if (Area.Contains(input.MouseWindowPosition))
            {
                if (_noiseListScroll.AbsoluteBounds.Contains(input.MouseWindowPosition))
                {
                    int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                    if (scrollDelta != 0) _noiseListScroll.ScrollOffset = new Vector2(0, System.Math.Max(0, _noiseListScroll.ScrollOffset.Y - scrollDelta * 0.5f));
                }

                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
            }
        }

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, _editorUI, _theme);
        public override string GetDebugInfo() => "";
    }
    public class LayerPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _listStack;
        private readonly UIStackPanel _controlsStack;
        private readonly UITheme _theme;

        public LayerPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
                : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont};

            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            // 1. Scrollable List Area
            _listStack = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = Vector2.Zero,
                Size = new Vector2(area.Width, area.Height - 40),
                AutoSize = false,
                ClipToBounds = true,
                PanelBackground = Color.Transparent
            };

            // 2. Bottom Controls Area
            _controlsStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(0, area.Height - 40),
                Size = new Vector2(area.Width, 40),
                AutoSize = false,
                PanelBackground = Color.Black * 0.6f
            };

            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "AddLayer", Command = new AddLayerCommand { Direction = true } });
            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "MoveLayer", Command = new MoveLayerCommand { Direction = false } });
            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "CycleLayerType", Command = new CycleNewLayerTypeCommand() });
            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Delete", Command = new DeleteActiveLayerCommand() });

            _rootPanel.AddChild(_listStack);
            _rootPanel.AddChild(_controlsStack);
        }

        public override void Update(EditorInputState input, EventBus eventBus)
        {
            var cycleBtn = (UIButton)_controlsStack.Children[2];
            cycleBtn.IconName = _editorState.Layers.NewLayerType.ToString() + "Layer";
            bool isTyping = _editorState.UI.FocusedElement is UITextBox txt && txt.DebugName.StartsWith("LayerNameBox");

            // Only rebuild the list if we ARE NOT typing. (Rebuilding destroys the textbox and drops focus).
            if (!isTyping)
            {
                BuildLayerList(eventBus);
            }
            if (Area.Contains(input.MouseWindowPosition))
            {
                // Handle Scrolling
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) _listStack.ScrollOffset = new Vector2(0, Math.Max(0, _listStack.ScrollOffset.Y - scrollDelta * 0.5f));

                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, eventBus);
            }
        }
        private void BuildLayerList(EventBus bus)
        {
            _listStack.ClearChildren();
            var layers = _editorState.Layers.Layers;

            for (int i = 0; i < layers.Count; i++)
            {
                int index = i;
                var layer = layers[i];
                bool isActiveLayer = _editorState.Layers.ActiveLayerIndex == i;

                var rowPanel = new UIButton
                {
                    Size = new Vector2(_listStack.Size.X - 10, 40),
                    BackgroundColor = isActiveLayer ? Color.Goldenrod * 0.4f : Color.Black * 0.2f,
                    BorderColor = isActiveLayer ? Color.Yellow : Color.Transparent,
                    Command = new SelectLayerCommand { LayerIndex = index }
                };

                var btnStack = new UIStackPanel { Direction = StackDirection.Horizontal, Size = rowPanel.Size, PanelBackground = Color.Transparent, Spacing = 5f, Padding = 8f };

                // 1. EXPAND BUTTON
                bool hasChildren = layer is ObjectLayer; // Only Object/Control layers have children
                var expandBtn = new UIButton { Size = new Vector2(24, 24), Text = layer.IsExpanded ? "v" : ">", BackgroundColor = Color.Transparent };
                expandBtn.OnClick = () => { if (hasChildren) layer.IsExpanded = !layer.IsExpanded; };
                if (!hasChildren) expandBtn.Text = "-"; // Indicator for TileLayers
                btnStack.AddChild(expandBtn);

                btnStack.AddChild(new UIButton { Size = new Vector2(24, 24), IconName = layer.IsVisible ? "Visible" : "Hidden", Command = new ToggleLayerVisibilityCommand { LayerIndex = index } });
                btnStack.AddChild(new UIButton { Size = new Vector2(24, 24), IconName = layer.IsLocked ? "Locked" : "Unlocked", Command = new ToggleLayerLockCommand { LayerIndex = index } });

                var txtName = new UITextBox { Size = new Vector2(rowPanel.Size.X - 110, 24), Text = layer.Name, DebugName = $"LayerNameBox_{index}" };
                txtName.OnTextChanged = (newText) => layer.Name = newText;
                txtName.OnGotFocus = () => bus.Publish(new SelectLayerCommand { LayerIndex = index });

                btnStack.AddChild(txtName);
                rowPanel.AddChild(btnStack);
                _listStack.AddChild(rowPanel);

                // 2. DRAW CHILDREN IF EXPANDED
                if (layer.IsExpanded && hasChildren)
                {
                    var children = GetObjectsFromLayer(layer);
                    foreach (var obj in children)
                    {
                        MapObject safeObj = obj; // Closure safety
                        bool isSelectedObj = _editorState.Selection.SelectedMapObject == safeObj;

                        var childRow = new UIButton
                        {
                            Size = new Vector2(_listStack.Size.X - 30, 30),
                            LocalPosition = new Vector2(20, 0), // Indent!
                            BackgroundColor = isSelectedObj ? Color.DarkCyan * 0.6f : Color.Black * 0.4f,
                            BorderColor = isSelectedObj ? Color.Cyan : Color.Transparent
                        };

                        var childStack = new UIStackPanel { Direction = StackDirection.Horizontal, Size = childRow.Size, PanelBackground = Color.Transparent, Padding = 5f, Spacing = 10f };

                        // Type and Name
                        string typeIndicator = safeObj.Type == ObjectType.Prop ? "[P]" : "[C]";
                        childStack.AddChild(new UILabel { Text = $"{typeIndicator} {safeObj.Name}", TextColor = isSelectedObj ? Color.White : Color.Gray });

                        // NEW: Rearrange Object Buttons (Up/Down)
                        var moveUpBtn = new UIButton { Size = new Vector2(20, 20), Text = "^", BackgroundColor = Color.DarkSlateGray };
                        moveUpBtn.OnClick = () => { bus.Publish(new MoveObjectCommand((ObjectLayer)layer, safeObj, false)); }; // False = move up list visually

                        var moveDownBtn = new UIButton { Size = new Vector2(20, 20), Text = "v", BackgroundColor = Color.DarkSlateGray };
                        moveDownBtn.OnClick = () => { bus.Publish(new MoveObjectCommand((ObjectLayer)layer, safeObj, true)); }; // True = move down list visually (drawn on top)

                        childStack.AddChild(moveUpBtn);
                        childStack.AddChild(moveDownBtn);

                        childRow.AddChild(childStack);
                        _listStack.AddChild(childRow);
                    }
                }
            }
        }
        private List<MapObject> GetObjectsFromLayer(Layer layer)
        {
            var list = new List<MapObject>();
            if (layer is ControlLayer cl)
            {
                list.AddRange(cl.Rectangles);
                list.AddRange(cl.Shapes);
                list.AddRange(cl.Points);
            }
            else if (layer is ObjectLayer ol)
            {
                list.AddRange(ol.Objects);
            }
            return list;
        }
        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, _editorUI, _theme);
        public override string GetDebugInfo()
        {
            var txt = _editorState.UI.FocusedElement as UITextBox;
            bool isTyping = txt != null && txt.DebugName.StartsWith("LayerNameBox");
            return $"Layers: {_editorState.Layers.Layers.Count} | Active Idx: {_editorState.Layers.ActiveLayerIndex}\n" +
                   $"Scroll Y: {_listStack.ScrollOffset.Y:F0} | Renaming: {isTyping}";
        }
    }
}
