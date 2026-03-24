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

    public class EditorUI
    {
        private Texture2D _iconsTexture;
        public Dictionary<string, Rectangle> IconSources { get; private set; }

        public TopPanel TopPanel { get; set; }
        public TilesetPanel TilesetPanel { get; set; }
        public PrefabCreatorPanel PrefabPanel { get; set; }
        public TagManagerPanel TagManagerPanel { get; set; }
        public ToolPanel ToolPanel { get; set; }
        public InspectorPanel InspectorPanel { get; set; }
        public MaskEditorPanel MaskEditorPanel { get; set; }
        public LayerPanel LayerPanel { get; set; }
        public readonly EditorState _editorState;
        public GraphicsDevice gd { get; }
        public SpriteFont DebugFont { get; private set; }
        // It will contain a list of all panels: Viewport, ToolPanel, LayerPanel, etc.
        private GridRenderer gridRenderer { get; set; }
        public bool IsMouseOverUI { get; private set; } // Prevents map clicks

        public EditorUI(EditorState editorState)
        {
            _editorState = editorState;
            IconSources = new Dictionary<string, Rectangle>();
            gd = editorState._graphics;
            
        }

        public void LoadContent(ContentManager content) 
        {
            DebugFont = content.Load<SpriteFont>("Font");
            UITheme.DefaultFont = DebugFont;
            _iconsTexture = content.Load<Texture2D>("EditorUI");
            string json = File.ReadAllText("Content/EditorIcons.json");
            var defFile = JsonConvert.DeserializeObject<dynamic>(json);
            int iconSize = defFile.icon_size;
            var icons = defFile.icons.ToObject<Dictionary<string, IconDefinition>>();
            foreach (var iconPair in icons)
            {
                IconSources[iconPair.Key] = new Rectangle(iconPair.Value.x, iconPair.Value.y, iconSize, iconSize);
            }

            // Create all the panel instances
            TopPanel = new TopPanel(_editorState._layoutmanager.TopPanel, this, _editorState);
            TilesetPanel = new TilesetPanel(_editorState._layoutmanager.TilesetPanel, this, _editorState);
            PrefabPanel = new PrefabCreatorPanel(_editorState._layoutmanager.prefabModalBounds, this, _editorState);
            LayerPanel = new LayerPanel(_editorState._layoutmanager.LayerPanel, this, _editorState);
            ToolPanel = new ToolPanel(_editorState._layoutmanager.ToolPanel, this, _editorState);
            InspectorPanel = new InspectorPanel(_editorState._layoutmanager.InspectorPanel, this, _editorState);
            MaskEditorPanel = new MaskEditorPanel(_editorState._layoutmanager.InspectorPanel, this, _editorState);
            TagManagerPanel = new TagManagerPanel(_editorState._layoutmanager.tagModalBounds, this, _editorState);
            gridRenderer = new GridRenderer(_editorState.CELL_SIZE);
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

            var activeLayer = _editorState.Layers.GetActiveLayer();
            if (activeLayer != null && activeLayer.Type == LayerType.Mask)
            {
                MaskEditorPanel.Draw(spriteBatch);
            }
            else
            {
                InspectorPanel.Draw(spriteBatch);
            }

            if (_editorState.IsTagManagerOpen) TagManagerPanel.Draw(spriteBatch);
            else LayerPanel.Draw(spriteBatch);

            if (_editorState.PrefabCreator.IsOpen) PrefabPanel.Draw(spriteBatch);
        }
        public void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color)
        {
            if (IconSources.TryGetValue(iconName, out var sourceRect))
            {
                sb.Draw(_iconsTexture, destination, sourceRect, color);
            }
        }
        public void SetFocus(UIElement element)
        {
            // 1. Unfocus the old element
            if (_editorState.UI.FocusedElement != null)
                _editorState.UI.FocusedElement.IsFocused = false;

            // 2. Update the central state
            _editorState.UI.FocusedElement = element;

            // 3. Focus the new element
            if (_editorState.UI.FocusedElement != null)
                _editorState.UI.FocusedElement.IsFocused = true;
        }

        public void CheckFocusClick(UIElement root, EditorInputState input)
        {
            if (!input.IsNewLeftClick) return;

            var hit = FindElementAt(root, input.MouseWindowPosition);

            // If we clicked a textbox, focus it. 
            // If we clicked empty space (null) or a simple Panel/Label, unfocus.
            // (We don't unfocus if we click a UIButton, so we can click "Save" while still typing!)
            if (hit is UITextBox)
                SetFocus(hit);
            else if (hit == null || !(hit is UIButton))
                SetFocus(null);
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
        public Rectangle InspectorPanel { get; private set; }
        public Rectangle TopPanel { get; private set; }
        public Rectangle tagModalBounds { get; set; }
        public Rectangle prefabModalBounds { get; set; }

        // A dictionary to easily look up panel names
        public readonly Dictionary<string, Rectangle> _panels = new Dictionary<string, Rectangle>();

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
            int inspectorHeight = 180; // Big, spacious bottom panel

            UpscaleFactor = editorUpscaler.Scale;
            // Calculate the positions and sizes
            //TopPanel = new Rectangle(tilesetWidth, 0, windowWidth - tilesetWidth - layerWidth, topPanelHeight);
            //ToolPanel = new Rectangle(tilesetWidth, windowHeight - toolPanelHeight, windowWidth - tilesetWidth - layerWidth, toolPanelHeight);
            //TilesetPanel = new Rectangle(0, 0, tilesetWidth, windowHeight);
            //LayerPanel = new Rectangle(windowWidth - layerWidth, 0, layerWidth, windowHeight);

            TopPanel = new Rectangle(tilesetWidth, 0, 500, topPanelHeight);
            ToolPanel = new Rectangle(tilesetWidth + 500, 0, ViewW - 500, topPanelHeight);
            TilesetPanel = new Rectangle(0, 0, tilesetWidth, windowHeight);
            InspectorPanel = new Rectangle(tilesetWidth, windowHeight - inspectorHeight, windowWidth - tilesetWidth, inspectorHeight);
            LayerPanel = new Rectangle(tilesetWidth + ViewW, 0, windowWidth - tilesetWidth - ViewW, windowHeight - inspectorHeight);
            tagModalBounds = new Rectangle(WindowBounds.Center.X - 250,WindowBounds.Center.Y - 275,500, 550);
            prefabModalBounds = new Rectangle(tilesetWidth, topPanelHeight, ViewW + LayerPanel.Width, ViewH);
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
            _panels["Inspector"] = InspectorPanel;
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