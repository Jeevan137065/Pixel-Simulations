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
    public enum LayerType { Tile, Object, Collision, Pathing }
    public enum ObjectType { Prop, Rectangle, Point }
    public enum SliceMode { RowFirst, ColumnFirst }
    

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Layer
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public bool IsVisible { get; set; } = true;

        [JsonProperty]
        public bool IsLocked { get; set; } = false;

        public abstract LayerType Type { get; }

        protected Layer(string name) { Name = name; }
        protected Layer() { } // For deserialization
    }

    public class TileLayer : Layer
    {
        public override LayerType Type => LayerType.Tile;

        [JsonProperty]
        public Dictionary<Point, Chunk> Chunks { get; set; }

        public TileLayer(string name) : base(name)
        {
            Chunks = new Dictionary<Point, Chunk>();
        }
        public TileLayer() : base() { } // For deserialization

        /// Places or replaces a tile at a specific grid coordinate.
        public void PlaceTile(Point globalCell, TileInfo tileInfo)
        {
            if (IsLocked) return;

            Point chunkCoord = new Point(
                (int)System.Math.Floor((double)globalCell.X / Chunk.CHUNK_SIZE),
                (int)System.Math.Floor((double)globalCell.Y / Chunk.CHUNK_SIZE)
            );
            if (!Chunks.TryGetValue(chunkCoord, out var chunk))
            {
                // If the chunk doesn't exist, create it on-demand.
                chunk = new Chunk(chunkCoord);
                Chunks[chunkCoord] = chunk;
            }
            int localX = globalCell.X - chunkCoord.X * Chunk.CHUNK_SIZE;
            int localY = globalCell.Y - chunkCoord.Y * Chunk.CHUNK_SIZE;

            chunk.PlaceTile(localX, localY, tileInfo);
        }

        /// Removes a tile from a specific grid coordinate.
        public void RemoveTile(Point cell)
        {
            if (IsLocked) return;
            Chunks.Remove(cell);
        }

        /// Gets the TileInfo at a specific cell, if one exists.
        public TileInfo GetTileAt(Point globalCell)
        {
            Point chunkCoord = new Point(globalCell.X / Chunk.CHUNK_SIZE, globalCell.Y / Chunk.CHUNK_SIZE);
            if (Chunks.TryGetValue(chunkCoord, out var chunk))
            {
                int localX = globalCell.X % Chunk.CHUNK_SIZE;
                int localY = globalCell.Y % Chunk.CHUNK_SIZE;
                return chunk.GetTileAt(localX, localY);
            }
            return null;
        }

    }

    // Placeholder for future implementation
    public class ObjectLayer : Layer
    {
        public override LayerType Type => LayerType.Object;
        public ObjectLayer(string name) : base(name) { }
        public ObjectLayer() : base() { }
    }

    [JsonObject(MemberSerialization.OptIn)]
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
        public string Tag { get; set; } // e.g., "Collision", "Trigger"
    }

    public class IconDefinition { public int x { get; set; } public int y { get; set; } }
    public interface IPanel
    {
        void Update(InputState input, EventBus bus);
        void Draw(SpriteBatch spriteBatch);
    }
    public class Button
    {
        public Rectangle Bounds { get; }
        public ICommand CommandToPublish { get; }
        public string IconName { get; }
        public bool IsHovered { get; private set; }

        public Button(Rectangle bounds, ICommand command, string iconName)
        {
            Bounds = bounds;
            CommandToPublish = command;
            IconName = iconName;
        }

        public bool Update(InputState input, EventBus eventBus)
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

        public abstract void Update(InputState input, EventBus bus);
        public abstract void Draw(SpriteBatch spriteBatch);
    }

    public class LayerRow
    {
        private readonly Rectangle _bounds;
        private readonly int _layerIndex;
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

                foreach (var button in _buttons)
                {
                    button.Draw(sb, editorUI);
                }
            sb.DrawString(editorUI.DebugFont, _layer.Name, new Vector2(_bounds.X + 80, _bounds.Y + 10), Color.White);
        }
    }

}