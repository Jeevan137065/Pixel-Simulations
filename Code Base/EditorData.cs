using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using Newtonsoft.Json;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Pixel_Simulations.Data
{

    public enum ObjectType { Prop, Rectangle,Shape, Point }
    public enum SliceMode { RowFirst, ColumnFirst }
    public enum ShapeOperation { None, Union, Intersection, Difference }
    public enum HandleType { None, Body, TopLeft, TopRight, BottomLeft, BottomRight, Top, Bottom, Left, Right, Center }
    [JsonObject(MemberSerialization.OptIn)] // Ensure this is present
    public abstract class MapObject
    {
        [JsonProperty]  public abstract ObjectType Type { get; }

        [JsonProperty]  public string Name { get; set; }

        [JsonProperty]  public Vector2 Position { get; set; }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ObjectPrefab
    {
        [JsonProperty]  public string ID { get; set; }
        [JsonProperty]  public string AtlasName { get; set; }
        [JsonProperty]  public Rectangle SourceRect { get; set; }
        [JsonProperty]  public Vector2 Pivot { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        // Helper: Get size in 16px cells
        public Point SizeInCells => new Point(SourceRect.Width / 16, SourceRect.Height / 16);
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ShapeObject : MapObject
    {
        public override ObjectType Type => ObjectType.Shape; // Add "Shape" to the enum
        [JsonProperty]  public Polygon Shape { get; set; }
        [JsonProperty]  public string Tag { get; set; }
        [JsonProperty]  public Vector2 Size { get; set; }
        [JsonProperty]  public Color DebugColor { get; set; }

        public ShapeObject()
        {
            Shape = new Polygon();
        }

        public void UpdateBoundsFromVertices()
        {
            if (Shape.Vertices.Count == 0) return;
            var rect = Shape.GetBounds();
            this.Position = new Vector2(rect.X, rect.Y);
            this.Size = new Vector2(rect.Width, rect.Height);
        }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class PropObject : MapObject
    {
        [JsonProperty]  public override ObjectType Type => ObjectType.Prop;
        [JsonProperty]  public string PrefabID { get; set; }
        [JsonProperty]  public Vector2 Scale { get; set; } = Vector2.One;
        [JsonProperty]  public float Rotation { get; set; } = 0f;
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class RectangleObject : MapObject
    {
        [JsonProperty]  public override ObjectType Type => ObjectType.Rectangle;
        [JsonProperty]  public Vector2 Size { get; set; }
        [JsonProperty]  public string TriggerType { get; set; } // e.g., "Collision", "Trigger"
        [JsonProperty]  public Color DebugColor { get; set; }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class PointObject : MapObject
    {
        [JsonProperty]  public override ObjectType Type => ObjectType.Point;
        [JsonProperty]  public float Radius { get; set; }
        [JsonProperty]  public Vector2 Center { get; set; }
        [JsonProperty]  public string Label { get; set; } // For identifying the trigger
        [JsonProperty]  public Color DebugColor { get; set; }
    }
    [JsonObject(MemberSerialization.OptOut)]
    public class IconDefinition { public int x { get; set; } public int y { get; set; } }
    public interface IPanel
    {
        void Update(InputState input, EventBus bus);
        void Draw(SpriteBatch spriteBatch);
    }
    public class LayerRow
    {
        public  Rectangle _bounds;
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
            _buttons.Add(new Button(new Rectangle(_bounds.X + 5, y, 32, 32), new ToggleLayerExpansionCommand { LayerIndex = _layerIndex}, _layer.IsExpanded ? "Collapse" : "Expand"));

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
            LayerType.Collision => Color.Red,
            LayerType.Trigger => Color.Yellow,
            LayerType.Navigation => Color.Cyan,
            _ => Color.LightBlue
        };
        private string GetTypeString() => _layer.Type switch
        {
            LayerType.Tile => "[T]",
            LayerType.Collision => "[C]",
            LayerType.Navigation => "[N]",
            LayerType.Trigger => "[E]",
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
            if (_layer is CollisionLayer col) foreach (var s in col.CollisionMesh) DrawItem(s);
            else if (_layer is NavigationLayer nav) foreach (var s in nav.NavigationMesh) DrawItem(s);
            else if (_layer is TriggerLayer trig)
            {
                foreach (var r in trig.TriggerMesh) DrawItem(r);
                foreach (var p in trig.PointTriggers) DrawItem(p);
            }
        }
        public int GetTotalHeight() => _layer.IsExpanded ? HEADER_HEIGHT + (GetChildCount() * CHILD_HEIGHT) : HEADER_HEIGHT;

        private int GetChildCount()
        {
            if (_layer is CollisionLayer col) return col.CollisionMesh.Count;
            if (_layer is TriggerLayer trig) return trig.TriggerMesh.Count + trig.PointTriggers.Count;
            if (_layer is ObjectLayer obj) return obj.Objects.Count;
            return 0;
        }
    }
}