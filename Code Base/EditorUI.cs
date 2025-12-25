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
using System.IO;
using System.Linq;


namespace Pixel_Simulations.UI
{
    public class IconDefinition { public int x { get; set; } public int y { get; set; } }
    public interface IPanel
    {
        void Update(InputState input);
        void Draw(SpriteBatch spriteBatch);
    }
    public class Button
    {
        public Rectangle Bounds { get; }
        public string ActionName { get; } // e.g., "Brush", "SaveMap"
        public string IconName { get; }
        public bool IsHovered { get; private set; }

        public Button(Rectangle bounds, string actionName, string iconName)
        {
            Bounds = bounds;
            ActionName = actionName;
            IconName = iconName;
        }

        public bool Update(InputState input)
        {
            IsHovered = Bounds.Contains(input.MouseWindowPosition);

            // Check for a *new* left click event.
            if (IsHovered && input.IsNewLeftClick())
            {
                // Fire the appropriate event based on the button's purpose.
                // This is a simplification; a better way is to pass the event type to the button's constructor.
                if (ActionName == "Brush" || ActionName == "Eraser") // Example check
                {
                    UIEvents.OnToolButtonClicked(ActionName);
                }
                else
                {
                    UIEvents.OnMenuActionClicked(ActionName);
                }
            }
            return IsHovered;
        }

        public void Draw(SpriteBatch sb, EditorUI editorUI)
        {
            Color tint = IsHovered ? Color.Yellow : Color.White;
            if (IsHovered) sb.FillRectangle(Bounds, Color.White * 0.2f);
            sb.DrawRectangle(Bounds, tint);
            editorUI.DrawIcon(sb, Bounds, IconName, tint);
        }


    }
    public class PopupWindow
    {
        public Rectangle Bounds { get; }
        public string Title { get; }
        public List<string> Items { get; }
        public string SelectedItem { get; private set; }

        public PopupWindow(string title, List<string> items, LayoutManager layout)
        {
            Title = title;
            Items = items;

            // Center the popup in the main viewport
            int width = 300;
            int height = 200;
            Bounds = new Rectangle(
                layout.ViewportPanel.X + (layout.ViewportPanel.Width - width) / 2,
                layout.ViewportPanel.Y + (layout.ViewportPanel.Height - height) / 2,
                width, height);
        }

        public void Update(InputState input)
        {
            SelectedItem = null;
            if (!Bounds.Contains(input.MouseWindowPosition)) return;

            if (input.IsNewLeftClick())
            {
                int index = (int)((input.MouseWindowPosition.Y - Bounds.Y - 30) / 20); // 30px for title
                if (index >= 0 && index < Items.Count)
                {
                    SelectedItem = Items[index];
                }
            }
        }

        public void Draw(SpriteBatch sb, SpriteFont font)
        {
            // Draw a semi-transparent overlay behind the popup
            sb.FillRectangle(new Rectangle(0, 0, 2000, 2000), Color.Black * 0.5f);

            // Draw the popup window
            sb.FillRectangle(Bounds, Color.DarkSlateGray);
            sb.DrawRectangle(Bounds, Color.Black, 2);
            sb.DrawString(font, Title, Bounds.Location.ToVector2() + new Vector2(5, 5), Color.White);

            // Draw the list of items
            int yOffset = 30;
            foreach (var item in Items)
            {
                var pos = new Vector2(Bounds.X + 10, Bounds.Y + yOffset);
                sb.DrawString(font, item, pos, Color.White);
                yOffset += 20;
            }
        }
    }
    public abstract class BasePanel : IPanel
    {
        protected Rectangle Area { get; }
        protected EditorUI EditorUI { get; }
        protected EditorState EditorState { get; }

        protected BasePanel(Rectangle area, EditorUI editorUI, EditorState editorState) 
        {
            Area = area;
            EditorUI = editorUI;
            EditorState = editorState;
        }

        public abstract void Update(InputState input);
        public abstract void Draw(SpriteBatch spriteBatch);
    }
    public class TopPanel : BasePanel // BasePanel holds Area, EditorUI, EditorState
    {
        private readonly List<Button> _buttons = new List<Button>();

