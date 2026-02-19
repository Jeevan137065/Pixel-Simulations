// Add the correct namespace for ImGui.NET if you are using the ImGui.NET library
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;
//using MonoGame.ImGuiNet;
using System.IO;
using System.Linq;
using System.Text;


namespace Pixel_Simulations.UI
{
    public class TopPanel : BasePanel // BasePanel holds Area, EditorUI, EditorState
    {
        private readonly List<Button> _buttons = new List<Button>();

        public TopPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            var state = editorState.TopState;
            _buttons.Add(new Button(new Rectangle(Area.X + 5, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "New" }, "New"));
            _buttons.Add(new Button(new Rectangle(Area.X + 40, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "Save" }, "Save"));
            _buttons.Add(new Button(new Rectangle(Area.X + 75, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "Load" }, "Load"));
            _buttons.Add(new Button(new Rectangle(Area.X + 110, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "Undo" }, "Undo"));
            _buttons.Add(new Button(new Rectangle(Area.X + 145, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "Redo" }, "Redo"));
            _buttons.Add(new Button(new Rectangle(Area.X + 180, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "Capture" }, "Capture"));
            _buttons.Add(new Button(new Rectangle(Area.X + 215, Area.Y + 4, 32, 32), new MenuActionCommand { ActionName = "Export" }, "Export"));
        }

