using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Pixel_Simulations.UI;

namespace Pixel_Simulations.Data
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
    // A powerful, flexible tool interface
    public interface ITool
    {
        string Name { get; }
        void Update(ToolInput input);
        void DrawPreview(SpriteBatch spriteBatch, ToolInput input);
    }

    // A struct to pass all context to the tool
    public struct ToolInput
    {
        public MouseState CurrentMouse;
        public MouseState PreviousMouse;
        public KeyboardState CurrentKeyboard;
        public Vector2 WorldPosition;
        public Layer ActiveLayer;
        public TileInfo ActiveBrush;
        // ... and more context as needed
    }
    public class PlaceTileCommand : ICommand
    {
        private readonly TileLayer _targetLayer;
        private readonly Point _cell;
        private readonly TileInfo _newTile;
        private readonly TileInfo _previousTile; // Store what was there before

        public PlaceTileCommand(TileLayer targetLayer, Point cell, TileInfo newTile)
        {
            _targetLayer = targetLayer;
            _cell = cell;
            _newTile = newTile;
            // Record the state *before* the change
            _previousTile = _targetLayer.GetTileAt(cell);
        }

        public void Execute()
        {
            // The action is to place the new tile.
            _targetLayer.PlaceTile(_cell, _newTile);
        }

        public void Undo()
        {
            // The undo action is to restore the previous tile.
            if (_previousTile != null)
            {
                _targetLayer.PlaceTile(_cell, _previousTile);
            }
            else
            {
                _targetLayer.RemoveTile(_cell);
            }
        }
    }
    public class EraseTileCommand : ICommand
    {
        private readonly TileLayer _targetLayer;
        private readonly Point _cell;
        private readonly TileInfo _erasedTile; // Store the tile we are erasing

        public EraseTileCommand(TileLayer targetLayer, Point cell)
        {
            _targetLayer = targetLayer;
            _cell = cell;
            _erasedTile = _targetLayer.GetTileAt(cell);
        }

        public void Execute()
        {
            // Only execute if there's actually something to erase.
            if (_erasedTile != null)
            {
                _targetLayer.RemoveTile(_cell);
            }
        }

        public void Undo()
        {
            // The undo action is to put the erased tile back.
            if (_erasedTile != null)
            {
                _targetLayer.PlaceTile(_cell, _erasedTile);
            }
        }
    }
    public class BrushTool : ITool
    {
        public string Name => "Brush";

        public void Update(ToolInput input)
        {
            if (input.CurrentMouse.LeftButton == ButtonState.Pressed)
            {
                // Use the safe 'is' type pattern here as well.
                if (input.ActiveLayer is TileLayer tileLayer)
                {
                    var cell = new Point((int)System.Math.Floor(input.WorldPosition.X / 16), (int)System.Math.Floor(input.WorldPosition.Y / 16));

                    // We only need to create a command if there is actually a tile to erase.
                    if (tileLayer.GetTileAt(cell) != null)
                    {
                        var command = new EraseTileCommand(tileLayer, cell);
                        UIEvents.OnCommandCreated(command);
                    }
                }
            }
        }

        public void DrawPreview(SpriteBatch sb, ToolInput input)
        {
            // A brush might draw a semi-transparent preview of the tile under the cursor.
            // For now, we'll leave it empty.
        }
    }

    public class EraserTool : ITool
    {
        public string Name => "Eraser";

        public void Update(ToolInput input)
        {
            if (input.CurrentMouse.LeftButton == ButtonState.Pressed)
            {
                if (input.ActiveLayer is TileLayer tileLayer)
                {
                    var cell = new Point((int)System.Math.Floor(input.WorldPosition.X / 16), (int)System.Math.Floor(input.WorldPosition.Y / 16));

                    // Create a command and ask the controller to execute it.
                    var command = new EraseTileCommand(tileLayer, cell);
                    UIEvents.OnCommandCreated(command);
                }
            }
        }

        public void DrawPreview(SpriteBatch sb, ToolInput input)
        {
            // A brush might draw a semi-transparent preview of the tile under the cursor.
            // For now, we'll leave it empty.
        }
    }
}