        public TopPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            var state = editorState.TopPanel;
            for (int i = 0; i < state.ButtonNames.Count; i++)
            {
                var rect = new Rectangle(Area.X + i * 32, Area.Y, 32, 32);
                // The button's action name and icon name are the same
                _buttons.Add(new Button(rect, state.ButtonNames[i], state.ButtonNames[i]));
            }
        }

        public override void Update(InputState input)
        {
            if (!Area.Contains(input.MouseWindowPosition))
            {
                EditorState.TopPanel.HoveredButton = null;
                return;
            }
            
            EditorState.TopPanel.HoveredButton = null;
            foreach (var button in _buttons)
            {
                button.Update(input);
                if (button.IsHovered)
                {
                    EditorState.TopPanel.HoveredButton = button.ActionName;
                }
                else
                {
                    EditorState.TopPanel.HoveredButton = null;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.FillRectangle(Area, Color.Gray * 0.3f);
            foreach (var button in _buttons)
            {
                // The button's internal state (IsHovered) now drives its drawing
                button.Draw(spriteBatch, EditorUI);
            }
        }
    }
    public class TilesetPanel : BasePanel
    {
        private readonly PopupWindow _atlasPickerPopup;
        private readonly Button _addTilesetButton;
        private const int MARGIN = 8;
        private const int DISPLAY_TILE_SIZE = 32;
        private const int TILESET_LIST_ITEM_HEIGHT = 20;
        private const int TILESET_LIST_ITEM_WIDTH = 40;
        private const int TILE_DISPLAY_MARGIN = 8;
        private const int TILE_DISPLAY_SIZE = 32;
        public enum Tab { Tiles, Objects } // Enum for managing the tab state
        private Tab _activeTab = Tab.Tiles; // The currently active tab

        private Rectangle _tilesetListArea; // Area for listing the tileset names
        private Rectangle _tileDisplayArea; // Area for displaying the tiles of the selected tileset
        private float _scrollOffset = 0;
        public int SelectedTileID { get; private set; } = -1;
        public Point HoveredTileCell { get; private set; } = new Point(-1, -1); // For debug info

        // Constants for layout
        
        public TilesetPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            _tileDisplayArea = new Rectangle(Area.X + MARGIN, Area.Y + MARGIN, Area.Width - 2* MARGIN, Area.Height - TILE_DISPLAY_SIZE - 2* MARGIN);
            _tilesetListArea = new Rectangle(Area.X + MARGIN + TILE_DISPLAY_SIZE, Area.Height - MARGIN - 32 , Area.Width - 2*TILE_DISPLAY_MARGIN - DISPLAY_TILE_SIZE , DISPLAY_TILE_SIZE);
            _addTilesetButton = new Button(new Rectangle(_tilesetListArea.X - TILE_DISPLAY_SIZE , _tilesetListArea.Y, _tilesetListArea.Height, _tilesetListArea.Height),"ShowAtlasPicker", "New");
        }

        public override void Update(InputState input)
        {
            // Reset hover state for this frame.
            HoveredTileCell = new Point(-1, -1);
            var panelState = EditorState.TilesetPanel; // Get the relevant state object

            if (!Area.Contains(input.MouseWindowPosition)) return;

            // --- Tab Interaction ---
            if (_tileDisplayArea.Contains(input.MouseWindowPosition))   { _activeTab = Tab.Tiles;}
            else    { _activeTab = Tab.Objects;}

            if (_activeTab == Tab.Tiles)
            {
                // Handle Scrolling in the tile display area
                if (_tileDisplayArea.Contains(input.MouseWindowPosition))
                {
                    int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                    if (scrollDelta != 0)
                    {
                        _scrollOffset -= scrollDelta * 0.5f;
                        // Clamp scroll offset later with _maxScroll
                    }
               
                    var activeTileset = EditorState.ActiveTileSets.FirstOrDefault(ts => ts.Name == panelState.ActiveTilesetName);
                    if (activeTileset != null)
                    {
                        int tilesPerRow = _tileDisplayArea.Width / TILE_DISPLAY_SIZE;
                        if (tilesPerRow > 0)
                        {
                            int relX = (int)input.MouseWindowPosition.X - _tileDisplayArea.X;
                            int relY = (int)input.MouseWindowPosition.Y - _tileDisplayArea.Y + (int)_scrollOffset;
                            int col = relX / TILE_DISPLAY_SIZE;
                            int row = relY / TILE_DISPLAY_SIZE;
                            HoveredTileCell = new Point(col, row);

                            if (input.IsNewLeftClick())
                            {
                                int tileIndex = row * tilesPerRow + col;
                                if (relX >= 0 && col < tilesPerRow && tileIndex >= 0 && tileIndex < activeTileset.SlicedAtlas.Count)
                                {
                                    // Fire an event to tell the controller to change the selected tile
                                    int tileId = activeTileset.SlicedAtlas.Keys.ElementAt(tileIndex);
                                    UIEvents.OnTileSelected(activeTileset.Name, tileId);
                                }
                            }
                        }
                    }
                }
            }
            else if(_activeTab == Tab.Objects)
            {
                _addTilesetButton.Update(input);
                // Handle Hovering and Clicking
                if (_tilesetListArea.Contains(input.MouseWindowPosition))
                {
                    if (input.IsNewLeftClick())
                    {
                        int index = (int)((input.MouseWindowPosition.Y - _tilesetListArea.Y) / TILESET_LIST_ITEM_HEIGHT);
                        if (index >= 0 && index < EditorState.ActiveTileSets.Count)
                        {
                            // Fire an event to tell the controller to change the active tileset
                            UIEvents.OnTilesetSelected(EditorState.ActiveTileSets[index].Name);
                        }
                    }
                }
            }
        }

        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);
            _addTilesetButton.Draw(sb, EditorUI);
            if (EditorState.ShowDebug) 
            { 
                sb.DrawRectangle(_tilesetListArea, Color.Black);
                sb.DrawRectangle(_tileDisplayArea, Color.White);
            }

