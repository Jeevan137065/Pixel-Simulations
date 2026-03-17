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
        public TopPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            var state = editorState.TopState;
            _theme = new UITheme { Font = editorUI.DebugFont };

            // 1. Create the container (Horizontal Stack)
            _rootStack = new UIStackPanel
            {
                Direction = StackDirection.Horizontal,
                LocalPosition = area.Location.ToVector2(),
                Size = new Vector2(area.Width, area.Height),
                AutoSize = false, // We want it to fill the layout bounds, not shrink
                Padding = 4f,
                Spacing = 5f,
                
                PanelBackground = Color.Gray * 0.3f // Match old look
            };

            // 2. Add Buttons automatically
            string[] actions = { "New", "Save", "Load", "Undo", "Redo", "Capture", "Export" };
            string[] icons = { "New", "SaveCloud", "LoadCloud", "Undo", "Redo", "Capture", "Export" };

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
            _theme = new UITheme { Font = editorUI.DebugFont, PanelBackground = Color.DarkSlateGray };

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
            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "NewObject", Command = new OpenPrefabCreatorCommand { AtlasName = "Basic" } });
            _footerStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Tags",Text = "Tags",Command = new ToggleTagManagerCommand()});
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
        private readonly UIStackPanel _controlStack;
        private readonly UIStackPanel _tagPickerStack;
        private readonly UITextBox _nameInput, _tagsInput;
        private readonly UITheme _theme;

        // Custom drawn areas (now using LOCAL coordinates strictly)
        private Rectangle _localAtlasArea;
        private Rectangle _localPreviewArea;

        // Cache for tag list
        private int _cachedTagCount = -1;
        private string _lastTagSearch = "";
        public PrefabCreatorPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.DarkSlateGray };

            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            // Local coordinates (relative to 0,0)
            _localAtlasArea = new Rectangle(10, 10, (int)(area.Width * 0.60f) - 20, area.Height - 20);

            int ctrlX = _localAtlasArea.Right + 20;
            int ctrlWidth = area.Width - _localAtlasArea.Width - 40;

            _localPreviewArea = new Rectangle(ctrlX, 10, ctrlWidth, 250);

            _controlStack = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                LocalPosition = new Vector2(ctrlX, _localPreviewArea.Bottom + 10), // Relative to 0,0
                Size = new Vector2(ctrlWidth, area.Height - 280),
                PanelBackground = Color.Black * 0.3f,
                Padding = 15f,
                Spacing = 5f
            };

            // Form Fields
            _controlStack.AddChild(new UILabel { Text = "Object ID (Unique Name):", TextColor = Color.Yellow });
            _nameInput = new UITextBox { Size = new Vector2(ctrlWidth - 30, 30), Placeholder = "e.g. Tree_Oak_1" };
            _controlStack.AddChild(_nameInput);

            _controlStack.AddChild(new UILabel { Text = "Tags (comma separated):", TextColor = Color.Yellow });
            _tagsInput = new UITextBox { Size = new Vector2(ctrlWidth - 30, 30), Placeholder = "e.g. #wood, #solid" };
            _controlStack.AddChild(_tagsInput);

            // Clickable Tag Picker
            _controlStack.AddChild(new UILabel { Text = "Click to Quick-Add Tags:", TextColor = Color.Gray });
            _tagPickerStack = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                Size = new Vector2(ctrlWidth - 30, 100),
                ClipToBounds = true,
                PanelBackground = Color.Black * 0.5f
            };
            _controlStack.AddChild(_tagPickerStack);

            // Action Buttons
            var btnRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 10f };
            btnRow.AddChild(new UIButton { Size = new Vector2(70, 35), Text = "Save", Command = new SavePrefabCommand { Mode = "New" }, BackgroundColor = Color.DarkGreen });
            btnRow.AddChild(new UIButton { Size = new Vector2(70, 35), Text = "Overwrt", Command = new SavePrefabCommand { Mode = "Replace" }, BackgroundColor = Color.DarkGoldenrod });
            btnRow.AddChild(new UIButton { Size = new Vector2(70, 35), Text = "Delete", Command = new DeletePrefabCommand(), BackgroundColor = Color.DarkRed });
            btnRow.AddChild(new UIButton { Size = new Vector2(70, 35), Text = "Exit", Command = new ClosePrefabCreatorCommand() });

            _controlStack.AddChild(btnRow);
            _rootPanel.AddChild(_controlStack);
        }
        private void BuildTagPicker(string filter)
        {
            _tagPickerStack.ClearChildren();
            string cleanFilter = filter.Trim().ToLower();

            foreach (var tag in _editorState.TagManager.Tags.Values)
            {
                // Skip tags that don't match the search filter
                if (!string.IsNullOrEmpty(cleanFilter) &&
                    !tag.HashID.ToLower().Contains(cleanFilter) &&
                    !tag.Name.ToLower().Contains(cleanFilter))
                {
                    continue;
                }

                var btn = new UIButton { Size = new Vector2(_tagPickerStack.Size.X - 10, 25), Text = $"{tag.HashID} - {tag.Name}", BackgroundColor = tag.TagColor * 0.5f };
                string hash = tag.HashID;

                btn.OnClick = () => {
                    var ctx = _editorState.PrefabCreator;

                    // 1. Remove the incomplete search term from the box
                    string currentText = ctx.TempTags;
                    int lastComma = currentText.LastIndexOf(',');
                    if (lastComma >= 0)
                        currentText = currentText.Substring(0, lastComma + 1) + " ";
                    else
                        currentText = ""; // Was the first tag being typed

                    // 2. Append the selected tag safely
                    if (!currentText.Contains(hash))
                    {
                        ctx.TempTags = currentText + hash + ", ";
                        _tagsInput.Text = ctx.TempTags; // Force UI update immediately
                        _editorUI.SetFocus(_tagsInput); // Keep typing!
                    }
                };
                _tagPickerStack.AddChild(btn);
            }
        }

        public override void Update(EditorInputState input, EventBus bus)
        {
            var ctx = _editorState.PrefabCreator;

            // Ensure preview loads immediately if empty
            if (ctx.SelectionRect.IsEmpty) ctx.SelectionRect = new Rectangle(0, 0, 16, 16);

            // Sync Text
            if (!_nameInput.IsFocused) _nameInput.Text = ctx.TempName;
            if (!_tagsInput.IsFocused) _tagsInput.Text = ctx.TempTags;

            _nameInput.OnTextChanged = (t) => ctx.TempName = t;
            _tagsInput.OnTextChanged = (t) => {
                ctx.TempTags = t;

                // Find the word currently being typed (after the last comma)
                string[] parts = t.Split(',');
                string currentSearch = parts[parts.Length - 1].Trim();

                // Rebuild picker if search term changed or tag count changed
                if (_lastTagSearch != currentSearch || _cachedTagCount != _editorState.TagManager.Tags.Count)
                {
                    _lastTagSearch = currentSearch;
                    _cachedTagCount = _editorState.TagManager.Tags.Count;
                    BuildTagPicker(_lastTagSearch);
                }
            };

            _editorUI.CheckFocusClick(_rootPanel, input);

            // Handle tag picker scrolling
            if (_tagPickerStack.AbsoluteBounds.Contains(input.MouseWindowPosition))
            {
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) _tagPickerStack.ScrollOffset = new Vector2(0, Math.Max(0, _tagPickerStack.ScrollOffset.Y - scrollDelta * 0.5f));
            }

            _rootPanel.Update(input, bus);

            // Convert Local to Absolute for Mouse checks
            Rectangle absAtlasArea = new Rectangle(Area.X + _localAtlasArea.X, Area.Y + _localAtlasArea.Y, _localAtlasArea.Width, _localAtlasArea.Height);

            if (absAtlasArea.Contains(input.MouseWindowPosition))
            {
                Vector2 mouseLocal = input.MouseWindowPosition - absAtlasArea.Location.ToVector2();
                Vector2 snapped = new Vector2((float)Math.Floor(mouseLocal.X / 16) * 16, (float)Math.Floor(mouseLocal.Y / 16) * 16);

                if (input.IsNewLeftClick)
                {
                    ctx.DragStart = snapped;
                    ctx.IsDragging = true;
                    _editorUI.SetFocus(null);
                }

                if (ctx.IsDragging)
                {
                    ctx.SelectionRect = new Rectangle(
                        (int)Math.Min(ctx.DragStart.X, snapped.X),
                        (int)Math.Min(ctx.DragStart.Y, snapped.Y),
                        (int)Math.Abs(ctx.DragStart.X - snapped.X) + 16,
                        (int)Math.Abs(ctx.DragStart.Y - snapped.Y) + 16);

                    if (!input.LeftHold) ctx.IsDragging = false;
                }
            }
        }


        private Rectangle CreateRect(Vector2 p1, Vector2 p2) => new Rectangle(
            (int)Math.Min(p1.X, p2.X), (int)Math.Min(p1.Y, p2.Y),
            (int)Math.Abs(p1.X - p2.X), (int)Math.Abs(p1.Y - p2.Y));

        public override void Draw(SpriteBatch sb)
        {
            var ctx = _editorState.PrefabCreator;
            _rootPanel.Draw(sb, _editorUI, _theme);

            // Convert local bounds to absolute bounds for rendering
            Rectangle absAtlasArea = new Rectangle(Area.X + _localAtlasArea.X, Area.Y + _localAtlasArea.Y, _localAtlasArea.Width, _localAtlasArea.Height);
            Rectangle absPreviewArea = new Rectangle(Area.X + _localPreviewArea.X, Area.Y + _localPreviewArea.Y, _localPreviewArea.Width, _localPreviewArea.Height);

            // 1. Draw Atlas
            sb.FillRectangle(absAtlasArea, Color.Black * 0.8f);
            var tex = _editorState.AssetLibrary.GetAtlas(ctx.ActiveAtlasName);
            if (tex != null)
            {
                sb.Draw(tex, absAtlasArea.Location.ToVector2(), Color.White);

                Rectangle drawSelect = new Rectangle(absAtlasArea.X + ctx.SelectionRect.X, absAtlasArea.Y + ctx.SelectionRect.Y, ctx.SelectionRect.Width, ctx.SelectionRect.Height);
                sb.DrawRectangle(drawSelect, Color.Yellow, 2);
                sb.FillRectangle(drawSelect, Color.Yellow * 0.2f);
            }

            // 2. Draw Preview Box
            sb.FillRectangle(absPreviewArea, Color.Black * 0.5f);
            sb.DrawRectangle(absPreviewArea, Color.White, 2);
            sb.DrawString(_editorUI.DebugFont, "PREVIEW", new Vector2(absPreviewArea.X + 5, absPreviewArea.Y + 5), Color.Gray);

            if (tex != null && !ctx.SelectionRect.IsEmpty)
            {
                float scale = Math.Min((absPreviewArea.Width - 40f) / ctx.SelectionRect.Width, (absPreviewArea.Height - 40f) / ctx.SelectionRect.Height);
                Vector2 centerPos = new Vector2(
                    absPreviewArea.Center.X - (ctx.SelectionRect.Width * scale) / 2,
                    absPreviewArea.Center.Y - (ctx.SelectionRect.Height * scale) / 2
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
            _theme = new UITheme { Font = ui.DebugFont, PanelBackground = Color.Black * 0.95f };

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
                Spacing = 5f,
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
        private readonly UIStackPanel _inspectorStack;
        private readonly UILabel _hintLabel;
        private readonly UITheme _theme;

        private readonly Dictionary<ITool, UIButton> _toolButtons = new Dictionary<ITool, UIButton>();

        private MapObject _cachedSelectedObject;
        private bool _isLinkingMode = false;
        private bool _needsInspectorRebuild = false; // Add this!
        public ToolPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _theme = new UITheme { Font = editorUI.DebugFont, PanelBackground = Color.DarkSlateGray * 0.9f };
            _rootPanel = new UIPanel { LocalPosition = area.Location.ToVector2(), Size = new Vector2(area.Width, area.Height) };

            // Tools (Left)
            _toolStack = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = new Vector2(10, 10), Spacing = 8f, PanelBackground = Color.Transparent };
            foreach (var tool in editorState.ToolState.Tools)
            {
                var btn = new UIButton { Size = new Vector2(32, 32), IconName = tool.IconName, Command = new ChangeToolCommand { ToolName = tool.Name } };
                _toolButtons.Add(tool, btn);
                _toolStack.AddChild(btn);
            }

            // Hints (Bottom Left)
            _hintLabel = new UILabel { LocalPosition = new Vector2(10, area.Height - 25), ColorOverride = Color.Yellow };

            // INSPECTOR (Right side)
            _inspectorStack = new UIStackPanel { Direction = StackDirection.Horizontal, LocalPosition = new Vector2(400, 10), Size = new Vector2(area.Width - 420, area.Height - 20), PanelBackground = Color.Black * 0.4f, Padding = 10f, Spacing = 20f };

            _rootPanel.AddChild(_toolStack);
            _rootPanel.AddChild(_hintLabel);
            _rootPanel.AddChild(_inspectorStack);
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

            // 1. Check if selection changed organically
            var selected = _editorState.Selection.SelectedMapObject;
            if (_cachedSelectedObject != selected)
            {
                _cachedSelectedObject = selected;
                _needsInspectorRebuild = true; // Trigger a rebuild
            }

            // 2. Safely Rebuild the UI BEFORE the UI Update loop runs
            if (_needsInspectorRebuild)
            {
                BuildInspector(selected, bus);
                _needsInspectorRebuild = false;
            }

            // 3. Handle Link Picking Mode (Blocks normal UI interaction)
            if (_isLinkingMode)
            {
                _hintLabel.Text = "LINKING MODE: Click a target object to link to. (Right Click or ESC to Cancel)";
                _hintLabel.ColorOverride = Color.Cyan;

                if (input.IsNewRightClick || input.CurrentKeyboard.IsKeyDown(Keys.Escape))
                {
                    _isLinkingMode = false;
                    _needsInspectorRebuild = true; // Refresh UI next frame
                }
                else if (input.IsNewLeftClick && _editorState._layoutmanager.ViewportPanel.Contains(input.MouseWindowPosition))
                {
                    var target = FindTopObjectUnderMouse(input.MouseWorldPosition);
                    if (target != null && target != _editorState.Selection.SelectedMapObject)
                    {
                        bus.Publish(new LinkObjectsCommand(_editorState.Selection.SelectedMapObject, target));
                    }
                    _isLinkingMode = false;
                    _needsInspectorRebuild = true; // Refresh UI next frame to show new link
                }
                return; // CRITICAL: Stop here so we don't click buttons behind the map!
            }

            // 4. Normal UI Update Loop
            if (Area.Contains(input.MouseWindowPosition))
            {
                ApplyGlobalScrolling(_rootPanel, input);
                _editorUI.CheckFocusClick(_rootPanel, input);
                _rootPanel.Update(input, bus);
                // Note: If a button clicked here changes state, it just sets _needsInspectorRebuild=true for NEXT frame. Safe!
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
            _inspectorStack.Spacing = 20f;
            if (_isLinkingMode)
            {
                _inspectorStack.AddChild(new UILabel { Text = "SELECT A TARGET ON THE MAP TO LINK...", TextColor = Color.Cyan });
                return;
            }

            if (obj == null)
            {
                _inspectorStack.AddChild(new UILabel { Text = "No Object Selected", TextColor = Color.Gray });
                return;
            }

            // --- COLUMN 1: Base Info ---
            var col1 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, Spacing = 15f, AutoSize = false, Size = new Vector2(250, 100) };
            col1.AddChild(new UILabel { Text = $"Inspector: {obj.Name}", TextColor = Color.White });
            col1.AddChild(new UILabel { Text = $"Type: {obj.Type}", TextColor = Color.LightGray });
            col1.AddChild(new UILabel { Text = $"Position: {obj.Position.X:F0}, {obj.Position.Y:F0}", TextColor = Color.Gray });
            _inspectorStack.AddChild(col1);

            _inspectorStack.AddChild(new UIPanel { Size = new Vector2(2, 80), BackgroundColor = Color.Gray });

            // --- COLUMN 2: Linking ---
            var col2 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, Spacing = 5f, AutoSize = false, Size = new Vector2(250, 100) };

            // Header Row (Puts Title and Button side-by-side to save vertical space)
            var linkHeaderRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 15f };
            linkHeaderRow.AddChild(new UILabel { Text = " Connections:", TextColor = Color.Cyan });
            var pickBtn = new UIButton { Size = new Vector2(100, 20), Text = "+ Pick Target", BackgroundColor = Color.DarkCyan };
            pickBtn.OnClick = () => { _isLinkingMode = true; _needsInspectorRebuild = true; };
            linkHeaderRow.AddChild(pickBtn);
            col2.AddChild(linkHeaderRow);

            // Scrollable List Box for multiple links (Keeps UI contained!)
            var linkListScrollBox = new UIStackPanel
            {
                Direction = StackDirection.Vertical,
                PanelBackground = Color.Black * 0.3f,
                Spacing = 8f,
                Padding = 5f,
                AutoSize = false,
                Size = new Vector2(250, 65), // Fixed height box
                ClipToBounds = true // Enables scrolling
            };

            foreach (string targetId in obj.LinkedObjects)
            {
                string safeTargetId = targetId;
                var row = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 15f };

                // Show short ID, or name if you want to look it up

                var delBtn = new UIButton { Size = new Vector2(40, 20), Text = "Del", BackgroundColor = Color.DarkRed };
                delBtn.OnClick = () => { bus.Publish(new UnlinkObjectCommand(obj, safeTargetId)); _needsInspectorRebuild = true; };

                row.AddChild(delBtn);
                row.AddChild(new UILabel { Text = $"> {targetId.Substring(0, 8)}", TextColor = Color.LightGray });
                linkListScrollBox.AddChild(row);
            }

            if (obj.LinkedObjects.Count == 0)
                linkListScrollBox.AddChild(new UILabel { Text = "Not linked.", TextColor = Color.Gray });

            col2.AddChild(linkListScrollBox);
            _inspectorStack.AddChild(col2);

            _inspectorStack.AddChild(new UIPanel { Size = new Vector2(2, 80), BackgroundColor = Color.Gray }); // Vertical Divider
            // --- COLUMN 3: Tags ---
            var col3 = new UIStackPanel { Direction = StackDirection.Vertical, PanelBackground = Color.Transparent, Spacing = 15f, AutoSize = false, Size = new Vector2(300, 100) };
            col3.AddChild(new UILabel { Text = " Applied Tags:", TextColor = Color.Yellow });

            // Create a horizontal flow container for tags
            var tagRow = new UIStackPanel { Direction = StackDirection.Horizontal, PanelBackground = Color.Transparent, Spacing = 15f };

            // 1. If it's a Prop, show Inherited tags from the Prefab (Grayed out/Locked)
            if (obj is PropObject prop)
            {
                var prefab = _editorState.PrefabManager.GetPrefab(prop.PrefabID);
                if (prefab != null)
                {
                    foreach (var pTag in prefab.Tags)
                    {
                        var def = _editorState.TagManager.GetTag(pTag);
                        tagRow.AddChild(new UIButton
                        {
                            Size = new Vector2(80, 20),
                            Text = pTag,
                            BackgroundColor = (def?.TagColor ?? Color.Gray) * 0.5f, // Dimmer to show it's inherited
                            TextColor = Color.LightGray
                        });
                    }
                }
            }

            // 2. Show Instance Specific Tags (Bright, editable)
            foreach (var iTag in obj.Tags)
            {
                string localTag = iTag; // Closure safe
                var def = _editorState.TagManager.GetTag(iTag);
                var btn = new UIButton
                {
                    Size = new Vector2(80, 20),
                    Text = iTag,
                    BackgroundColor = def?.TagColor ?? Color.Gray
                };

                // Clicking an instance tag removes it!
                btn.OnClick = () => {
                    obj.Tags.Remove(localTag);
                    _needsInspectorRebuild = true;
                };
                tagRow.AddChild(btn);
            }

            // 3. Simple Textbox to add new tags to the instance
            var addTagInput = new UITextBox { Size = new Vector2(120, 20), Placeholder = "Type tag & Enter..." };
            addTagInput.OnTextChanged = (text) =>
            {
                if (text.EndsWith("\n") || text.EndsWith(" ")) // Primitive 'Enter' detection, or just rely on a button
                {
                    string cleanTag = text.Trim();
                    if (!string.IsNullOrEmpty(cleanTag))
                    {
                        obj.Tags.Add(cleanTag);
                        addTagInput.Text = "";
                        _needsInspectorRebuild = true;
                    }
                }
            };

            col3.AddChild(tagRow);
            col3.AddChild(addTagInput);

            _inspectorStack.AddChild(col3);
        }

        // Helper to find whatever is under the mouse in the Map
        private MapObject FindTopObjectUnderMouse(Vector2 mouseWorld)
        {
            // Same logic as before
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
                        if (prefab != null)
                        {
                            RectangleF bounds = new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, prefab.SourceRect.Width, prefab.SourceRect.Height);
                            if (bounds.Contains(mouseWorld)) return prop;
                        }
                    }
                }
            }
            return null;
        }

        public override void Draw(SpriteBatch sb)
        {
            _rootPanel.Draw(sb, _editorUI, _theme);
        }

        public override string GetDebugInfo() => $"Linking Mode: {_isLinkingMode}";
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
            _theme = new UITheme { Font = editorUI.DebugFont, PanelBackground = Color.DarkSlateGray };

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

            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "AddUp", Command = new AddLayerCommand { Direction = true } });
            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "AddDown", Command = new AddLayerCommand { Direction = false } });
            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "CycleLayerType", Command = new CycleNewLayerTypeCommand() });
            _controlsStack.AddChild(new UIButton { Size = new Vector2(32, 32), IconName = "Delete", Command = new DeleteActiveLayerCommand() });

            _rootPanel.AddChild(_listStack);
            _rootPanel.AddChild(_controlsStack);
        }

        public override void Update(EditorInputState input, EventBus eventBus)
        {
            var cycleBtn = (UIButton)_controlsStack.Children[2];
            cycleBtn.IconName = _editorState.Layers.NewLayerType.ToString() + "Layer";
            bool isTyping = _editorUI.FocusedElement is UITextBox txt && txt.DebugName.StartsWith("LayerNameBox");

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
                bool isActive = _editorState.Layers.ActiveLayerIndex == i;

                var rowPanel = new UIButton
                {
                    Size = new Vector2(_listStack.Size.X - 10, 48), // Taller row
                    BackgroundColor = isActive ? Color.Goldenrod * 0.4f : Color.Black * 0.2f,
                    BorderColor = isActive ? Color.Yellow : Color.Transparent,
                    Command = new SelectLayerCommand { LayerIndex = index } // Clicking the row selects it!
                };

                // Tighten spacing between buttons to 2f
                var btnStack = new UIStackPanel { Direction = StackDirection.Horizontal, Size = rowPanel.Size, PanelBackground = Color.Transparent, Spacing = 2f };

                btnStack.AddChild(new UIButton { Size = new Vector2(24, 24), IconName = layer.IsVisible ? "Visible" : "Hidden", Command = new ToggleLayerVisibilityCommand { LayerIndex = index } });
                btnStack.AddChild(new UIButton { Size = new Vector2(24, 24), IconName = layer.IsLocked ? "Locked" : "Unlocked", Command = new ToggleLayerLockCommand { LayerIndex = index } });

                // MOVE BUTTON: Left Click = Up (True), Right Click = Down (False)
                btnStack.AddChild(new UIButton
                {
                    Size = new Vector2(24, 24),
                    IconName = "MoveUp",
                    Command = new MoveLayerCommand { LayerIndex = index, Direction = true },
                    RightCommand = new MoveLayerCommand { LayerIndex = index, Direction = false }
                });

                // TEXT BOX: Calculates remaining width. (3 buttons * 24px) + spacing
                var txtName = new UITextBox
                {
                    Size = new Vector2(rowPanel.Size.X - 86, 24),
                    Text = layer.Name,
                    DebugName = $"LayerNameBox_{index}"
                };

                txtName.OnTextChanged = (newText) => layer.Name = newText;
                txtName.OnGotFocus = () => bus.Publish(new SelectLayerCommand { LayerIndex = index });

                btnStack.AddChild(txtName);
                rowPanel.AddChild(btnStack);
                _listStack.AddChild(rowPanel);
            }
        }

        public override void Draw(SpriteBatch sb) => _rootPanel.Draw(sb, _editorUI, _theme);
        public override string GetDebugInfo()
        {
            var txt = _editorUI.FocusedElement as UITextBox;
            bool isTyping = txt != null && txt.DebugName.StartsWith("LayerNameBox");
            return $"Layers: {_editorState.Layers.Layers.Count} | Active Idx: {_editorState.Layers.ActiveLayerIndex}\n" +
                   $"Scroll Y: {_listStack.ScrollOffset.Y:F0} | Renaming: {isTyping}";
        }
    }
}
