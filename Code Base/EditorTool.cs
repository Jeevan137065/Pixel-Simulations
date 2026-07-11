using Clipper2Lib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.UI;
using Pixel_Simulations.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Editor
{
  
    public interface ITool
    {
        public string Name { get; }
        public string IconName { get; }
        string GetShortcutHints();
        void Update(ToolInput toolInput, EditorInputState input , EventBus bus, EditorState editorState);
        void DrawPreview(ToolInput toolInput, SpriteBatch spriteBatch, EditorInputState input);
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
        public string IconName { get; set; } = "Brush";

        private bool _isAreaMode = false;
        private Point _startCell;
        public bool _isDrawing => false;
        private EditorState _es;
        public void Update(ToolInput toolInput, EditorInputState input,EventBus eventBus, EditorState editorState) // Add EventBus parameter
        {
            _es = editorState;
            var selection = toolInput.ActiveBrush;
            if (selection != null)
            {
                byte currentRot = selection.Rotation;
                bool rotChanged = false;
                if (input.CurrentKeyboard.IsKeyDown(Keys.E) && input.PreviousKeyboard.IsKeyUp(Keys.E))
                {
                    currentRot = (byte)((currentRot + 1) % 4);
                    rotChanged = true;
                }
                if (input.CurrentKeyboard.IsKeyDown(Keys.Q) && input.PreviousKeyboard.IsKeyUp(Keys.Q))
                {
                    currentRot = (byte)((currentRot + 3) % 4);
                    rotChanged = true;
                }
                if (rotChanged)
                {
                    editorState.Selection.ActiveTileBrush = new TileInfo(selection.TilesetName, selection.TileID, currentRot);
                    selection = editorState.Selection.ActiveTileBrush; // Update local ref for painting
                }
            }
            // If we rotated, create a BRAND NEW brush instance and assign it to the global state
            
            bool shiftHeld = input.CurrentKeyboard.IsKeyDown(Keys.LeftShift);
            if (toolInput.ActiveLayer is not TileLayer layer || toolInput.ActiveBrush == null) return;

            // Toggle Area Mode with Shift
            shiftHeld = input.CurrentKeyboard.IsKeyDown(Keys.LeftShift);

            if (input.IsNewLeftClick)
            {
                _startCell = input.MouseGridCell;
                _isAreaMode = shiftHeld;

                if (!_isAreaMode)
                {
                    // FIX: Create a brand new TileInfo instance so it's disconnected from the brush!
                    var tileClone = new TileInfo(toolInput.ActiveBrush.TilesetName, toolInput.ActiveBrush.TileID, toolInput.ActiveBrush.Rotation);
                    eventBus.Publish(new PlaceTileCommand(layer, _startCell, tileClone));
                }
            }
            if (input.LeftHold && !_isAreaMode)
            {
                var tileClone = new TileInfo(toolInput.ActiveBrush.TilesetName, toolInput.ActiveBrush.TileID, toolInput.ActiveBrush.Rotation);
                eventBus.Publish(new PlaceTileCommand(layer, input.MouseGridCell, tileClone));
            }
            // Handle Rectangle Fill on Release
            if (input.CurrentMouse.LeftButton == ButtonState.Released && _isAreaMode)
            {
                Point endCell = input.MouseGridCell;
                var tilesToPlace = new Dictionary<Point, TileInfo>();

                int minX = Math.Min(_startCell.X, endCell.X);
                int maxX = Math.Max(_startCell.X, endCell.X);
                int minY = Math.Min(_startCell.Y, endCell.Y);
                int maxY = Math.Max(_startCell.Y, endCell.Y);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        tilesToPlace[new Point(x, y)] = new TileInfo(
                            toolInput.ActiveBrush.TilesetName,
                            toolInput.ActiveBrush.TileID,
                            toolInput.ActiveBrush.Rotation);
                    }
                }

                eventBus.Publish(new PlaceTileAreaCommand(layer, tilesToPlace));
                _isAreaMode = false;
            }

            // Standard dragging for normal brush
            if (input.LeftHold && !_isAreaMode)
            {
                eventBus.Publish(new PlaceTileCommand(layer, input.MouseGridCell, toolInput.ActiveBrush));
            }
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            var activeBrush = toolInput.ActiveBrush;
            if (activeBrush == null) return;

            var tex = _es.TilesetManager.GetTileTexture(activeBrush);
            if (tex == null) return;

            float rot = activeBrush.Rotation * MathHelper.PiOver2;
            Vector2 origin = new Vector2(8, 8);

            if (_isAreaMode && input.LeftHold)
            {
                // Calculate the grid range
                int minX = Math.Min(_startCell.X, input.MouseGridCell.X);
                int maxX = Math.Max(_startCell.X, input.MouseGridCell.X);
                int minY = Math.Min(_startCell.Y, input.MouseGridCell.Y);
                int maxY = Math.Max(_startCell.Y, input.MouseGridCell.Y);

                // Draw a ghost tile for every cell in the rectangle
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        Vector2 pos = new Vector2(x * 16 + 8, y * 16 + 8);
                        sb.Draw(tex, pos, null, Color.White * 0.3f, rot, origin, 1f, SpriteEffects.None, 0f);
                    }
                }

                // Draw the selection border
                RectangleF area = GetGridRect(_startCell, input.MouseGridCell);
                sb.DrawRectangle(area, Color.White * 0.5f, 1f / input.Zoom);
            }
            else
            {
                // Standard single-tile ghost
                Vector2 pos = new Vector2(input.MouseGridCell.X * 16 + 8, input.MouseGridCell.Y * 16 + 8);
                sb.Draw(tex, pos, null, Color.White * 0.4f, rot, origin, 1f, SpriteEffects.None, 0f);
            }
        }
        private RectangleF GetGridRect(Point p1, Point p2)
        {
            int x = Math.Min(p1.X, p2.X) * 16;
            int y = Math.Min(p1.Y, p2.Y) * 16;
            int w = (Math.Abs(p1.X - p2.X) + 1) * 16;
            int h = (Math.Abs(p1.Y - p2.Y) + 1) * 16;
            return new RectangleF(x, y, w, h);
        }
        public string GetShortcutHints() => "Q/E: Rotate | SHIFT: Fill Area ";
    }
    public class EraserTool : ITool
    {
        public string Name => "Eraser";
        public string IconName => Name;
        public bool _isDrawing = false;
        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            if (toolInput.CurrentMouse.LeftButton == ButtonState.Pressed)
            {
                if (toolInput.ActiveLayer is TileLayer tileLayer && (toolInput.ActiveLayer.Type == LayerType.Tile) && toolInput.ActiveBrush != null)
                {
                    var cell = new Point((int)Math.Floor(toolInput.WorldPosition.X / 16), (int)Math.Floor(toolInput.WorldPosition.Y / 16));
                    if (tileLayer.GetTileAt(cell) != null)
                    {
                        var command = new EraseTileCommand(tileLayer, cell);
                        eventBus.Publish(command);
                    }
                }
            }
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            // A brush might draw a semi-transparent preview of the tile under the cursor.
            // For now, we'll leave it empty.
        }
        public string GetShortcutHints() => "";
    }
    public class ObjectPlacerTool : ITool
    {
        public string Name => "ObjectPlacer";
        public string IconName => Name;

        private ObjectPrefab _activePrefab;
        private EditorState _es;

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            // 1. Get current selection from UI (To be implemented in TilesetPanel)
            _activePrefab = editorState.Selection.ActivePrefab;
            _es = editorState;
            if (_activePrefab == null) return;

            if (input.IsNewLeftClick)
            {
                Vector2 pos = toolInput.WorldPosition;

                // Handle Snapping if CapsLock is on
                if (input.CurrentKeyboard.CapsLock)
                {
                    //pos = new Vector2(
                    //    (float)Math.Floor(pos.X / 16) * 16,
                    //    (float)Math.Floor(pos.Y / 16) * 16
                    //);
                    pos = GridHelper.SnapToSubCell(pos, _activePrefab.Pivot);
                }

                if (toolInput.ActiveLayer is ObjectLayer objLayer)
                {
                    var instance = new PropObject
                    {
                        PrefabID = _activePrefab.ID,
                        Position = pos,
                        Name = GetUniqueName(objLayer, _activePrefab.ID) // Dynamic unique naming
                    };
                    foreach (var tag in _activePrefab.Tags)
                    {
                        instance.Tags.Add(tag);
                    }
                    foreach (var prop in _activePrefab.Properties)
                    {
                        instance.Properties[prop.Key] = new MapProperty(prop.Value.Type, prop.Value.Value);
                    }

                    eventBus.Publish(new AddObjectCommand(objLayer, instance));
                    editorState.Selection.SelectedMapObject = instance;
                }
            }

        }
        private int GetObjectCount(Layer ActiveLayer)
        {
            if (ActiveLayer is ObjectLayer col) { return col.Objects.Count; }
            else return -1;
        }
        private string GetUniqueName(ObjectLayer layer, string prefabID)
        {
            int count = 1;
            string candidate;

            // Loop until we find a name that isn't in the list
            do
            {
                candidate = $"{prefabID}_{count}";
                count++;
            } while (layer.Objects.Any(o => o.Name == candidate));

            return candidate;
        }
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (_activePrefab == null || _es == null) return;

            Vector2 pos = input.MouseWorldPosition;
            if (input.CurrentKeyboard.CapsLock)
            {
                pos = GridHelper.SnapToSubCell(pos, _activePrefab.Pivot);
            }

            Vector2 origin = _activePrefab.Pivot;
            Vector2 topLeft = pos - origin;

            // Collect and sort layers by Z-Index so the placement ghost composites correctly
            var layersToDraw = _activePrefab.AlternateStates.Keys.ToList();
            layersToDraw.Sort((a, b) =>
                _activePrefab.AlternateStates[a].ZIndex.CompareTo(_activePrefab.AlternateStates[b].ZIndex)
            );

            foreach (var key in layersToDraw)
            {
                var sLayer = _activePrefab.AlternateStates[key];
                var tex = _es.AssetLibrary.GetAtlas(sLayer.AtlasName);
                if (tex != null)
                {
                    // Composite the layered preview at 50% opacity
                    sb.Draw(tex, topLeft + sLayer.Offset.ToVector2(), sLayer.SourceRect, Color.White * 0.5f);
                }
            }
        }
        public string GetShortcutHints() => "";
    }
    public class FreeRectangleTool : ITool
    {
        public bool _isDrawing = false;
        public bool Mode = false;
        public string Name => "FreeRectangle";
        public string IconName { get; set; } = "FreeRectangle";
        private Vector2 _startWorldPos;

        private float Zoom = 1f;
        private LayerType type;
        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {

            if (input.CurrentKeyboard.CapsLock) { Mode = true; }    else { Mode = false; }

            input.Drawing = _isDrawing;
            Zoom = input.Zoom;
            type = toolInput.ActiveLayer.Type;
            if (!(type == LayerType.Control)) 
            {
                _isDrawing = false;
                return; 
            }
           
                if (input.IsNewLeftClick)
                {
                    _startWorldPos = toolInput.WorldPosition;
                    //if (Mode) { _startWorldPos = new Vector2((float)Math.Floor(_startWorldPos.X / 16) * 16, (float)Math.Floor(_startWorldPos.Y / 16) * 16); }
                    if (Mode) { _startWorldPos = GridHelper.SnapCoordinate(_startWorldPos); }
                    _isDrawing = true;
                }

                if (_isDrawing)
                {
                    
                    Vector2 start = _startWorldPos;
                    Vector2 end = toolInput.WorldPosition;

                    //if (Mode) { end = new Vector2((float)Math.Floor(end.X / 16) * 16, (float)Math.Floor(end.Y / 16) * 16); }
                    if (Mode) { end = GridHelper.SnapCoordinate(toolInput.WorldPosition); }
                var rectObject = new RectangleObject
                {
                    Position = new Vector2(System.Math.Min(start.X, end.X), System.Math.Min(start.Y, end.Y)),
                    Size = new Vector2(System.Math.Abs(start.X - end.X), System.Math.Abs(start.Y - end.Y)),
                    // Set the type based on the layer's type!
                    Name = $"{toolInput.ActiveLayer.Name}__{1}",
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
                case LayerType.Control: return Color.LimeGreen;
                default: return Color.White;
            }
        }
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!_isDrawing) return;
            Vector2 start = _startWorldPos;
            Vector2 end = input.MouseWorldPosition;

            if (Mode)
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
        public string GetShortcutHints() => "CAPS: Grid Snap";
    }
    public class ShapeTool : ITool
    {
        public string Name => "ShapeTool";
        public string IconName { get; set; } = "ShapeTool";
        public bool Mode = false;
        private Vector2 _startPos;
        private bool _isDrawing;
        private LayerType type;
        private ShapeOperation op = ShapeOperation.None;
        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            var selectedShape = editorState.Selection.SelectedMapObject as ShapeObject;
            op = HandleKeys(input);
            editorState.ToolState.ActiveBooleanOp = op;
            if (op == ShapeOperation.Union) IconName = "ShapeUnion";
            else if (op == ShapeOperation.Intersection) IconName = "ShapeIntersection";
            else if (op == ShapeOperation.Difference) IconName = "ShapeDifference";
            else IconName = "ShapeTool";
            type = toolInput.ActiveLayer.Type;
            if(type == LayerType.Tile || type == LayerType.Object) { return; }
            if (input.IsNewLeftClick)
            {
                _startPos = toolInput.WorldPosition;
                //if (input.CurrentKeyboard.CapsLock) _startPos = Snap(_startPos);
                if (Mode) { _startPos = GridHelper.SnapCoordinate(_startPos); }
                _isDrawing = true;
            }

            if (_isDrawing && input.CurrentMouse.LeftButton == ButtonState.Released)
            {
                Vector2 endPos = toolInput.WorldPosition;
                if (Mode) { endPos = GridHelper.SnapCoordinate(toolInput.WorldPosition); }

                Polygon drawnPoly = Polygon.FromRectangle(_startPos, endPos);
                bool performedOp = false;
                if (selectedShape != null && op != ShapeOperation.None)
                {
                    RectangleF selectedBounds = selectedShape.Shape.GetBounds();

                    // We use Intersects to decide if we merge or create new
                    if (selectedBounds.Intersects(drawnPoly.GetBounds()))
                    {
                        var resultVertices = ExecuteBooleanOp(selectedShape.Shape, drawnPoly, op);
                        if (resultVertices != null && resultVertices.Count > 0)
                        {
                            eventBus.Publish(new BooleanShapeCommand(selectedShape, resultVertices));
                            performedOp = true;
                        }
                    }
                }
                else if(!performedOp)
                {
                    // Create New Shape Object
                    var newShape = new ShapeObject
                    {
                        Shape = drawnPoly,
                        DebugColor = GetLayerColor(toolInput.ActiveLayer.Type)
                    };
                    // Generate Unique Name: LayerName + ObjectType + Count
                    int currentCount = GetObjectCount(toolInput.ActiveLayer);
                    newShape.Name = $"{toolInput.ActiveLayer.Name}_{newShape.Type}_{currentCount + 1}";
                    newShape.UpdateBoundsFromVertices();
                    eventBus.Publish(new AddPolygonCommand(toolInput.ActiveLayer, newShape));
                }

                _isDrawing = false;
            }
        }
        private List<Vector2> ExecuteBooleanOp(Polygon subject, Polygon clip, ShapeOperation op)
        {
            // 1. Convert to Clipper Paths
            Paths64 subjectPaths = new Paths64 { subject.ToClipperPath() };
            Paths64 clipPaths = new Paths64 { clip.ToClipperPath() };
            Paths64 solution = new Paths64();

            // 2. Perform Operation
            switch (op)
            {
                case ShapeOperation.Union:
                    solution = Clipper.Union(subjectPaths, clipPaths, FillRule.NonZero);
                    break;
                case ShapeOperation.Intersection:
                    solution = Clipper.Intersect(subjectPaths, clipPaths, FillRule.NonZero);
                    break;
                case ShapeOperation.Difference:
                    solution = Clipper.Difference(subjectPaths, clipPaths, FillRule.NonZero);
                    break;
            }

            // 3. Return the most significant path (Clipper might return multiple if the shape splits)
            if (solution.Count > 0)
            {
                var simplified = Clipper.SimplifyPaths(solution, 0.05 * 1000); // 1000 is our CLIPPER_SCALE
                var largestPath = simplified.OrderByDescending(p => Math.Abs(Clipper.Area(p))).First();
                return Polygon.FromClipperPath(largestPath);
            }

            return null;
        }
        private ShapeOperation HandleKeys(EditorInputState input)
        {
            var kbs = input.CurrentKeyboard;
            if (input.CurrentKeyboard.CapsLock) { Mode = true; } else { Mode = false; }
            if (kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift))
                return ShapeOperation.Union;
            else if (kbs.IsKeyDown(Keys.LeftControl) || kbs.IsKeyDown(Keys.RightControl))
                return ShapeOperation.Intersection;
            else if (kbs.IsKeyDown(Keys.LeftAlt))
                return ShapeOperation.Difference;
            else
                return ShapeOperation.None;
        }
        private Vector2 Snap(Vector2 pos) => GridHelper.SnapCoordinate(pos);

        private bool IsValidLayer(Layer l) => l is ControlLayer;

        private Color GetLayerColor(LayerType type)
        {
            switch (type)
            {
                case LayerType.Control: return Color.Red;
                default: return Color.White; // Fallback color
            }
        }
        private int GetObjectCount(Layer ActiveLayer)
        {
            if(ActiveLayer is ControlLayer col) { return col.Shapes.Count; }
            else return -1;
        }
        public string GetShortcutHints() => "CAPS: Grid Snap | SHIFT: Union | ALT: Difference | CTRL: Intersect";
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!_isDrawing) return;
            Vector2 start = _startPos;
            Vector2 end = input.MouseWorldPosition;

            if (Mode)
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
            var color = GetLayerColor(type);
            sb.DrawRectangle(rect, color, 1f / input.Zoom);
        }
    }
    public class SelectionTool : ITool
    {
        public string Name => "Selection";
        public string IconName { get; set; } = "Selection";

        private enum InteractionMode { None, Moving, Resizing }
        private InteractionMode _currentMode = InteractionMode.None;
        private int _activeHandle = -1; // 0-3: Corners, 4-7: Edges
        private MapObject CurrentSelect { get; set; }
        private Vector2 _initialPos, _initialSize;
        private Vector2 _dragOffset;
        private EditorState _es;
        private bool _isSpineHandle = false;
        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            _es = editorState;
            if (_es == null) return;

            if (!(editorState.UI.FocusedElement is UITextBox))
            {
                if (input.CurrentKeyboard.IsKeyDown(Keys.Delete) && input.PreviousKeyboard.IsKeyUp(Keys.Delete))
                {
                    if (CurrentSelect != null)
                    {
                        var layer = _es.ActiveMap.Layers.FirstOrDefault(l =>
                            (l is ObjectLayer ol && ol.Objects.Contains(CurrentSelect)) ||
                            (l is ControlLayer cl && (cl.Rectangles.Contains(CurrentSelect) || cl.Shapes.Contains(CurrentSelect) || cl.Points.Contains(CurrentSelect))));

                        if (layer is ObjectLayer targetLayer)
                        {
                            eventBus.Publish(new RemoveObjectCommand(targetLayer, CurrentSelect));
                            editorState.Selection.SelectedMapObject = null;
                            CurrentSelect = null;
                            _currentMode = InteractionMode.None;
                            return;
                        }
                    }
                }
            }

            if (input.IsNewLeftClick)
            {
                // Check if clicking on an individual vertex handle of an already selected ShapeObject
                if (_es.Selection.SelectedMapObject is ShapeObject poly)
                {
                    _activeHandle = GetVertexHandleAt(poly, input.MouseWorldPosition, input.Zoom, out _isSpineHandle);
                    if (_activeHandle != -1)
                    {
                        _currentMode = InteractionMode.Resizing; // Abuse resizing mode for vertex dragging
                        _initialPos = poly.Position;
                        _initialSize = GetObjectSize(poly);
                    }
                }

                if (_currentMode == InteractionMode.None)
                {
                    bool absoluteMode = input.CurrentKeyboard.IsKeyDown(Keys.LeftAlt);
                    var hit = FindTopObject(toolInput.ActiveLayer, input.MouseWorldPosition, absoluteMode);
                    editorState.Selection.SelectedMapObject = hit;

                    if (hit != null)
                    {
                        _currentMode = InteractionMode.Moving;
                        _initialPos = hit.Position;
                        _initialSize = GetObjectSize(hit);
                        _dragOffset = hit.Position - input.MouseWorldPosition;
                    }
                }

                CurrentSelect = editorState.Selection.SelectedMapObject;
            }

            if (CurrentSelect != null && input.LeftHold && _currentMode != InteractionMode.None)
            {
                if (_currentMode == InteractionMode.Moving)
                {
                    Vector2 newPos = input.MouseWorldPosition + _dragOffset;
                    if (input.CurrentKeyboard.CapsLock)
                        newPos = new Vector2((float)Math.Round(newPos.X / 16) * 16, (float)Math.Round(newPos.Y / 16) * 16);

                    Vector2 delta = newPos - CurrentSelect.Position;

                    if (CurrentSelect is ShapeObject poly)
                    {
                        poly.Shape.Offset(delta);
                        poly.UpdateBoundsFromVertices();
                    }
                    else
                    {
                        CurrentSelect.Position = newPos;
                    }
                }
                else if (_currentMode == InteractionMode.Resizing)
                {
                    if (CurrentSelect is ShapeObject selPoly && _activeHandle != -1)
                    {
                        // Drag single node handle
                        Vector2 targetPos = input.MouseWorldPosition;
                        if (input.CurrentKeyboard.CapsLock)
                        {
                            targetPos = new Vector2((float)Math.Round(targetPos.X / 16) * 16, (float)Math.Round(targetPos.Y / 16) * 16);
                        }

                        if (_isSpineHandle)
                        {
                            selPoly.SpineWaypoints[_activeHandle] = targetPos;
                        }
                        else
                        {
                            selPoly.Shape.Vertices[_activeHandle] = targetPos;
                        }
                        selPoly.UpdateBoundsFromVertices();
                    }
                    else
                    {
                        // Standard rectangle bounds scaling...
                        ApplyResize(CurrentSelect, input.MouseWorldPosition, _activeHandle, input.CurrentKeyboard.CapsLock);
                    }
                }
            }

            if (CurrentSelect != null && _currentMode == InteractionMode.None)
            {
                HandleKeyboardMovement(CurrentSelect, input, toolInput.ActiveLayer);
            }

            if (input.CurrentMouse.LeftButton == ButtonState.Released && _currentMode != InteractionMode.None)
            {
                if (CurrentSelect != null)
                {
                    Vector2 currentSize = GetObjectSize(CurrentSelect);
                    if (CurrentSelect.Position != _initialPos || currentSize != _initialSize)
                    {
                        eventBus.Publish(new TransformObjectCommand(CurrentSelect, _initialPos, CurrentSelect.Position, _initialSize, currentSize));
                    }
                }
                _currentMode = InteractionMode.None;
                _activeHandle = -1;
            }
        }

        private void HandleKeyboardMovement(MapObject obj, EditorInputState input, Layer activeLayer)
        {
            if (_es.UI.FocusedElement is UITextBox) return;

            bool snapMode = input.CurrentKeyboard.CapsLock;
            float speed = snapMode ? 16f : 1f;
            Vector2 move = Vector2.Zero;

            bool discrete = snapMode;
            bool up = discrete ? (input.CurrentKeyboard.IsKeyDown(Keys.W) && input.PreviousKeyboard.IsKeyUp(Keys.W)) : input.CurrentKeyboard.IsKeyDown(Keys.W);
            bool down = discrete ? (input.CurrentKeyboard.IsKeyDown(Keys.S) && input.PreviousKeyboard.IsKeyUp(Keys.S)) : input.CurrentKeyboard.IsKeyDown(Keys.S);
            bool left = discrete ? (input.CurrentKeyboard.IsKeyDown(Keys.A) && input.PreviousKeyboard.IsKeyUp(Keys.A)) : input.CurrentKeyboard.IsKeyDown(Keys.A);
            bool right = discrete ? (input.CurrentKeyboard.IsKeyDown(Keys.D) && input.PreviousKeyboard.IsKeyUp(Keys.D)) : input.CurrentKeyboard.IsKeyDown(Keys.D);

            if (up) move.Y -= speed;
            if (down) move.Y += speed;
            if (left) move.X -= speed;
            if (right) move.X += speed;

            if (move != Vector2.Zero)
            {
                if (obj != null)
                {
                    if (obj is ShapeObject poly)
                    {
                        poly.Shape.Offset(move);
                        poly.UpdateBoundsFromVertices();
                    }
                    else obj.Position += move;
                }
            }
        }

        private int GetHandleAt(MapObject obj, Vector2 mouseWorld, float zoom)
        {
            float handleSize = 8f / zoom;
            RectangleF bounds = GetObjectBounds(obj);
            if (bounds.IsEmpty) return -1;

            Vector2[] corners = {
            new Vector2(bounds.Left, bounds.Top),
            new Vector2(bounds.Right, bounds.Top),
            new Vector2(bounds.Right, bounds.Bottom),
            new Vector2(bounds.Left, bounds.Bottom)
        };

            for (int i = 0; i < corners.Length; i++)
            {
                if (Vector2.Distance(mouseWorld, corners[i]) < handleSize) return i;
            }
            return -1;
        }

        private void ApplyResize(MapObject obj, Vector2 mouseWorld, int handle, bool snap)
        {
            if (snap) mouseWorld = new Vector2((float)Math.Round(mouseWorld.X / 16) * 16, (float)Math.Round(mouseWorld.Y / 16) * 16);

            Vector2 oldPos = obj.Position;
            Vector2 oldSize = GetObjectSize(obj);

            if (obj is RectangleObject rect)
            {
                Vector2 pos = rect.Position;
                Vector2 size = rect.Size;
                Vector2 bottomRight = pos + size;

                switch (handle)
                {
                    case 0: pos = Vector2.Min(mouseWorld, bottomRight - Vector2.One); size = bottomRight - pos; break;
                    case 1: pos.Y = Math.Min(mouseWorld.Y, bottomRight.Y - 1); size = new Vector2(Math.Max(1, mouseWorld.X - pos.X), bottomRight.Y - pos.Y); break;
                    case 2: size = Vector2.Max(Vector2.One, mouseWorld - pos); break;
                    case 3: pos.X = Math.Min(mouseWorld.X, bottomRight.X - 1); size = new Vector2(bottomRight.X - pos.X, Math.Max(1, mouseWorld.Y - pos.Y)); break;
                }
                rect.Position = pos; rect.Size = size;
            }
            else if (obj is ShapeObject poly)
            {
                Vector2 pos = poly.Position;
                Vector2 size = poly.Size;
                Vector2 br = pos + size;

                Vector2 newPos = pos;
                Vector2 newSize = size;

                switch (handle)
                {
                    case 0: newPos = mouseWorld; newSize = br - mouseWorld; break;
                    case 1: newPos.Y = mouseWorld.Y; newSize = new Vector2(mouseWorld.X - pos.X, br.Y - mouseWorld.Y); break;
                    case 2: newSize = mouseWorld - pos; break;
                    case 3: newPos.X = mouseWorld.X; newSize = new Vector2(br.X - mouseWorld.X, mouseWorld.Y - pos.Y); break;
                }

                Vector2 anchor = GetAnchor(handle, pos, br);
                Vector2 scale = new Vector2(
                    oldSize.X != 0 ? newSize.X / oldSize.X : 1,
                    oldSize.Y != 0 ? newSize.Y / oldSize.Y : 1
                );

                poly.Shape.Scale(scale, anchor);
                poly.UpdateBoundsFromVertices();
            }
            else if (obj is PointObject pt)
            {
                pt.Radius = Math.Max(1, Vector2.Distance(pt.Position, mouseWorld));
            }
        }

        private Vector2 GetAnchor(int handle, Vector2 tl, Vector2 br)
        {
            return handle switch
            {
                0 => br,
                1 => new Vector2(tl.X, br.Y),
                2 => tl,
                3 => new Vector2(br.X, tl.Y),
                _ => tl
            };
        }
        private int GetVertexHandleAt(ShapeObject poly, Vector2 mouseWorld, float zoom, out bool isSpine)
        {
            float handleSize = 8f / zoom;
            isSpine = false;

            // Check Spine waypoints first
            if (poly.SpineWaypoints != null)
            {
                for (int i = 0; i < poly.SpineWaypoints.Count; i++)
                {
                    if (Vector2.Distance(mouseWorld, poly.SpineWaypoints[i]) < handleSize)
                    {
                        isSpine = true;
                        return i;
                    }
                }
            }

            // Check Poly vertices
            for (int i = 0; i < poly.Shape.Vertices.Count; i++)
            {
                if (Vector2.Distance(mouseWorld, poly.Shape.Vertices[i]) < handleSize)
                {
                    return i;
                }
            }

            return -1;
        }
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (_es == null) return;
            var selected = _es.Selection.SelectedMapObject;
            if (selected == null) return;

            RectangleF b = GetObjectBounds(selected);
            float hs = 6f / input.Zoom;
            if (selected is ShapeObject selPoly)
            {
                // Render boundary vertex nodes
                for (int i = 0; i < selPoly.Shape.Vertices.Count; i++)
                {
                    Vector2 v = selPoly.Shape.Vertices[i];
                    sb.FillRectangle(new RectangleF(v.X - hs / 2, v.Y - hs / 2, hs, hs), Color.White);
                    sb.DrawRectangle(new RectangleF(v.X - hs / 2, v.Y - hs / 2, hs, hs), Color.Teal, 1f / input.Zoom);
                }

                // Render spine waypoints nodes
                if (selPoly.SpineWaypoints != null)
                {
                    for (int i = 0; i < selPoly.SpineWaypoints.Count; i++)
                    {
                        Vector2 wp = selPoly.SpineWaypoints[i];
                        sb.FillRectangle(new RectangleF(wp.X - hs / 2, wp.Y - hs / 2, hs, hs), Color.Orange);
                        sb.DrawRectangle(new RectangleF(wp.X - hs / 2, wp.Y - hs / 2, hs, hs), Color.DarkOrange, 1f / input.Zoom);
                    }
                }
            }
            else
            {
                // Standard rectangle boxes selection preview...
                sb.DrawRectangle(b, GetObjectColor(selected.Type) * 0.4f, 1 / input.Zoom, 0);
                sb.FillRectangle(new RectangleF(b.Left - hs / 2, b.Top - hs / 2, hs, hs), Color.White);
                sb.FillRectangle(new RectangleF(b.Right - hs / 2, b.Top - hs / 2, hs, hs), Color.White);
                sb.FillRectangle(new RectangleF(b.Right - hs / 2, b.Bottom - hs / 2, hs, hs), Color.White);
                sb.FillRectangle(new RectangleF(b.Left - hs / 2, b.Bottom - hs / 2, hs, hs), Color.White);

                if (selected.Links.Count > 0)
                {
                    Vector2 startPos = b.Center;

                    foreach (var link in selected.Links)
                    {
                        var targetObj = FindObjectByID(link.TargetID);
                        if (targetObj != null)
                        {
                            RectangleF targetBounds = GetObjectBounds(targetObj);
                            Color linkColor = targetObj.Type switch
                            {
                                ObjectType.Prop => Color.LimeGreen,
                                ObjectType.Rectangle => Color.DeepSkyBlue,
                                ObjectType.Shape => Color.OrangeRed,
                                ObjectType.Point => Color.Yellow,
                                _ => Color.Cyan
                            };

                            string label = $"{link.SourcePort} -> {link.TargetPort}";

                            Pixel_Simulations.UI.UIDrawExtensions.DrawBezierCurve(
                                sb,
                                Pixel_Simulations.UI.UITheme.DefaultFont,
                                startPos,
                                targetBounds.Center,
                                Pixel_Simulations.Studio.NodePort.Right,
                                Pixel_Simulations.Studio.NodePort.Left,
                                linkColor,
                                2f / input.Zoom,
                                Pixel_Simulations.Studio.LineStyle.Dashed,
                                label
                            );

                            sb.DrawRectangle(targetBounds, linkColor * 0.8f, 2f / input.Zoom);
                        }
                    }
                }
            }
        }

        private Color GetObjectColor(ObjectType type)
        {
            return type switch
            {
                ObjectType.Shape => Color.Red,
                ObjectType.Rectangle => Color.Blue,
                _ => Color.White
            };
        }

        private RectangleF GetObjectBounds(MapObject obj)
        {
            if (obj is RectangleObject r) return new RectangleF(r.Position, r.Size);
            if (obj is ShapeObject s) return s.Shape.GetBounds();
            if (obj is PointObject p) return new RectangleF(p.Position.X - p.Radius, p.Position.Y - p.Radius, p.Radius * 2, p.Radius * 2);
            if (obj is PropObject prop)
            {
                var prefab = _es.PrefabManager.GetPrefab(prop.PrefabID);
                if (prefab != null)
                {
                    int w = Math.Max(prefab.SourceRect.Width, prefab.CanvasSize.X);
                    int h = Math.Max(prefab.SourceRect.Height, prefab.CanvasSize.Y);
                    return new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, w * prop.Scale.X, h * prop.Scale.Y);
                }
            }
            return RectangleF.Empty;
        }

        private Vector2 GetObjectSize(MapObject obj)
        {
            if (obj is RectangleObject r) return r.Size;
            if (obj is ShapeObject s) return s.Size;
            if (obj is PointObject p) return new Vector2(p.Radius, p.Radius);
            if (obj is PropObject prop)
            {
                var prefab = _es.PrefabManager.GetPrefab(prop.PrefabID);
                if (prefab != null)
                {
                    int w = Math.Max(prefab.SourceRect.Width, prefab.CanvasSize.X);
                    int h = Math.Max(prefab.SourceRect.Height, prefab.CanvasSize.Y);
                    return new Vector2(w, h);
                }
            }
            return Vector2.Zero;
        }

        private MapObject FindTopObject(Layer activeLayer, Vector2 mouseWorld, bool searchAllLayers)
        {
            var layersToSearch = searchAllLayers ? _es.ActiveMap.Layers.AsEnumerable() : new List<Layer> { activeLayer };

            foreach (var layer in layersToSearch.Reverse())
            {
                if (!layer.IsVisible || layer.IsLocked) continue;

                if (layer is ObjectLayer objLayer)
                {
                    for (int i = objLayer.Objects.Count - 1; i >= 0; i--)
                    {
                        var obj = objLayer.Objects[i];
                        if (obj is PropObject prop)
                        {
                            var prefab = _es.PrefabManager.GetPrefab(prop.PrefabID);
                            if (prefab != null)
                            {
                                int w = Math.Max(prefab.SourceRect.Width, prefab.CanvasSize.X);
                                int h = Math.Max(prefab.SourceRect.Height, prefab.CanvasSize.Y);

                                RectangleF bounds = new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, w, h);
                                if (bounds.Contains(mouseWorld)) return prop;
                            }
                        }
                    }
                }

                if (layer is ControlLayer trig)
                {
                    var shap = trig.Shapes.LastOrDefault(r => new RectangleF(r.Position, r.Size).Contains(mouseWorld));
                    if (shap != null) return shap;

                    var rect = trig.Rectangles.LastOrDefault(r => new RectangleF(r.Position, r.Size).Contains(mouseWorld));
                    if (rect != null) return rect;

                    var pt = trig.Points.LastOrDefault(p => Vector2.Distance(p.Position, mouseWorld) <= p.Radius);
                    if (pt != null) return pt;
                }
            }
            return null;
        }

        private MapObject FindObjectByID(string id)
        {
            foreach (var layer in _es.ActiveMap.Layers)
            {
                if (layer is ControlLayer cl)
                {
                    var match = cl.Rectangles.Cast<MapObject>().Concat(cl.Shapes).Concat(cl.Points).FirstOrDefault(o => o.ID == id);
                    if (match != null) return match;
                }
                else if (layer is ObjectLayer ol && ol.Type == LayerType.Object)
                {
                    var match = ol.Objects.FirstOrDefault(o => o.ID == id);
                    if (match != null) return match;
                }
            }
            return null;
        }
        public string GetShortcutHints() => "ALT: Pick Any Layer | DEL: Delete | CAPS: Grid Nudge";
    }
    public class PointPlacerTool : ITool
    {
        public string Name => "PointPlacer";
        public string IconName { get; set; } = "PointPlacer";
        private Vector2 _startWorldPos;
        private bool _isDrawing = false;
        private float _currentRadius = 0f;

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            input.Drawing = _isDrawing;

            // This tool ONLY works on TriggerLayers.
            if (!(toolInput.ActiveLayer is ControlLayer controlLayer))
            {
                _isDrawing = false;
                return;
            }

            if (input.IsNewLeftClick)
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
                        eventBus.Publish<IUndoableCommand>(new AddPointCommand(controlLayer, pointObject));
                    }
                    _isDrawing = false;
                }
            }
        }
        public string GetShortcutHints() => "Drag Left mouse to expand Radius";
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!_isDrawing) return;

            // Draw a circle and crosshair for the preview.
            sb.DrawCircle(_startWorldPos, _currentRadius, 32, Color.Yellow, 1f / input.Zoom);
            sb.DrawLine(_startWorldPos - new Vector2(4, 0), _startWorldPos + new Vector2(4, 0), Color.Yellow, 1f / input.Zoom);
            sb.DrawLine(_startWorldPos - new Vector2(0, 4), _startWorldPos + new Vector2(0, 4), Color.Yellow, 1f / input.Zoom);
        }
    }
    public enum PaintChannel { R_Biome, G_SpawnType, B_Elevation, A_SpawnIndex }
    public enum BrushMode { Solid, DetailAdd, DetailSub }
    public enum DetailerBehavior { Overwrite, Additive }
    public class TerrainBrushTool : ITool
    {
        public string Name => "TerrainBrush";
        public string IconName { get; set; } = "TerrainBrush";

        // --- CONFIGURATION ---
        public int BrushGridSize { get; set; } = 1;
        public float BrushRadius { get; set; } = 32f;
        public int TargetValue { get; set; } = 128;

        public PaintChannel ActiveChannel { get; set; } = PaintChannel.B_Elevation;
        public BrushMode ActiveMode { get; set; } = BrushMode.Solid;
        public DetailerBehavior ActiveBehavior { get; set; } = DetailerBehavior.Additive;

        private bool _isDrawing = false;
        private Texture2D _solidPixelTexture;
        private Texture2D _brushTexture;
        public bool BrushNeedsUpdate { get; set; } = true;

        private Dictionary<Point, Color[]> _strokeBefore;
        private HashSet<Point> _touchedChunksThisStroke;

        private Point _lastDrawCell;
        private Vector2 _lastDrawPos;

        // --- BLEND STATES ---
        private static readonly BlendState SolidR = new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Max, ColorWriteChannels = ColorWriteChannels.Red };
        private static readonly BlendState SolidG = new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Max, ColorWriteChannels = ColorWriteChannels.Green };
        private static readonly BlendState SolidB = new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Max, ColorWriteChannels = ColorWriteChannels.Blue };
        private static readonly BlendState SolidA = new BlendState { AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.One, AlphaBlendFunction = BlendFunction.Max, ColorWriteChannels = ColorWriteChannels.Alpha };

        private static readonly BlendState DetailAddBlue = new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.Add, ColorWriteChannels = ColorWriteChannels.Blue };
        private static readonly BlendState DetailSubBlue = new BlendState { ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One, ColorBlendFunction = BlendFunction.ReverseSubtract, ColorWriteChannels = ColorWriteChannels.Blue };

        // Overwrite BlendState: Selectively modifies ONLY the Blue Channel using SourceAlpha interpolation
        private static readonly BlendState OverwriteElevation = new BlendState
        {
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            ColorBlendFunction = BlendFunction.Add,
            ColorWriteChannels = ColorWriteChannels.Blue
        };

        private static readonly BlendState EraseR = new BlendState { ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add, ColorWriteChannels = ColorWriteChannels.Red };
        private static readonly BlendState EraseG = new BlendState { ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add, ColorWriteChannels = ColorWriteChannels.Green };
        private static readonly BlendState EraseB = new BlendState { ColorSourceBlend = Blend.Zero, ColorDestinationBlend = Blend.Zero, ColorBlendFunction = BlendFunction.Add, ColorWriteChannels = ColorWriteChannels.Blue };
        private static readonly BlendState EraseA = new BlendState { AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.Zero, AlphaBlendFunction = BlendFunction.Add, ColorWriteChannels = ColorWriteChannels.Alpha };

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            if (!(toolInput.ActiveLayer is MaskLayer maskLayer)) return;

            var kbs = input.CurrentKeyboard;
            var prevKbs = input.PreviousKeyboard;
            bool shift = kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift);
            bool isErasing = kbs.IsKeyDown(Keys.LeftControl) || kbs.IsKeyDown(Keys.RightControl);

            // --- SAFELY TERMINATE STROKES OUTSIDE VIEWPORT ---
            if (editorState.UI.ActivePanelName != "Viewport" && _isDrawing)
            {
                TerminateStroke(maskLayer, eventBus, editorState._graphics);
                return;
            }

            // --- VALUE & SIZE ADJUSTMENTS ---
            if (kbs.IsKeyDown(Keys.OemCloseBrackets) && prevKbs.IsKeyUp(Keys.OemCloseBrackets))
            {
                if (shift)
                {
                    if (ActiveMode == BrushMode.Solid)
                        BrushGridSize = MathHelper.Clamp(BrushGridSize + 2, 1, 31);
                    else
                        BrushRadius = MathHelper.Clamp(BrushRadius + 4f, 8f, 256f);
                }
                else
                {
                    AdjustValue(editorState, true);
                }
                BrushNeedsUpdate = true;
            }

            if (kbs.IsKeyDown(Keys.OemOpenBrackets) && prevKbs.IsKeyUp(Keys.OemOpenBrackets))
            {
                if (shift)
                {
                    if (ActiveMode == BrushMode.Solid)
                        BrushGridSize = MathHelper.Clamp(BrushGridSize - 2, 1, 31);
                    else
                        BrushRadius = MathHelper.Clamp(BrushRadius - 4f, 8f, 256f);
                }
                else
                {
                    AdjustValue(editorState, false);
                }
                BrushNeedsUpdate = true;
            }

            // --- INPUT HANDLING ---
            if (input.LeftHold)
            {
                if (!_isDrawing)
                {
                    if (input.IsNewLeftClick && editorState.UI.ActivePanelName == "Viewport")
                    {
                        _isDrawing = true;
                        _strokeBefore = new Dictionary<Point, Color[]>();
                        _touchedChunksThisStroke = new HashSet<Point>();

                        _lastDrawCell = input.MouseGridCell;
                        _lastDrawPos = input.MouseWorldPosition;

                        PaintStamp(maskLayer, _lastDrawCell, editorState, isErasing);
                    }
                }
                else
                {
                    if (ActiveMode == BrushMode.Solid)
                    {
                        if (input.MouseGridCell != _lastDrawCell)
                        {
                            _lastDrawCell = input.MouseGridCell;
                            PaintStamp(maskLayer, _lastDrawCell, editorState, isErasing);
                        }
                    }
                    else
                    {
                        float dist = Vector2.Distance(_lastDrawPos, input.MouseWorldPosition);
                        float spacing = Math.Max(2f, BrushRadius * 0.15f);

                        if (dist >= spacing)
                        {
                            Vector2 dir = Vector2.Normalize(input.MouseWorldPosition - _lastDrawPos);
                            int steps = (int)Math.Floor(dist / spacing);

                            for (int i = 1; i <= steps; i++)
                            {
                                Vector2 stampPos = _lastDrawPos + dir * (spacing * i);
                                PaintStamp(maskLayer, input.MouseGridCell, editorState, isErasing);
                            }

                            _lastDrawPos = _lastDrawPos + dir * (spacing * steps);
                        }
                    }
                }
            }
            else if (_isDrawing)
            {
                TerminateStroke(maskLayer, eventBus, editorState._graphics);
            }
        }

        private void TerminateStroke(MaskLayer layer, EventBus eventBus, GraphicsDevice gd)
        {
            _isDrawing = false;
            if (_touchedChunksThisStroke != null && _touchedChunksThisStroke.Count > 0)
            {
                var strokeAfter = new Dictionary<Point, Color[]>();
                foreach (var chunkCoord in _touchedChunksThisStroke)
                {
                    Color[] data = new Color[MaskLayer.CHUNK_PIXEL_SIZE * MaskLayer.CHUNK_PIXEL_SIZE];
                    layer.Chunks[chunkCoord].GetData(data);
                    strokeAfter[chunkCoord] = data;
                }
                eventBus.Publish(new PaintMaskCommand(layer, _strokeBefore, strokeAfter, gd));
            }
            _strokeBefore = null;
            _touchedChunksThisStroke = null;
        }

        private void AdjustValue(EditorState es, bool increase)
        {
            if (ActiveChannel == PaintChannel.B_Elevation)
            {
                var sortedElevations = es.MaskData?.Elevations?
                    .Select(e => e.Value)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                if (sortedElevations == null || sortedElevations.Count == 0)
                {
                    sortedElevations = new List<int> { 0, 15, 120, 128, 160, 192, 200, 240, 255 };
                }

                int currentIndex = 0;
                int minDifference = int.MaxValue;
                for (int i = 0; i < sortedElevations.Count; i++)
                {
                    int diff = Math.Abs(sortedElevations[i] - TargetValue);
                    if (diff < minDifference)
                    {
                        minDifference = diff;
                        currentIndex = i;
                    }
                }

                if (increase)
                    currentIndex = Math.Min(currentIndex + 1, sortedElevations.Count - 1);
                else
                    currentIndex = Math.Max(currentIndex - 1, 0);

                TargetValue = sortedElevations[currentIndex];
            }
            else
            {
                TargetValue = MathHelper.Clamp(TargetValue + (increase ? 5 : -5), 0, 255);
            }
        }

        private Texture2D GetSolidPixelTexture(GraphicsDevice gd)
        {
            if (_solidPixelTexture == null)
            {
                _solidPixelTexture = new Texture2D(gd, 1, 1);
                _solidPixelTexture.SetData(new[] { Color.White });
            }
            return _solidPixelTexture;
        }

        private void PaintStamp(MaskLayer layer, Point centerCell, EditorState es, bool isErasing)
        {
            if (ActiveMode == BrushMode.Solid)
            {
                PaintGridStamp(layer, centerCell, es, isErasing);
            }
            else
            {
                PaintNoiseStamp(layer, es.Input.MouseWorldPosition, es, isErasing);
            }
        }

        // --- GRID ALIGNED PAINTING ---
        private void PaintGridStamp(MaskLayer layer, Point centerCell, EditorState es, bool isErasing)
        {
            var gd = es._graphics;
            int halfSize = (BrushGridSize - 1) / 2;
            const int CELLS_PER_MASK_CHUNK = 16;

            var prevTargets = gd.GetRenderTargets();

            using (var sb = new SpriteBatch(gd))
            {
                var cellsByChunk = new Dictionary<Point, List<Point>>();

                for (int dx = -halfSize; dx <= halfSize; dx++)
                {
                    for (int dy = -halfSize; dy <= halfSize; dy++)
                    {
                        Point targetCell = new Point(centerCell.X + dx, centerCell.Y + dy);

                        Point chunkCoord = new Point(
                            (int)Math.Floor((double)targetCell.X / CELLS_PER_MASK_CHUNK),
                            (int)Math.Floor((double)targetCell.Y / CELLS_PER_MASK_CHUNK)
                        );

                        if (!cellsByChunk.ContainsKey(chunkCoord))
                            cellsByChunk[chunkCoord] = new List<Point>();

                        cellsByChunk[chunkCoord].Add(targetCell);
                    }
                }

                foreach (var kvp in cellsByChunk)
                {
                    Point chunkCoord = kvp.Key;
                    var chunkRT = layer.GetOrCreateChunk(chunkCoord, gd);

                    if (_touchedChunksThisStroke != null && !_touchedChunksThisStroke.Contains(chunkCoord))
                    {
                        Color[] data = new Color[MaskLayer.CHUNK_PIXEL_SIZE * MaskLayer.CHUNK_PIXEL_SIZE];
                        chunkRT.GetData(data);
                        _strokeBefore[chunkCoord] = data;
                        _touchedChunksThisStroke.Add(chunkCoord);
                    }

                    gd.SetRenderTarget(chunkRT);

                    BlendState activeState = isErasing
                        ? ActiveChannel switch { PaintChannel.R_Biome => EraseR, PaintChannel.G_SpawnType => EraseG, PaintChannel.A_SpawnIndex => EraseA, _ => EraseB }
                        : ActiveChannel switch { PaintChannel.R_Biome => SolidR, PaintChannel.G_SpawnType => SolidG, PaintChannel.A_SpawnIndex => SolidA, _ => SolidB };

                    sb.Begin(blendState: activeState, samplerState: SamplerState.PointClamp);

                    Texture2D pixelTex = GetSolidPixelTexture(gd);
                    Color paintColor = Color.White;

                    if (!isErasing)
                    {
                        byte val = (byte)TargetValue;
                        paintColor = ActiveChannel switch
                        {
                            PaintChannel.R_Biome => new Color((int)val, 0, 0, 255),
                            PaintChannel.G_SpawnType => new Color(0, (int)val, 0, 255),
                            PaintChannel.B_Elevation => new Color(0, 0, (int)val, 255),
                            PaintChannel.A_SpawnIndex => new Color(0, 0, 0, (int)val),
                            _ => Color.White
                        };
                    }

                    foreach (Point targetCell in kvp.Value)
                    {
                        int localCellX = targetCell.X - (chunkCoord.X * CELLS_PER_MASK_CHUNK);
                        int localCellY = targetCell.Y - (chunkCoord.Y * CELLS_PER_MASK_CHUNK);

                        Rectangle destRect = new Rectangle(
                            localCellX * es.CELL_SIZE,
                            localCellY * es.CELL_SIZE,
                            es.CELL_SIZE,
                            es.CELL_SIZE
                        );

                        sb.Draw(pixelTex, destRect, paintColor);
                    }

                    sb.End();
                }
            }

            gd.SetRenderTargets(prevTargets);
        }

        // --- ORGANIC NOISE & CIRCLE DETAIL PAINTING ---
        private void PaintNoiseStamp(MaskLayer layer, Vector2 worldPos, EditorState es, bool isErasing)
        {
            var gd = es._graphics;
            EnsureBrushTexture(gd, es.noiseManager);

            float r = BrushRadius;
            int minChunkX = (int)Math.Floor((worldPos.X - r) / MaskLayer.CHUNK_PIXEL_SIZE);
            int maxChunkX = (int)Math.Floor((worldPos.X + r) / MaskLayer.CHUNK_PIXEL_SIZE);
            int minChunkY = (int)Math.Floor((worldPos.Y - r) / MaskLayer.CHUNK_PIXEL_SIZE);
            int maxChunkY = (int)Math.Floor((worldPos.Y + r) / MaskLayer.CHUNK_PIXEL_SIZE);

            var prevTargets = gd.GetRenderTargets();

            using (var sb = new SpriteBatch(gd))
            {
                for (int y = minChunkY; y <= maxChunkY; y++)
                {
                    for (int x = minChunkX; x <= maxChunkX; x++)
                    {
                        Point chunkCoord = new Point(x, y);
                        var chunkRT = layer.GetOrCreateChunk(chunkCoord, gd);

                        if (_touchedChunksThisStroke != null && !_touchedChunksThisStroke.Contains(chunkCoord))
                        {
                            Color[] data = new Color[MaskLayer.CHUNK_PIXEL_SIZE * MaskLayer.CHUNK_PIXEL_SIZE];
                            chunkRT.GetData(data);
                            _strokeBefore[chunkCoord] = data;
                            _touchedChunksThisStroke.Add(chunkCoord);
                        }

                        Vector2 localPos = new Vector2(
                            worldPos.X - (x * MaskLayer.CHUNK_PIXEL_SIZE),
                            worldPos.Y - (y * MaskLayer.CHUNK_PIXEL_SIZE)
                        );

                        gd.SetRenderTarget(chunkRT);

                        // Select BlendState
                        BlendState activeState = EraseR;
                        if (isErasing)
                        {
                            activeState = ActiveChannel switch { PaintChannel.R_Biome => EraseR, PaintChannel.G_SpawnType => EraseG, PaintChannel.A_SpawnIndex => EraseA, _ => EraseB };
                        }
                        else
                        {
                            if (ActiveBehavior == DetailerBehavior.Overwrite)
                                activeState = OverwriteElevation;
                            else
                                activeState = ActiveMode == BrushMode.DetailAdd ? DetailAddBlue : DetailSubBlue;
                        }

                        sb.Begin(blendState: activeState, samplerState: SamplerState.PointClamp);

                        Color paintColor = isErasing ? new Color(0, 0, 0, 0) : Color.White;
                        if (!isErasing && ActiveBehavior == DetailerBehavior.Overwrite)
                        {
                            // Overwrite mode pushes target value modulated into the alpha channel
                            byte val = (byte)TargetValue;
                            paintColor = new Color(0, 0, (int)val, 255);
                        }

                        Vector2 origin = new Vector2(_brushTexture.Width / 2f, _brushTexture.Height / 2f);
                        sb.Draw(_brushTexture, localPos, null, paintColor, 0f, origin, 1f, SpriteEffects.None, 0f);

                        sb.End();
                    }
                }
            }

            gd.SetRenderTargets(prevTargets);
        }

        private void EnsureBrushTexture(GraphicsDevice gd, NoiseManager nm)
        {
            if (!BrushNeedsUpdate && _brushTexture != null) return;

            int diameter = (int)(BrushRadius * 2);
            if (diameter < 1) diameter = 1;

            if (_brushTexture != null && _brushTexture.Width != diameter)
            {
                _brushTexture.Dispose();
                _brushTexture = null;
            }

            if (_brushTexture == null) _brushTexture = new Texture2D(gd, diameter, diameter);
            Color[] data = new Color[diameter * diameter];

            Texture2D noiseTex = nm.GetActiveNoise();
            Color[] noiseData = null;
            if (noiseTex != null)
            {
                noiseData = new Color[noiseTex.Width * noiseTex.Height];
                noiseTex.GetData(noiseData);
            }

            float radius = diameter / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Vector2.Distance(center, new Vector2(x, y));
                    if (dist <= radius)
                    {
                        float noiseVal = 1f;
                        if (noiseData != null)
                        {
                            noiseVal = noiseData[(x % noiseTex.Width) + (y % noiseTex.Height) * noiseTex.Width].R / 255f;
                        }

                        // Circular radial edge falloff
                        float falloff = 1.0f - (dist / radius);

                        int intensityValue = 255;
                        if (ActiveBehavior == DetailerBehavior.Additive)
                        {
                            // Additive uses minor delta steps
                            intensityValue = (int)(15.0f * falloff * noiseVal);
                        }
                        else
                        {
                            // Overwrite behaves as a smooth stamp
                            intensityValue = (int)(255f * falloff * noiseVal);
                        }

                        data[x + y * diameter] = new Color(intensityValue, intensityValue, intensityValue, intensityValue);
                    }
                    else
                    {
                        data[x + y * diameter] = Color.Transparent;
                    }
                }
            }

            _brushTexture.SetData(data);
            BrushNeedsUpdate = false;
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!(toolInput.ActiveLayer is MaskLayer)) return;

            bool isErasing = input.CurrentKeyboard.IsKeyDown(Keys.LeftControl) || input.CurrentKeyboard.IsKeyDown(Keys.RightControl);
            Color cursorColor = ActiveChannel switch
            {
                PaintChannel.R_Biome => Color.Red,
                PaintChannel.G_SpawnType => Color.LimeGreen,
                PaintChannel.B_Elevation => Color.DeepSkyBlue,
                PaintChannel.A_SpawnIndex => Color.White,
                _ => Color.Cyan
            };
            if (isErasing) cursorColor = Color.Magenta;

            float thickness = 2f / input.Zoom;

            if (ActiveMode == BrushMode.Solid)
            {
                int halfSize = (BrushGridSize - 1) / 2;
                Point centerCell = input.MouseGridCell;

                RectangleF previewBounds = new RectangleF(
                    (centerCell.X - halfSize) * 16,
                    (centerCell.Y - halfSize) * 16,
                    BrushGridSize * 16,
                    BrushGridSize * 16
                );

                sb.DrawRectangle(previewBounds, cursorColor, thickness);

                Vector2 textPos = new Vector2(previewBounds.Right + 10, previewBounds.Top);
                string modeText = isErasing ? $"ERASE ({ActiveChannel})" : $"{ActiveChannel}: {TargetValue} ({BrushGridSize}x{BrushGridSize} Grid)";

                sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, modeText, textPos + Vector2.One, Color.Black, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
                sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, modeText, textPos, cursorColor, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
            }
            else
            {
                sb.DrawCircle(input.MouseWorldPosition, BrushRadius, 32, cursorColor, thickness);

                Vector2 textPos = input.MouseWorldPosition + new Vector2(BrushRadius + 10, -10) / input.Zoom;
                string modeText = isErasing ? $"ERASE ({ActiveChannel})" : $"{ActiveChannel}: {TargetValue} | {ActiveMode} ({ActiveBehavior}) (Radius: {BrushRadius:F0}px)";

                sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, modeText, textPos + Vector2.One, Color.Black, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
                sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, modeText, textPos, cursorColor, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
            }
        }

        public string GetShortcutHints() => "[ / ]: Adjust Value | SHIFT + [ / ]: Adjust Size | CTRL: Erase";
    }
    public class ReflectionTool : ITool
    {
        public string Name => "ReflectionPlane";
        public string IconName { get; set; } = "ReflectionPlane"; // Replace with a Mirror icon later

        private Vector2 _startPos;
        private bool _isDrawing = false;

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            // ONLY allow drawing on Control Layers
            if (toolInput.ActiveLayer.Type != LayerType.Control) return;

            if (input.IsNewLeftClick)
            {
                _startPos = input.MouseWorldPosition;
                if (input.CurrentKeyboard.CapsLock) _startPos = Snap(_startPos);
                _isDrawing = true;
            }

            if (_isDrawing && input.CurrentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
            {
                Vector2 endPos = input.MouseWorldPosition;
                if (input.CurrentKeyboard.CapsLock) endPos = Snap(endPos);

                // Prevent creating 0-size objects
                if (System.Math.Abs(_startPos.X - endPos.X) > 1 && System.Math.Abs(_startPos.Y - endPos.Y) > 1)
                {
                    Polygon drawnPoly = Polygon.FromRectangle(_startPos, endPos);

                    int currentCount = ((ControlLayer)toolInput.ActiveLayer).Shapes.Count;

                    var newReflection = new ShapeObject
                    {
                        Shape = drawnPoly,
                        Name = $"Reflection_{currentCount + 1}",
                        DebugColor = Color.DeepSkyBlue * 0.4f
                    };

                    // --- AUTO-POPULATE TAGS AND SHADER PROPERTIES ---
                    newReflection.Tags.Add(10);

                    // Direction: "Negative" = Floor/Water (Flipped Y). "Positive" = Mirror (Flipped X)
                    newReflection.Properties.Add("Direction", new MapProperty(PropertyType.Boolean, "True"));
                    newReflection.Properties.Add("Blur", new MapProperty(PropertyType.Float, "1.5"));
                    newReflection.Properties.Add("RippleSpeed", new MapProperty(PropertyType.Float, "1.0"));
                    newReflection.Properties.Add("Offset", new MapProperty(PropertyType.Float, "0.0")); // NEW!
                    newReflection.Properties.Add("Tint", new MapProperty(PropertyType.String, "128,180,255,200"));
                    newReflection.UpdateBoundsFromVertices();
                    eventBus.Publish(new AddPolygonCommand(toolInput.ActiveLayer, newReflection));

                    // Instantly select it so the Inspector opens with the properties ready to edit!
                    editorState.Selection.SelectedMapObject = newReflection;
                }

                _isDrawing = false;
            }
        }

        private Vector2 Snap(Vector2 pos) => new Vector2((float)System.Math.Floor(pos.X / 16) * 16, (float)System.Math.Floor(pos.Y / 16) * 16);

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!_isDrawing) return;

            Vector2 start = _startPos;
            Vector2 end = input.MouseWorldPosition;

            if (input.CurrentKeyboard.CapsLock)
            {
                start = Snap(start);
                end = Snap(end);
            }

            var rect = new RectangleF(System.Math.Min(start.X, end.X), System.Math.Min(start.Y, end.Y), System.Math.Abs(start.X - end.X), System.Math.Abs(start.Y - end.Y));

            sb.FillRectangle(rect, Color.DeepSkyBlue * 0.2f);
            sb.DrawRectangle(rect, Color.DeepSkyBlue, 2f / input.Zoom);
        }

        public string GetShortcutHints() => "CAPS: Grid Snap | Draw over water or mirrors";
    }
    public enum LightShapeMode { Circle, Rectangle }
    public class LightShapeTool : ITool
    {
        public string Name => "LightShape";
        public string IconName { get; set; } = "LightShape"; // Assign an icon in your JSON

        private Vector2 _startPos;
        private bool _isDrawing = false;
        private LightShapeMode _currentMode = LightShapeMode.Circle;

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            if (toolInput.ActiveLayer.Type != LayerType.Control) return;

            // Toggle Mode (Caps Lock = Rectangle, Off = Circle)
            _currentMode = input.CurrentKeyboard.CapsLock ? LightShapeMode.Rectangle : LightShapeMode.Circle;

            // --- 1. START DRAWING ---
            if (input.IsNewLeftClick)
            {
                _startPos = input.MouseWorldPosition;
                if (_currentMode == LightShapeMode.Rectangle) _startPos = Snap(_startPos);
                _isDrawing = true;
            }

            // --- 2. FINISH DRAWING ---
            if (_isDrawing && input.CurrentMouse.LeftButton == ButtonState.Released)
            {
                Vector2 endPos = input.MouseWorldPosition;
                if (_currentMode == LightShapeMode.Rectangle) endPos = Snap(endPos);

                float radius = Vector2.Distance(_startPos, endPos);

                // Prevent tiny/accidental clicks
                if (radius > 2f || (_currentMode == LightShapeMode.Rectangle && Math.Abs(_startPos.X - endPos.X) > 2))
                {
                    GenerateLinkedLightShapes(toolInput.ActiveLayer, _startPos, endPos, radius, eventBus);
                }

                _isDrawing = false;
            }
        }

        private void GenerateLinkedLightShapes(Layer activeLayer, Vector2 start, Vector2 end, float radius, EventBus bus)
        {
            int count = ((ControlLayer)activeLayer).Shapes.Count;
            ShapeObject sourceShape, falloffShape;

            if (_currentMode == LightShapeMode.Rectangle)
            {
                Polygon sourcePoly = Polygon.FromRectangle(start, end);
                Polygon falloffPoly = Polygon.FromRectangle(start + new Vector2(0, 64), end + new Vector2(0, 64));

                sourceShape = CreateShape("LightSource_Rect", sourcePoly, 10);
                falloffShape = CreateShape("LightFalloff_Rect", falloffPoly, 11);
            }
            else
            {
                Polygon sourcePoly = CreateCirclePolygon(start, radius, 16);
                Polygon falloffPoly = CreateCirclePolygon(start, radius * 2.5f, 16);

                sourceShape = CreateShape("LightSource_Circ", sourcePoly, 10);
                falloffShape = CreateShape("LightFalloff_Circ", falloffPoly, 11);
            }

            // --- THE FIX: LINK THEM USING THEIR UNIQUE IDs! ---
            sourceShape.Links.Add(new ObjectLink { SourcePort = "LightData", TargetID = falloffShape.ID, TargetPort = "FalloffMask" });
            falloffShape.Links.Add(new ObjectLink { SourcePort = "FalloffMask", TargetID = sourceShape.ID, TargetPort = "LightData" });

            sourceShape.Properties.Add("Color", new MapProperty(PropertyType.String, "255,220,150,255"));
            sourceShape.Properties.Add("Intensity", new MapProperty(PropertyType.Float, "1.0"));
            sourceShape.Properties.Add("Flicker", new MapProperty(PropertyType.Boolean, "False"));

            bus.Publish(new AddPolygonCommand(activeLayer, sourceShape));
            bus.Publish(new AddPolygonCommand(activeLayer, falloffShape));
        }

        private ShapeObject CreateShape(string namePrefix, Polygon poly, int tag)
        {
            var shape = new ShapeObject
            {
                Name = $"{namePrefix}_{Guid.NewGuid().ToString().Substring(0, 4)}",
                Shape = poly,
                DebugColor = Color.Yellow * 0.4f
            };
            shape.Tags.Add(tag);
            shape.UpdateBoundsFromVertices();
            return shape;
        }

        private Polygon CreateCirclePolygon(Vector2 center, float radius, int segments)
        {
            var verts = new List<Vector2>();
            float angleStep = MathHelper.TwoPi / segments;
            for (int i = 0; i < segments; i++)
            {
                verts.Add(new Vector2(
                    center.X + (float)Math.Cos(i * angleStep) * radius,
                    center.Y + (float)Math.Sin(i * angleStep) * radius
                ));
            }
            return new Polygon(verts);
        }

        private Vector2 Snap(Vector2 pos) => new Vector2((float)Math.Floor(pos.X / 16) * 16, (float)Math.Floor(pos.Y / 16) * 16);

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!_isDrawing) return;

            Vector2 end = input.MouseWorldPosition;
            if (_currentMode == LightShapeMode.Rectangle)
            {
                Vector2 s = Snap(_startPos);
                Vector2 e = Snap(end);
                var rect = new RectangleF(Math.Min(s.X, e.X), Math.Min(s.Y, e.Y), Math.Abs(s.X - e.X), Math.Abs(s.Y - e.Y));
                sb.DrawRectangle(rect, Color.Yellow, 2f / input.Zoom);
            }
            else
            {
                float radius = Vector2.Distance(_startPos, end);
                sb.DrawCircle(_startPos, radius, 32, Color.Yellow, 2f / input.Zoom);
            }
        }

        public string GetShortcutHints() => "CAPS: Draw Rectangle | OFF: Draw Circle";
    }

    public class SpawnPointerTool : ITool
    {
        public string Name => "SpawnPointer";
        public string IconName { get; set; } = "SpotPlacer"; // Feel free to add a "SpawnIcon" to your JSON later

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            // Only works on Control/Trigger layers
            if (!(toolInput.ActiveLayer is ControlLayer controlLayer)) return;

            if (input.IsNewLeftClick)
            {
                // Snap to the center of the hovered 16x16 cell for perfectly aligned spawns
                Vector2 snapPos = GridHelper.GetPlacementPosition(toolInput.WorldPosition, false);
                // Offset back up by half the cell so the visual center is exactly in the middle of the tile
                snapPos.Y -= 8;

                var spawnPoint = new PointObject
                {
                    Position = snapPos,
                    Radius = 8f, // Fixed, perfectly sized radius
                    Label = "Spawn Point",
                    DebugColor = Color.Magenta
                };

                // Add properties to define who spawns here (defaults to Player)
                spawnPoint.Properties.Add("SpawnType", new MapProperty(PropertyType.String, "Player"));
                // Add a systemic tag if you have one (e.g., 20 for spawns)
                spawnPoint.Tags.Add(20); 

                eventBus.Publish(new AddPointCommand(controlLayer, spawnPoint));

                // Select it immediately so the user can change "Player" to "NPC_Blacksmith" in the Inspector
                editorState.Selection.SelectedMapObject = spawnPoint;
            }
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!(toolInput.ActiveLayer is ControlLayer)) return;

            Vector2 snapPos = GridHelper.GetPlacementPosition(input.MouseWorldPosition, false);
            snapPos.Y -= 8;

            sb.DrawCircle(snapPos, 8f, 16, Color.Magenta, 2f / input.Zoom);
            sb.DrawLine(snapPos - new Vector2(4, 0), snapPos + new Vector2(4, 0), Color.Magenta, 2f / input.Zoom);
            sb.DrawLine(snapPos - new Vector2(0, 4), snapPos + new Vector2(0, 4), Color.Magenta, 2f / input.Zoom);

            // Draw text tag
            if (Pixel_Simulations.UI.UITheme.DefaultFont != null)
            {
                sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, "SPAWN", snapPos + new Vector2(10, -10), Color.Magenta, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
            }
        }

        public string GetShortcutHints() => "Click to place a fixed-size Spawn Point";
    }

    public class MoveTool : ITool
    {
        public string Name => "Move";
        public string IconName => "MoveTool"; // Reuses standard selection icon

        private enum MoveState { Idle, SelectingBox, Translating }
        private MoveState _currentState = MoveState.Idle;

        private List<MapObject> _selectedCollection = new List<MapObject>();
        private Dictionary<MapObject, Vector2> _initialPositions = new Dictionary<MapObject, Vector2>();

        private Vector2 _selectionBoxStart;
        private Vector2 _dragStartWorld;
        private Vector2 _dragDelta;

        public string GetShortcutHints() => "DRAG: Select Box | SHIFT+CLICK: Toggle | DRAG GROUP: Translate";

        public void Update(ToolInput toolInput, EditorInputState input, EventBus bus, EditorState editorState)
        {
            if (toolInput.ActiveLayer == null || toolInput.ActiveLayer.IsLocked) return;

            Vector2 mouseWorld = input.MouseWorldPosition;

            switch (_currentState)
            {
                case MoveState.Idle:
                    if (input.IsNewLeftClick)
                    {
                        // 1. Check if clicking on an already selected object to begin translation
                        MapObject hitObject = FindObjectUnderMouse(toolInput.ActiveLayer, mouseWorld, editorState);
                        if (hitObject != null && _selectedCollection.Contains(hitObject))
                        {
                            _currentState = MoveState.Translating;
                            _dragStartWorld = mouseWorld;
                            _initialPositions.Clear();
                            foreach (var obj in _selectedCollection)
                            {
                                _initialPositions[obj] = obj.Position;
                            }
                        }
                        // 2. Check if Shift-Clicking to toggle individual objects
                        else if (input.CurrentKeyboard.IsKeyDown(Keys.LeftShift) || input.CurrentKeyboard.IsKeyDown(Keys.RightShift))
                        {
                            if (hitObject != null)
                            {
                                if (_selectedCollection.Contains(hitObject))
                                    _selectedCollection.Remove(hitObject);
                                else
                                    _selectedCollection.Add(hitObject);
                            }
                        }
                        // 3. Otherwise, click empty space to clear collection and drag a selection box
                        else
                        {
                            if (hitObject != null)
                            {
                                _selectedCollection.Clear();
                                _selectedCollection.Add(hitObject);
                                _currentState = MoveState.Translating;
                                _dragStartWorld = mouseWorld;
                                _initialPositions.Clear();
                                _initialPositions[hitObject] = hitObject.Position;
                            }
                            else
                            {
                                _selectedCollection.Clear();
                                _currentState = MoveState.SelectingBox;
                                _selectionBoxStart = mouseWorld;
                            }
                        }
                    }
                    break;

                case MoveState.SelectingBox:
                    if (!input.LeftHold)
                    {
                        // Selection Box Released: Capture all objects within the geometric area
                        RectangleF box = GetBoxRect(_selectionBoxStart, mouseWorld);
                        _selectedCollection = FindObjectsInArea(toolInput.ActiveLayer, box, editorState);
                        _currentState = MoveState.Idle;
                    }
                    break;

                case MoveState.Translating:
                    if (input.LeftHold)
                    {
                        _dragDelta = mouseWorld - _dragStartWorld;

                        // Support Caps Lock grid snapping
                        if (input.CurrentKeyboard.CapsLock)
                        {
                            _dragDelta.X = (float)Math.Round(_dragDelta.X / 16) * 16;
                            _dragDelta.Y = (float)Math.Round(_dragDelta.Y / 16) * 16;
                        }

                        // Translate all objects temporarily
                        foreach (var obj in _selectedCollection)
                        {
                            Vector2 startPos = _initialPositions[obj];
                            obj.Position = startPos + _dragDelta;

                            if (obj is ShapeObject poly)
                            {
                                poly.UpdateBoundsFromVertices();
                            }
                        }
                    }
                    else
                    {
                        // Translation Released: Compile and publish the unified Compound Command
                        var moveCommands = new List<IUndoableCommand>();
                        foreach (var obj in _selectedCollection)
                        {
                            Vector2 oldPos = _initialPositions[obj];
                            Vector2 newPos = obj.Position;

                            if (oldPos != newPos)
                            {
                                moveCommands.Add(new TransformObjectCommand(obj, oldPos, newPos, GetObjectSize(obj, editorState), GetObjectSize(obj, editorState)));
                            }
                        }

                        if (moveCommands.Count > 0)
                        {
                            bus.Publish<IUndoableCommand>(new CompoundCommand(moveCommands));
                        }

                        _currentState = MoveState.Idle;
                    }
                    break;
            }
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            float lineThickness = 1f / input.Zoom;

            // 1. Draw Selection Box Drag Outline
            if (_currentState == MoveState.SelectingBox)
            {
                RectangleF selectionBox = GetBoxRect(_selectionBoxStart, input.MouseWorldPosition);
                sb.DrawRectangle(selectionBox, Color.Cyan * 0.8f, lineThickness);
                sb.FillRectangle(selectionBox, Color.Cyan * 0.15f);
            }

            // 2. Highlight all selected items in the collection
            foreach (var obj in _selectedCollection)
            {
                RectangleF bounds = GetObjectBounds(obj, toolInput.ActiveLayer as ObjectLayer);
                sb.DrawRectangle(bounds, Color.DeepSkyBlue, lineThickness * 2f);
                sb.FillRectangle(bounds, Color.DeepSkyBlue * 0.1f);
            }
        }

        // --- INTERNAL GEOMETRY HELPERS ---

        private RectangleF GetBoxRect(Vector2 p1, Vector2 p2)
        {
            float minX = Math.Min(p1.X, p2.X);
            float minY = Math.Min(p1.Y, p2.Y);
            float maxX = Math.Max(p1.X, p2.X);
            float maxY = Math.Max(p1.Y, p2.Y);
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        private MapObject FindObjectUnderMouse(Layer activeLayer, Vector2 mouseWorld, EditorState state)
        {
            if (activeLayer is ObjectLayer objLayer)
            {
                for (int i = objLayer.Objects.Count - 1; i >= 0; i--)
                {
                    var obj = objLayer.Objects[i];
                    if (GetObjectBounds(obj, objLayer).Contains(mouseWorld)) return obj;
                }
            }
            if (activeLayer is ControlLayer cl)
            {
                var shape = cl.Shapes.LastOrDefault(s => s.Shape.GetBounds().Contains(mouseWorld));
                if (shape != null) return shape;

                var rect = cl.Rectangles.LastOrDefault(r => new RectangleF(r.Position, r.Size).Contains(mouseWorld));
                if (rect != null) return rect;

                var pt = cl.Points.LastOrDefault(p => Vector2.Distance(p.Position, mouseWorld) <= p.Radius);
                if (pt != null) return pt;
            }
            return null;
        }

        private List<MapObject> FindObjectsInArea(Layer activeLayer, RectangleF area, EditorState state)
        {
            var results = new List<MapObject>();
            if (activeLayer is ObjectLayer objLayer)
            {
                foreach (var obj in objLayer.Objects)
                {
                    if (area.Intersects(GetObjectBounds(obj, objLayer))) results.Add(obj);
                }
            }
            if (activeLayer is ControlLayer cl)
            {
                results.AddRange(cl.Shapes.Where(s => area.Intersects(s.Shape.GetBounds())));
                results.AddRange(cl.Rectangles.Where(r => area.Intersects(new RectangleF(r.Position, r.Size))));
                results.AddRange(cl.Points.Where(p => area.Intersects(new RectangleF(p.Position.X - p.Radius, p.Position.Y - p.Radius, p.Radius * 2, p.Radius * 2))));
            }
            return results;
        }

        private RectangleF GetObjectBounds(MapObject obj, ObjectLayer layer)
        {
            if (obj is RectangleObject r) return new RectangleF(r.Position, r.Size);
            if (obj is ShapeObject s) return s.Shape.GetBounds();
            if (obj is PointObject p) return new RectangleF(p.Position.X - p.Radius, p.Position.Y - p.Radius, p.Radius * 2, p.Radius * 2);
            if (obj is PropObject prop)
            {
                var prefab = SpawnerLogicGetPrefab(prop.PrefabID);
                if (prefab != null)
                {
                    return new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, prefab.CanvasSize.X * prop.Scale.X, prefab.CanvasSize.Y * prop.Scale.Y);
                }
            }
            return RectangleF.Empty;
        }

        private Vector2 GetObjectSize(MapObject obj, EditorState state)
        {
            if (obj is RectangleObject r) return r.Size;
            if (obj is ShapeObject s) return s.Size;
            if (obj is PointObject p) return new Vector2(p.Radius, p.Radius);
            if (obj is PropObject prop)
            {
                var prefab = state.PrefabManager.GetPrefab(prop.PrefabID);
                if (prefab != null) return new Vector2(prefab.CanvasSize.X, prefab.CanvasSize.Y);
            }
            return Vector2.Zero;
        }

        private ObjectPrefab SpawnerLogicGetPrefab(string id)
        {
            // Direct safety wrapper for static registry retrieval
            return windowPrefabManagerReference?.GetPrefab(id);
        }

        // Global link hook to access state safely
        private static PrefabManager windowPrefabManagerReference;
        public static void LinkPrefabManager(PrefabManager pm) => windowPrefabManagerReference = pm;
    }

    // Inside EditorTool.cs
    public class WaterShapeTool : ITool
    {
        public string Name => "WaterShape";
        public string IconName => "SlopeTerrain";

        public enum DrawSubMode { Bounds, Spine }
        public DrawSubMode ActiveSubMode { get; set; } = DrawSubMode.Bounds;

        private List<Vector2> _boundsVertices = new List<Vector2>();
        private List<Vector2> _spineWaypoints = new List<Vector2>();
        private bool _isDrawing = false;

        public string GetShortcutHints() => "TAB: Switch SubMode (Bounds/Spine) | ENTER: Save Polygon | ESC: Clear Draft";

        public void Update(ToolInput toolInput, EditorInputState input, EventBus bus, EditorState editorState)
        {
            if (!(toolInput.ActiveLayer is ControlLayer controlLayer)) return;

            Vector2 mouseWorld = input.MouseWorldPosition;

            // Caps Lock grid snap
            if (input.CurrentKeyboard.CapsLock)
            {
                mouseWorld.X = (float)Math.Round(mouseWorld.X / 16) * 16;
                mouseWorld.Y = (float)Math.Round(mouseWorld.Y / 16) * 16;
            }

            // TAB Key: Swap between drawing Bounds or River Spline Centerlines
            if (input.CurrentKeyboard.IsKeyDown(Keys.Tab) && input.PreviousKeyboard.IsKeyUp(Keys.Tab))
            {
                ActiveSubMode = ActiveSubMode == DrawSubMode.Bounds ? DrawSubMode.Spine : DrawSubMode.Bounds;
                System.Diagnostics.Debug.WriteLine($"[WaterTool] Submode Switched to: {ActiveSubMode}");
            }

            // Node Snapping Check (Bounds Close Loop)
            bool isSnappedToStart = false;
            if (ActiveSubMode == DrawSubMode.Bounds && _boundsVertices.Count >= 3)
            {
                float snapTolerance = 10f / input.Zoom;
                if (Vector2.Distance(mouseWorld, _boundsVertices[0]) < snapTolerance)
                {
                    mouseWorld = _boundsVertices[0];
                    isSnappedToStart = true;
                }
            }

            // 1. PLACE NODE ON CLICK
            if (input.IsNewLeftClick)
            {
                _isDrawing = true;
                if (ActiveSubMode == DrawSubMode.Bounds)
                {
                    if (isSnappedToStart)
                    {
                        CloseAndBakePolygon(controlLayer, bus, editorState);
                    }
                    else
                    {
                        _boundsVertices.Add(mouseWorld);
                    }
                }
                else
                {
                    _spineWaypoints.Add(mouseWorld);
                }
            }

            // 2. CLOSE LOOP ON ENTER
            if (input.CurrentKeyboard.IsKeyDown(Keys.Enter) && input.PreviousKeyboard.IsKeyUp(Keys.Enter) && _isDrawing)
            {
                CloseAndBakePolygon(controlLayer, bus, editorState);
            }

            // 3. CANCEL DRAFT
            if (input.CurrentKeyboard.IsKeyDown(Keys.Escape) && _isDrawing)
            {
                ClearDraft();
            }
        }

        private void CloseAndBakePolygon(ControlLayer controlLayer, EventBus bus, EditorState state)
        {
            if (_boundsVertices.Count >= 3)
            {
                Polygon waterPolygon = new Polygon(new List<Vector2>(_boundsVertices));
                var waterShape = new ShapeObject
                {
                    Shape = waterPolygon,
                    Name = $"WaterBody_{Guid.NewGuid().ToString().Substring(0, 4)}",
                    DebugColor = Color.SlateGray * 0.6f,
                    SpineWaypoints = new List<Vector2>(_spineWaypoints) // Save spine centerline waypoints
                };

                waterShape.Tags.Add(SystemTags.WATER);

                // AUTO-POPULATE GEOLOGY DEFAULTS modeled after the HTML editor
                waterShape.Properties.Add("ProjectionMode", new MapProperty(PropertyType.String, "spline"));
                waterShape.Properties.Add("EasingMode", new MapProperty(PropertyType.String, "linear"));
                waterShape.Properties.Add("BaseHeight", new MapProperty(PropertyType.Float, "160"));
                waterShape.Properties.Add("StartHeightOffset", new MapProperty(PropertyType.Float, "0"));
                waterShape.Properties.Add("EndHeightOffset", new MapProperty(PropertyType.Float, "-60"));
                waterShape.Properties.Add("RiverWidth", new MapProperty(PropertyType.Float, "8"));
                waterShape.Properties.Add("Asymmetric", new MapProperty(PropertyType.Boolean, "True"));
                waterShape.Properties.Add("Snap", new MapProperty(PropertyType.Boolean, "True"));

                waterShape.UpdateBoundsFromVertices();

                bus.Publish<IUndoableCommand>(new AddPolygonCommand(controlLayer, waterShape));
                state.Selection.SelectedMapObject = waterShape;
            }

            ClearDraft();
        }

        private void ClearDraft()
        {
            _boundsVertices.Clear();
            _spineWaypoints.Clear();
            _isDrawing = false;
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!_isDrawing) return;

            float thickness = 2f / input.Zoom;
            Vector2 mouseWorld = input.MouseWorldPosition;

            if (input.CurrentKeyboard.CapsLock)
            {
                mouseWorld.X = (float)Math.Round(mouseWorld.X / 16) * 16;
                mouseWorld.Y = (float)Math.Round(mouseWorld.Y / 16) * 16;
            }

            // Draw outer boundary segments
            if (_boundsVertices.Count > 0)
            {
                for (int i = 0; i < _boundsVertices.Count - 1; i++)
                {
                    sb.DrawLine(_boundsVertices[i], _boundsVertices[i + 1], Color.Teal, thickness);
                }
                if (ActiveSubMode == DrawSubMode.Bounds)
                {
                    sb.DrawLine(_boundsVertices[_boundsVertices.Count - 1], mouseWorld, Color.Teal * 0.6f, thickness);
                }
                foreach (var vertex in _boundsVertices)
                {
                    sb.DrawRectangle(new RectangleF(vertex.X - 2, vertex.Y - 2, 4, 4), Color.White, thickness);
                }
            }

            // Draw spline centerline segments
            if (_spineWaypoints.Count > 0)
            {
                for (int i = 0; i < _spineWaypoints.Count - 1; i++)
                {
                    sb.DrawLine(_spineWaypoints[i], _spineWaypoints[i + 1], Color.Orange * 0.8f, thickness);
                }
                if (ActiveSubMode == DrawSubMode.Spine)
                {
                    sb.DrawLine(_spineWaypoints[_spineWaypoints.Count - 1], mouseWorld, Color.Orange * 0.4f, thickness);
                }
                foreach (var waypoint in _spineWaypoints)
                {
                    sb.DrawCircle(waypoint, 3f, 8, Color.Orange, thickness);
                }
            }

            // Render tool status text
            Vector2 textPos = mouseWorld + new Vector2(15, -15);
            string toolText = $"Water Designer [{ActiveSubMode}] (Bounds: {_boundsVertices.Count} | Spine: {_spineWaypoints.Count})";
            sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, toolText, textPos + Vector2.One, Color.Black, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
            sb.DrawString(Pixel_Simulations.UI.UITheme.DefaultFont, toolText, textPos, Color.Cyan, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
        }
    }
}


