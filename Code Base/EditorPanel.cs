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
            _rootStack.Draw(sb, (IUIContext)_editorUI, _theme);
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

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
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

        // --- NEW FIELDS FOR BUGS/FEATURES ---
        private UIButton _typeBtn;
        private UITextBox _stateNameInput;
        private UIStackPanel _statesListScroll;
        private UIStackPanel _templateLibraryScroll;

        // Atlas Canvas
        private Rectangle _canvasArea;
        private Rectangle _previewArea;
        private readonly UITheme _theme;

        // Property Temp State
        private string _newPropKey = "";
        private bool _isTagPickerOpen = false;
        private bool _isTemplatePickerOpen = false; // NEW
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
            _middleStack = new UIStackPanel { Direction = StackDirection.Vertical, LocalPosition = new Vector2(midX, 10), Size = new Vector2(midWidth, area.Height - 20), PanelBackground = Color.Transparent, Spacing = 10f };

            _middleStack.AddChild(new UILabel { Text = "Object ID:", TextColor = Color.Yellow });
            _nameInput = new UITextBox { Size = new Vector2(midWidth, 24), Placeholder = "e.g. Tree_Oak" };
            _middleStack.AddChild(_nameInput);

            // --- FEATURE C: TEMPLATES ---
            var templateRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
            templateRow.AddChild(new UILabel { Text = "Template:", TextColor = Color.Cyan });
            var tmplBtn = new UIButton { Size = new Vector2(120, 20), Text = "Copy Existing...", BackgroundColor = Color.DarkSlateGray };
            tmplBtn.OnClick = () => { _isTemplatePickerOpen = !_isTemplatePickerOpen; RebuildUI(); };
            templateRow.AddChild(tmplBtn);
            _middleStack.AddChild(templateRow);

            _templateLibraryScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(midWidth, 80), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            _middleStack.AddChild(_templateLibraryScroll);

            // Tags Section
            _middleStack.AddChild(new UIPanel { Size = new Vector2(midWidth, 2), BackgroundColor = Color.Gray });
            var tagHeaderRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
            tagHeaderRow.AddChild(new UILabel { Text = "Active Tags:", TextColor = Color.Cyan });

            var addTagBtn = new UIButton { Size = new Vector2(30, 20), Text = "+", BackgroundColor = Color.DarkCyan };
            addTagBtn.OnClick = () => { _isTagPickerOpen = !_isTagPickerOpen; RebuildUI(); };
            tagHeaderRow.AddChild(addTagBtn);
            _middleStack.AddChild(tagHeaderRow);

            _activeTagsFlow = new UIFlowPanel { Size = new Vector2(midWidth, 30), BackgroundColor = Color.Black * 0.2f, SpacingX = 5f, SpacingY = 5f };
            _middleStack.AddChild(_activeTagsFlow);

            _tagLibraryScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(midWidth, 100), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            _middleStack.AddChild(_tagLibraryScroll);

            // Properties Section
            _middleStack.AddChild(new UIPanel { Size = new Vector2(midWidth, 2), BackgroundColor = Color.Gray });
            _middleStack.AddChild(new UILabel { Text = "Default Properties:", TextColor = Color.Cyan });

            _propListScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(midWidth, 100), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            _middleStack.AddChild(_propListScroll);

            var addPropRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 5f };
            var newKeyInput = new UITextBox { Size = new Vector2(80, 24), Placeholder = "Key...", Text = _newPropKey, DebugName = "PrefabPropKey" };
            newKeyInput.OnTextChanged = (t) => _newPropKey = t;
            addPropRow.AddChild(newKeyInput);

            // --- BUG A FIX: Track the button so we can update its Text directly ---
            _typeBtn = new UIButton { Size = new Vector2(40, 24), Text = _newPropType.ToString().Substring(0, 3), BackgroundColor = Color.DarkGoldenrod };
            _typeBtn.OnClick = () => {
                _newPropType = (PropertyType)(((int)_newPropType + 1) % 4);
                _typeBtn.Text = _newPropType.ToString().Substring(0, 3); // Visual Update!
            };
            addPropRow.AddChild(_typeBtn);

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

            _previewArea = new Rectangle(rightX, 10, rightWidth, 200);
            _rightStack.AddChild(new UIPanel { Size = new Vector2(rightWidth, 200), BackgroundColor = Color.Transparent });

            _rightStack.AddChild(new UILabel { Text = "Select Atlas:", TextColor = Color.Yellow });

            var atlasScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(rightWidth, 80), AutoSize = false, PanelBackground = Color.Black * 0.3f, ClipToBounds = true, Spacing = 5f, Padding = 5f };
            foreach (var atlasName in _editorState.AssetLibrary.GetNamesByType(AtlasType.Object))
            {
                var aBtn = new UIButton { Size = new Vector2(rightWidth - 20, 25), Text = atlasName, BackgroundColor = Color.Black * 0.6f };
                aBtn.OnClick = () => { _editorState.PrefabCreator.ActiveAtlasName = atlasName; _editorState.PrefabCreator.AtlasPanOffset = Vector2.Zero; };
                atlasScroll.AddChild(aBtn);
            }
            _rightStack.AddChild(atlasScroll);

            // --- BUG B FIX: Alternate States Form & List ---
            _rightStack.AddChild(new UIPanel { Size = new Vector2(rightWidth, 2), BackgroundColor = Color.Gray });
            var mainSpriteBtn = new UIButton { Size = new Vector2(rightWidth, 25), Text = "Set MAIN Sprite", BackgroundColor = Color.DarkGreen };
            mainSpriteBtn.OnClick = () => {
                _editorState.PrefabCreator.BaseSourceRect = _editorState.PrefabCreator.SelectionRect;
            };
            _rightStack.AddChild(mainSpriteBtn);
            _rightStack.AddChild(new UILabel { Text = "Alternate States:", TextColor = Color.Cyan });

            var stateRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 5f };
            _stateNameInput = new UITextBox { Size = new Vector2(90, 25), Placeholder = "Name...", DebugName = "StateNameInput" };
            _stateNameInput.OnTextChanged = (t) => _editorState.PrefabCreator.NewStateName = t;
            stateRow.AddChild(_stateNameInput);

            var addStateBtn = new UIButton { Size = new Vector2(90, 25), Text = "Capture Rect", BackgroundColor = Color.DarkCyan };
            addStateBtn.OnClick = () => {
                var ctx = _editorState.PrefabCreator;
                if (!string.IsNullOrWhiteSpace(ctx.NewStateName))
                {
                    ctx.TempAlternateStates[ctx.NewStateName.Trim()] = ctx.SelectionRect;
                    ctx.NewStateName = "";
                    RebuildUI();
                }
            };
            stateRow.AddChild(addStateBtn);
            _rightStack.AddChild(stateRow);

            _statesListScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(rightWidth, 70), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            _rightStack.AddChild(_statesListScroll);

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

            // 1. Rebuild Templates (Feature C)
            _templateLibraryScroll.IsVisible = _isTemplatePickerOpen;
            if (_isTemplatePickerOpen)
            {
                _templateLibraryScroll.ClearChildren();
                foreach (var prefab in _editorState.PrefabManager.Prefabs.Values)
                {
                    var btn = new UIButton { Size = new Vector2(_templateLibraryScroll.Size.X - 20, 24), Text = prefab.ID, BackgroundColor = Color.DarkSlateGray };
                    btn.OnClick = () => {
                        // Copy Tags
                        foreach (var tag in prefab.Tags) if (!ctx.TempTags.Contains(tag)) ctx.TempTags.Add(tag);
                        // Copy Properties
                        foreach (var prop in prefab.Properties) ctx.TempProperties[prop.Key] = new MapProperty(prop.Value.Type, prop.Value.Value);
                        // Copy Alternate States
                        foreach (var alt in prefab.AlternateStates) ctx.TempAlternateStates[alt.Key] = alt.Value;

                        _isTemplatePickerOpen = false;
                        RebuildUI();
                    };
                    _templateLibraryScroll.AddChild(btn);
                }
            }

            // 2. Rebuild Active Tags
            _activeTagsFlow.ClearChildren();
            foreach (int tagId in ctx.TempTags.ToList())
            {
                int safeTag = tagId;
                var def = _editorState.TagManager.GetTag(tagId);
                string displayText = def != null ? $"{tagId}: {def.Name}" : tagId.ToString();

                Vector2 tSize = UITheme.DefaultFont != null ? UITheme.DefaultFont.MeasureString(displayText) : new Vector2(50, 20);
                var btn = new UIButton { Size = new Vector2(tSize.X + 16, 24), Text = displayText, BackgroundColor = def?.TagColor ?? Color.Gray };

                btn.OnClick = () => { ctx.TempTags.Remove(safeTag); RebuildUI(); };
                _activeTagsFlow.AddChild(btn);
            }

            // 3. Rebuild Tag Library
            _tagLibraryScroll.IsVisible = _isTagPickerOpen;
            if (_isTagPickerOpen)
            {
                _tagLibraryScroll.ClearChildren();
                foreach (var tag in _editorState.TagManager.Tags.Values)
                {
                    // Skip tags we already have
                    if (ctx.TempTags.Contains(tag.ID)) continue;

                    int safeId = tag.ID;
                    var btn = new UIButton { Size = new Vector2(_tagLibraryScroll.Size.X - 20, 24), Text = $"{tag.ID}: {tag.Name}", BackgroundColor = tag.TagColor * 0.4f };

                    btn.OnClick = () => {
                        ctx.TempTags.Add(safeId);
                        _isTagPickerOpen = false;
                        RebuildUI();
                    };
                    _tagLibraryScroll.AddChild(btn);
                }
                if (_tagLibraryScroll.Children.Count == 0) _tagLibraryScroll.AddChild(new UILabel { Text = "All tags applied.", TextColor = Color.Gray });
            }

            // 4. Rebuild Properties
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

            // 5. Rebuild Alternate States (Bug B Fix)
            _statesListScroll.ClearChildren();
            foreach (var kvp in ctx.TempAlternateStates)
            {
                string safeKey = kvp.Key;
                var sRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 5f };

                var showBtn = new UIButton { Size = new Vector2(130, 20), Text = safeKey, BackgroundColor = Color.DarkSlateGray };
                showBtn.OnClick = () => { ctx.SelectionRect = kvp.Value; }; // Click to preview the rect!
                sRow.AddChild(showBtn);

                var delBtn = new UIButton { Size = new Vector2(25, 20), Text = "X", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => { ctx.TempAlternateStates.Remove(safeKey); RebuildUI(); };
                sRow.AddChild(delBtn);

                _statesListScroll.AddChild(sRow);
            }
            if (ctx.TempAlternateStates.Count == 0) _statesListScroll.AddChild(new UILabel { Text = "No alternate states.", TextColor = Color.Gray });

            _activeTagsFlow.UpdateLayout();
            _middleStack.UpdateLayout();
            _rightStack.UpdateLayout();
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

            // Sync Text Inputs
            if (!_nameInput.IsFocused) _nameInput.Text = ctx.TempName;
            _nameInput.OnTextChanged = (t) => ctx.TempName = t;

            // --- BUG B FIX: Ensure Alternate State Name syncs! ---
            if (!_stateNameInput.IsFocused) _stateNameInput.Text = ctx.NewStateName;

            // Initialize Rebuild if first frame
            if (_activeTagsFlow.Children.Count == 0 && ctx.TempTags.Count > 0) RebuildUI();

            _editorUI.CheckFocusClick(_rootPanel, input);
            _rootPanel.Update(input, bus);

            ApplyGlobalScrolling(_rootPanel, input);

            // --- CANVAS PANNING ---
            if (_editorState.UI.FocusedElement == null)
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

            _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);

            Rectangle absCanvas = new Rectangle(Area.X + _canvasArea.X, Area.Y + _canvasArea.Y, _canvasArea.Width, _canvasArea.Height);
            Rectangle absPreview = new Rectangle(Area.X + _previewArea.X, Area.Y + _previewArea.Y, _previewArea.Width, _previewArea.Height);

            // 1. Draw Canvas
            sb.FillRectangle(absCanvas, Color.Black * 0.8f);
            sb.DrawRectangle(absCanvas, Color.White, 1);

            var tex = _editorState.AssetLibrary.GetAtlas(ctx.ActiveAtlasName);
            if (tex != null)
            {
                Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
                sb.End();
                sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
                sb.GraphicsDevice.ScissorRectangle = absCanvas;

                Vector2 drawPos = absCanvas.Location.ToVector2() + ctx.AtlasPanOffset;
                sb.Draw(tex, drawPos, Color.White);

                for (int x = 0; x < tex.Width; x += 16) sb.DrawLine(drawPos.X + x, drawPos.Y, drawPos.X + x, drawPos.Y + tex.Height, Color.White * 0.1f);
                for (int y = 0; y < tex.Height; y += 16) sb.DrawLine(drawPos.X, drawPos.Y + y, drawPos.X + tex.Width, drawPos.Y + y, Color.White * 0.1f);

                Rectangle baseSelect = new Rectangle(
                    (int)drawPos.X + ctx.BaseSourceRect.X,
                    (int)drawPos.Y + ctx.BaseSourceRect.Y,
                    ctx.BaseSourceRect.Width, ctx.BaseSourceRect.Height);

                // Draw the Saved Main Sprite in Green
                sb.DrawRectangle(baseSelect, Color.LimeGreen, 2);
                sb.FillRectangle(baseSelect, Color.LimeGreen * 0.2f);

                // Draw the Current Selection Box in Yellow
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
        public override string GetDebugInfo() => "";
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
            _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
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
                Size = new Vector2(area.Width - 10, area.Height - 10),
                PanelBackground = Color.Transparent,
                Spacing = 5f
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
            obj.Tags ??= new System.Collections.Generic.HashSet<int>();
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

            // --- BUG FIX: Make the Tag Flow Panel scrollable if there are many tags ---
            var tagFlowScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(250, 80), AutoSize = false, ClipToBounds = true, PanelBackground = Color.Transparent };

            var tagFlow = new UIFlowPanel { Size = new Vector2(250, 40), BackgroundColor = Color.Black * 0.2f, BorderColor = Color.Transparent, SpacingX = 8f, SpacingY = 0f, Padding = 2f };

            // Inherited Tags (from Prefab)
            if (obj is PropObject prop)
            {
                var prefab = _editorState.PrefabManager.GetPrefab(prop.PrefabID);
                if (prefab != null)
                {
                    foreach (int pTag in prefab.Tags)
                    {
                        var def = _editorState.TagManager.GetTag(pTag);
                        string displayText = def != null ? $"{pTag}: {def.Name}" : pTag.ToString();

                        Vector2 tSize = (UITheme.DefaultFont != null) ? UITheme.DefaultFont.MeasureString(displayText) : new Vector2(50, 20);
                        tagFlow.AddChild(new UIButton { Size = new Vector2(tSize.X + 16, 24), Text = displayText, BackgroundColor = (def?.TagColor ?? Color.Gray) * 0.5f, TextColor = Color.LightGray });
                    }
                }
            }

            // Instance Tags (on the Object)
            foreach (int iTag in obj.Tags)
            {
                int localTag = iTag;
                var def = _editorState.TagManager.GetTag(iTag);
                string displayText = def != null ? $"{iTag}: {def.Name}" : iTag.ToString();

                Vector2 tSize = (UITheme.DefaultFont != null) ? UITheme.DefaultFont.MeasureString(displayText) : new Vector2(50, 20);
                var btn = new UIButton { Size = new Vector2(tSize.X + 16, 24), Text = displayText, BackgroundColor = def?.TagColor ?? Color.Gray };
                btn.OnClick = () => { obj.Tags.Remove(localTag); _needsInspectorRebuild = true; };
                tagFlow.AddChild(btn);
            }

            tagFlowScroll.AddChild(tagFlow);
            col3.AddChild(tagFlowScroll);

            // Tag Library Expander (The picker list)
            var tagLibraryScroll = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(250, 100), AutoSize = false, PanelBackground = Color.Black * 0.4f, ClipToBounds = true, Spacing = 4f, Padding = 5f };
            tagLibraryScroll.IsVisible = _isTagPickerOpen;

            if (_isTagPickerOpen)
            {
                // Get all tags that haven't been applied yet
                var availableTags = _editorState.TagManager.Tags.Values.Where(t => !obj.Tags.Contains(t.ID)).ToList();

                if (availableTags.Count == 0)
                {
                    tagLibraryScroll.AddChild(new UILabel { Text = "All tags applied.", TextColor = Color.Gray });
                }
                else
                {
                    for (int i = 0; i < availableTags.Count; i += 2)
                    {
                        var row = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };

                        for (int j = 0; j < 2; j++)
                        {
                            if (i + j < availableTags.Count)
                            {
                                var tag = availableTags[i + j];
                                int safeId = tag.ID;

                                // Display "ID: Name"
                                var btn = new UIButton { Size = new Vector2(115, 24), Text = $"{tag.ID}: {tag.Name}", BackgroundColor = tag.TagColor * 0.4f };
                                btn.OnClick = () => {
                                    obj.Tags.Add(safeId);
                                    _isTagPickerOpen = false;
                                    _needsInspectorRebuild = true;
                                };
                                row.AddChild(btn);
                            }
                        }
                        tagLibraryScroll.AddChild(row);
                    }
                }
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

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
        public override string GetDebugInfo() => $"Linking Mode: {_editorState.UI.IsLinkingMode}";
    }
    public class MaskEditorPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _mainStack;
        private readonly UITheme _theme;

        // Dynamic Sub-Stacks
        private UIStackPanel _col2Stack;
        private UIStackPanel _noiseListScroll;

        // Labels & State
        private UILabel _radiusLabel, _elevationLabel;
        private PaintChannel _lastBuiltChannel = PaintChannel.B_Elevation;
        private string _newDataName = "";
        private int _newDataValue = 0;

        // Expose Light Preview to the Editor State!

        public MaskEditorPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.Black * 0.85f, BorderColor = Color.DarkGray };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height), BackgroundColor = _theme.PanelBackground, BorderColor = Color.Transparent };

            _mainStack = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = new Vector2(15, 10), Size = new Vector2(area.Width - 30, area.Height - 20), PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 30f };

            // ==========================================================
            // COLUMN 1: BRUSH STATS & VIEW TOGGLES (250px)
            // ==========================================================
            var col1 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(250, 160), Spacing = 10f };
            //col1.AddChild(new UILabel { Text = "Terrain Brush Tools", TextColor = Color.Cyan });

            _radiusLabel = new UILabel { Text = "Radius: --", TextColor = Color.White };
            _elevationLabel = new UILabel { Text = "Value: --", TextColor = Color.White };
            //col1.AddChild(_radiusLabel);
            //col1.AddChild(_elevationLabel);

            col1.AddChild(new UIPanel { Size = new Vector2(250, 2), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });
            col1.AddChild(new UILabel { Text = "View Toggles:", TextColor = Color.Yellow });

            var toggleRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 8f };
            toggleRow.AddChild(CreateViewToggleBtn("R", Color.Red, () => state.ShowMaskRed = !state.ShowMaskRed, () => state.ShowMaskRed));
            toggleRow.AddChild(CreateViewToggleBtn("G", Color.LimeGreen, () => state.ShowMaskGreen = !state.ShowMaskGreen, () => state.ShowMaskGreen));
            toggleRow.AddChild(CreateViewToggleBtn("B", Color.DeepSkyBlue, () => state.ShowMaskBlue = !state.ShowMaskBlue, () => state.ShowMaskBlue));

            col1.AddChild(toggleRow);

            // ==========================================================
            // COLUMN 2: CHANNEL & DATA EDITOR (550px)
            // ==========================================================
            _col2Stack = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(550, 160), Spacing = 10f };

            // ==========================================================
            // COLUMN 3: NOISE MANAGER (300px)
            // ==========================================================
            var col3 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(300, 160), Spacing = 10f };
            col3.AddChild(new UILabel { Text = "Active Noise:", TextColor = Color.Cyan });

            _noiseListScroll = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.4f, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(300, 120), ClipToBounds = true, Spacing = 5f, Padding = 5f };
            col3.AddChild(_noiseListScroll);

            // Assemble
            _mainStack.AddChild(col1);
            _mainStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });
            _mainStack.AddChild(_col2Stack);
            _mainStack.AddChild(new UIPanel { Size = new Vector2(2, 140), BackgroundColor = Color.Gray, BorderColor = Color.Transparent });
            _mainStack.AddChild(col3);

            _rootPanel.AddChild(_mainStack);

            RebuildColumn2(PaintChannel.B_Elevation);
        }

        // ==========================================================
        // DYNAMIC REBUILDER FOR COLUMN 2
        // ==========================================================
        private void RebuildColumn2(PaintChannel activeChannel)
        {
            _col2Stack.ClearChildren();
            _lastBuiltChannel = activeChannel;

            // 1. Channel Selector Row
            var channelHeader = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };
            channelHeader.AddChild(new UILabel { Text = "Channel:", TextColor = Color.Yellow });
            channelHeader.AddChild(CreateChannelBtn("Biome (Red)", PaintChannel.R_Biome, Color.Red));
            channelHeader.AddChild(CreateChannelBtn("Spawn (Green)", PaintChannel.G_SpawnType, Color.LimeGreen));
            channelHeader.AddChild(CreateChannelBtn("Elev (Blue)", PaintChannel.B_Elevation, Color.DeepSkyBlue));
            _col2Stack.AddChild(channelHeader);

            // 2. Brush Mode Row (Only show for Elevation!)
            if (activeChannel == PaintChannel.B_Elevation)
            {
                var modeRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };
                modeRow.AddChild(new UILabel { Text = "Mode:", TextColor = Color.Cyan });
                modeRow.AddChild(CreateModeBtn("Solid", BrushMode.Solid));
                modeRow.AddChild(CreateModeBtn("Detail (+)", BrushMode.DetailAdd));
                modeRow.AddChild(CreateModeBtn("Detail (-)", BrushMode.DetailSub));
                _col2Stack.AddChild(modeRow);
            }

            _col2Stack.AddChild(new UIPanel { Size = new Vector2(550, 2), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

            // 3. Data Split Layout
            var splitLayout = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 20f };

            // --- LEFT: ADD NEW DATA FORM (220px) ---
            var leftForm = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Size = new Vector2(220, 100), AutoSize = false, Spacing = 10f };

            string title = activeChannel switch { PaintChannel.R_Biome => "Biomes (R):", PaintChannel.G_SpawnType => "Spawns (G):", _ => "Elevations (B):" };
            leftForm.AddChild(new UILabel { Text = title, TextColor = Color.Cyan });
            leftForm.AddChild(new UILabel { Text = "Add New Definition:", TextColor = Color.Gray });

            var nameIn = new UITextBox { Size = new Vector2(220, 24), Placeholder = "Name", Text = _newDataName, DebugName = "DataNameIn" };
            nameIn.OnTextChanged = (t) => _newDataName = t;
            leftForm.AddChild(nameIn);

            var valRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f };
            var valIn = new UITextBox { Size = new Vector2(140, 24), Placeholder = "Val (0-255)", Text = _newDataValue.ToString(), DebugName = "DataValIn" };
            valIn.OnTextChanged = (t) => { if (int.TryParse(t, out int v)) _newDataValue = v; };
            valRow.AddChild(valIn);

            var dataList = activeChannel switch { PaintChannel.R_Biome => _editorState.MaskData.Biomes, PaintChannel.G_SpawnType => _editorState.MaskData.Spawns, _ => _editorState.MaskData.Elevations };

            var addBtn = new UIButton { Size = new Vector2(70, 24), Text = "+ Add", BackgroundColor = Color.DarkGreen };
            addBtn.OnClick = () => {
                if (!string.IsNullOrWhiteSpace(_newDataName))
                {
                    dataList.Add(new MaskDataDef { Name = _newDataName, Value = MathHelper.Clamp(_newDataValue, 0, 255) });
                    _editorState.MaskData.Save(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "mask_data.json"));
                    _newDataName = ""; _newDataValue = 0;
                    _editorUI.SetFocus(null);
                    RebuildColumn2(activeChannel);
                }
            };
            valRow.AddChild(addBtn);
            leftForm.AddChild(valRow);
            splitLayout.AddChild(leftForm);

            // --- RIGHT: SCROLLABLE LIST OF SAVED DATA (310px) ---
            var rightList = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(310, 85), PanelBackground = Color.Black * 0.3f, BorderColor = Color.Transparent, ClipToBounds = true, Spacing = 4f, Padding = 5f, AutoSize = false };

            foreach (var def in dataList.ToList())
            {
                var safeDef = def;
                var listRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };

                var btn = new UIButton { Size = new Vector2(260, 24), Text = $"[{safeDef.Value}] {safeDef.Name}", BackgroundColor = Color.DarkSlateGray };
                btn.OnClick = () => {
                    var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                    if (tool != null) tool.TargetValue = safeDef.Value;
                };

                var delBtn = new UIButton { Size = new Vector2(30, 24), Text = "X", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => {
                    dataList.Remove(safeDef);
                    _editorState.MaskData.Save(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "mask_data.json"));
                    RebuildColumn2(activeChannel);
                };

                listRow.AddChild(btn);
                listRow.AddChild(delBtn);
                rightList.AddChild(listRow);
            }
            if (dataList.Count == 0) rightList.AddChild(new UILabel { Text = "No data defined.", TextColor = Color.Gray });

            splitLayout.AddChild(rightList);
            _col2Stack.AddChild(splitLayout);
            _col2Stack.UpdateLayout();
        }

        // ==========================================================
        // HELPER METHODS
        // ==========================================================
        private UIButton CreateChannelBtn(string text, PaintChannel channel, Color c)
        {
            var btn = new UIButton { Size = new Vector2(100, 24), Text = text, BackgroundColor = c * 0.4f, TextColor = Color.White };
            btn.OnClick = () => {
                var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                if (tool != null) tool.ActiveChannel = channel;
            };
            return btn;
        }
        private UIButton CreateModeBtn(string text, BrushMode mode)
        {
            var btn = new UIButton { Size = new Vector2(70, 24), Text = text, BackgroundColor = Color.DarkSlateGray, TextColor = Color.White };
            btn.OnClick = () => {
                var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                if (tool != null) tool.ActiveMode = mode;
                RebuildColumn2(_lastBuiltChannel); // Refresh UI highlights
            };
            var t = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
            btn.BorderColor = (t != null && t.ActiveMode == mode) ? Color.Yellow : Color.Transparent;
            return btn;
        }
        private UIButton CreateViewToggleBtn(string text, Color baseColor, System.Action onClick, System.Func<bool> getState)
        {
            var btn = new UIButton { Size = new Vector2(60, 24), Text = text, BackgroundColor = baseColor * 0.5f, TextColor = Color.White };
            btn.OnClick = () => {
                onClick();
                btn.BorderColor = getState() ? Color.Yellow : Color.Transparent;
                btn.BackgroundColor = getState() ? baseColor : baseColor * 0.2f;
            };
            btn.BorderColor = getState() ? Color.Yellow : Color.Transparent;
            btn.BackgroundColor = getState() ? baseColor : baseColor * 0.2f;
            return btn;
        }

        public void PopulateNoises()
        {
            _noiseListScroll.ClearChildren();

            var availableNoises = _editorState.noiseManager.Noises.Keys.ToList();
            availableNoises.Insert(0, "None"); // Add the default "Solid Brush" option

            // Loop through the list in steps of 2 to create columns!
            for (int i = 0; i < availableNoises.Count; i += 2)
            {
                var row = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f };

                for (int j = 0; j < 2; j++)
                {
                    if (i + j < availableNoises.Count)
                    {
                        string safeName = availableNoises[i + j];
                        string displayText = safeName == "None" ? "Solid" : safeName;

                        // Width of 135px fits 2 buttons perfectly in the 300px panel width
                        var btn = new UIButton { Size = new Vector2(135, 24), Text = displayText, BackgroundColor = Color.DarkSlateGray };
                        btn.OnClick = () => _editorState.noiseManager.ActiveNoiseName = safeName;
                        row.AddChild(btn);
                    }
                }
                _noiseListScroll.AddChild(row);
            }
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;

            if (tool != null)
            {
                _radiusLabel.Text = $"Radius: {tool.BrushRadius:F0} px";
                _elevationLabel.Text = $"Value ({tool.ActiveChannel}): {tool.TargetValue}";

                if (tool.ActiveChannel != _lastBuiltChannel) RebuildColumn2(tool.ActiveChannel);
            }

            if (_noiseListScroll.Children.Count <= 1 && _editorState.noiseManager.Noises.Count > 0) PopulateNoises();

            // Check nested buttons for visual highlighting
            foreach (UIElement row in _noiseListScroll.Children)
            {
                foreach (UIButton btn in row.Children)
                {
                    bool isActive = (btn.Text == _editorState.noiseManager.ActiveNoiseName) || (btn.Text == "Solid" && _editorState.noiseManager.ActiveNoiseName == "None");
                    btn.BorderColor = isActive ? Color.Yellow : Color.Transparent;
                    btn.BackgroundColor = isActive ? Color.DarkCyan : Color.DarkSlateGray;
                }
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

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
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
        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
        public override string GetDebugInfo()
        {
            var txt = _editorState.UI.FocusedElement as UITextBox;
            bool isTyping = txt != null && txt.DebugName.StartsWith("LayerNameBox");
            return $"Layers: {_editorState.Layers.Layers.Count} | Active Idx: {_editorState.Layers.ActiveLayerIndex}\n" +
                   $"Scroll Y: {_listStack.ScrollOffset.Y:F0} | Renaming: {isTyping}";
        }
    }
}
