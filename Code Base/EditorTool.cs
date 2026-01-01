using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using MonoGame.Extended.Particles.Modifiers;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations.Data
{
    public interface ICommand { }
    public interface IUndoableCommand : ICommand
    {
        void Execute();
        void Undo();
    }
    public class CompoundCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands;
        public CompoundCommand(List<IUndoableCommand> commands) { _commands = commands; }
        public void Execute() { foreach (var cmd in _commands) cmd.Execute(); }
        public void Undo() { _commands.Reverse(); foreach (var cmd in _commands) cmd.Undo(); _commands.Reverse(); }
    }
    public class AddObjectCommand : IUndoableCommand
    {
        private readonly ObjectLayer _targetLayer;
        private readonly MapObject _objectToAdd;
        public AddObjectCommand(ObjectLayer layer, MapObject obj) { _targetLayer = layer; _objectToAdd = obj; }
        public void Execute() { if (!_targetLayer.Objects.Contains(_objectToAdd)) _targetLayer.Objects.Add(_objectToAdd); }
        public void Undo() { _targetLayer.Objects.Remove(_objectToAdd); }
    }
    public class RemoveObjectCommand : IUndoableCommand
    {
        private readonly ObjectLayer _targetLayer;
        private readonly MapObject _objectToRemove;
        public RemoveObjectCommand(ObjectLayer layer, MapObject obj) { _targetLayer = layer; _objectToRemove = obj; }
        public void Execute() { _targetLayer.Objects.Remove(_objectToRemove); }
        public void Undo() { if (!_targetLayer.Objects.Contains(_objectToRemove)) _targetLayer.Objects.Add(_objectToRemove); }
    }
    public class AddRectangleCommand : IUndoableCommand
    {
        private  CollisionLayer _collisionLayer;
        private  NavigationLayer _navigationLayer;
        private  TriggerLayer _triggerLayer;
        private LayerType _type;
        private readonly RectangleObject _objectToAdd;
        public AddRectangleCommand(Layer layer,LayerType type ,RectangleObject obj) { AssignLayer(type,layer); _type = type; _objectToAdd = obj; }
        private void AssignLayer(LayerType type, Layer layer) 
        {
            switch (type)
            {
                case LayerType.Collision:
                    _collisionLayer = (CollisionLayer)layer;
                    break;
                case LayerType.Navigation:
                    _navigationLayer = (NavigationLayer)layer;
                    break;
                case LayerType.Trigger:
                    _triggerLayer = (TriggerLayer)layer;
                    break;
                default:
                    _collisionLayer = (CollisionLayer)layer;
                    break;
            }
            
        }
        public void Execute() 
        {
            switch (_type)
            {
                case LayerType.Collision:
                    if (!_collisionLayer.CollisionMesh.Contains(_objectToAdd)) 
                        { _collisionLayer.CollisionMesh.Add(_objectToAdd); }
                    break;
                case LayerType.Navigation:
                    if (!_navigationLayer.NavigationMesh.Contains(_objectToAdd))
                        { _navigationLayer.NavigationMesh.Add(_objectToAdd); }
                    break;
                case LayerType.Trigger:
                    if (!_triggerLayer.TriggerMesh.Contains(_objectToAdd))
                        { _triggerLayer.TriggerMesh.Add(_objectToAdd); }
                    break;
                default:
                    if (!_collisionLayer.CollisionMesh.Contains(_objectToAdd))
                        { _collisionLayer.CollisionMesh.Add(_objectToAdd); }
                    break;
            }
        }
        public void Undo() 
        {
            switch (_type)
            {
                case LayerType.Collision:
                    _collisionLayer.CollisionMesh.Remove(_objectToAdd); 
                    break;
                case LayerType.Navigation:
                    _navigationLayer.NavigationMesh.Remove(_objectToAdd); 
                    break;
                case LayerType.Trigger:
                    _triggerLayer.TriggerMesh.Remove(_objectToAdd); 
                    break;
                default:
                     _collisionLayer.CollisionMesh.Remove(_objectToAdd);
                    break;
            }
        }
    }
    public struct MenuActionCommand : ICommand
    {
        public string ActionName; // "Save", "Load", "Undo", "Redo"
    }
    public class ChangeToolCommand : ICommand { public string ToolName; }
    public struct CycleNewLayerTypeCommand : ICommand { }
    public struct CreateTilesetCommand : ICommand
    {
        public string AtlasName;
    }
    public class SelectTilesetCommand : ICommand { public string TilesetName; }
    public class SelectTileCommand : ICommand { public string TilesetName; public int TileID; }
    public class PlaceTileCommand : IUndoableCommand
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
    public class EraseTileCommand : IUndoableCommand
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
    public class AddPointCommand : IUndoableCommand
    {
        private readonly TriggerLayer _targetLayer;
        private readonly PointObject _objectToAdd;
        public AddPointCommand(TriggerLayer layer, PointObject obj) { _targetLayer = layer; _objectToAdd = obj; }
        public void Execute() { if (!_targetLayer.PointTriggers.Contains(_objectToAdd)) _targetLayer.PointTriggers.Add(_objectToAdd); }
        public void Undo() { _targetLayer.PointTriggers.Remove(_objectToAdd); }
    }
    public struct SelectLayerCommand : ICommand { public int LayerIndex; }
    public struct ToggleLayerVisibilityCommand : ICommand { public int LayerIndex; }
    public struct ToggleLayerLockCommand : ICommand { public int LayerIndex; }
    public struct MoveLayerCommand : ICommand { public int LayerIndex; public bool Direction; } // -1 for Up, 1 for Down
    public struct AddLayerCommand : ICommand { public bool Direction; } // 1 for Above, -1 for Below
    public struct DeleteActiveLayerCommand : ICommand { }
    public interface ITool
    {
        string Name { get; }
        void Update(ToolInput toolInput, InputState input , EventBus bus);
        void DrawPreview(SpriteBatch spriteBatch, InputState input);
    }
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
    public class BrushTool : ITool
    {
        public string Name => "Brush";
        public bool _isDrawing => false;
        public void Update(ToolInput toolInput, InputState input,EventBus eventBus) // Add EventBus parameter
        {
            if (toolInput.CurrentMouse.LeftButton == ButtonState.Pressed)
            {
                if (toolInput.ActiveLayer is TileLayer tileLayer && (toolInput.ActiveLayer.Type == LayerType.Tile) &&toolInput.ActiveBrush != null)
                {
                    var cell = new Point((int)System.Math.Floor(toolInput.WorldPosition.X / 16), (int)System.Math.Floor(toolInput.WorldPosition.Y / 16));

                    // ... (check if tile is already present) ...
                    var existingTile = tileLayer.GetTileAt(cell);
                    if (existingTile != null && existingTile.Equals(toolInput.ActiveBrush)) return;
                    // Create the command and publish it to the bus
                    var command = new PlaceTileCommand(tileLayer, cell, toolInput.ActiveBrush);
                    eventBus.Publish(command);
                }
            }
        }

        public void DrawPreview(SpriteBatch sb, InputState input)
        {
            // A brush might draw a semi-transparent preview of the tile under the cursor.
            // For now, we'll leave it empty.
        }
    }
    public class EraserTool : ITool
    {
        public string Name => "Eraser";
        public bool _isDrawing = false;
        public void Update(ToolInput toolInput, InputState input, EventBus eventBus)
        {
            if (toolInput.CurrentMouse.LeftButton == ButtonState.Pressed)
            {
                if (toolInput.ActiveLayer is TileLayer tileLayer && (toolInput.ActiveLayer.Type == LayerType.Tile) && toolInput.ActiveBrush != null)
                {
                    var cell = new Point((int)System.Math.Floor(toolInput.WorldPosition.X / 16), (int)System.Math.Floor(toolInput.WorldPosition.Y / 16));
                    if (tileLayer.GetTileAt(cell) != null)
                    {
                        var command = new EraseTileCommand(tileLayer, cell);
                        eventBus.Publish(command);
                    }
                }
            }
        }

        public void DrawPreview(SpriteBatch sb, InputState input)
        {
            // A brush might draw a semi-transparent preview of the tile under the cursor.
            // For now, we'll leave it empty.
        }
    }
    public class FreeRectangleTool : ITool
    {
        public bool _isDrawing = false;
        public string Name => "FreeRectangle";
        private Vector2 _startWorldPos;

        private float Zoom = 1f;
        private LayerType type;
        public void Update(ToolInput toolInput, InputState input, EventBus eventBus)
        {
            input.Drawing = _isDrawing;
            type = toolInput.ActiveLayer.Type;
            if ((type == LayerType.Tile) || (type is LayerType.Object)) 
            {
                _isDrawing = false;
                return; 
            }
           
                if (input.IsNewLeftClick())
                {
                    _startWorldPos = toolInput.WorldPosition;
                    _isDrawing = true;
                }

                if (_isDrawing)
                {
                    
                    Vector2 start = _startWorldPos;
                    Vector2 end = toolInput.WorldPosition;
                    bool snapToGrid = input.CurrentKeyboard.IsKeyDown(Keys.LeftControl);

                    if (snapToGrid)
                    {
                        start.X = (float)Math.Floor(start.X / 16) * 16;
                        start.Y = (float)Math.Floor(start.Y / 16) * 16;
                        end.X = (float)Math.Floor(end.X / 16) * 16;
                        end.Y = (float)Math.Floor(end.Y / 16) * 16;
                    }

                    var rectObject = new RectangleObject
                    {
                        Position = new Vector2(System.Math.Min(start.X, end.X), System.Math.Min(start.Y, end.Y)),
                        Size = new Vector2(System.Math.Abs(start.X - end.X), System.Math.Abs(start.Y - end.Y)),
                        // Set the type based on the layer's type!
                        TriggerType = type.ToString(),
                        DebugColor = GetColorForLayerType(type)
                    };

                    if(input.CurrentMouse.LeftButton == ButtonState.Pressed)
                    {

                    }
                    else if(input.CurrentMouse.LeftButton == ButtonState.Released) 
                    {
                        if (rectObject.Size.X > 1 && rectObject.Size.Y > 1){
                                eventBus.Publish<IUndoableCommand>(new AddRectangleCommand(toolInput.ActiveLayer, toolInput.ActiveLayer.Type, rectObject));
                            }
                        _isDrawing = false;
                    }
                }
        }
        private Color GetColorForLayerType(LayerType type)
        {
            switch (type)
            {
                case LayerType.Collision: return Color.Red;
                case LayerType.Trigger: return Color.LimeGreen;
                case LayerType.Navigation: return Color.Blue;
                default: return Color.Magenta;
            }
        }
        public void DrawPreview(SpriteBatch sb, InputState input)
        {
            if (!_isDrawing) return;
            Vector2 start = _startWorldPos;
            Vector2 end = input.MouseWorldPosition;
            bool snapToGrid = input.CurrentKeyboard.IsKeyDown(Keys.LeftControl);

            if (snapToGrid)
            {
                start.X = (float)Math.Floor(start.X / 16) * 16;
                start.Y = (float)Math.Floor(start.Y / 16) * 16;
                end.X = (float)Math.Floor(end.X / 16) * 16;
                end.Y = (float)Math.Floor(end.Y / 16) * 16;
            }

            var rect = new RectangleF(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Abs(start.X - end.X),
                Math.Abs(start.Y - end.Y)
            );
            var color = GetColorForLayerType(type);
            sb.DrawRectangle(rect, color, 1f / Zoom);
        }
    }
    public class GridRectangleTool : ITool
    {
        public string Name => "GridRectangle";
        private Point _startCell;
        public bool _isDrawing = false;
        private LayerType type;
        private float Zoom = 1f;

        public void Update(ToolInput toolInput, InputState input, EventBus eventBus)
        {
            type = toolInput.ActiveLayer.Type;
            input.Drawing = _isDrawing;
            if ((type == LayerType.Tile) || (type is LayerType.Object))
            {
                _isDrawing = false;
                return;
            }
                var currentCell = input.MouseGridCell;
                if (input.IsNewLeftClick())
                {
                    _startCell = currentCell;
                    _isDrawing = true;
                }
                else if (_isDrawing)
                {
                    // --- ON MOUSE UP ---
                    int minX = Math.Min(_startCell.X, currentCell.X);
                    int maxX = Math.Max(_startCell.X, currentCell.X);
                    int minY = Math.Min(_startCell.Y, currentCell.Y);
                    int maxY = Math.Max(_startCell.Y, currentCell.Y);

                    var rectObject = new RectangleObject
                    {
                        Position = new Vector2(minX * 16, minY * 16),
                        Size = new Vector2((maxX - minX + 1) * 16, (maxY - minY + 1) * 16),
                        TriggerType = type.ToString(),
                        DebugColor = GetColorForLayerType(type)
                    };
                    if (input.CurrentMouse.LeftButton == ButtonState.Pressed)
                    {

                    }
                    else if (input.CurrentMouse.LeftButton == ButtonState.Released)
                    {
                        if (rectObject.Size.X > 1 && rectObject.Size.Y > 1)
                        {
                            eventBus.Publish<IUndoableCommand>(new AddRectangleCommand(toolInput.ActiveLayer, toolInput.ActiveLayer.Type, rectObject));
                        }
                        _isDrawing = false;
                    }
                }
        }

        private Color GetColorForLayerType(LayerType type)
        {
            switch (type)
            {
                case LayerType.Collision: return Color.Red;
                case LayerType.Trigger: return Color.LimeGreen;
                case LayerType.Navigation: return Color.Blue;
                default: return Color.White;
            }
        }
        public void DrawPreview(SpriteBatch sb, InputState input)
        {
            //if (!_isDrawing || !(input.ActiveLayer is TileLayer)) return;
            if (!_isDrawing) return;
            var currentCell = input.MouseGridCell;

            int minX = Math.Min(_startCell.X, currentCell.X);
            int maxX = Math.Max(_startCell.X, currentCell.X);
            int minY = Math.Min(_startCell.Y, currentCell.Y);
            int maxY = Math.Max(_startCell.Y, currentCell.Y);

            var rect = new Rectangle(
                minX * 16,
                minY * 16,
                (maxX - minX + 1) * 16,
                (maxY - minY + 1) * 16
            );
            var color = GetColorForLayerType(type);
            sb.DrawRectangle(rect, color*0.5f, 1f / Zoom);
        }
    }
    public class SelectionTool : ITool
    {
        public string Name => "Selection";
        public bool _isDrawing => false;
        public void Update(ToolInput toolInput, InputState input, EventBus eventBus)
        {
            // --- Eraser-like functionality on Object Layers ---
            // If right-clicking on an object layer, create a command to remove it.
            if (input.IsNewRightClick() && toolInput.ActiveLayer is ObjectLayer objectLayer)
            {
                // Find the topmost object under the cursor
                MapObject objectToErase = null;
                // Loop backwards to find the one drawn on top
                for (int i = objectLayer.Objects.Count - 1; i >= 0; i--)
                {
                    var mapObject = objectLayer.Objects[i];
                    if (mapObject is RectangleObject rectObj)
                    {
                        var bounds = new RectangleF(rectObj.Position, rectObj.Size);
                        if (bounds.Contains(toolInput.WorldPosition))
                        {
                            objectToErase = mapObject;
                            break;
                        }
                    }
                }

                if (objectToErase != null)
                {
                    // Publish an undoable command to remove the object
                    eventBus.Publish<IUndoableCommand>(new RemoveObjectCommand(objectLayer, objectToErase));
                }
            }
            // Future: Left-click logic to select objects and show properties
        }

        public void DrawPreview(SpriteBatch sb, InputState input) { }
    }
    public class PointPlacerTool : ITool
    {
        public string Name => "PointPlacer";
        private Vector2 _startWorldPos;
        private bool _isDrawing = false;
        private float _currentRadius = 0f;

        public void Update(ToolInput toolInput, InputState input, EventBus eventBus)
        {
            input.Drawing = _isDrawing;

            // This tool ONLY works on TriggerLayers.
            if (!(toolInput.ActiveLayer is TriggerLayer triggerLayer))
            {
                _isDrawing = false;
                return;
            }

            if (input.IsNewLeftClick())
            {
                _startWorldPos = toolInput.WorldPosition;
                _isDrawing = true;
            }

            if (_isDrawing)
            {
                // While dragging, continuously update the radius for the preview.
                _currentRadius = Vector2.Distance(_startWorldPos, toolInput.WorldPosition);

                // When the mouse button is released, finalize the object.
                if (input.CurrentMouse.LeftButton == ButtonState.Released)
                {
                    var pointObject = new PointObject
                    {
                        Position = _startWorldPos,
                        Radius = _currentRadius,
                        Label = "New Trigger Point",
                        DebugColor = Color.Yellow
                    };

                    if (pointObject.Radius > 1) // Only add if it has a size
                    {
                        eventBus.Publish<IUndoableCommand>(new AddPointCommand(triggerLayer, pointObject));
                    }
                    _isDrawing = false;
                }
            }
        }

        public void DrawPreview(SpriteBatch sb, InputState input)
        {
            if (!_isDrawing) return;

            // Draw a circle and crosshair for the preview.
            sb.DrawCircle(_startWorldPos, _currentRadius, 32, Color.Yellow, 1f / input.Zoom);
            sb.DrawLine(_startWorldPos - new Vector2(4, 0), _startWorldPos + new Vector2(4, 0), Color.Yellow, 1f / input.Zoom);
            sb.DrawLine(_startWorldPos - new Vector2(0, 4), _startWorldPos + new Vector2(0, 4), Color.Yellow, 1f / input.Zoom);
        }
    }
}
