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
        private ControlLayer _triggerLayer;
        private LayerType _type;
        private readonly RectangleObject _objectToAdd;
        public AddRectangleCommand(Layer layer, LayerType type, RectangleObject obj) { AssignLayer(type, layer); _type = type; _objectToAdd = obj; }
        private void AssignLayer(LayerType type, Layer layer)
        {
            switch (type)
            {
                case LayerType.Control:
                    _triggerLayer = (ControlLayer)layer;
                    break;
            }

        }
        public void Execute()
        {
            switch (_type)
            {
                case LayerType.Control:
                    if (!_triggerLayer.Rectangles.Contains(_objectToAdd))
                    { _triggerLayer.Rectangles.Add(_objectToAdd); }
                    break;
                default:
                    break;
            }
        }
        public void Undo()
        {
            switch (_type)
            {
                case LayerType.Control:
                    _triggerLayer.Rectangles.Remove(_objectToAdd);
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
            if (_targetLayer is ControlLayer colLayer && !colLayer.Shapes.Contains(_polygonToAdd))
            {
                colLayer.Shapes.Add(_polygonToAdd);
            }
        }

        public void Undo()
        {
            if (_targetLayer is ControlLayer colLayer)
            {
                colLayer.Shapes.Remove(_polygonToAdd);
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
    public class PlaceTileAreaCommand : IUndoableCommand
    {
        private readonly TileLayer _layer;
        private readonly Dictionary<Point, TileInfo> _newTiles;
        private readonly Dictionary<Point, TileInfo> _oldTiles;

        public PlaceTileAreaCommand(TileLayer layer, Dictionary<Point, TileInfo> tilesToPlace)
        {
            _layer = layer;
            _newTiles = tilesToPlace;
            _oldTiles = new Dictionary<Point, TileInfo>();

            foreach (var coord in _newTiles.Keys)
                _oldTiles[coord] = _layer.GetTileAt(coord);
        }

        public void Execute() { foreach (var kvp in _newTiles) _layer.PlaceTile(kvp.Key, kvp.Value); }
        public void Undo()
        {
            foreach (var kvp in _oldTiles)
            {
                if (kvp.Value == null) _layer.RemoveTile(kvp.Key);
                else _layer.PlaceTile(kvp.Key, kvp.Value);
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
        private readonly ControlLayer _targetLayer;
        private readonly PointObject _objectToAdd;
        public AddPointCommand(ControlLayer layer, PointObject obj) { _targetLayer = layer; _objectToAdd = obj; }
        public void Execute() { if (!_targetLayer.Points.Contains(_objectToAdd)) _targetLayer.Points.Add(_objectToAdd); }
        public void Undo() { _targetLayer.Points.Remove(_objectToAdd); }
    }
    public class LinkObjectsCommand : IUndoableCommand
    {
        private readonly MapObject _source;
        private readonly MapObject _target;

        public LinkObjectsCommand(MapObject source, MapObject target)
        {
            _source = source;
            _target = target;
        }

        public void Execute()
        {
            if (!_source.LinkedObjects.Contains(_target.ID))
                _source.LinkedObjects.Add(_target.ID);
        }

        public void Undo() { _source.LinkedObjects.Remove(_target.ID); }
    }

    public class UnlinkObjectCommand : IUndoableCommand
    {
        private readonly MapObject _source;
        private readonly string _targetIdToRemove;

        public UnlinkObjectCommand(MapObject source, string targetId)
        {
            _source = source;
            _targetIdToRemove = targetId;
        }

        public void Execute() { _source.LinkedObjects.Remove(_targetIdToRemove); }
        public void Undo() { _source.LinkedObjects.Add(_targetIdToRemove); }
    }
    public class AddTagCommand : IUndoableCommand
    {
        private readonly MapObject _obj;
        private readonly string _tag;
        public AddTagCommand(MapObject obj, string tag) { _obj = obj; _tag = tag; }
        public void Execute() { _obj.Tags.Add(_tag); }
        public void Undo() { _obj.Tags.Remove(_tag); }
    }
    public struct ToggleTagManagerCommand : ICommand { }
    public struct SaveTagCommand : ICommand { }
    public struct DeleteTagCommand : ICommand { public string HashID; }
    public struct SelectLayerCommand : ICommand { public int LayerIndex; }
    public struct ToggleLayerVisibilityCommand : ICommand { public int LayerIndex; }
    public struct ToggleLayerLockCommand : ICommand { public int LayerIndex; }
    public struct MoveLayerCommand : ICommand { public int LayerIndex; public bool Direction; } // -1 for Up, 1 for Down
    public struct AddLayerCommand : ICommand { public bool Direction; } // 1 for Above, -1 for Below
    public struct DeleteActiveLayerCommand : ICommand { }
    public struct ToggleLayerExpansionCommand : ICommand { public int LayerIndex; }
    public struct ChangeTilesetTabCommand : ICommand { public bool ShowObjects; }
    public class SelectPrefabCommand : ICommand { public string PrefabID; }
    public struct ClosePrefabCreatorCommand : ICommand { }
    public struct OpenAtlasPickerCommand : ICommand { } // For Tileset +
    public struct SavePrefabCommand : ICommand { public String Mode; }
    public struct DeletePrefabCommand : ICommand { }
    public struct CaptureMapCommand : ICommand { }
    public struct SelectAtlasForCreatorCommand : ICommand { public string AtlasName; }
    public struct TogglePrefabCreatorCommand : ICommand { public string DefaultAtlasName; }
}
