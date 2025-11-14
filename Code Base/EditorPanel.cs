using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace Pixel_Simulations
{

    public class TilesetPanel
    {
        public Rectangle Area { get; private set; }
        public TileInfo? ActiveBrush { get; private set; }

        // Public properties for the debug menu
        public int SelectedTileID { get; private set; } = -1;
        public Point HoveredTileCell { get; private set; } = new Point(-1, -1);

        private TilesetManager _tilesetManager;
        private TileSet _selectedTileset;
        private float _scrollOffset = 0;
        private float _maxScroll = 0;

        private Rectangle _tilesetListArea;
        private Rectangle _tileDisplayArea;

        // Constants for drawing
        private const int MARGIN = 10;
        private const int DISPLAY_TILE_SIZE = 24;
        private const int TILESET_LIST_ITEM_HEIGHT = 10;

        public TilesetPanel(Rectangle area)
        {
            Area = area;
            // Split the panel area into two halves
            int listHeight = 60;
            _tilesetListArea = new Rectangle(area.X, area.Y, area.Width, listHeight);
            _tileDisplayArea = new Rectangle(area.X, area.Y + listHeight, area.Width, area.Height - listHeight);
        }

        public void SetManager(TilesetManager manager)
        {
            _tilesetManager = manager;
            SelectTileset(_tilesetManager.TileSets.Values.FirstOrDefault());
        }

        public void CycleToNextTileset()
        {
            if (_tilesetManager == null || _tilesetManager.TileSets.Count <= 1) return;

            var tilesetList = _tilesetManager.TileSets.Values.ToList();
            int currentIndex = tilesetList.IndexOf(_selectedTileset);
            int nextIndex = (currentIndex + 1) % tilesetList.Count;
            SelectTileset(tilesetList[nextIndex]);
        }

        private void SelectTileset(TileSet tileset)
        {
            _selectedTileset = tileset;
            if (_selectedTileset != null && _selectedTileset.Atlas.Any())
            {
                SelectedTileID = _selectedTileset.Atlas.Keys.First();
            }
            else
            {
                SelectedTileID = -1;
            }
            _scrollOffset = 0;
            UpdateActiveBrush();
            CalculateMaxScroll();
        }

        public void Update(MouseState mouse, MouseState prevMouse)
        {
            HoveredTileCell = new Point(-1, -1);
            if (!Area.Contains(mouse.Position) || _tilesetManager == null) return;

            if (_tileDisplayArea.Contains(mouse.Position))
            {
                HandleTileDisplayInteraction(mouse, prevMouse);
            }
            else if (_tilesetListArea.Contains(mouse.Position) && mouse.LeftButton == ButtonState.Pressed)// && prevMouse.LeftButton == ButtonState.Released)
            {
                HandleTilesetListInteraction(mouse);
            }
        }
        private void HandleTilesetListInteraction(MouseState mouse)
        {
            int index = (mouse.Y - _tilesetListArea.Y) / TILESET_LIST_ITEM_HEIGHT;
            if (index < _tilesetManager.TileSets.Count)
            {
                SelectTileset(_tilesetManager.TileSets.Values.ElementAt(index));
            }
        }

        private void HandleTileDisplayInteraction(MouseState mouse, MouseState prevMouse)
        {
            // Scrolling
            int scrollDelta = mouse.ScrollWheelValue - prevMouse.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                _scrollOffset -= scrollDelta * 0.5f;
                _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, _maxScroll);
            }

            // Hovering and Selecting
            if (_selectedTileset == null) return;

            int tilesPerRow = (_tileDisplayArea.Width - MARGIN * 2) / DISPLAY_TILE_SIZE;
            if (tilesPerRow == 0) return;

            int relX = mouse.X - _tileDisplayArea.X - MARGIN;
            int relY = mouse.Y - _tileDisplayArea.Y - MARGIN + (int)_scrollOffset;

            if (relX < 0 || relY < 0) return;

            int col = relX / DISPLAY_TILE_SIZE;
            int row = relY / DISPLAY_TILE_SIZE;

            if (col >= tilesPerRow) return;

            HoveredTileCell = new Point(col, row);
            int tileIndex = row * tilesPerRow + col;

            if(mouse.LeftButton == ButtonState.Pressed)// && prevMouse.LeftButton == ButtonState.Released)
            {
                if (tileIndex >= 0 && tileIndex < _selectedTileset.Atlas.Count)
                {
                    // If all checks pass, update the SelectedTileID.
                    SelectedTileID = _selectedTileset.Atlas.Keys.ElementAt(tileIndex);
                    UpdateActiveBrush();
                }
            }
        }

        private void UpdateActiveBrush()
        {
            ActiveBrush = (_selectedTileset != null && SelectedTileID != -1)
                ? new TileInfo(_selectedTileset.Name, SelectedTileID)
                : (TileInfo?)null;
        }

        private void CalculateMaxScroll()
        {
            if (_selectedTileset == null || _selectedTileset.Atlas.Count == 0)
            {
                _maxScroll = 0;
                return;
            }
            int tilesPerRow = (_tileDisplayArea.Width - MARGIN * 2) / DISPLAY_TILE_SIZE;
            int totalRows = (int)System.Math.Ceiling((double)_selectedTileset.Atlas.Count / tilesPerRow);
            int totalContentHeight = totalRows * DISPLAY_TILE_SIZE + MARGIN * 2;
            _maxScroll = System.Math.Max(0, totalContentHeight - _tileDisplayArea.Height);
        }

        public void Draw(SpriteBatch sb, SpriteFont font)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);
            sb.DrawLine(_tileDisplayArea.X, _tileDisplayArea.Y, _tileDisplayArea.Right, _tileDisplayArea.Y, Color.Black);

            // Draw Tileset List
            if (_tilesetManager?.TileSets != null)
            {
                int yOffset = 5;
                foreach (var tileset in _tilesetManager.TileSets.Values)
                {
                    Color color = (tileset == _selectedTileset) ? Color.Yellow : Color.White;
                    sb.DrawString(font, tileset.Name, new Vector2(_tilesetListArea.X + 5, _tilesetListArea.Y + yOffset), color);
                    yOffset += TILESET_LIST_ITEM_HEIGHT;
                }
            }

            // Draw Tiles from Selected Set (using a scissor rectangle for clipping)
            if (_selectedTileset != null)
            {
                var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
                sb.End();

                var scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };
                sb.Begin(rasterizerState: scissorRasterizerState);
                sb.GraphicsDevice.ScissorRectangle = _tileDisplayArea;

                int tilesPerRow = (_tileDisplayArea.Width - MARGIN * 2) / DISPLAY_TILE_SIZE;
                int index = 0;
                foreach (var tilePair in _selectedTileset.Atlas.OrderBy(p => p.Key))
                {
                    int col = index % tilesPerRow;
                    int row = index / tilesPerRow;
                    var destRect = new Rectangle(
                        _tileDisplayArea.X + MARGIN + col * DISPLAY_TILE_SIZE,
                        _tileDisplayArea.Y + MARGIN + row * DISPLAY_TILE_SIZE - (int)_scrollOffset,
                        DISPLAY_TILE_SIZE, DISPLAY_TILE_SIZE);

                    sb.Draw(tilePair.Value, destRect, Color.White);
                    sb.DrawRectangle(destRect, Color.Black * 0.5f, 1);

                    if (tilePair.Key == SelectedTileID)
                    {
                        sb.DrawRectangle(destRect, Color.Yellow, 2);
                    }

                    if (HoveredTileCell.X == col && HoveredTileCell.Y == row)
                    {
                        sb.DrawRectangle(destRect, Color.CornflowerBlue * 0.8f, 1);
                    }
                    index++;
                }

                sb.End();
                sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
                sb.Begin();
            }
        }
    }

    public class ToolPanel
    {
        public Rectangle Area { get; }

        private ToolManager _toolManager;
        private Texture2D _iconsTexture;
        private Dictionary<string, Rectangle> _iconSources;
        private const int ICON_SIZE = 32;
        public ToolPanel(Rectangle area, ToolManager toolManager)
        {
            Area = area;
            _toolManager = toolManager;
            _iconSources = new Dictionary<string, Rectangle>();
        }



        public void LoadContent(Texture2D iconsTexture)
        {
            _iconsTexture = iconsTexture;
            // Slice the 32x32 icons from the sheet
            _iconSources["Brush"] = new Rectangle(0, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["Eraser"] = new Rectangle(32, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["Fill"] = new Rectangle(64, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["Rectangle"] = new Rectangle(96, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["Line"] = new Rectangle(0, 32, ICON_SIZE, ICON_SIZE);
            _iconSources["Selection"] = new Rectangle(32, 32, ICON_SIZE, ICON_SIZE);
            _iconSources["CollisionBrush"] = new Rectangle(64, 32, ICON_SIZE, ICON_SIZE); // Note: using a different icon
            _iconSources["ObjectPlacer"] = new Rectangle(96, 32, ICON_SIZE, ICON_SIZE);
        }

        public void Update(MouseState mouse, MouseState prevMouse)
        {
            if (!Area.Contains(mouse.Position) || mouse.LeftButton != ButtonState.Pressed || prevMouse.LeftButton != ButtonState.Released)
                return;

            int iconIndex = (mouse.X - Area.X) / 32;
            if (iconIndex >= 0 && iconIndex < _toolManager.Tools.Count)
            {
                _toolManager.SetActiveTool(_toolManager.Tools[iconIndex].Name);
            }
        }

        public void Draw(SpriteBatch sb)
        {
            sb.FillRectangle(Area, Color.Black * 0.5f); // Dark background for the toolbar

            for (int i = 0; i < _toolManager.Tools.Count; i++)
            {
                var tool = _toolManager.Tools[i];
                if (_iconSources.TryGetValue(tool.Name, out var sourceRect))
                {
                    var destRect = new Rectangle(Area.X + i * 32, Area.Y, 32, 32);

                    // Highlight the active tool
                    if (tool == _toolManager.ActiveTool)
                    {
                        sb.FillRectangle(destRect, Color.CornflowerBlue * 0.7f);
                        sb.DrawRectangle(destRect, Color.White, 1);
                    }

                    sb.Draw(_iconsTexture, destRect, sourceRect, Color.White);
                }
            }
        }
    }

    public class LayerPanel
    {
        public Rectangle Area { get; }
        public EditorState _editorState;
        public bool ControlHover = false;
        private Texture2D _iconsTexture;
        private Dictionary<string, Rectangle> _iconSources;
        private float _scrollOffset = 0;

        private Rectangle _layerListArea;
        private Rectangle _controlsArea;
        private Dictionary<string, Rectangle> _controlButtonRects = new Dictionary<string, Rectangle>();

        private const int ROW_HEIGHT = 40;
        private const int ICON_SIZE = 32;
        private string _hoveredButton = null;
        public LayerPanel(Rectangle area, EditorState editorState)
        {
            Area = area;
            _editorState = editorState;
            _iconSources = new Dictionary<string, Rectangle>();

            int controlsHeight = 80;
            _layerListArea = new Rectangle(area.X, area.Y, area.Width, area.Height - controlsHeight);
            _controlsArea = new Rectangle(_layerListArea.X, _layerListArea.Y + _layerListArea.Height, area.Width, controlsHeight);

            // Pre-define the locations of the global control buttons
            _controlButtonRects["Add Up"] = new Rectangle(_controlsArea.X + 8, _controlsArea.Y + 8, ICON_SIZE, ICON_SIZE);
            _controlButtonRects["Add Down"] = new Rectangle(_controlsArea.X + 48, _controlsArea.Y + 8, ICON_SIZE, ICON_SIZE);
            _controlButtonRects["MoveUp"] = new Rectangle(_controlsArea.X + 88, _controlsArea.Y + 8, ICON_SIZE, ICON_SIZE);
            _controlButtonRects["MoveDown"] = new Rectangle(_controlsArea.X + 128, _controlsArea.Y + 8, ICON_SIZE, ICON_SIZE);
            _controlButtonRects["Delete"] = new Rectangle(_controlsArea.X + 128, _controlsArea.Y + 48, ICON_SIZE, ICON_SIZE);
        }

        public void LoadContent(Texture2D iconsTexture)
        {
            _iconsTexture = iconsTexture;
            // Your icon sheet has "AddUp" and "AddDown", we'll just use one for a generic "Add"
            _iconSources["Visible"] = new Rectangle(0, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["Hidden"] = new Rectangle(32, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["MoveUp"] = new Rectangle(64, 0, ICON_SIZE, ICON_SIZE);
            _iconSources["MoveDown"] = new Rectangle(96, 0, ICON_SIZE, ICON_SIZE);
            
            _iconSources["Add Up"] = new Rectangle(0, 32, ICON_SIZE, ICON_SIZE);
            _iconSources["Add Down"] = new Rectangle(32, 32, ICON_SIZE, ICON_SIZE);
            _iconSources["Delete"] = new Rectangle(64, 32, ICON_SIZE, ICON_SIZE);

            _iconSources["Unlocked"] = new Rectangle(0, 64, ICON_SIZE, ICON_SIZE);
            _iconSources["Locked"] = new Rectangle(32, 64, ICON_SIZE, ICON_SIZE);
        }

        public void Update(MouseState mouse, MouseState prevMouse)
        {
            // Phase 1: Reset hover state and check if mouse is in the panel at all.
            _hoveredButton = null;
            if (!Area.Contains(mouse.Position)) return;

            // Phase 2: Determine the current state based on mouse position (continuous actions).
            // This phase runs every frame, regardless of clicks.
            if (_layerListArea.Contains(mouse.Position))
            {
                // Handle Scrolling
                int scrollDelta = mouse.ScrollWheelValue - prevMouse.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    _scrollOffset -= scrollDelta * 0.2f;
                    _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, System.Math.Max(0, _editorState.ActiveMap.Layers.Count * ROW_HEIGHT - _layerListArea.Height));
                }
            }
            else if (_controlsArea.Contains(mouse.Position))
            {
                // Handle Hovering over control buttons to set the state for clicks and drawing.
                foreach (var button in _controlButtonRects)
                {
                    if (button.Value.Contains(mouse.Position))
                    {
                        _hoveredButton = button.Key;
                        break; // We found the hovered button, no need to check others.
                    }
                }
            }

            // Phase 3: Act on momentary events (like a single click).
            // This check is now the gatekeeper for all click actions.
            if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
            {
                if (_layerListArea.Contains(mouse.Position))
                {
                    HandleLayerListClick(mouse.Position); // This still needs position to find the layer index.
                }
                else if (_controlsArea.Contains(mouse.Position))
                {
                    // We now call the new, more robust HandleControlsClick method.
                    HandleControlsClick();
                }
            }
        }

        private void HandleLayerListClick(Point mousePos)
        {
            int index = (int)((mousePos.Y - _layerListArea.Y + _scrollOffset) / ROW_HEIGHT);
            if (index < 0 || index >= _editorState.ActiveMap.Layers.Count) return;

            var layer = _editorState.ActiveMap.Layers[index];
            var rowArea = new Rectangle(Area.X, Area.Y + index * ROW_HEIGHT - (int)_scrollOffset, Area.Width, ROW_HEIGHT);

            var visibilityRect = new Rectangle(rowArea.X + 5, rowArea.Y + 4, ICON_SIZE, ICON_SIZE);
            var lockRect = new Rectangle(rowArea.X + 40, rowArea.Y + 4, ICON_SIZE, ICON_SIZE);

            if (visibilityRect.Contains(mousePos)) layer.IsVisible = !layer.IsVisible;
            else if (lockRect.Contains(mousePos)) layer.IsLocked = !layer.IsLocked;
            else _editorState.ActiveLayerIndex = index;
        }

        private void HandleControlsClick()
        {
            // If we clicked in the controls area but not on a specific button, do nothing.
            if (_hoveredButton == null) return;

            // Use a switch statement for clarity and easy extension.
            switch (_hoveredButton)
            {
                case "Add Up":
                    _editorState.AddNewLayerUp();
                    break;
                case "Add Down":
                    _editorState.AddNewLayerDown();
                    _editorState.ActiveLayerIndex++;
                    break;
                case "Delete":
                    _editorState.DeleteActiveLayer();
                    break;
                case "MoveUp":
                    _editorState.MoveActiveLayerUp();
                    break;
                case "MoveDown":
                    _editorState.MoveActiveLayerDown();
                    break;
            }
        }


        public void Draw(SpriteBatch sb, SpriteFont font)
        {
            sb.FillRectangle(Area, Color.DarkSlateGray);

            // Use a ScissorRectangle to prevent the list from drawing over the controls
            var originalScissorRect = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            var scissorRasterizerState = new RasterizerState { ScissorTestEnable = true };
            sb.Begin(rasterizerState: scissorRasterizerState);
            sb.GraphicsDevice.ScissorRectangle = _layerListArea;

            for (int i = 0; i < _editorState.ActiveMap.Layers.Count; i++)
            {
                var layer = _editorState.ActiveMap.Layers[i];
                var rowArea = new Rectangle(Area.X, Area.Y + i * ROW_HEIGHT - (int)_scrollOffset, Area.Width, ROW_HEIGHT);

                if (i == _editorState.ActiveLayerIndex) sb.FillRectangle(rowArea, Color.CornflowerBlue * 0.3f);

                // Per-layer status icons
                var visibilityIcon = layer.IsVisible ? _iconSources["Visible"] : _iconSources["Hidden"];
                sb.Draw(_iconsTexture, new Vector2(rowArea.X + 5, rowArea.Y + 4), visibilityIcon, Color.White);

                var lockIcon = layer.IsLocked ? _iconSources["Locked"] : _iconSources["Unlocked"];
                sb.Draw(_iconsTexture, new Vector2(rowArea.X + 40, rowArea.Y + 4), lockIcon, Color.White);

                sb.DrawString(font, layer.Name, new Vector2(rowArea.X + 80, rowArea.Y + 10), Color.White);
                sb.DrawLine(rowArea.X, rowArea.Bottom, rowArea.Right, rowArea.Bottom, Color.Black * 0.5f);
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = originalScissorRect;
            sb.Begin();

            // Draw global controls
            sb.FillRectangle(_controlsArea, Color.Black * 0.7f);
            sb.Draw(_iconsTexture, _controlButtonRects["Add Up"], _iconSources["Add Up"], Color.White);
            sb.Draw(_iconsTexture, _controlButtonRects["Add Down"], _iconSources["Add Down"], Color.White);
            sb.Draw(_iconsTexture, _controlButtonRects["Delete"], _iconSources["Delete"], Color.White);
            sb.Draw(_iconsTexture, _controlButtonRects["MoveUp"], _iconSources["MoveUp"], Color.White);
            sb.Draw(_iconsTexture, _controlButtonRects["MoveDown"], _iconSources["MoveDown"], Color.White);

            foreach (var button in _controlButtonRects)
            {
                Color tint = Color.White;
                if (_hoveredButton == button.Key)
                {
                    tint = Color.Red; // Highlight with red tint
                                      // You can also draw a highlight rectangle
                    sb.FillRectangle(button.Value, Color.Red * 0.3f);
                }
                sb.Draw(_iconsTexture, button.Value, _iconSources[button.Key], tint);
            }
        }
    }
}