            var panelState = EditorState.TilesetPanel;
            var font = EditorUI.DebugFont; // Assuming EditorUI has the font

            // Draw list of active tilesets
            int xOffset = 40;
            foreach (var tileset in EditorState.ActiveTileSets)
            {
                Color color = (tileset.Name == panelState.ActiveTilesetName) ? Color.Yellow : Color.White;
                sb.DrawString(font, tileset.Name, new Vector2(_addTilesetButton.Bounds.X + xOffset, _addTilesetButton.Bounds.Y), color);
                xOffset += TILESET_LIST_ITEM_WIDTH;
            }

            // Draw the divider
            int dividerY = Area.Y; // Fixed position for the divider
            sb.DrawLine(Area.X, dividerY, Area.Right, dividerY, Color.Black);

            // Draw the tiles from the active tileset
            var activeTileset = EditorState.ActiveTileSets.FirstOrDefault(ts => ts.Name == panelState.ActiveTilesetName);
            if (activeTileset != null)
            {
                // Scissor rect logic here to clip the tile display
                // ...

                int tilesPerRow = _tileDisplayArea.Width / TILE_DISPLAY_SIZE;
                int index = 0;
                foreach (var tilePair in activeTileset.SlicedAtlas.OrderBy(p => p.Key))
                {
                    int col = index % tilesPerRow;
                    int row = index / tilesPerRow;
                    var destRect = new Rectangle(
                        _tileDisplayArea.X + col * TILE_DISPLAY_SIZE,
                        _tileDisplayArea.Y + row * TILE_DISPLAY_SIZE, // - (int)_scrollOffset,
                        TILE_DISPLAY_SIZE, TILE_DISPLAY_SIZE);

                    sb.Draw(tilePair.Value, destRect, Color.White);
                    sb.DrawRectangle(destRect, Color.Black * 0.5f, 1);

                    if (tilePair.Key == panelState.SelectedTileID)
                        sb.DrawRectangle(destRect, Color.Yellow, 2);

                    index++;
                }
            }

