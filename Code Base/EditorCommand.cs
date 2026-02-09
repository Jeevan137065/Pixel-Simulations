using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using MonoGame.Extended.Particles.Modifiers;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

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
        private TriggerLayer _triggerLayer;
        private LayerType _type;
        private readonly RectangleObject _objectToAdd;
        public AddRectangleCommand(Layer layer, LayerType type, RectangleObject obj) { AssignLayer(type, layer); _type = type; _objectToAdd = obj; }
        private void AssignLayer(LayerType type, Layer layer)
        {
            switch (type)
            {
                case LayerType.Trigger:
                    _triggerLayer = (TriggerLayer)layer;
                    break;
            }

        }
        public void Execute()
        {
            switch (_type)
            {
                case LayerType.Trigger:
                    if (!_triggerLayer.TriggerMesh.Contains(_objectToAdd))
                    { _triggerLayer.TriggerMesh.Add(_objectToAdd); }
                    break;
                default:
                    break;
            }
        }
        public void Undo()
        {
            switch (_type)
            {
                case LayerType.Trigger:
                    _triggerLayer.TriggerMesh.Remove(_objectToAdd);
                    break;
                default:
                    break;
            }
        }
    }
    public class AddPolygonCommand : IUndoableCommand
    {
        private readonly Layer _targetLayer;
        private readonly ShapeObject _polygonToAdd;

        public AddPolygonCommand(Layer layer, ShapeObject polygon)
        {
            _targetLayer = layer;
            _polygonToAdd = polygon;
        }

        public void Execute()
        {
            // Add the polygon to the correct list based on the layer type
            if (_targetLayer is CollisionLayer colLayer && !colLayer.CollisionMesh.Contains(_polygonToAdd))
            {
                colLayer.CollisionMesh.Add(_polygonToAdd);
            }
            else if (_targetLayer is NavigationLayer navLayer && !navLayer.NavigationMesh.Contains(_polygonToAdd))
            {
                navLayer.NavigationMesh.Add(_polygonToAdd);
            }
        }

        public void Undo()
        {
            if (_targetLayer is CollisionLayer colLayer)
            {
                colLayer.CollisionMesh.Remove(_polygonToAdd);
            }
            else if (_targetLayer is NavigationLayer navLayer)
            {
                navLayer.NavigationMesh.Remove(_polygonToAdd);
            }
        }
    }
    public class BooleanShapeCommand : IUndoableCommand
    {
        private readonly ShapeObject _target;
        private readonly List<Vector2> _oldVertices;
        private readonly List<Vector2> _newVertices;

        public BooleanShapeCommand(ShapeObject target, List<Vector2> resultVertices)
        {
            _target = target;
            _oldVertices = new List<Vector2>(target.Shape.Vertices);
            _newVertices = resultVertices;
        }

        public void Execute()
        {
            _target.Shape.Vertices = new List<Vector2>(_newVertices);
            _target.UpdateBoundsFromVertices();
        }

        public void Undo()
        {
            _target.Shape.Vertices = new List<Vector2>(_oldVertices);
            _target.UpdateBoundsFromVertices();
        }
    }
    public class TransformObjectCommand : IUndoableCommand
    {
        private readonly MapObject _target;
        private readonly Vector2 _oldPos, _newPos;
        private readonly Vector2 _oldSize, _newSize;

        public TransformObjectCommand(MapObject target, Vector2 oldPos, Vector2 newPos, Vector2 oldSize, Vector2 newSize)
        {
            _target = target;
            _oldPos = oldPos; _newPos = newPos;
            _oldSize = oldSize; _newSize = newSize;
        }

        public void Execute()
        {
            _target.Position = _newPos;
            if (_target is RectangleObject r) r.Size = _newSize;
            if (_target is ShapeObject s) s.Size = _newSize; // Assuming Shape has a Size for its bounds
        }

        public void Undo()
        {
            _target.Position = _oldPos;
            if (_target is RectangleObject r) r.Size = _oldSize;
            if (_target is ShapeObject s) s.Size = _oldSize;
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
    public struct ToggleLayerExpansionCommand : ICommand { public int LayerIndex; }
}