        public override void Update(InputState input, EventBus bus)
        {
            var panelState = _editorState.TopState;
            panelState.HoveredButtonName = null;
            if (!Area.Contains(input.MouseWindowPosition))
            {
                panelState.HoveredButtonName = null;
                return;
            }

            foreach (var button in _buttons)
            {
                if (button.Update(input))
                {
                    panelState.HoveredButtonName = button.IconName.ToString();
                    if (input.IsNewLeftClick)
                    {
                        bus.Publish(button.CommandToPublish);
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.FillRectangle(Area, Color.Gray * 0.3f);
            foreach (var button in _buttons)
            {
                // The button's internal state (IsHovered) now drives its drawing
                button.Draw(spriteBatch, _editorUI);
            }
        }
    }
    public class TilesetPanel : BasePanel
    {
        private enum Tab { Tiles, Objects }
        private Tab _activeTab = Tab.Tiles;

        private readonly Rectangle _tabArea;
        private readonly Rectangle _contentArea;
        private readonly Rectangle _footerArea;

        // Sub-areas for Tiles tab
        private readonly Rectangle _tileDisplayArea;

        // Buttons in Footer
        private readonly Button _addTilesetButton;
        private readonly Button _addObjectButton;
        private List<Button> _tilesetStackButtons = new List<Button>();

        private float _scrollOffset = 0;
        private const int TAB_HEIGHT = 30;
        private const int FOOTER_HEIGHT = 40;
        private const int MARGIN = 8;
        private const int TILE_DISPLAY_SIZE = 32;

        public TilesetPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _tabArea = new Rectangle(Area.X, Area.Y, Area.Width, TAB_HEIGHT);
            _footerArea = new Rectangle(Area.X, Area.Bottom - FOOTER_HEIGHT, Area.Width, FOOTER_HEIGHT);
            _contentArea = new Rectangle(Area.X, Area.Y + TAB_HEIGHT, Area.Width, Area.Height - TAB_HEIGHT - FOOTER_HEIGHT);

            _tileDisplayArea = new Rectangle(_contentArea.X + MARGIN, _contentArea.Y + MARGIN, _contentArea.Width - 2 * MARGIN, _contentArea.Height - 2 * MARGIN);

            // Footer Controls
            _addTilesetButton = new Button(new Rectangle(_footerArea.X + 5, _footerArea.Y + 4, 32, 32), new OpenAtlasPickerCommand(), "NewTileSet");
            _addObjectButton = new Button(new Rectangle(_footerArea.X + 42, _footerArea.Y + 4, 32, 32), new OpenPrefabCreatorCommand(), "NewObject");
        }

        private void RefreshTilesetStack()
        {
            _tilesetStackButtons.Clear();
            int xOffset = 85;
            foreach (var ts in _editorState.ActiveTileSets)
            {
                Rectangle bound = new Rectangle(_footerArea.X + xOffset, _footerArea.Y + 5, 80, 30);
                _tilesetStackButtons.Add(new Button(bound, new SelectTilesetCommand { TilesetName = ts.Name }, ts.Name));
                xOffset += 85;
            }
        }

        public override void Update(InputState input, EventBus eventBus)
        {
            RefreshTilesetStack();

            // 1. Handle Tab Switching (Check if we clicked the top 30px area)
            if (_tabArea.Contains(input.MouseWindowPosition) && input.IsNewLeftClick)
            {
                if (input.MouseWindowPosition.X < _tabArea.Center.X)
                    _activeTab = Tab.Tiles;
                else
                    _activeTab = Tab.Objects;

                return; // Don't process grid clicks on the same frame as a tab switch
            }
            if (!Area.Contains(input.MouseWindowPosition)) return;

            // 2. Footer Updates
            UpdateFooter(input, eventBus);

            foreach (var btn in _tilesetStackButtons)
            {
                if (btn.Update(input) && input.IsNewLeftClick) eventBus.Publish(btn.CommandToPublish);
            }

            // 3. Content Updates
            if (_activeTab == Tab.Tiles) UpdateTilesTab(input, eventBus);

            else if (_activeTab == Tab.Objects) UpdateObjectsTab(input, eventBus);
        }
        private void UpdateTilesTab(InputState input, EventBus eventBus)
        {
            if (!_tileDisplayArea.Contains(input.MouseWindowPosition)) return;

            var activeTs = _editorState.ActiveTileSets.FirstOrDefault(ts => ts.Name == _editorState.TilesetPanel.ActiveTilesetName);
            if (activeTs == null) return;

            int tilesPerRow = _tileDisplayArea.Width / TILE_DISPLAY_SIZE;
            int relX = (int)input.MouseWindowPosition.X - _tileDisplayArea.X;
            int relY = (int)input.MouseWindowPosition.Y - _tileDisplayArea.Y + (int)_scrollOffset;

            int col = relX / TILE_DISPLAY_SIZE;
            int row = relY / TILE_DISPLAY_SIZE;
            int tileIndex = row * tilesPerRow + col;

            if (input.IsNewLeftClick && tileIndex >= 0 && tileIndex < activeTs.SlicedAtlas.Count)
            {
                int tileId = activeTs.SlicedAtlas.Keys.ElementAt(tileIndex);
                eventBus.Publish(new SelectTileCommand { TilesetName = activeTs.Name, TileID = tileId });
            }

            // Scroll
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            _scrollOffset = MathHelper.Clamp(_scrollOffset - scrollDelta * 0.5f, 0, 5000); // MaxScroll calculated dynamic usually
        }
        private void UpdateObjectsTab(InputState input, EventBus eventBus)
        {
            if (!_tileDisplayArea.Contains(input.MouseWindowPosition)) return;

            int x = _tileDisplayArea.X;
            int y = _tileDisplayArea.Y - (int)_scrollOffset;

            foreach (var prefab in _editorState.PrefabManager.Prefabs.Values)
            {
                Rectangle slot = new Rectangle(x, y, 64, 64);
                if (input.IsNewLeftClick && slot.Contains(input.MouseWindowPosition))
                {
                    eventBus.Publish(new SelectPrefabCommand { PrefabID = prefab.ID });
                }

                x += 70;
                if (x + 70 > _tileDisplayArea.Right) { x = _tileDisplayArea.X; y += 70; }
            }
        }
        private void UpdateFooter(InputState input, EventBus bus)
        {
            if (_addTilesetButton.Update(input) && input.IsNewLeftClick)
            {
                // This opens the picker for TILE atlases
                bus.Publish(new OpenAtlasPickerCommand());
            }

            if (_addObjectButton.Update(input) && input.IsNewLeftClick)
            {
                // This opens the creator for OBJECT atlases
                // We pass the first available Object Atlas as a default
                string defaultAtlas = _editorState.AssetLibrary.GetNamesByType(AtlasType.Object).FirstOrDefault() ?? "Basic";
                bus.Publish(new OpenPrefabCreatorCommand { AtlasName = defaultAtlas });
            }
        }
        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);

            // Draw Tabs
            DrawTabs(sb);

            // Draw Content
            if (_activeTab == Tab.Tiles) DrawTilesContent(sb);
            else DrawObjectsContent(sb);

            // Draw Footer
            sb.FillRectangle(_footerArea, Color.Black * 0.4f);
            _addTilesetButton.Draw(sb, _editorUI);
            _addObjectButton.Draw(sb, _editorUI);
            foreach (var btn in _tilesetStackButtons)
            {
                bool isSelected = btn.IconName == _editorState.TilesetPanel.ActiveTilesetName;
                btn.Draw(sb, _editorUI);
                sb.DrawString(_editorUI.DebugFont, btn.IconName, btn.Bounds.Location.ToVector2() + new Vector2(5, 10), isSelected ? Color.Yellow : Color.White);
            }
        }