            // Draw popup if it's active
            if (panelState.IsAtlasPickerVisible)
            {
                _atlasPickerPopup?.Draw(sb, font);
            }
        }
        private void HandleTileSelection(string tilesetName, int tileId)
        {
            var panelState = EditorState.TilesetPanel;
            panelState.ActiveTilesetName = tilesetName;
            panelState.SelectedTileID = tileId;

            // The selection state's brush is now just a new TileInfo object.
            EditorState.Selection.ActiveTileBrush = new TileInfo(tilesetName, tileId);
        }
        private void UpdateActiveBrush()
        {
            var _selectedTileID = EditorState.TilesetPanel.SelectedTileID;
            var _selectedTileset = EditorState.TilesetPanel.TilesetNames;
            if (_selectedTileset != null && _selectedTileID != -1)
            {
                EditorState.Selection.ActiveTileBrush = new TileInfo(_selectedTileset[SelectedTileID], _selectedTileID);
            }
            else
            {
                EditorState.Selection.ActiveTileBrush = null; // Set to null if nothing is selected
            }
        }
    }
    public class ToolPanel : BasePanel
    {
        private readonly List<Button> _buttons = new List<Button>();

        public ToolPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState)
        {
            var state = editorState.ToolPanel;
            // The ToolManager will populate state.ButtonNames
            // This is just a placeholder for initialization
            for (int i = 0; i < 16; i++) // Assume max 16 tools
            {
                var rect = new Rectangle(Area.X + 5 + i * 35, Area.Y + 4, 32, 32);
                // We'll create the real buttons later when the tool list is known
            }
        }

        public void InitializeButtons() // Call this after ToolManager is created
        {
            _buttons.Clear();
            var state = EditorState.ToolPanel;
            for (int i = 0; i < state.ButtonNames.Count; i++)
            {
                var name = state.ButtonNames[i];
                var rect = new Rectangle(Area.X + 5 + i * 35, Area.Y + 4, 32, 32);
                _buttons.Add(new Button(rect, name, name)); // ActionName and IconName are the same
            }
        }

        public override void Update(InputState input)
        {
            var panelState = EditorState.ToolPanel;
            //panelState.HoveredButton = null;

            if (!Area.Contains(input.MouseWindowPosition)) return;

            // We can use the existing Button components to handle everything.
            // The InitializeButtons method from before should have populated the _buttons list.
            foreach (var button in _buttons)
            {
                // The button's own Update method checks for hover and clicks.
                // It will automatically fire the OnToolButtonClicked event.
                if (button.Update(input)) // button.Update returns true if hovered
                {
                    panelState.HoveredButton = button.ActionName;
                    
                }
                //else { panelState.HoveredButton = null; }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.FillRectangle(Area, Color.Black * 0.5f);
            foreach (var button in _buttons)
            {
                // Highlight if active or hovered
                bool isActive = EditorState.ToolPanel.ActiveToolName == button.ActionName;
                if (isActive)
                {
                    spriteBatch.FillRectangle(button.Bounds, Color.CornflowerBlue * 0.7f);
                }
                button.Draw(spriteBatch, EditorUI);
            }
        }
    }
    public class LayerPanel : BasePanel
    {
        private readonly Rectangle _layerListArea;
        private readonly Rectangle _controlsArea;
        private readonly Dictionary<string, Button> _controlButtons = new Dictionary<string, Button>();
        
        private float _scrollOffset = 0;
        private const int ROW_HEIGHT = 40;
        private const int ICON_SIZE = 32;

        public LayerPanel(Rectangle area, EditorUI editorUI, EditorState editorState)
            : base(area, editorUI, editorState) 
        {
            int controlsHeight = 40;
            _controlsArea = new Rectangle(area.X, area.Bottom - controlsHeight, area.Width, controlsHeight);
            _layerListArea = new Rectangle(area.X, area.Y, area.Width, area.Height - controlsHeight);

            // Define the global control buttons
            _controlButtons["AddUp"] = new Button(new Rectangle(_controlsArea.X + 5, _controlsArea.Y + 4, ICON_SIZE, ICON_SIZE), "AddUp", "AddUp");
            _controlButtons["AddDown"] = new Button(new Rectangle(_controlsArea.X + 40, _controlsArea.Y + 4, ICON_SIZE, ICON_SIZE), "AddDown", "AddDown");
            _controlButtons["Delete"] = new Button(new Rectangle(_controlsArea.X + 75, _controlsArea.Y + 4, ICON_SIZE, ICON_SIZE), "Delete", "Delete");
        }

        public override void Update(InputState input)
        {
            var panelState = EditorState.Layers;
            panelState.HoveredButton = null;
            if (!Area.Contains(input.MouseWindowPosition)) return;

            // --- Update Global Control Buttons ---
            if (_controlsArea.Contains(input.MouseWindowPosition))
            {
                foreach (var button in _controlButtons.Values)
                {
                    // The button's update checks for hover and fires a generic LayerActionRequested event on click.
                    if (button.Update(input))
                    {
                        panelState.HoveredButton = button.ActionName;
                    }
                }
            }
            
            // --- Update Layer List ---
            if (_layerListArea.Contains(input.MouseWindowPosition))
            {
                // Scrolling
                int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
                if (scrollDelta != 0) _scrollOffset -= scrollDelta * 0.2f;
                _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, System.Math.Max(0, EditorState.Layers.Layers.Count * ROW_HEIGHT - _layerListArea.Height));

                // Clicking
                if (input.IsNewLeftClick())
                {
                    HandleLayerListClick(input.MouseWindowPosition.ToPoint());
                }
            }
        }

        private void HandleLayerListClick(Point mousePos)
        {
            int index = (int)((mousePos.Y - _layerListArea.Y + _scrollOffset) / ROW_HEIGHT);
            if (index < 0 || index >= EditorState.Layers.Layers.Count) return;

            // Define clickable hitboxes for the per-layer icons
            var rowArea = new Rectangle(Area.X, Area.Y + index * ROW_HEIGHT - (int)_scrollOffset, Area.Width, ROW_HEIGHT);
            var visibilityRect = new Rectangle(rowArea.X + 5, rowArea.Y + 4, ICON_SIZE, ICON_SIZE);
            var lockRect = new Rectangle(rowArea.X + 40, rowArea.Y + 4, ICON_SIZE, ICON_SIZE);
            var moveUpRect = new Rectangle(rowArea.Right - 70, rowArea.Y + 4, ICON_SIZE, ICON_SIZE);
            var moveDownRect = new Rectangle(rowArea.Right - 38, rowArea.Y + 4, ICON_SIZE, ICON_SIZE);

            if (visibilityRect.Contains(mousePos)) UIEvents.OnLayerVisibilityToggled(index);
            else if (lockRect.Contains(mousePos)) UIEvents.OnLayerLockToggled(index);
            else if (moveUpRect.Contains(mousePos)) UIEvents.OnLayerActionRequested($"MoveUp:{index}"); // Pass index in action name
            else if (moveDownRect.Contains(mousePos)) UIEvents.OnLayerActionRequested($"MoveDown:{index}");
            else UIEvents.OnLayerSelected(index); // If no icon was clicked, select the layer
        }

        public override void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);
            var font = EditorUI.DebugFont;
            
            // Use a ScissorRectangle for the scrollable list
            var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            var scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };
            sb.Begin(rasterizerState: scissorRasterizerState);
            sb.GraphicsDevice.ScissorRectangle = _layerListArea;
            
            for (int i = 0; i < EditorState.Layers.Layers.Count; i++)
            {
                var layer = EditorState.Layers.Layers[i];
                var rowArea = new Rectangle(Area.X, Area.Y + i * ROW_HEIGHT - (int)_scrollOffset, Area.Width, ROW_HEIGHT);
                
                if(i == EditorState.Layers.ActiveLayerIndex) sb.FillRectangle(rowArea, Color.CornflowerBlue * 0.3f);
                
                // Per-layer icons
                EditorUI.DrawIcon(sb, new Rectangle(rowArea.X + 5, rowArea.Y + 4, ICON_SIZE, ICON_SIZE), layer.IsVisible ? "Visible" : "Hidden", Color.White);
                EditorUI.DrawIcon(sb, new Rectangle(rowArea.X + 40, rowArea.Y + 4, ICON_SIZE, ICON_SIZE), layer.IsLocked ? "Locked" : "Unlocked", Color.White);
                EditorUI.DrawIcon(sb, new Rectangle(rowArea.Right - 70, rowArea.Y + 4, ICON_SIZE, ICON_SIZE), "MoveUp", Color.White);
                EditorUI.DrawIcon(sb, new Rectangle(rowArea.Right - 38, rowArea.Y + 4, ICON_SIZE, ICON_SIZE), "MoveDown", Color.White);
                
                sb.DrawString(font, layer.Name, new Vector2(rowArea.X + 80, rowArea.Y + 10), Color.White);
                sb.DrawLine(rowArea.X, rowArea.Bottom, rowArea.Right, rowArea.Bottom, Color.Black * 0.5f);
            }
            
            sb.End();
            sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
            sb.Begin();

            // Draw global controls
            sb.FillRectangle(_controlsArea, Color.Black * 0.5f);
            foreach(var button in _controlButtons.Values)
            {
                button.Draw(sb, EditorUI);
            }
        }
    }
    public static class UIEvents
    {
        // Example: An event that is raised when a toolbar button is clicked.
        // The string argument will be the name of the tool to activate.
        public static event Action<string> ToolButtonClicked;
        public static void OnToolButtonClicked(string toolName) => ToolButtonClicked?.Invoke(toolName);
        public static event Action<string> MenuActionClicked;
        public static void OnMenuActionClicked(string actionName) => MenuActionClicked?.Invoke(actionName);
        public static event Action<string> CreateTilesetFromAtlas;
        public static void OnCreateTilesetFromAtlas(string atlasName) => CreateTilesetFromAtlas?.Invoke(atlasName);
        public static event Action<string> TilesetSelected;
        public static void OnTilesetSelected(string tilesetName) => TilesetSelected?.Invoke(tilesetName);
        public static event Action<string, int> TileSelected;
        public static void OnTileSelected(string tilesetName, int tileId) => TileSelected?.Invoke(tilesetName, tileId);
        public static event Action<int> LayerSelected;
        public static void OnLayerSelected(int layerIndex) => LayerSelected?.Invoke(layerIndex);
        public static event Action<int> LayerVisibilityToggled;
        public static void OnLayerVisibilityToggled(int layerIndex) => LayerVisibilityToggled?.Invoke(layerIndex);
        public static event Action<int> LayerLockToggled;
        public static void OnLayerLockToggled(int layerIndex) => LayerLockToggled?.Invoke(layerIndex);
        public static event Action<string> LayerActionRequested;
        public static void OnLayerActionRequested(string actionName) => LayerActionRequested?.Invoke(actionName);
        public static event Action<ICommand> CommandCreated;
        public static void OnCommandCreated(ICommand command) => CommandCreated?.Invoke(command);

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
        public EditorUI(GraphicsDevice graphicsDevice, EditorState editorState)
        {
            _editorState = editorState;
            IconSources = new Dictionary<string, Rectangle>();
            gd = graphicsDevice;
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
            if (_editorState.ShowDebug == true)
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

        public GridRenderer(int cell)
        {
            CELL_SIZE = cell;
        }
        public void Draw(SpriteBatch spriteBatch, EditorCamera camera, Rectangle viewportBounds)
        {
            // Get the visible area of the world from the camera
            RectangleF visibleWorld = camera.GetVisibleWorldBounds(viewportBounds);
            
            // Calculate the start and end points for the grid lines, snapped to the grid
            float left = (float)System.Math.Floor(visibleWorld.Left / CELL_SIZE) * CELL_SIZE;
            float top = (float)System.Math.Floor(visibleWorld.Top / CELL_SIZE) * CELL_SIZE;
            float right = visibleWorld.Right;
            float bottom = visibleWorld.Bottom;

            // Compensate line thickness for zoom to keep lines crisp
            float lineThickness = 1f * camera.Zoom;

            // Draw vertical lines
            for (float x = left; x < right; x += CELL_SIZE)
            {
                spriteBatch.DrawLine(x, top, x, bottom, _gridColor, lineThickness);
            }

            // Draw horizontal lines
            for (float y = top; y < bottom; y += CELL_SIZE)
            {
                spriteBatch.DrawLine(left, y, right, y, _gridColor, lineThickness);
            }

            // Draw a thicker line at the world origin (0,0) for reference
            spriteBatch.DrawLine(0, top, 0, bottom, Color.Red * 0.5f, lineThickness * 2);
            spriteBatch.DrawLine(left, 0, right, 0, Color.LimeGreen * 0.5f, lineThickness * 2);
        }
    }
}