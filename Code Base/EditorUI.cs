using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;


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

        // --- UI Layout Rectangles ---
        private readonly Rectangle _tabArea;
        private readonly Rectangle _contentArea;
        // Content areas for the "Tiles" tab
        private readonly Rectangle _tilesetListArea;
        private readonly Rectangle _tileDisplayArea;
        private readonly Button _addTilesetButton;
        private List<Button> tileSets = new List<Button>();
        // --- State for "Tiles" Tab ---
        private float _scrollOffset = 0;
        private float _maxScroll = 0;

        // --- Constants ---
        private const int TAB_HEIGHT = 30;
        private const int MARGIN = 8;
        private const int TILE_DISPLAY_SIZE = 32;
        private const int TILESET_LIST_ITEM_WIDTH = 80;
        private const int TILESET_LIST_ITEM_HEIGHT = 40;


        // Constants for layout

        public TilesetPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _tabArea = new Rectangle(Area.X, Area.Y, Area.Width, TAB_HEIGHT);
            _contentArea = new Rectangle(Area.X, Area.Y + TAB_HEIGHT, Area.Width, Area.Height - TAB_HEIGHT);
            _tileDisplayArea = new Rectangle(_contentArea.X + MARGIN, _contentArea.Y + MARGIN, _contentArea.Width - 2 * MARGIN, _contentArea.Height - TILE_DISPLAY_SIZE - MARGIN);
            _tilesetListArea = new Rectangle(_contentArea.X + MARGIN, _contentArea.Height - MARGIN - TILE_DISPLAY_SIZE, Area.Width - 2 * MARGIN - TILE_DISPLAY_SIZE, TILE_DISPLAY_SIZE);
            _addTilesetButton = new Button(new Rectangle(_tilesetListArea.X, _tilesetListArea.Y, _tilesetListArea.Height, _tilesetListArea.Height), new CreateTilesetCommand { AtlasName = "ShowAtlasPicker" }, "New");
        }

        private void Create_Buttons(EditorState editorState)
        {
            if (tileSets.Count > 1) {tileSets.Clear(); }
            for (int i=0; i < editorState.ActiveTileSets.Count; i++)
            {
                Rectangle bound = new Rectangle(_tilesetListArea.X + _addTilesetButton.Bounds.Width + i* TILESET_LIST_ITEM_WIDTH, _tilesetListArea.Y, TILESET_LIST_ITEM_WIDTH, _tilesetListArea.Height);
                Button button = new Button(bound,new SelectTilesetCommand(), _editorState.ActiveTileSets[i].Name); 
                tileSets.Add(button);
            }
        }

        public override void Update(InputState input, EventBus eventBus)
        {

            Create_Buttons(_editorState);
            if (!Area.Contains(input.MouseWindowPosition)) return;
            // --- 1. Handle Tab Switching ---
            if (_tabArea.Contains(input.MouseWindowPosition) && input.IsNewLeftClick)
            {
                if (input.MouseWindowPosition.X < Area.X + Area.Width / 2)
                    _activeTab = Tab.Tiles;
                else
                    _activeTab = Tab.Objects;
            }

            // --- 2. Delegate to the Active Tab's Update Logic ---
            if (_activeTab == Tab.Tiles)
            {
                UpdateTilesTab(input, eventBus);
            }
            else if (_activeTab == Tab.Objects)
            {
                // Future: UpdateObjectsTab(input, eventBus);
            }
        }
        private void UpdateTilesTab(InputState input, EventBus eventBus)
        {
            var panelState = _editorState.TilesetPanel;
            var TileSets = _editorState.ActiveTileSets;
            panelState.HoveredTileCell = new Point(-1, -1);
            panelState.HoveredTilesetName = null;

            if (_tilesetListArea.Contains(input.MouseWindowPosition))
            {
                 if( _addTilesetButton.Update(input))
                {
                    if (input.IsNewLeftClick)
                    { eventBus.Publish(new CreateTilesetCommand { AtlasName = "Wild" }); }
                }
                foreach(var tile in tileSets)
                {
                    if (tile.Update(input))
                    {
                        panelState.HoveredTilesetName = tile.IconName;
                        if (input.IsNewLeftClick) { eventBus.Publish(tile.CommandToPublish); }
                    }
                }

            }
            else if (_tileDisplayArea.Contains(input.MouseWindowPosition))
            {
                var activeTileset = TileSets.FirstOrDefault(ts => ts.Name == panelState.ActiveTilesetName);
                if (activeTileset != null)
                {
                    int tilesPerRow = (_tileDisplayArea.Width) / TILE_DISPLAY_SIZE;
                    if (tilesPerRow > 0)
                    {
                        int relX = (int)input.MouseWindowPosition.X - _tileDisplayArea.X;
                        int relY = (int)input.MouseWindowPosition.Y - _tileDisplayArea.Y + (int)_scrollOffset;
                        int col = relX / TILE_DISPLAY_SIZE;
                        int row = relY / TILE_DISPLAY_SIZE;
                        panelState.HoveredTileCell = new Point(col, row);
                        int tileIndex = row * tilesPerRow + col;

                        if (input.IsNewLeftClick)
                        {
                            if (relX >= 0 && col < tilesPerRow && tileIndex >= 0 && tileIndex < activeTileset.SlicedAtlas.Count) {
                                int tileId = activeTileset.SlicedAtlas.Keys.ElementAt(tileIndex);
                                eventBus.Publish(new SelectTileCommand { TilesetName = activeTileset.Name, TileID = tileId });
                            }
                        }
                    }
                }
            }

            // Handle Scrolling in the tile display area
            if (_tileDisplayArea.Contains(input.MouseWindowPosition))
            {
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    _scrollOffset -= scrollDelta * 0.5f;
                    // You would calculate _maxScroll based on content height
                    _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, _maxScroll);
                }
            }
        }

        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);
            sb.DrawRectangle(_tileDisplayArea, Color.White * 0.3f);
            sb.DrawRectangle(_tilesetListArea, Color.Blue * 0.3f);
            var font = _editorUI.DebugFont;

            // --- Draw Tabs ---
            var tilesTabRect = new Rectangle(_tabArea.X, _tabArea.Y, _tabArea.Width / 2, _tabArea.Height);
            var objectsTabRect = new Rectangle(_tabArea.X + _tabArea.Width / 2, _tabArea.Y, _tabArea.Width / 2, _tabArea.Height);

            sb.FillRectangle(tilesTabRect, _activeTab == Tab.Tiles ? Color.DarkSlateGray : Color.Black * 0.5f);
            sb.DrawString(font, "Tiles", tilesTabRect.Center.ToVector2() - font.MeasureString("Tiles") / 2, Color.White);

            sb.FillRectangle(objectsTabRect, _activeTab == Tab.Objects ? Color.DarkSlateGray : Color.Black * 0.5f);
            sb.DrawString(font, "Objects", objectsTabRect.Center.ToVector2() - font.MeasureString("Objects") / 2, Color.White);

            sb.DrawLine(_contentArea.X, _contentArea.Y, _contentArea.Right, _contentArea.Y, Color.Black);

            // --- Draw Active Tab Content ---
            if (_activeTab == Tab.Tiles)
            {
                DrawTilesTabContent(sb, font);
            }
            else if (_activeTab == Tab.Objects)
            {
                sb.DrawString(font, "Object Browser - Future", _contentArea.Location.ToVector2() + new Vector2(10, 10), Color.White);
            }
        }

        private void DrawTilesTabContent(SpriteBatch sb, SpriteFont font)
        {
            var panelState = _editorState.TilesetPanel;
            var TileSets = _editorState.ActiveTileSets;
            // Draw the '+' button and the list of active tilesets
            _addTilesetButton.Draw(sb, _editorUI);
            foreach (var tile in tileSets)
            {
                Color color; 
                tile.Draw(sb,_editorUI);
                if (tile.IsHovered) color = Color.Yellow; else color = Color.White; 
                    sb.DrawString(font, tile.IconName, tile.Bounds.Center.ToVector2(), color);
            }

            var activeTileset = TileSets.FirstOrDefault(ts => ts.Name == panelState.ActiveTilesetName);
            if (activeTileset != null)
            {
                // Use a scissor rectangle to clip the scrolling tile grid
                var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
                sb.End();
                var scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };
                sb.Begin(rasterizerState: scissorRasterizerState);
                sb.GraphicsDevice.ScissorRectangle = _tileDisplayArea;

                int tilesPerRow = (_tileDisplayArea.Width) / TILE_DISPLAY_SIZE;
                int index = 0;
                foreach (var tilePair in activeTileset.SlicedAtlas.OrderBy(p => p.Key))
                {
                    int col = index % tilesPerRow;
                    int row = index / tilesPerRow;
                    var destRect = new Rectangle(
                        _tileDisplayArea.X + col * TILE_DISPLAY_SIZE,
                        _tileDisplayArea.Y + row * TILE_DISPLAY_SIZE - (int)_scrollOffset,
                        TILE_DISPLAY_SIZE, TILE_DISPLAY_SIZE);

                    sb.Draw(tilePair.Value, destRect, Color.White);
                    sb.DrawRectangle(destRect, Color.Black * 0.5f, 1);

                    if (tilePair.Key == panelState.SelectedTileID)
                        sb.DrawRectangle(destRect, Color.Yellow, 2);

                    index++;
                }

                sb.End();
                sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
                sb.Begin();
            }
        }
    }
    public class ToolPanel : BasePanel
    {
        private readonly List<Button> _buttons = new List<Button>();
        private ToolState ToolStack;
        public ToolPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            ToolStack = editorState.ToolState;
        }

        public void InitializeButtons() // Call this after ToolManager is created
        {
            _buttons.Clear();
            var toolNames = ToolStack.Tools.Select(t => t.Name).ToList();
            for (int i = 0; i < toolNames.Count; i++)
            {
                var name = toolNames[i];
                var rect = new Rectangle(Area.X + 5 + i * 35, Area.Y + 4, 32, 32);
                _buttons.Add(new Button(rect, new ChangeToolCommand { ToolName = name }, name));
            }
        }

        public override void Update(InputState input, EventBus bus)
        {

            ToolStack.HoveredButtonName = null; // Reset hover state for this frame.

            if (!Area.Contains(input.MouseWindowPosition)) return;

            foreach (var button in _buttons)
            {
                if (button.Update(input))
                {
                    ToolStack.HoveredButtonName = button.IconName;

                    if (input.IsNewLeftClick)// && ToolStack.HoveredButtonName != null)
                    {
                        ToolStack.ActiveToolName = button.IconName;
                        ToolStack.ActiveTool = ToolStack.Tools.FirstOrDefault(t => t.Name == ToolStack.ActiveToolName);
                        bus.Publish(button.CommandToPublish);
                    }
                }
            }

        }

        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.Gray * 0.5f);
            foreach (var button in _buttons)
            {
                bool isActive = ToolStack.ActiveToolName == button.IconName.ToString();
                bool isHovered = ToolStack.HoveredButtonName == button.IconName.ToString();

                // Draw visual feedback based on state
                if (isActive)
                {
                    sb.FillRectangle(button.Bounds, Color.CornflowerBlue * 0.7f);
                    sb.DrawRectangle(button.Bounds, Color.White, 1);
                }
                else if (isHovered)
                {
                    sb.FillRectangle(button.Bounds, Color.White * 0.2f);
                }

                _editorUI.DrawIcon(sb, button.Bounds, button.IconName, Color.White);
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
            LayerPanel = new LayerPanel(_editorState._layoutmanager.LayerPanel,this,editorState);
            ToolPanel = new ToolPanel(_editorState._layoutmanager.ToolPanel, this, editorState);
            ToolPanel.InitializeButtons();
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