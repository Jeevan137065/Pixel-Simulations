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
        private readonly UIStackPanel _headerSelectorStack; // Compact top selector
        private readonly UIStackPanel _footerStack;
        private readonly UIGridView _contentGrid;
        private readonly UITheme _theme;

        private readonly UIButton _prevBtn;
        private readonly UIButton _nextBtn;
        private readonly UIButton _activeTilesetNameBtn;

        public TilesetPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            // 1. TABS (Top - Height: 30)
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

            // 2. COMPACT TILESET CYCLE SELECTOR (Y: 30, Height: 32)
            _headerSelectorStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(0, 30),
                Size = new Vector2(area.Width, 32),
                AutoSize = false,
                PanelBackground = Color.Black * 0.3f,
                Spacing = 4f,
                Padding = 4f
            };

            _prevBtn = new UIButton { Size = new Vector2(24, 24), Text = "<", BackgroundColor = Color.DarkSlateGray };
            _prevBtn.OnClick = () => CycleTileset(false);

            _activeTilesetNameBtn = new UIButton { Size = new Vector2(area.Width - 84, 24), Text = "None", BackgroundColor = Color.Black * 0.4f };

            _nextBtn = new UIButton { Size = new Vector2(24, 24), Text = ">", BackgroundColor = Color.DarkSlateGray };
            _nextBtn.OnClick = () => CycleTileset(true);

            _headerSelectorStack.AddChild(_prevBtn);
            _headerSelectorStack.AddChild(_activeTilesetNameBtn);
            _headerSelectorStack.AddChild(_nextBtn);

            // 3. SCROLLABLE CONTENT (Middle - Starts at Y: 62)
            _contentGrid = new UIGridView(_editorState, this)
            {
                LocalPosition = new Vector2(0, 62),
                Size = new Vector2(area.Width, area.Height - 102), // Fills remaining space above footer
                ClipToBounds = true
            };

            // 4. FOOTER UTILITIES (Bottom - Height: 40)
            _footerStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(0, area.Height - 40),
                Size = new Vector2(area.Width, 40),
                AutoSize = false,
                PanelBackground = Color.Black * 0.5f,
                Spacing = 6f,
                Padding = 4f
            };

            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewTileSet", Command = new OpenAtlasPickerCommand() });
            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewObject", Command = new TogglePrefabCreatorCommand { DefaultAtlasName = "Basic" } });
            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewTags", Text = "Tags", Command = new ToggleTagManagerCommand() });

            var atBtn = new UIButton { Size = new Vector2(32, 32), IconName = "AutoTile", Text = "AT" };
            atBtn.OnClick = () => _editorUI.AutoTilePanel.Open(_editorState.AutoTileManager.GetRuleForAtlas(_editorState.TilesetPanel.ActiveTilesetName));
            _footerStack.AddChild(atBtn);

            _rootPanel.AddChild(_tabStack);
            _rootPanel.AddChild(_headerSelectorStack);
            _rootPanel.AddChild(_contentGrid);
            _rootPanel.AddChild(_footerStack);
        }

        private void CycleTileset(bool forward)
        {
            if (_editorState.ActiveTileSets.Count == 0) return;

            int idx = _editorState.ActiveTileSets.FindIndex(ts => ts.Name == _editorState.TilesetPanel.ActiveTilesetName);
            if (idx == -1) idx = 0;

            if (forward)
                idx = (idx + 1) % _editorState.ActiveTileSets.Count;
            else
                idx = (idx - 1 + _editorState.ActiveTileSets.Count) % _editorState.ActiveTileSets.Count;

            var nextTs = _editorState.ActiveTileSets[idx];
            _editorUI.SetFocus(null);
            _editorState.TilesetPanel.ActiveTilesetName = nextTs.Name;

            if (nextTs.SlicedAtlas.Keys.Any())
            {
                _editorState.TilesetPanel.SelectedTileID = nextTs.SlicedAtlas.Keys.First();
                _editorState.Selection.ActiveTileBrush = new TileInfo(nextTs.Name, nextTs.SlicedAtlas.Keys.First());
            }
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            // Sync Tab States
            ((UIButton)_tabStack.Children[0]).IsSelected = (_activeTab == Tab.Tiles);
            ((UIButton)_tabStack.Children[1]).IsSelected = (_activeTab == Tab.Objects);

            // Update Active Selection Readout
            var activeTsName = _editorState.TilesetPanel.ActiveTilesetName;
            _activeTilesetNameBtn.Text = !string.IsNullOrEmpty(activeTsName) ? activeTsName : "No Tileset Loaded";

            // Hide cycle selector in Object Mode
            _headerSelectorStack.IsVisible = (_activeTab == Tab.Tiles);
            _contentGrid.LocalPosition = new Vector2(0, _activeTab == Tab.Tiles ? 62 : 30);
            _contentGrid.Size = new Vector2(Area.Width, _activeTab == Tab.Tiles ? Area.Height - 102 : Area.Height - 70);

            if (Area.Contains(input.MouseWindowPosition))
            {
                _rootPanel.Update(input, bus);
            }
        }

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
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
            _rootPanel = new UIFlowPanel
            {
                LocalPosition = new Vector2(area.X + 10, area.Y + 10),
                Size = new Vector2(area.Width - 20, area.Height - 20),
                SpacingX = 6f, // Spacing between columns
                SpacingY = 6f, // Spacing between wrapped rows
                Padding = 0f,
                BackgroundColor = Color.Transparent,
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
        private readonly UIStackPanel _mainScroll;
        private readonly UILabel _linkingNotice;
        private readonly UITheme _theme;

        private MapObject _cachedSelectedObject;
        private bool _needsInspectorRebuild = false;

        // Accordion States
        private bool _expandInfo = true;
        private bool _expandLinks = true;
        private bool _expandTags = true;
        private bool _expandProps = true;

        // Property Temp State
        private string _newPropKey = "";
        private PropertyType _newPropType = PropertyType.String;

        // Tag Picker Temp State
        private bool _isTagPickerOpen = false;
        private string _tagSearchFilter = "";

        // Linking State
        private string _linkSourcePort = "Out";
        private string _linkTargetPort = "In";

        public InspectorPanel(Rectangle area, EditorUI editorUI, EditorState editorState) : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont, PanelBackground = Color.Black * 0.85f, BorderColor = Color.DarkGray };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height), BackgroundColor = _theme.PanelBackground };

            _mainScroll = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(10, 10),
                Size = new Vector2(area.Width - 20, area.Height - 20),
                PanelBackground = Color.Transparent,
                Spacing = 10f,
                ClipToBounds = true,
                AutoSize = false
            };

            _linkingNotice = new UILabel { LocalPosition = new Vector2(15, 10), Text = " LINKING MODE: Click target on map. (ESC to Cancel)", ColorOverride = Color.Cyan, IsVisible = false };

            _rootPanel.AddChild(_mainScroll);
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

            // Handle Linking Click Intercept
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
                    var source = _editorState.Selection.SelectedMapObject;

                    if (source != null && target != null && target != source)
                    {
                        bus.Publish(new LinkObjectsCommand(source, target, _linkSourcePort, _linkTargetPort));
                    }

                    _editorState.UI.IsLinkingMode = false;
                    _needsInspectorRebuild = true;
                }
                return;
            }

            if (Area.Contains(input.MouseWindowPosition))
            {
                ApplyGlobalScrolling(_rootPanel, input);
                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
            }
        }

        private void BuildInspector(MapObject obj, EventBus bus)
        {
            _mainScroll.ClearChildren();
            _linkingNotice.IsVisible = _editorState.UI.IsLinkingMode;
            if (_editorState.UI.IsLinkingMode || obj == null) return;

            obj.Tags ??= new HashSet<int>();
            obj.Properties ??= new Dictionary<string, MapProperty>();
            obj.Links ??= new List<ObjectLink>();

            float w = _mainScroll.Size.X - 10;

            // ==========================================
            // 1. BASE INFO ACCORDION
            // ==========================================
            var infoHeader = CreateAccordionHeader($"Base Info ({obj.Name})", _expandInfo, () => { _expandInfo = !_expandInfo; _needsInspectorRebuild = true; }, w);
            _mainScroll.AddChild(infoHeader);

            if (_expandInfo)
            {
                var infoPnl = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.3f, Spacing = 8f, Padding = 10f };
                infoPnl.AddChild(new UILabel { Text = $"ID: {obj.ID}", TextColor = Color.LightGray });
                infoPnl.AddChild(new UILabel { Text = $"Type: {obj.Type}", TextColor = Color.LightGray });
                infoPnl.AddChild(new UILabel { Text = $"Pos: {obj.Position.X:F0}, {obj.Position.Y:F0}", TextColor = Color.Gray });

                // --- NEW: Add extra Prefab Data ---
                if (obj is PropObject pObj)
                {
                    var pfab = _editorState.PrefabManager.GetPrefab(pObj.PrefabID);
                    if (pfab != null)
                    {
                        infoPnl.AddChild(new UILabel { Text = $"Canvas: {pfab.CanvasSize.X}x{pfab.CanvasSize.Y}", TextColor = Color.LightGray });
                        infoPnl.AddChild(new UILabel { Text = $"Pivot: {pfab.Pivot.X},{pfab.Pivot.Y}", TextColor = Color.LightGray });
                        infoPnl.AddChild(new UILabel { Text = $"Sprite Layers: {pfab.AlternateStates.Count}", TextColor = Color.Cyan });
                    }
                }

                var delBtn = new UIButton { Size = new Vector2(w - 20, 24), Text = "Delete Object", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => {
                    var layer = _editorState.ActiveMap.Layers.FirstOrDefault(l =>
                        (l is ObjectLayer ol && ol.Objects.Contains(obj)) ||
                        (l is ControlLayer cl && (cl.Rectangles.Contains(obj) || cl.Shapes.Contains(obj) || cl.Points.Contains(obj))));
                    if (layer is ObjectLayer targetLayer)
                    {
                        bus.Publish(new RemoveObjectCommand(targetLayer, obj));
                        _editorState.Selection.SelectedMapObject = null;
                    }
                };
                infoPnl.AddChild(delBtn);
                _mainScroll.AddChild(infoPnl);
            }

            // ==========================================
            // 2. LINKS ACCORDION
            // ==========================================
            var linkHeader = CreateAccordionHeader($"Links ({obj.Links.Count})", _expandLinks, () => { _expandLinks = !_expandLinks; _needsInspectorRebuild = true; }, w);
            _mainScroll.AddChild(linkHeader);

            if (_expandLinks)
            {
                var linkPnl = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.3f, Spacing = 8f, Padding = 10f };

                // Create Link Interface
                var createLinkRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 5f, PanelBackground = Color.Transparent };
                var srcPortBox = new UITextBox { Size = new Vector2(80, 24), Text = _linkSourcePort }; srcPortBox.OnTextChanged = (t) => _linkSourcePort = t;
                var tgtPortBox = new UITextBox { Size = new Vector2(80, 24), Text = _linkTargetPort }; tgtPortBox.OnTextChanged = (t) => _linkTargetPort = t;

                var pickBtn = new UIButton { Size = new Vector2(90, 24), Text = "+ Target", BackgroundColor = Color.DarkCyan };
                pickBtn.OnClick = () => { _editorState.UI.IsLinkingMode = true; _needsInspectorRebuild = true; };

                createLinkRow.AddChild(new UILabel { Text = "Port:" }); createLinkRow.AddChild(srcPortBox);
                createLinkRow.AddChild(new UILabel { Text = " -> " }); createLinkRow.AddChild(tgtPortBox);
                createLinkRow.AddChild(pickBtn);
                linkPnl.AddChild(createLinkRow);
                linkPnl.AddChild(new UIPanel { Size = new Vector2(w - 20, 1), BackgroundColor = Color.Gray });

                // List Links
                foreach (var link in obj.Links.ToList()) // ToList protects against modification during iteration
                {
                    var row = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };
                    var delBtn = new UIButton { Size = new Vector2(30, 20), Text = "X", BackgroundColor = Color.DarkRed };
                    delBtn.OnClick = () => { bus.Publish(new UnlinkObjectCommand(obj, link)); _needsInspectorRebuild = true; };

                    row.AddChild(delBtn);
                    row.AddChild(new UILabel { Text = $"[{link.SourcePort}] -> [{link.TargetID.Substring(0, 4)}] : {link.TargetPort}", TextColor = Color.LightGray });
                    linkPnl.AddChild(row);
                }
                if (obj.Links.Count == 0) linkPnl.AddChild(new UILabel { Text = "No links.", TextColor = Color.Gray });
                _mainScroll.AddChild(linkPnl);
            }

            // ==========================================
            // 3. TAGS ACCORDION (Cleaner, Flow-Based)
            // ==========================================
            var tagHeader = CreateAccordionHeader($"Tags ({obj.Tags.Count})", _expandTags, () => { _expandTags = !_expandTags; _needsInspectorRebuild = true; }, w);
            _mainScroll.AddChild(tagHeader);

            if (_expandTags)
            {
                var tagPnl = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.3f, Spacing = 8f, Padding = 10f };

                // A. Active Tags Flow (Wrapped horizontally)
                var activeFlow = new UIFlowPanel { Size = new Vector2(w - 20, 10), BackgroundColor = Color.Transparent, SpacingX = 5f, SpacingY = 5f, Padding = 0f };
                foreach (int iTag in obj.Tags)
                {
                    int localTag = iTag;
                    var def = _editorState.TagManager.GetTag(iTag);
                    string text = def != null ? def.Name : iTag.ToString();
                    Vector2 sz = UITheme.DefaultFont.MeasureString(text);

                    // Draw Active Tags fully opaque
                    var btn = new UIButton { Size = new Vector2(sz.X + 16, 24), Text = text, BackgroundColor = def?.TagColor ?? Color.Gray };
                    btn.OnClick = () => { obj.Tags.Remove(localTag); _needsInspectorRebuild = true; };
                    activeFlow.AddChild(btn);
                }
                if (obj.Tags.Count == 0) activeFlow.AddChild(new UILabel { Text = "No tags applied.", TextColor = Color.Gray });

                tagPnl.AddChild(activeFlow);
                tagPnl.AddChild(new UIPanel { Size = new Vector2(w - 20, 1), BackgroundColor = Color.Gray }); // Separator

                // B. Add Tag Toggle
                var openPickerBtn = new UIButton { Size = new Vector2(w - 20, 24), Text = _isTagPickerOpen ? "[-] Hide Library" : "[+] Browse Tag Library", BackgroundColor = Color.Black * 0.5f, TextColor = Color.LightGray };
                openPickerBtn.OnClick = () => { _isTagPickerOpen = !_isTagPickerOpen; _needsInspectorRebuild = true; };
                tagPnl.AddChild(openPickerBtn);

                // C. Tag Library Picker
                if (_isTagPickerOpen)
                {
                    // Search Box
                    var searchBox = new UITextBox { Size = new Vector2(w - 20, 28), Text = _tagSearchFilter, Placeholder = "Search library..." };
                    searchBox.OnTextChanged = (t) => { _tagSearchFilter = t.ToLower(); _needsInspectorRebuild = true; };
                    tagPnl.AddChild(searchBox);

                    // Group and Filter Tags
                    var availableTags = _editorState.TagManager.Tags.Values.Where(t => !obj.Tags.Contains(t.ID)).ToList();
                    if (!string.IsNullOrEmpty(_tagSearchFilter))
                        availableTags = availableTags.Where(t => t.Name.ToLower().Contains(_tagSearchFilter) || t.Category.ToLower().Contains(_tagSearchFilter)).ToList();

                    var groupedTags = availableTags.GroupBy(t => t.Category);

                    foreach (var group in groupedTags)
                    {
                        // Clean, subtle header for categories
                        var catHeader = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 5f, PanelBackground = Color.Transparent };
                        catHeader.AddChild(new UILabel { Text = group.Key, TextColor = Color.Cyan });
                        tagPnl.AddChild(catHeader);

                        var groupFlow = new UIFlowPanel { Size = new Vector2(w - 20, 10), BackgroundColor = Color.Transparent, SpacingX = 5f, SpacingY = 5f, Padding = 0f };

                        foreach (var tag in group)
                        {
                            int safeId = tag.ID;
                            Vector2 sz = UITheme.DefaultFont.MeasureString(tag.Name);

                            // Draw Available Tags slightly dimmed to differentiate from active tags
                            var tBtn = new UIButton { Size = new Vector2(sz.X + 16, 24), Text = "+ " + tag.Name, BackgroundColor = tag.TagColor * 0.25f, BorderColor = tag.TagColor * 0.5f, TextColor = Color.LightGray };
                            tBtn.OnClick = () => { obj.Tags.Add(safeId); _needsInspectorRebuild = true; };
                            groupFlow.AddChild(tBtn);
                        }
                        tagPnl.AddChild(groupFlow);
                    }
                }
                _mainScroll.AddChild(tagPnl);
            }

            // ==========================================
            // 4. PROPERTIES ACCORDION
            // ==========================================
            var propHeader = CreateAccordionHeader($"Properties ({obj.Properties.Count})", _expandProps, () => { _expandProps = !_expandProps; _needsInspectorRebuild = true; }, w);
            _mainScroll.AddChild(propHeader);

            if (_expandProps)
            {
                var propPnl = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.3f, Spacing = 8f, Padding = 10f };

                foreach (var kvp in obj.Properties)
                {
                    string safeKey = kvp.Key;
                    var pRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 8f };

                    string typeIndicator = kvp.Value.Type.ToString().Substring(0, 3);
                    pRow.AddChild(new UILabel { Text = $"[{typeIndicator}] {safeKey}:", TextColor = Color.LightGray });

                    if (kvp.Value.Type == PropertyType.Boolean)
                    {
                        var tBtn = new UIButton { Size = new Vector2(60, 24), Text = kvp.Value.Value, BackgroundColor = kvp.Value.Value == "True" ? Color.DarkGreen : Color.DarkRed };
                        tBtn.OnClick = () => { kvp.Value.Value = kvp.Value.Value == "True" ? "False" : "True"; _needsInspectorRebuild = true; };
                        pRow.AddChild(tBtn);
                    }
                    else
                    {
                        var valInp = new UITextBox { Size = new Vector2(100, 24), Text = kvp.Value.Value };
                        valInp.OnTextChanged = (t) => kvp.Value.Value = t;
                        valInp.OnSubmit = (t) => {
                            kvp.Value.Value = t;
                            _needsInspectorRebuild = true;
                        };
                        pRow.AddChild(valInp);
                    }

                    var delBtn = new UIButton { Size = new Vector2(30, 24), Text = "X", BackgroundColor = Color.DarkRed };
                    delBtn.OnClick = () => { obj.Properties.Remove(safeKey); _needsInspectorRebuild = true; };
                    pRow.AddChild(delBtn);
                    propPnl.AddChild(pRow);
                }

                var addRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 8f };
                var newKeyInput = new UITextBox { Size = new Vector2(120, 24), Placeholder = "Key...", Text = _newPropKey };
                newKeyInput.OnTextChanged = (t) => _newPropKey = t;

                var typeBtn = new UIButton { Size = new Vector2(50, 24), Text = _newPropType.ToString().Substring(0, 3), BackgroundColor = Color.DarkGoldenrod };
                typeBtn.OnClick = () => { _newPropType = (PropertyType)(((int)_newPropType + 1) % 4); _needsInspectorRebuild = true; };

                var addPropBtn = new UIButton { Size = new Vector2(60, 24), Text = "Add", BackgroundColor = Color.DarkGreen };
                addPropBtn.OnClick = () => {
                    if (!string.IsNullOrWhiteSpace(_newPropKey) && !obj.Properties.ContainsKey(_newPropKey))
                    {
                        string defaultVal = _newPropType == PropertyType.Boolean ? "False" : (_newPropType == PropertyType.String ? "" : "0");
                        obj.Properties.Add(_newPropKey.Trim(), new MapProperty(_newPropType, defaultVal));
                        _newPropKey = ""; _needsInspectorRebuild = true;
                    }
                };

                addRow.AddChild(newKeyInput); addRow.AddChild(typeBtn); addRow.AddChild(addPropBtn);
                propPnl.AddChild(addRow);
                _mainScroll.AddChild(propPnl);
            }
        }

        private UIButton CreateAccordionHeader(string title, bool isExpanded, Action onClick, float width)
        {
            var btn = new UIButton { Size = new Vector2(width, 30), Text = (isExpanded ? "[-] " : "[+] ") + title, BackgroundColor = Color.Black * 0.6f, TextColor = Color.Cyan };
            btn.OnClick = onClick;
            return btn;
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

        // Ensure FindTopObjectUnderMouse is implemented here based on your previous class logic!
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
            channelHeader.AddChild(new UILabel { Text = "Active Paint Channel:", TextColor = Color.Yellow });
            channelHeader.AddChild(CreateChannelBtn("Biome (Red)", PaintChannel.R_Biome, Color.Red));
            channelHeader.AddChild(CreateChannelBtn("Spawn (Green)", PaintChannel.G_SpawnType, Color.LimeGreen));
            channelHeader.AddChild(CreateChannelBtn("Elevation (Blue)", PaintChannel.B_Elevation, Color.DeepSkyBlue));
            _col2Stack.AddChild(channelHeader);

            // 2. Setup Mode Rows (Only applicable to Elevation / Blue Channel)
            if (activeChannel == PaintChannel.B_Elevation)
            {
                var modeRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };
                modeRow.AddChild(new UILabel { Text = "Mode:", TextColor = Color.Cyan });
                modeRow.AddChild(CreateModeBtn("Solid (Grid)", BrushMode.Solid));
                modeRow.AddChild(CreateModeBtn("Detail (+)", BrushMode.DetailAdd));
                modeRow.AddChild(CreateModeBtn("Detail (-)", BrushMode.DetailSub));
                _col2Stack.AddChild(modeRow);

                // Behavior toggle for detailing modes
                var behaviorRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };
                behaviorRow.AddChild(new UILabel { Text = "Detailer Blend Mode:", TextColor = Color.Yellow });

                var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                if (tool != null)
                {
                    var optOverwrite = new UIButton { Size = new Vector2(100, 24), Text = "Overwrite", BackgroundColor = tool.ActiveBehavior == DetailerBehavior.Overwrite ? Color.DarkCyan : Color.DarkSlateGray };
                    optOverwrite.OnClick = () => { tool.ActiveBehavior = DetailerBehavior.Overwrite; RebuildColumn2(activeChannel); };

                    var optAdditive = new UIButton { Size = new Vector2(100, 24), Text = "Additive", BackgroundColor = tool.ActiveBehavior == DetailerBehavior.Additive ? Color.DarkCyan : Color.DarkSlateGray };
                    optAdditive.OnClick = () => { tool.ActiveBehavior = DetailerBehavior.Additive; RebuildColumn2(activeChannel); };

                    behaviorRow.AddChild(optOverwrite);
                    behaviorRow.AddChild(optAdditive);
                }
                _col2Stack.AddChild(behaviorRow);
            }

            _col2Stack.AddChild(new UIPanel { Size = new Vector2(550, 2), BackgroundColor = Color.DarkGray, BorderColor = Color.Transparent });

            // 3. Dynamic Dictionary Selection List
            var splitLayout = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 20f };
            var dataList = activeChannel switch { PaintChannel.R_Biome => _editorState.MaskData.Biomes, PaintChannel.G_SpawnType => _editorState.MaskData.Spawns, _ => _editorState.MaskData.Elevations };

            // --- CONDITION: Completely strip Add/Edit input panels when Elevation (Blue) is active ---
            if (activeChannel != PaintChannel.B_Elevation)
            {
                // LEFT: ADD NEW DATA FORM (Only visible for Biomes / Spawns)
                var leftForm = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Size = new Vector2(220, 100), AutoSize = false, Spacing = 5f };
                string title = activeChannel == PaintChannel.R_Biome ? "Biomes (R):" : "Spawns (G):";
                leftForm.AddChild(new UILabel { Text = title, TextColor = Color.Cyan });
                leftForm.AddChild(new UILabel { Text = "Quick-Add Entry:", TextColor = Color.Gray });

                var nameIn = new UITextBox { Size = new Vector2(220, 24), Placeholder = "Name", Text = _newDataName, DebugName = "DataNameIn" };
                nameIn.OnTextChanged = (t) => _newDataName = t;
                leftForm.AddChild(nameIn);

                var valRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 10f };
                var valIn = new UITextBox { Size = new Vector2(140, 24), Placeholder = "Val (0-255)", Text = _newDataValue.ToString(), DebugName = "DataValIn" };
                valIn.OnTextChanged = (t) => { if (int.TryParse(t, out int v)) _newDataValue = v; };
                valRow.AddChild(valIn);

                var addBtn = new UIButton { Size = new Vector2(70, 24), Text = "+ Add", BackgroundColor = Color.DarkGreen };
                addBtn.OnClick = () => {
                    if (!string.IsNullOrWhiteSpace(_newDataName))
                    {
                        dataList.Add(new MaskDataDef { Name = _newDataName, Value = MathHelper.Clamp(_newDataValue, 0, 255), Category = "None" });
                        _editorState.MaskData.Save(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "mask_data.json"));
                        _newDataName = ""; _newDataValue = 0;
                        _editorUI.SetFocus(null);
                        RebuildColumn2(activeChannel);
                    }
                };
                valRow.AddChild(addBtn);
                leftForm.AddChild(valRow);
                splitLayout.AddChild(leftForm);
            }
            else
            {
                // Simple descriptive spacer box replacing the left input form for Elevations
                var leftForm = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Size = new Vector2(220, 100), AutoSize = false, Spacing = 5f };
                leftForm.AddChild(new UILabel { Text = "Elevations (Blue):", TextColor = Color.Cyan });
                leftForm.AddChild(new UILabel { Text = "Systemic Height Levels", TextColor = Color.Gray });
                leftForm.AddChild(new UILabel { Text = "Value steps are defined in\nmask_data.json.", TextColor = Color.DarkGray });
                splitLayout.AddChild(leftForm);
            }

            // RIGHT: SELECTION LIST (Takes up the rest of the workspace)
            var rightList = new UIStackPanel { Direction = StackDirection.Vertical, Size = new Vector2(310, 85), PanelBackground = Color.Black * 0.3f, BorderColor = Color.Transparent, ClipToBounds = true, Spacing = 4f, Padding = 5f, AutoSize = false };
            var groupedData = dataList.GroupBy(d => d.Category ?? "None").OrderBy(g => g.Key);

            foreach (var group in groupedData)
            {
                rightList.AddChild(new UILabel { Text = group.Key.ToUpper(), TextColor = Color.Yellow });

                foreach (var item in group.OrderBy(i => i.Value))
                {
                    var safeDef = item;
                    var listRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, BorderColor = Color.Transparent, Spacing = 5f };

                    // Paint selector button
                    var btn = new UIButton { Size = new Vector2(260, 24), Text = $"[{safeDef.Value}] {safeDef.Name}", BackgroundColor = _newDataValue == safeDef.Value ? Color.DarkCyan : Color.DarkSlateGray };
                    btn.OnClick = () => {
                        _newDataValue = safeDef.Value;
                        var tool = _editorState.ToolState.Tools.FirstOrDefault(t => t.Name == "TerrainBrush") as TerrainBrushTool;
                        if (tool != null)
                        {
                            tool.TargetValue = safeDef.Value;
                            tool.BrushNeedsUpdate = true;
                        }
                        RebuildColumn2(activeChannel);
                    };
                    listRow.AddChild(btn);

                    // Only show delete button for non-elevation indices
                    if (activeChannel != PaintChannel.B_Elevation)
                    {
                        var delBtn = new UIButton { Size = new Vector2(30, 24), Text = "X", BackgroundColor = Color.DarkRed };
                        delBtn.OnClick = () => {
                            dataList.Remove(safeDef);
                            _editorState.MaskData.Save(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "mask_data.json"));
                            RebuildColumn2(activeChannel);
                        };
                        listRow.AddChild(delBtn);
                    }

                    rightList.AddChild(listRow);
                }
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
                _radiusLabel.Text = $"Radius: {tool.BrushGridSize:F0} px";
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

    public class WaterEditorPanel : BasePanel
    {
        private readonly UIPanel _rootPanel;
        private readonly UIStackPanel _horizontalContainer;
        private readonly UITheme _theme;

        // Numerical Indicators
        private readonly UILabel _lblSimStatus;
        private readonly UILabel _lblWaterLevel;
        private readonly UILabel _lblInflow;
        private readonly UILabel _lblSeepage;
        private readonly UILabel _lblBaseHeight;
        private readonly UILabel _lblStartOffset;
        private readonly UILabel _lblEndOffset;
        private readonly UILabel _lblRiverWidth;

        public WaterEditorPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.Black * 0.85f, BorderColor = Color.DarkGray };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height), BackgroundColor = _theme.PanelBackground, BorderColor = Color.Transparent };

            // 1. Root Horizontal Stack (Divides the widescreen workspace)
            _horizontalContainer = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = new Vector2(15, 10),
                Size = new Vector2(area.Width - 30, area.Height - 20),
                PanelBackground = Color.Transparent,
                BorderColor = Color.Transparent,
                Spacing = 20f,
                AutoSize = false
            };

            float colWidth = (area.Width - 120f) / 4f; // Dynamically allocate column widths
            float colHeight = area.Height - 30f;

            // ==========================================
            // COLUMN 1: SYSTEM TRIGGERS & SIMULATOR
            // ==========================================
            var col1 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.2f, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(colWidth, colHeight), Padding = 8f, Spacing = 6f };
            col1.AddChild(new UILabel { Text = "Core Operations", TextColor = Color.Cyan });

            _lblSimStatus = new UILabel { Text = "Sim: STOPPED", ColorOverride = Color.Red };
            col1.AddChild(_lblSimStatus);

            var btnSimToggle = new UIButton { Size = new Vector2(colWidth - 16, 24), Text = "Run Live Test" };
            btnSimToggle.OnClick = () => {
                bool nextState = !_editorState.WaterSystem.IsSimulating;
                //_editorState.WaterSystem.ToggleSimulation(nextState, _editorState._graphics, _editorState._layoutmanager.EditorUpscalerReference, _editorState.ActiveMap);

                _lblSimStatus.Text = nextState ? "Sim: RUNNING" : "Sim: STOPPED";
                _lblSimStatus.ColorOverride = nextState ? Color.LimeGreen : Color.Red;
                btnSimToggle.Text = nextState ? "Pause Physics" : "Run Live Test";
            };
            col1.AddChild(btnSimToggle);

            var btnBakeMask = new UIButton { Size = new Vector2(colWidth - 16, 24), Text = "Bake Mask Blue", BackgroundColor = Color.DarkGreen };
            btnBakeMask.OnClick = () => {
                var maskLayer = _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer;
                if (maskLayer != null)
                {
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, maskLayer);
                }
            };
            col1.AddChild(btnBakeMask);

            var btnSaveBaked = new UIButton { Size = new Vector2(colWidth - 16, 24), Text = "Save Water Map", BackgroundColor = Color.DarkMagenta };
            btnSaveBaked.OnClick = () => {
                string assetsPath = PathHelper.GetAssetsPath();
                string mapName = string.IsNullOrWhiteSpace(_editorState.CurrentMapFile) ? "level1" : _editorState.CurrentMapFile;
                mapName = mapName.Replace(".json", "").Replace(".map", "");
                string savePath = Path.Combine(assetsPath, "Maps", $"{mapName}_water.png");

                var maskLayer = _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer;
                if (maskLayer != null)
                {
                    _editorState.WaterSystem.SaveBakedWaterMap(savePath, maskLayer, _editorState._layoutmanager.EditorUpscalerReference);
                }
            };
            col1.AddChild(btnSaveBaked);
            _horizontalContainer.AddChild(col1);

            _horizontalContainer.AddChild(new UIPanel { Size = new Vector2(2, colHeight), BackgroundColor = Color.Gray * 0.3f, BorderColor = Color.Transparent });

            // ==========================================
            // COLUMN 2: GEOLOGY MODELLING OPTIONS
            // ==========================================
            var col2 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.2f, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(colWidth, colHeight), Padding = 8f, Spacing = 6f };
            col2.AddChild(new UILabel { Text = "Geology Options", TextColor = Color.Yellow });

            var btnProj = new UIButton { Size = new Vector2(colWidth - 16, 24), Text = "Slope: SPLINE", BackgroundColor = Color.DarkSlateGray };
            btnProj.OnClick = () => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    string cur = poly.GetProperty("ProjectionMode", "spline");
                    string next = cur == "spline" ? "linear" : (cur == "linear" ? "concentric" : "spline");
                    poly.Properties["ProjectionMode"] = new MapProperty(PropertyType.String, next);
                    btnProj.Text = $"Slope: {next.ToUpper()}";
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            col2.AddChild(btnProj);

            var btnEase = new UIButton { Size = new Vector2(colWidth - 16, 24), Text = "Curve: LINEAR", BackgroundColor = Color.DarkSlateGray };
            btnEase.OnClick = () => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    string cur = poly.GetProperty("EasingMode", "linear");
                    string[] curves = { "linear", "easeIn", "easeOut", "easeInOut", "zoned" };
                    int idx = (Array.IndexOf(curves, cur) + 1) % curves.Length;
                    poly.Properties["EasingMode"] = new MapProperty(PropertyType.String, curves[idx]);
                    btnEase.Text = $"Curve: {curves[idx].ToUpper()}";
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            col2.AddChild(btnEase);

            // SNAP CONTROL (Issue B2)
            var btnSnapToggle = new UIButton { Size = new Vector2(colWidth - 16, 24), Text = "Snapping: ENABLED", BackgroundColor = Color.DarkGoldenrod };
            btnSnapToggle.OnClick = () => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    bool curSnap = poly.GetProperty("Snap", "True") == "True";
                    string nextVal = (!curSnap).ToString();
                    poly.Properties["Snap"] = new MapProperty(PropertyType.Boolean, nextVal);
                    btnSnapToggle.Text = !curSnap ? "Snapping: ENABLED" : "Snapping: DISABLED";

                    // Re-bake heightmap automatically to show smooth slope transition on canvas
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            col2.AddChild(btnSnapToggle);

            _horizontalContainer.AddChild(col2);

            _horizontalContainer.AddChild(new UIPanel { Size = new Vector2(2, colHeight), BackgroundColor = Color.Gray * 0.3f, BorderColor = Color.Transparent });

            // ==========================================
            // COLUMN 3: ELEVATION STAGING SLIDERS
            // ==========================================
            var col3 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.2f, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(colWidth, colHeight), Padding = 8f, Spacing = 6f };
            col3.AddChild(new UILabel { Text = "Height Variables", TextColor = Color.Coral });

            // Base height
            var hBaseRow = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
            _lblBaseHeight = new UILabel { Text = "Base Height: 160", Size = new Vector2(colWidth - 16, 14) };
            var sldBaseHeight = new UISlider { Size = new Vector2(colWidth - 16, 14), Min = 0f, Max = 255f, Value = 160f };
            sldBaseHeight.OnValueChanged = (val) => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    poly.Properties["BaseHeight"] = new MapProperty(PropertyType.Float, Math.Round(val).ToString());
                    _lblBaseHeight.Text = $"Base Height: {Math.Round(val)}";
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            hBaseRow.AddChild(_lblBaseHeight); hBaseRow.AddChild(sldBaseHeight); col3.AddChild(hBaseRow);

            // Start Height Offset
            var hStartRow = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
            _lblStartOffset = new UILabel { Text = "Start Offset: 0", Size = new Vector2(colWidth - 16, 14) };
            var sldStart = new UISlider { Size = new Vector2(colWidth - 16, 14), Min = -128f, Max = 128f, Value = 0f };
            sldStart.OnValueChanged = (val) => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    poly.Properties["StartHeightOffset"] = new MapProperty(PropertyType.Float, Math.Round(val).ToString());
                    _lblStartOffset.Text = $"Start Offset: {Math.Round(val)}";
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            hStartRow.AddChild(_lblStartOffset); hStartRow.AddChild(sldStart); col3.AddChild(hStartRow);

            // End Height Offset
            var hEndRow = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
            _lblEndOffset = new UILabel { Text = "End Offset: -60", Size = new Vector2(colWidth - 16, 14) };
            var sldEnd = new UISlider { Size = new Vector2(colWidth - 16, 14), Min = -128f, Max = 128f, Value = -60f };
            sldEnd.OnValueChanged = (val) => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    poly.Properties["EndHeightOffset"] = new MapProperty(PropertyType.Float, Math.Round(val).ToString());
                    _lblEndOffset.Text = $"End Offset: {Math.Round(val)}";
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            hEndRow.AddChild(_lblEndOffset); hEndRow.AddChild(sldEnd); col3.AddChild(hEndRow);

            _horizontalContainer.AddChild(col3);

            _horizontalContainer.AddChild(new UIPanel { Size = new Vector2(2, colHeight), BackgroundColor = Color.Gray * 0.3f, BorderColor = Color.Transparent });

            // ==========================================
            // COLUMN 4: RIVER SHAPES & FLUID OPTICS
            // ==========================================
            var col4 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Black * 0.2f, BorderColor = Color.Transparent, AutoSize = false, Size = new Vector2(colWidth, colHeight), Padding = 8f, Spacing = 6f };
            col4.AddChild(new UILabel { Text = "Flow Parameters", TextColor = Color.LimeGreen });

            // Channel Width
            var sldWidthRow = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
            _lblRiverWidth = new UILabel { Text = "Channel Width: 8", Size = new Vector2(colWidth - 16, 14) };
            var sldWidth = new UISlider { Size = new Vector2(colWidth - 16, 14), Min = 2f, Max = 32f, Value = 8f };
            sldWidth.OnValueChanged = (val) => {
                var sel = _editorState.Selection.SelectedMapObject;
                if (sel is ShapeObject poly)
                {
                    poly.Properties["RiverWidth"] = new MapProperty(PropertyType.Float, Math.Round(val).ToString());
                    _lblRiverWidth.Text = $"Channel Width: {Math.Round(val)}";
                    MapExporter.BakeWaterBedIntoMask(_editorState._graphics, _editorState.ActiveMap, _editorState.ActiveMap.Layers.FirstOrDefault(l => l.Type == LayerType.Mask) as MaskLayer);
                }
            };
            sldWidthRow.AddChild(_lblRiverWidth); sldWidthRow.AddChild(sldWidth); col4.AddChild(sldWidthRow);

            // Global Water Level
            var sldWaterLevelRow = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
            _lblWaterLevel = new UILabel { Text = "Global Level: 130", Size = new Vector2(colWidth - 16, 14) };
            var sldWaterLevel = new UISlider { Size = new Vector2(colWidth - 16, 14), Min = 0f, Max = 255f, Value = 130f };
            sldWaterLevel.OnValueChanged = (val) => {
                _editorState.WaterSystem.WaterLevel = val;
                _lblWaterLevel.Text = $"Global Level: {Math.Round(val)}";
            };
            sldWaterLevelRow.AddChild(_lblWaterLevel); sldWaterLevelRow.AddChild(sldWaterLevel); col4.AddChild(sldWaterLevelRow);

            // Seepage / Decay
            var sldSeepageRow = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
            _lblSeepage = new UILabel { Text = "Seepage Rate: 0.0", Size = new Vector2(colWidth - 16, 14) };
            var sldSeepage = new UISlider { Size = new Vector2(colWidth - 16, 14), Min = 0f, Max = 10f, Value = 0f };
            sldSeepage.OnValueChanged = (val) => {
                _editorState.WaterSystem.SeepageDecayRate = val;
                _lblSeepage.Text = $"Seepage Rate: {val:F1}";
            };
            sldSeepageRow.AddChild(_lblSeepage); sldSeepageRow.AddChild(sldSeepage); col4.AddChild(sldSeepageRow);

            _horizontalContainer.AddChild(col4);

            _rootPanel.AddChild(_horizontalContainer);
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            if (Area.Contains(input.MouseWindowPosition))
            {
                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
            }
        }

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, (IUIContext)_editorUI, _theme);
        public override string GetDebugInfo() => "";
    }
    public class AutoTilePanel : BasePanel
    {
        private readonly UIPanel _root;
        private readonly UITheme _theme;
        public bool IsOpen = false;

        private AutoTileRule _activeRule;
        private UIStackPanel _mainScroll;
        private UITextBox _ruleNameBox;
        private UITextBox _atlasNameBox;

        private Dictionary<int, UIButton> _maskButtons = new Dictionary<int, UIButton>();

        public AutoTilePanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.Black * 0.95f };
            _root = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height), BackgroundColor = _theme.PanelBackground, ClipToBounds = true };

            _mainScroll = new UIStackPanel { Direction = StackDirection.Vertical, Padding = 15f, Spacing = 10f, ClipToBounds = true, Size = new Vector2(area.Width, area.Height) };
            _mainScroll.AddChild(new UILabel { Text = "Auto-Tile Configurator (15-Piece Corner Format)", TextColor = Color.Cyan });

            // Setup Header
            var headerRow = new UIStackPanel { Direction = StackDirection.Horizontal, Spacing = 10f, PanelBackground = Color.Transparent };
            _ruleNameBox = new UITextBox { Size = new Vector2(150, 30), Placeholder = "Rule Name" };
            _atlasNameBox = new UITextBox { Size = new Vector2(150, 30), Placeholder = "Target Atlas" };

            var saveBtn = new UIButton { Size = new Vector2(80, 30), Text = "Save", BackgroundColor = Color.DarkGreen };
            saveBtn.OnClick = () => {
                if (_activeRule != null)
                {
                    _activeRule.RuleName = _ruleNameBox.Text;
                    _activeRule.TargetAtlas = _atlasNameBox.Text;
                    _editorState.AutoTileManager.Rules[_activeRule.RuleName] = _activeRule;
                    _editorState.AutoTileManager.Save(System.IO.Path.Combine(PathHelper.GetAssetsPath(), "Data", "autotiles.json"));
                }
            };

            var closeBtn = new UIButton { Size = new Vector2(80, 30), Text = "Close", BackgroundColor = Color.DarkRed };
            closeBtn.OnClick = () => IsOpen = false;

            headerRow.AddChild(_ruleNameBox); headerRow.AddChild(_atlasNameBox); headerRow.AddChild(saveBtn); headerRow.AddChild(closeBtn);
            _mainScroll.AddChild(headerRow);
            _mainScroll.AddChild(new UIPanel { Size = new Vector2(area.Width - 30, 2), BackgroundColor = Color.Gray });

            _root.AddChild(_mainScroll);
        }

        private void RebuildGrid()
        {
            while (_mainScroll.Children.Count > 3) _mainScroll.RemoveChild(_mainScroll.Children.Last());
            _maskButtons.Clear();

            if (_activeRule == null) return;

            // TL=1, TR=2, BL=4, BR=8
            var groups = new Dictionary<string, Dictionary<int, string>>
            {
                { "Full & Diagonals", new Dictionary<int, string> { { 15, "Full (15)" }, { 6, "TR+BL (6)" }, { 9, "TL+BR (9)" } } },
                { "2 Corners (Sides)", new Dictionary<int, string> { { 3, "Top (3)" }, { 12, "Bottom (12)" }, { 5, "Left (5)" }, { 10, "Right (10)" } } },
                { "3 Corners (Inner)", new Dictionary<int, string> { { 14, "No TL (14)" }, { 13, "No TR (13)" }, { 11, "No BL (11)" }, { 7, "No BR (7)" } } },
                { "1 Corner (Outer)", new Dictionary<int, string> { { 1, "TL (1)" }, { 2, "TR (2)" }, { 4, "BL (4)" }, { 8, "BR (8)" } } }
            };

            foreach (var group in groups)
            {
                _mainScroll.AddChild(new UILabel { Text = group.Key, TextColor = Color.Yellow });
                var flow = new UIFlowPanel { Size = new Vector2(Area.Width - 40, 100), SpacingX = 15f, SpacingY = 10f, BackgroundColor = Color.Transparent };

                foreach (var maskKvp in group.Value)
                {
                    int mask = maskKvp.Key;
                    var slotStack = new UIStackPanel { Direction = StackDirection.Vertical, Spacing = 2f, PanelBackground = Color.Transparent };
                    slotStack.AddChild(new UILabel { Text = maskKvp.Value, TextColor = Color.LightGray });

                    var btn = new UIButton { Size = new Vector2(48, 48), BackgroundColor = Color.Black * 0.5f, BorderColor = Color.White * 0.3f };
                    btn.OnClick = () => {
                        if (_editorState.Selection.ActiveTileBrush != null)
                        {
                            _activeRule.TargetAtlas = _editorState.Selection.ActiveTileBrush.TilesetName;
                            _atlasNameBox.Text = _activeRule.TargetAtlas;
                            _activeRule.BitmaskMap[mask] = _editorState.Selection.ActiveTileBrush.TileID;
                            RebuildGrid();
                        }
                    };

                    _maskButtons[mask] = btn;
                    slotStack.AddChild(btn);
                    flow.AddChild(slotStack);
                }
                _mainScroll.AddChild(flow);
                _mainScroll.AddChild(new UIPanel { Size = new Vector2(Area.Width - 30, 1), BackgroundColor = Color.DarkGray * 0.5f });
            }
        }

        public void Open(AutoTileRule rule = null)
        {
            IsOpen = true;
            _activeRule = rule ?? new AutoTileRule();
            _ruleNameBox.Text = _activeRule.RuleName;
            _atlasNameBox.Text = _activeRule.TargetAtlas;
            RebuildGrid();
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            if (!IsOpen) return;
            if (_mainScroll.AbsoluteBounds.Contains(input.MouseWindowPosition))
            {
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) _mainScroll.ScrollOffset = new Vector2(0, System.Math.Max(0, _mainScroll.ScrollOffset.Y - scrollDelta * 0.5f));
            }
            _editorUI.CheckFocusClick(_root, input);
            _root.Update(input, bus);
        }

        public override void Draw(SpriteBatch sb)
        {
            if (!IsOpen) return;
            _root.Draw(sb, _editorUI, _theme);

            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true }, samplerState: SamplerState.PointClamp);

            var clipRect = Rectangle.Intersect(_mainScroll.AbsoluteBounds, sb.GraphicsDevice.Viewport.Bounds);
            if (clipRect.Width > 0 && clipRect.Height > 0) sb.GraphicsDevice.ScissorRectangle = clipRect;

            foreach (var kvp in _maskButtons)
            {
                if (_activeRule != null && _activeRule.BitmaskMap.TryGetValue(kvp.Key, out int tileId))
                {
                    var tex = _editorState.TilesetManager.GetTileTexture(new TileInfo(_activeRule.TargetAtlas, tileId));
                    if (tex != null)
                    {
                        Rectangle bounds = kvp.Value.AbsoluteBounds;
                        if (bounds.Bottom > _mainScroll.AbsoluteBounds.Top && bounds.Top < _mainScroll.AbsoluteBounds.Bottom)
                        {
                            float scale = 2.0f;
                            Vector2 pos = new Vector2(bounds.Center.X - (tex.Width * scale) / 2f, bounds.Center.Y - (tex.Height * scale) / 2f);
                            sb.Draw(tex, pos, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                        }
                    }
                }
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = prevScissor;
            sb.Begin();
        }

        public override string GetDebugInfo() => "";

    }
}
