using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations.UI
{
    public interface IPanel
    {
        void Update(EditorInputState input, EventBus bus);
        void Draw(SpriteBatch spriteBatch);
        string GetDebugInfo();
    }
    public class IconDefinition { public int x { get; set; } public int y { get; set; } }
    public class LayerRow
    {
        public Rectangle _bounds;
        public readonly int _layerIndex;
        public readonly Layer _layer;
        public List<Button> _buttons = new List<Button>();

        private const int HEADER_HEIGHT = 40;
        private const int CHILD_HEIGHT = 20;
        private const int ICON_SIZE = 24;
        public LayerRow(Layer layer, int index, Rectangle bounds)
        {
            _layer = layer;
            _layerIndex = index;
            _bounds = bounds;
            RefreshButtons();
        }
        /// Recalculates button positions based on current _bounds.
        /// Call this whenever the layer list is scrolled or reordered.
        public void RefreshButtons()
        {
            _buttons.Clear();
            int x = _bounds.X + 45;
            int y = _bounds.Y + (HEADER_HEIGHT - 20) / 2;
            // 1. Expand/Collapse Button (Leftmost)
            _buttons.Add(new Button(new Rectangle(_bounds.X + 5, y, 32, 32), new ToggleLayerExpansionCommand { LayerIndex = _layerIndex }, _layer.IsExpanded ? "Collapse" : "Expand"));

            // 2. Visibility & Lock
            _buttons.Add(new Button(new Rectangle(_bounds.X + 40, y, 32, 32), new ToggleLayerVisibilityCommand { LayerIndex = _layerIndex }, _layer.IsVisible ? "Visible" : "Hidden"));
            _buttons.Add(new Button(new Rectangle(_bounds.X + 75, y, 32, 32), new ToggleLayerLockCommand { LayerIndex = _layerIndex }, _layer.IsLocked ? "Locked" : "Unlocked"));

            // 3. Reorder Buttons (Right Aligned)
            _buttons.Add(new Button(new Rectangle(_bounds.Right - 70, y, 32, 32), new MoveLayerCommand { LayerIndex = _layerIndex, Direction = true }, "MoveUp"));
            _buttons.Add(new Button(new Rectangle(_bounds.Right - 38, y, 32, 32), new MoveLayerCommand { LayerIndex = _layerIndex, Direction = false }, "MoveDown"));
        }

        public void Draw(SpriteBatch sb, EditorUI ui, bool isActive)
        {
            // Row Highlight
            Color typeColor = GetTypeColor();
            string typeId = GetTypeString();

            if (isActive) sb.FillRectangle(_bounds, Color.Black * 0.3f);
            else sb.FillRectangle(_bounds, typeColor * 0.5f);

            foreach (var button in _buttons) button.Draw(sb, ui);

            // Type Symbol and Name

            Vector2 typePos = new Vector2(_bounds.X + 115, _bounds.Y + 10);
            sb.DrawString(ui.DebugFont, typeId, typePos, typeColor);

            Vector2 namePos = new Vector2(typePos.X + 35, _bounds.Y + 10);
            sb.DrawString(ui.DebugFont, _layer.Name, namePos, Color.White);

            // Draw Children
            if (_layer.IsExpanded) DrawChildren(sb, ui);
        }
        private Color GetTypeColor() => _layer.Type switch
        {
            LayerType.Tile => Color.LightGreen,
            LayerType.Control => Color.Red,
            _ => Color.LightBlue
        };
        private string GetTypeString() => _layer.Type switch
        {
            LayerType.Tile => "[T]",
            LayerType.Control => "[C]",
            _ => "[O]"
        };
        private void DrawChildren(SpriteBatch sb, EditorUI ui)
        {
            int yOffset = HEADER_HEIGHT;
            var selectedObj = ui._editorState.Selection.SelectedMapObject;

            // Local helper to draw items
            Action<MapObject> DrawItem = (obj) => {
                if (obj == null) return; // Critical Null Guard

                bool isSelected = (selectedObj == obj);
                Color color = isSelected ? Color.Yellow : Color.White * 0.8f;

                // Safely resolve the display name
                string displayName = "Unknown";
                if (!string.IsNullOrEmpty(obj.Name))
                    displayName = obj.Name;
                else
                    displayName = obj.Type.ToString(); // Fallback if name is null

                sb.DrawString(ui.DebugFont, "  |- " + displayName,
                    new Vector2(_bounds.X + 20, _bounds.Y + yOffset), color);

                yOffset += CHILD_HEIGHT;
            };

            if (_layer is ObjectLayer objLayer)
            {
                foreach (var obj in objLayer.Objects)
                {
                    bool isSelected = (ui._editorState.Selection.SelectedMapObject == obj);
                    string displayName = obj.Name ?? "Unnamed Object";

                    // Indicate if the prefab link is broken
                    if (obj is PropObject p && ui._editorState.PrefabManager.GetPrefab(p.PrefabID) == null)
                        displayName += " (Broken Link)";

                    sb.DrawString(ui.DebugFont, "  |- " + displayName,
                        new Vector2(_bounds.X + 20, _bounds.Y + yOffset),
                        isSelected ? Color.Yellow : Color.White);

                    yOffset += CHILD_HEIGHT;
                }
            }
            if (_layer is ControlLayer col)
            {
                foreach (var s in col.Shapes) DrawItem(s);
                foreach (var s in col.Rectangles) DrawItem(s);
                foreach (var s in col.Points) DrawItem(s);
            }

        }
        public int GetTotalHeight() => _layer.IsExpanded ? HEADER_HEIGHT + (GetChildCount() * CHILD_HEIGHT) : HEADER_HEIGHT;

        private int GetChildCount()
        {
            if (_layer is ControlLayer col) return col.Shapes.Count;
            if (_layer is ObjectLayer obj) return obj.Objects.Count;
            return 0;
        }
    }
    public class Button
    {
        public Rectangle Bounds { get; }
        public ICommand CommandToPublish { get; }
        public string IconName { get; set; }
        public bool IsHovered { get; private set; }

        public Button(Rectangle bounds, ICommand command, string iconName)
        {
            Bounds = bounds;
            CommandToPublish = command;
            IconName = iconName;
        }

        public bool Update(EditorInputState input)// Returns true if mouse is on this Button
        {
            return IsHovered = Bounds.Contains(input.MouseWindowPosition);
        }

        public void Draw(SpriteBatch sb, EditorUI editorUI)
        {
            Color tint = IsHovered ? Color.Yellow : Color.White;
            if (IsHovered) sb.FillRectangle(Bounds, Color.White * 0.2f);
            sb.DrawRectangle(Bounds, tint);
            editorUI.DrawIcon(sb, Bounds, IconName, tint);
        }


    }
    public abstract class BasePanel : IPanel
    {
        protected Rectangle Area { get; }
        protected EditorUI _editorUI { get; }
        protected EditorState _editorState { get; }

        protected BasePanel(Rectangle area, EditorUI editorUI, EditorState editorState)
        {
            Area = area;
            _editorUI = editorUI;
            _editorState = editorState;
        }

        public abstract void Update(EditorInputState input, EventBus bus);
        public abstract void Draw(SpriteBatch spriteBatch);
        public abstract string GetDebugInfo();

    }
}
