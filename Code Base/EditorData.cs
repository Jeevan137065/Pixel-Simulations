using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Data
{

    public enum ObjectType { Prop, Rectangle, Point }
    public enum SliceMode { RowFirst, ColumnFirst }
    public abstract class MapObject
    {
        public Vector2 Position { get; set; }
        public abstract ObjectType Type { get; }
    }

    public class PropObject : MapObject
    {
        public override ObjectType Type => ObjectType.Prop;
        public string AssetName { get; set; }
    }

    public class RectangleObject : MapObject
    {
        public override ObjectType Type => ObjectType.Rectangle;
        public Vector2 Size { get; set; }
        public string TriggerType { get; set; } // e.g., "Collision", "Trigger"

        public Color DebugColor { get; set; }
    }
    public class PointObject : MapObject
    {
        [JsonProperty]
        public override ObjectType Type => ObjectType.Point;
        public float Radius { get; set; }
        public Vector2 Center { get; set; }
        [JsonProperty]
        public string Label { get; set; } // For identifying the trigger
        [JsonProperty]
        public Color DebugColor { get; set; }
    }
    public class IconDefinition { public int x { get; set; } public int y { get; set; } }
    public interface IPanel
    {
        void Update(InputState input, EventBus bus);
        void Draw(SpriteBatch spriteBatch);
    }

    public class LayerRow
    {
        public readonly Rectangle _bounds;
        public readonly int _layerIndex;
        private readonly Layer _layer;
        public  List<Button> _buttons = new List<Button>();

        public LayerRow(Layer layer, int index, Rectangle bounds, EditorUI editorUI)
        {
            _layer = layer;
            _layerIndex = index;
            _bounds = bounds;

            // Create this row's per-layer control buttons
            _buttons.Add(new Button(new Rectangle(bounds.X + 5, bounds.Y + 4, 32, 32), new ToggleLayerVisibilityCommand { LayerIndex = index }, layer.IsVisible ? "Visible" : "Hidden"));
            _buttons.Add(new Button(new Rectangle(bounds.X + 40, bounds.Y + 4, 32, 32), new ToggleLayerLockCommand { LayerIndex = index }, layer.IsLocked ? "Locked" : "Unlocked"));
            _buttons.Add(new Button(new Rectangle(bounds.Right - 70, bounds.Y + 4, 32, 32), new MoveLayerCommand { LayerIndex = index, Direction = true }, "MoveUp"));
            _buttons.Add(new Button(new Rectangle(bounds.Right - 38, bounds.Y + 4, 32, 32), new MoveLayerCommand {  LayerIndex = index, Direction = false}, "MoveDown"));
        }

        public void Update(InputState input, EventBus eventBus)
        {
            foreach (var button in _buttons)
            {
                button.Update(input, eventBus);
            }
        }

        public void Draw(SpriteBatch sb, EditorUI editorUI, bool isActive)
        {
            if (isActive)   sb.FillRectangle(_bounds, Color.CornflowerBlue * 0.3f);
            else            sb.DrawRectangle(_bounds, Color.Gray * 0.3f);
            string typeIdentifier = "";
            Color typeColor = Color.White;
            switch (_layer.Type)
            {
                case LayerType.Tile:
                    typeIdentifier = "[T]";
                    typeColor = Color.LightGreen;
                    break;
                case LayerType.Object:
                    typeIdentifier = "[O]";
                    typeColor = Color.LightBlue;
                    break;
                case LayerType.Collision:
                    typeIdentifier = "[C]";
                    typeColor = Color.Red;
                    break;
                case LayerType.Navigation:
                    typeIdentifier = "[N]"; 
                    typeColor = Color.Cyan; 
                    break;
                case LayerType.Trigger: 
                    typeIdentifier = "[E]"; 
                    typeColor = Color.Yellow; 
                    break;
                    // Add cases for Navigation, Trigger, etc.
            }
            foreach (var button in _buttons)
                {
                    button.Draw(sb, editorUI);
                }
            sb.DrawString(editorUI.DebugFont, typeIdentifier, new Vector2(_bounds.X + 80, _bounds.Y + 10), typeColor);
            sb.DrawString(editorUI.DebugFont, _layer.Name, new Vector2(_bounds.X + 110, _bounds.Y + 10), Color.White);
        }
    }

}