        private void DrawTabs(SpriteBatch sb)
        {
            var font = _editorUI.DebugFont;
            Rectangle tRect = new Rectangle(_tabArea.X, _tabArea.Y, _tabArea.Width / 2, _tabArea.Height);
            Rectangle oRect = new Rectangle(_tabArea.X + _tabArea.Width / 2, _tabArea.Y, _tabArea.Width / 2, _tabArea.Height);

            sb.FillRectangle(tRect, _activeTab == Tab.Tiles ? Color.DarkSlateGray : Color.Black * 0.5f);
            sb.DrawString(font, "Tiles", tRect.Center.ToVector2() - font.MeasureString("Tiles") / 2, Color.White);

            sb.FillRectangle(oRect, _activeTab == Tab.Objects ? Color.DarkSlateGray : Color.Black * 0.5f);
            sb.DrawString(font, "Objects", oRect.Center.ToVector2() - font.MeasureString("Objects") / 2, Color.White);
        }
        private void DrawObjectsContent(SpriteBatch sb)
        {
            var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
            sb.GraphicsDevice.ScissorRectangle = _tileDisplayArea;

            int slotSize = 64;
            int spacing = 6;
            int x = _tileDisplayArea.X + spacing;
            int y = _tileDisplayArea.Y + spacing - (int)_scrollOffset;

            foreach (var prefab in _editorState.PrefabManager.Prefabs.Values)
            {
                Rectangle slotRect = new Rectangle(x, y, slotSize, slotSize);
                bool isSelected = _editorState.Selection.ActivePrefab?.ID == prefab.ID;

                // 1. Draw Slot Background
                sb.FillRectangle(slotRect, Color.Black * 0.3f);
                sb.DrawRectangle(slotRect, isSelected ? Color.Yellow : Color.White * 0.2f, 1);

                // 2. Draw Sprite scaled into slot
                var tex = _editorState.AssetLibrary.GetAtlas(prefab.AtlasName);
                if (tex != null)
                {
                    // Calculate scale to fit either width or height into (slotSize - 8)
                    float maxDim = Math.Max(prefab.SourceRect.Width, prefab.SourceRect.Height);
                    float scale = (slotSize - 10f) / maxDim;

                    // Center the sprite in the slot
                    Vector2 spritePos = new Vector2(
                        slotRect.Center.X - (prefab.SourceRect.Width * scale) / 2,
                        slotRect.Center.Y - (prefab.SourceRect.Height * scale) / 2
                    );

                    sb.Draw(tex, spritePos, prefab.SourceRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }

                // 3. Hover Tooltip (Handle in Update, draw here or in Top Layer)
                if (slotRect.Contains(_editorState.Input.MouseWindowPosition))
                {
                    _editorState.TilesetPanel.HoveredTilesetName = $"{prefab.ID}\nTags: {string.Join(", ", prefab.Tags)}";
                }

                x += slotSize + spacing;
                if (x + slotSize > _tileDisplayArea.Right)
                {
                    x = _tileDisplayArea.X + spacing;
                    y += slotSize + spacing;
                }
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
            sb.Begin();
        }
        private void DrawTilesContent(SpriteBatch sb)
        {
            var panelState = _editorState.TilesetPanel;
            var activeTileset = _editorState.ActiveTileSets.FirstOrDefault(ts => ts.Name == panelState.ActiveTilesetName);

            if (activeTileset == null) return;

            // Use Scissor Rectangle to clip the scrolling grid
            var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
            sb.GraphicsDevice.ScissorRectangle = _tileDisplayArea;

            int tilesPerRow = _tileDisplayArea.Width / TILE_DISPLAY_SIZE;
            int index = 0;

            foreach (var tilePair in activeTileset.SlicedAtlas.OrderBy(p => p.Key))
            {
                int col = index % tilesPerRow;
                int row = index / tilesPerRow;

                var destRect = new Rectangle(
                    _tileDisplayArea.X + col * TILE_DISPLAY_SIZE,
                    _tileDisplayArea.Y + row * TILE_DISPLAY_SIZE - (int)_scrollOffset,
                    TILE_DISPLAY_SIZE, TILE_DISPLAY_SIZE);

                // Optimization: Only draw if visible
                if (destRect.Bottom > _tileDisplayArea.Top && destRect.Top < _tileDisplayArea.Bottom)
                {
                    sb.Draw(tilePair.Value, destRect, Color.White);

                    if (tilePair.Key == panelState.SelectedTileID)
                        sb.DrawRectangle(destRect, Color.Yellow, 2);
                    else
                        sb.DrawRectangle(destRect, Color.Black * 0.2f, 1);
                }
                index++;
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
            sb.Begin();
        }
    }
    public class PrefabCreatorPanel : BasePanel
    {
        private readonly Rectangle _atlasArea;
        private readonly Rectangle _controlArea;
        private readonly List<Button> _atlasSelectors = new List<Button>();

        private Button _btnSaveNew, _btnReplace, _btnDelete, _btnExit;

        // To handle text input focus
        private int _focusedField = 0; // 0 = none, 1 = Name, 2 = Tags
        private Rectangle _nameInputRect, _tagsInputRect;

        public PrefabCreatorPanel(Rectangle area, EditorUI ui, EditorState state) : base(area, ui, state)
        {
            // Layout: 70% Atlas View, 30% Control Panel
            _atlasArea = new Rectangle(area.X + 10, area.Y + 10, (int)(area.Width * 0.7f) - 20, area.Height - 20);
            _controlArea = new Rectangle(_atlasArea.Right + 20, area.Y + 10, (int)(area.Width * 0.3f) - 30, area.Height - 20);

            // Input Box Areas
            _nameInputRect = new Rectangle(_controlArea.X + 10, _controlArea.Y + 40, _controlArea.Width - 20, 30);
            _tagsInputRect = new Rectangle(_controlArea.X + 10, _controlArea.Y + 100, _controlArea.Width - 20, 30);

            // Buttons
            int bx = _controlArea.X + 10;
            int bw = _controlArea.Width - 15;
            _btnSaveNew = new Button(new Rectangle(bx, _controlArea.Y + 480, 32, 32), new SavePrefabCommand { Mode = "New" }, "New");
            _btnReplace = new Button(new Rectangle(bx +40, _controlArea.Y + 480, 32, 32), new SavePrefabCommand { Mode = "Replace" }, "Stack");
            _btnDelete = new Button(new Rectangle(bx +80, _controlArea.Y + 480, 32, 32), new DeletePrefabCommand(), "Delete");
            _btnExit = new Button(new Rectangle(bx +120, _controlArea.Y + 480, 32, 32), new ClosePrefabCreatorCommand(), "Exit (TAB)");

        }

        private void BuildAtlasSelectors()
        {
            _atlasSelectors.Clear();
            // Get all atlases marked as 'Object' or 'Universal'
            var available = _editorState.AssetLibrary.GetNamesByType(AtlasType.Object);
            int x = _atlasArea.X;

            // List selectors vertically inside the Control Panel
            int y = _controlArea.Y + 220;
            foreach (var name in available)
            {
                Rectangle rect = new Rectangle(_controlArea.X + 10, y, _controlArea.Width/2, 25);
                _atlasSelectors.Add(new Button(rect, new SelectAtlasForCreatorCommand { AtlasName = name }, "Atlas: " + name));
                y += 30;
            }
        }

        public override void Update(InputState input, EventBus bus)
        {
            var ctx = _editorState.PrefabCreator;

            BuildAtlasSelectors();
            // 1. Toggle/Exit Logic
            if (input.CurrentKeyboard.IsKeyDown(Keys.Tab) && input.PreviousKeyboard.IsKeyUp(Keys.Tab))
                bus.Publish(new ClosePrefabCreatorCommand());

            // 2. Atlas Interaction (Selection)
            if (_atlasArea.Contains(input.MouseWindowPosition))
            {
                // Calculate mouse position relative to atlas top-left
                Vector2 mouseLocal = input.MouseWindowPosition - _atlasArea.Location.ToVector2();
                // Snap to 16px grid
                Vector2 snapped = new Vector2((float)Math.Floor(mouseLocal.X / 16) * 16, (float)Math.Floor(mouseLocal.Y / 16) * 16);

                if (input.IsNewLeftClick)
                {
                    ctx.DragStart = snapped;
                    ctx.IsDragging = true;
                    _focusedField = 0; // Unfocus text on atlas click
                }

                if (ctx.IsDragging)
                {
                    // Create a rect from start to current, ensuring at least 16x16
                    ctx.SelectionRect = CreateRect(ctx.DragStart, snapped + new Vector2(16, 16));
                    if (!input.LeftHold) ctx.IsDragging = false;
                }
            }

            // 3. Text Input Focus
            if (input.IsNewLeftClick)
            {
                if (_nameInputRect.Contains(input.MouseWindowPosition)) _focusedField = 1;
                else if (_tagsInputRect.Contains(input.MouseWindowPosition)) _focusedField = 2;
            }

            if (_focusedField > 0) HandleTextTyping(input);

            // 4. Buttons & Selectors
            foreach (var btn in _atlasSelectors)
            {
                if (btn.Update(input) && input.IsNewLeftClick) bus.Publish(btn.CommandToPublish);
            }

            if (_btnSaveNew.Update(input) && input.IsNewLeftClick) bus.Publish(_btnSaveNew.CommandToPublish);
            if (_btnReplace.Update(input) && input.IsNewLeftClick) bus.Publish(_btnReplace.CommandToPublish);
            if (_btnDelete.Update(input) && input.IsNewLeftClick) bus.Publish(_btnDelete.CommandToPublish);
            if (_btnExit.Update(input) && input.IsNewLeftClick) bus.Publish(_btnExit.CommandToPublish);
        }

        private void HandleTextTyping(InputState input)
        {
            var ctx = _editorState.PrefabCreator;
            string target = _focusedField == 1 ? ctx.TempName : ctx.TempTags;

            // Handle Backspace
            if (input.CurrentKeyboard.IsKeyDown(Keys.Back) && input.PreviousKeyboard.IsKeyUp(Keys.Back) && target.Length > 0)
                target = target.Substring(0, target.Length - 1);

            // Handle Keys
            foreach (var key in input.CurrentKeyboard.GetPressedKeys())
            {
                if (input.PreviousKeyboard.IsKeyUp(key))
                {
                    char c = GetCharFromKey(key, input.CurrentKeyboard.IsKeyDown(Keys.LeftShift));
                    if (c != '\0') target += c;
                }
            }

            // Write back
            if (_focusedField == 1) ctx.TempName = target;
            else ctx.TempTags = target;
        }

        public override void Draw(SpriteBatch sb)
        {
            var ctx = _editorState.PrefabCreator;
            sb.FillRectangle(Area, Color.DarkSlateGray);
            sb.DrawRectangle(Area, Color.White, 2);

            // --- DRAW ATLAS VIEW ---
            sb.FillRectangle(_atlasArea, Color.Black * 0.5f);
            var tex = _editorState.AssetLibrary.GetAtlas(ctx.ActiveAtlasName);
            if (tex != null)
            {
                sb.Draw(tex, _atlasArea.Location.ToVector2(), Color.White);
                DrawGrid(sb, _atlasArea);

                // Draw selection box relative to atlas area
                Rectangle drawSelect = new Rectangle(
                    _atlasArea.X + ctx.SelectionRect.X,
                    _atlasArea.Y + ctx.SelectionRect.Y,
                    ctx.SelectionRect.Width,
                    ctx.SelectionRect.Height
                );
                sb.DrawRectangle(drawSelect, Color.Yellow, 2);
            }

            // --- DRAW CONTROL PANEL ---
            sb.FillRectangle(_controlArea, Color.Black * 0.3f);

            // Name Field
            sb.DrawString(_editorUI.DebugFont, "Object ID:", new Vector2(_controlArea.X + 10, _controlArea.Y + 20), Color.White);
            sb.FillRectangle(_nameInputRect, _focusedField == 1 ? Color.White : Color.Gray);
            sb.DrawString(_editorUI.DebugFont, ctx.TempName + (_focusedField == 1 ? "_" : ""), new Vector2(_nameInputRect.X + 5, _nameInputRect.Y + 5), Color.Black);

            // Tags Field
            sb.DrawString(_editorUI.DebugFont, "Tags (comma separated):", new Vector2(_controlArea.X + 10, _controlArea.Y + 80), Color.White);
            sb.FillRectangle(_tagsInputRect, _focusedField == 2 ? Color.White : Color.Gray);
            sb.DrawString(_editorUI.DebugFont, ctx.TempTags + (_focusedField == 2 ? "_" : ""), new Vector2(_tagsInputRect.X + 5, _tagsInputRect.Y + 5), Color.Black);


            // Preview of what is selected
            if (tex != null && !ctx.SelectionRect.IsEmpty)
            {
                sb.DrawString(_editorUI.DebugFont, "Preview:", new Vector2(_controlArea.X + 10, _controlArea.Y + 140), Color.White);
                sb.Draw(tex, new Vector2(_controlArea.X + 10, _controlArea.Y + 160), ctx.SelectionRect, Color.White, 0, Vector2.Zero, 1f, SpriteEffects.None, 0);
            }
            // Buttons & Selectors
            foreach (var btn in _atlasSelectors) 
            { 
                btn.Draw(sb, _editorUI);
                sb.DrawString(_editorUI.DebugFont, btn.IconName.ToString(), btn.Bounds.Location.ToVector2(), Color.White);
            }
            _btnSaveNew.Draw(sb, _editorUI);
            _btnReplace.Draw(sb, _editorUI);
            _btnDelete.Draw(sb, _editorUI);
            _btnExit.Draw(sb, _editorUI);
        }

        private Rectangle CreateRect(Vector2 p1, Vector2 p2) => new Rectangle(
            (int)Math.Min(p1.X, p2.X), (int)Math.Min(p1.Y, p2.Y),
            (int)Math.Abs(p1.X - p2.X), (int)Math.Abs(p1.Y - p2.Y));

        private void DrawGrid(SpriteBatch sb, Rectangle area)
        {
            for (int x = 0; x < area.Width; x += 16)
                sb.DrawLine(area.X + x, area.Y, area.X + x, area.Bottom, Color.White * 0.15f);
            for (int y = 0; y < area.Height; y += 16)
                sb.DrawLine(area.X, area.Y + y, area.Right, area.Y + y, Color.White * 0.15f);
        }

        private char GetCharFromKey(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z) return shift ? (char)key : char.ToLower((char)key);
            if (key >= Keys.D0 && key <= Keys.D9) return (char)key;
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            if (key == Keys.Space) return ' ';
            if (key == Keys.OemComma) return ',';
            return '\0';
        }
    }
    public class ToolPanel : BasePanel
    {
        private Dictionary<ITool, Button> _toolButtons = new Dictionary<ITool, Button>();
        public ToolPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            int yOffset = 10;
            foreach (var tool in editorState.ToolState.Tools)
            {
                var btnRect = new Rectangle(area.X + yOffset, area.Y + 10, 32, 32);
                var btn = new Button(btnRect, new ChangeToolCommand { ToolName = tool.Name }, tool.IconName);

                _toolButtons.Add(tool, btn);
                yOffset += 40;
            }
        }

        public override void Update(InputState input, EventBus bus)
        {
            foreach (var kvp in _toolButtons)
            {
                ITool tool = kvp.Key;
                Button button = kvp.Value;

                // REACTION POINT: Update the button's icon name from the tool's property
                button.IconName = tool.IconName;

                if (button.Update(input) && input.IsNewLeftClick)
                {
                    bus.Publish(button.CommandToPublish);
                }
            }

        }

        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);
            foreach (var btn in _toolButtons.Values)
            {
                btn.Draw(sb, _editorUI);
            }
        }
    }
    public class LayerPanel : BasePanel
    {
        private readonly Rectangle _layerListArea;
        private readonly Rectangle _controlsArea;
        private readonly List<Button> _globalControlButtons = new List<Button>();
        private readonly List<LayerRow> _layerRows = new List<LayerRow>();

        private float _scrollOffset = 0;
        private float _totalContentHeight = 0;
        private LayerPanelState LayerStack;

        public LayerPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            LayerStack = editorState.Layers;
            int controlsHeight = 40;
            _controlsArea = new Rectangle(area.X, area.Bottom - controlsHeight, area.Width, controlsHeight);
            _layerListArea = new Rectangle(area.X, area.Y, area.Width, area.Height - controlsHeight);

            _globalControlButtons.Add(new Button(new Rectangle(_controlsArea.X + 5, _controlsArea.Y + 4, 32, 32), new AddLayerCommand { Direction = true }, "AddUp"));
            _globalControlButtons.Add(new Button(new Rectangle(_controlsArea.X + 40, _controlsArea.Y + 4, 32, 32), new AddLayerCommand { Direction = false }, "AddDown"));
            _globalControlButtons.Add(new Button(new Rectangle(_controlsArea.X + 75, _controlsArea.Y + 4, 32, 32), new CycleNewLayerTypeCommand(), "CycleLayerType"));
            _globalControlButtons.Add(new Button(new Rectangle(_controlsArea.X + 110, _controlsArea.Y + 4, 32, 32), new DeleteActiveLayerCommand{ }, "Delete"));
        }

        public override void Update(InputState input, EventBus eventBus)
        {

            BuildLayerRows();
            LayerStack.HoveredButtonName = null;
            LayerStack.HoveredLayerIconName = null;
            if (!Area.Contains(input.MouseWindowPosition)) return;
            
            // --- Update Global Control Buttons ---
            if (_controlsArea.Contains(input.MouseWindowPosition))
            {
                foreach (var button in _globalControlButtons)
                {
                    if (button.Update(input))
                    {
                        LayerStack.HoveredButtonName = button.IconName.ToString();

                        if (input.IsNewLeftClick){
                            eventBus.Publish(button.CommandToPublish);
                        }
                    }
                }
            }
            // --- Update Layer List Area ---
            else if (_layerListArea.Contains(input.MouseWindowPosition))
            {
             // Handle Scrolling
             int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) _scrollOffset -= scrollDelta * 0.5f;
                _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, Math.Max(0, _totalContentHeight - _layerListArea.Height));
                // Update each individual row's buttons
                foreach (var row in _layerRows)
                {
                    // We only interact with rows currently within the viewable list area
                 if (row._bounds.Intersects(_layerListArea))
                    {
                        if (row._bounds.Contains(input.MouseWindowPosition))
                        {
                            LayerStack.HoveredLayerIndex = row._layerIndex;

                            // Handle Row Selection (if not clicking a button)
                            bool clickedButton = false;
                            foreach (var btn in row._buttons)
                            {
                                if (btn.Update(input))
                                {
                                    clickedButton = true;
                                    if (input.IsNewLeftClick) eventBus.Publish(btn.CommandToPublish);
                                }
                            }

                            if (input.IsNewLeftClick && !clickedButton)
                            {
                                LayerStack.ActiveLayerIndex = row._layerIndex;
                            }

                            if (input.NewDoubleLeftClick && !clickedButton)
                            {
                                LayerStack.RenamingLayerIndex = row._layerIndex;
                                LayerStack.TextEditorString = row._layer.Name;
                            }
                        }
                    }
                }
            }

            if (_editorState.Layers.RenamingLayerIndex != -1)
            {
                HandleTextInput(input);
                // IMPORTANT: We return here to prevent any other tools or shortcuts from processing while in text input mode.
                return;
            }
        }
        private void BuildLayerRows()
        {
            _layerRows.Clear();
            // Start drawing at the top of the list area, adjusted by scroll
            int currentY = _layerListArea.Y - (int)_scrollOffset;

            for (int i = 0; i < LayerStack.Layers.Count; i++)
            {
                var layer = LayerStack.Layers[i];

                // Create a row with temporary bounds to calculate height
                var tempRow = new LayerRow(layer, i, new Rectangle(_layerListArea.X, currentY, _layerListArea.Width, 40));
                int rowHeight = tempRow.GetTotalHeight();

                // Finalize the bounds for this frame
                tempRow._bounds = new Rectangle(_layerListArea.X, currentY, _layerListArea.Width, rowHeight);
                tempRow.RefreshButtons(); // Ensure buttons move with the row

                _layerRows.Add(tempRow);
                currentY += rowHeight;
            }

            // Track total height for scrolling clamp logic
            _totalContentHeight = (currentY + (int)_scrollOffset) - _layerListArea.Y;
        }
        private void HandleTextInput(InputState input)
        {
            var panelState = _editorState.Layers;
            var kbs = input.CurrentKeyboard;
            var prevKbs = input.PreviousKeyboard;

            // --- Check for finishing the edit ---
            if (kbs.IsKeyDown(Keys.Enter) && prevKbs.IsKeyUp(Keys.Enter))
            {
                // Commit the change
                var layer = _editorState.Layers.Layers[panelState.RenamingLayerIndex];
                layer.Name = panelState.TextEditorString;
                panelState.RenamingLayerIndex = -1; // Exit renaming mode
                return;
            }

            // --- Handle Text Modification ---
            if (kbs.IsKeyDown(Keys.Back) && prevKbs.IsKeyUp(Keys.Back) && panelState.TextEditorString.Length > 0)
            {
                panelState.TextEditorString = panelState.TextEditorString.Substring(0, panelState.TextEditorString.Length - 1);
            }

            // A simple way to get typed characters
            foreach (var key in kbs.GetPressedKeys())
            {
                if (prevKbs.IsKeyUp(key)) // Only process new key presses
                {
                    char character = GetCharFromKey(key, kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift));
                    if (character != '\0') // If it's a printable character
                    {
                        panelState.TextEditorString += character;
                    }
                }
            }
        }
        private char GetCharFromKey(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return shift ? (char)key : char.ToLower((char)key);
            if (key >= Keys.D0 && key <= Keys.D9)
                return (char)key;
            if (key == Keys.Space) return ' ';
            // Add more characters as needed (-, _, etc.)
            return '\0';
        }

        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);

            // Scissor for the scrollable list
            var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
            sb.GraphicsDevice.ScissorRectangle = _layerListArea;

            foreach (var row in _layerRows)
            {
                // Optimization: Only draw if on screen
                if (row._bounds.Bottom > _layerListArea.Top && row._bounds.Top < _layerListArea.Bottom)
                {
                    row.Draw(sb, _editorUI, row._layerIndex == LayerStack.ActiveLayerIndex);
                }
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
            sb.Begin();

            // Global controls background and drawing
            sb.FillRectangle(_controlsArea, Color.Black * 0.6f);
            foreach (var button in _globalControlButtons)
            {
                if (button.IconName == "CycleLayerType")
                {
                    string iconName = _editorState.Layers.NewLayerType.ToString() + "Layer";
                    _editorUI.DrawIcon(sb, button.Bounds, iconName, button.IsHovered ? Color.Yellow : Color.White);
                }
                else
                {
                    button.Draw(sb, _editorUI);
                }
            }
        }
    }
    public class EditorUI
    {
        private Texture2D _iconsTexture;
        public Dictionary<string, Rectangle> IconSources { get; private set; }

        public TopPanel TopPanel { get; }
        public TilesetPanel TilesetPanel { get; }
        public PrefabCreatorPanel PrefabPanel { get; }
        public ToolPanel ToolPanel { get; }
        public LayerPanel LayerPanel { get; }
        public readonly EditorState _editorState;
        public GraphicsDevice gd { get; }
        public SpriteFont DebugFont { get; private set; }
        // It will contain a list of all panels: Viewport, ToolPanel, LayerPanel, etc.
        private GridRenderer gridRenderer { get; set; }
        public EditorUI(EditorState editorState)
        {
            _editorState = editorState;
            IconSources = new Dictionary<string, Rectangle>();
            gd = editorState._graphics;
            // Create all the panel instances
            TopPanel = new TopPanel(_editorState._layoutmanager.TopPanel, this, editorState);
            TilesetPanel = new TilesetPanel(_editorState._layoutmanager.TilesetPanel, this, editorState);
            PrefabPanel = new PrefabCreatorPanel(_editorState._layoutmanager.ViewportPanel,this,_editorState);
            LayerPanel = new LayerPanel(_editorState._layoutmanager.LayerPanel,this,editorState);
            ToolPanel = new ToolPanel(_editorState._layoutmanager.ToolPanel, this, editorState);
            gridRenderer = new GridRenderer(_editorState.CELL_SIZE);
        }

        public void LoadContent(ContentManager content) 
        {
            DebugFont = content.Load<SpriteFont>("Font");
            _iconsTexture = content.Load<Texture2D>("EditorUI");
            string json = File.ReadAllText("Content/EditorIcons.json");
            var defFile = JsonConvert.DeserializeObject<dynamic>(json);
            int iconSize = defFile.icon_size;
            var icons = defFile.icons.ToObject<Dictionary<string, IconDefinition>>();
            foreach (var iconPair in icons)
            {
                IconSources[iconPair.Key] = new Rectangle(iconPair.Value.x, iconPair.Value.y, iconSize, iconSize);
            }
        }

        public void PreDraw(SpriteBatch spriteBatch)
        {
            if (_editorState.ShowGrid == true)
            {
                gridRenderer.Draw(spriteBatch, _editorState.camera, _editorState._layoutmanager.ViewportPanel);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            
            TopPanel.Draw(spriteBatch);
            TilesetPanel.Draw(spriteBatch);
            ToolPanel.Draw(spriteBatch);
            LayerPanel.Draw(spriteBatch);
            if (_editorState.PrefabCreator.IsOpen)
            {
                // Draw a semi-transparent black overlay to dim the background
                //spriteBatch.FillRectangle(new Rectangle(0, 0, 1920, 1080), Color.Black * 0.5f);
                PrefabPanel.Draw(spriteBatch);
            }
        }
        public void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color)
        {
            if (IconSources.TryGetValue(iconName, out var sourceRect))
            {
                sb.Draw(_iconsTexture, destination, sourceRect, color);
            }
        }
    }
    public class LayoutManager
    {
        //Upscaler data
        public int UpscaleFactor { get; set; }
        // Public properties for every panel's bounds
        public Rectangle WindowBounds { get; private set; }
        public Rectangle ViewportPanel { get; private set; }
        public Rectangle TilesetPanel { get; private set; }
        public Rectangle LayerPanel { get; private set; }
        public Rectangle ToolPanel { get; private set; }
        public Rectangle TopPanel { get; private set; }

        // A dictionary to easily look up panel names
        private readonly Dictionary<string, Rectangle> _panels = new Dictionary<string, Rectangle>();

        public LayoutManager(int windowWidth, int windowHeight,EditorUpscaler editorUpscaler)
        {
            WindowBounds = new Rectangle(0, 0, windowWidth, windowHeight);
            //Needed View Port Size = 480*270 * 2 = 960*540
            int ViewW = 960;
            int ViewH = 540;
            // Define panel dimensions based on your requirements
            int tilesetWidth = 400;
            int layerWidth = 80;
            int topPanelHeight = 60;
            int toolPanelHeight = 120;

            UpscaleFactor = editorUpscaler.Scale;
            // Calculate the positions and sizes
            //TopPanel = new Rectangle(tilesetWidth, 0, windowWidth - tilesetWidth - layerWidth, topPanelHeight);
            //ToolPanel = new Rectangle(tilesetWidth, windowHeight - toolPanelHeight, windowWidth - tilesetWidth - layerWidth, toolPanelHeight);
            //TilesetPanel = new Rectangle(0, 0, tilesetWidth, windowHeight);
            //LayerPanel = new Rectangle(windowWidth - layerWidth, 0, layerWidth, windowHeight);
            
            TopPanel = new Rectangle(tilesetWidth, 0, ViewW, topPanelHeight);
            ToolPanel = new Rectangle(tilesetWidth, windowHeight - toolPanelHeight, ViewW, toolPanelHeight);

            TilesetPanel = new Rectangle(0, 0, tilesetWidth, windowHeight);
            LayerPanel = new Rectangle(tilesetWidth + ViewW, 0, windowWidth - tilesetWidth - ViewW, windowHeight);

            ViewportPanel = new Rectangle(
                tilesetWidth,
                topPanelHeight,
                ViewW,
                ViewH
            );

            // Populate the dictionary for easy lookup
            _panels["Tileset"] = TilesetPanel;
            _panels["Layer"] = LayerPanel;
            _panels["Top"] = TopPanel;
            _panels["Tool"] = ToolPanel;
            _panels["Viewport"] = ViewportPanel;
        }

        /// <summary>
        /// Checks which panel the mouse is currently over.
        /// </summary>
        /// <param name="mousePosition">The mouse position in window coordinates.</param>
        /// <returns>The name of the panel, or "None" if it's not over any defined panel.</returns>
        public string GetPanelAt(Point mousePosition)
        {
            foreach (var panel in _panels)
            {
                if (panel.Value.Contains(mousePosition))
                {
                    return panel.Key;
                }
            }
            return "None";
        }
    }
    public class GridRenderer
    {
        private int CELL_SIZE { get; set; }
        private readonly Color _gridColor = Color.White * 0.15f;
        private readonly Color _chunkGridColor = Color.Cyan * 0.25f;

        public GridRenderer(int cell)
        {
            CELL_SIZE = cell;
        }
        public void Draw(SpriteBatch spriteBatch, EditorCamera camera, Rectangle viewportBounds)
        {
            // Get the visible area of the world from the camera
            RectangleF visibleWorld = camera.GetVisibleWorldBounds(viewportBounds);
            float lineThickness = 1f / camera.Zoom;

            if (camera.Zoom >= 0.5f)
            {
                DrawGridLines(spriteBatch, visibleWorld, CELL_SIZE, _gridColor, lineThickness);
            }

            int chunkSizePixels = Chunk.CHUNK_SIZE * CELL_SIZE;
            DrawGridLines(spriteBatch, visibleWorld, chunkSizePixels, _chunkGridColor, lineThickness * 2f);

            // Always draw the origin lines.
            spriteBatch.DrawLine(0, visibleWorld.Top, 0, visibleWorld.Bottom, Color.Red * 0.5f, lineThickness * 3f);
            spriteBatch.DrawLine(visibleWorld.Left, 0, visibleWorld.Right, 0, Color.LimeGreen * 0.5f, lineThickness * 3f);
        }

        private void DrawGridLines(SpriteBatch spriteBatch, RectangleF visibleWorld, int gridSize, Color color, float thickness)
        {
            float left = (float)System.Math.Floor(visibleWorld.Left / gridSize) * gridSize;
            float top = (float)System.Math.Floor(visibleWorld.Top / gridSize) * gridSize;

            for (float x = left; x < visibleWorld.Right; x += gridSize)
            {
                spriteBatch.DrawLine(x, visibleWorld.Top, x, visibleWorld.Bottom, color, thickness);
            }
            for (float y = top; y < visibleWorld.Bottom; y += gridSize)
            {
                spriteBatch.DrawLine(visibleWorld.Left, y, visibleWorld.Right, y, color, thickness);
            }
        }
    }

}