using Clipper2Lib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Collisions.Layers;
using MonoGame.Extended.Particles.Modifiers;
using Pixel_Simulations;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Data
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
                    var cell = new Point((int)System.Math.Floor(toolInput.WorldPosition.X / 16), (int)System.Math.Floor(toolInput.WorldPosition.Y / 16));
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
                    pos = new Vector2(
                        (float)Math.Floor(pos.X / 16) * 16,
                        (float)Math.Floor(pos.Y / 16) * 16
                    );
                }

                if (toolInput.ActiveLayer is ObjectLayer objLayer)
                {
                    var instance = new PropObject
                    {
                        PrefabID = _activePrefab.ID,
                        Position = pos,
                        Name = GetUniqueName(objLayer, _activePrefab.ID) // Dynamic unique naming
                    };

                    eventBus.Publish(new AddObjectCommand(objLayer, instance));
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
        public void DrawPreview(ToolInput toolInput,SpriteBatch sb, EditorInputState input)
        {
            if (_activePrefab == null) return;

            var atlas = _es.AssetLibrary.GetAtlas(_activePrefab.AtlasName);
            if (atlas != null)
            {
                Vector2 pos = input.MouseWorldPosition;
                if (input.CurrentKeyboard.CapsLock)
                {
                    pos = new Vector2((float)Math.Floor(pos.X / 16) * 16, (float)Math.Floor(pos.Y / 16) * 16);
                }

                // FIX: Use the Pivot as the origin so the "Ghost" matches the final placement
                Vector2 origin = _activePrefab.Pivot;

                sb.Draw(atlas, pos, _activePrefab.SourceRect, Color.White * 0.5f,
                        0f, origin, 1f, SpriteEffects.None, 0f);
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
                    if (Mode) { _startWorldPos = new Vector2((float)Math.Floor(_startWorldPos.X / 16) * 16, (float)Math.Floor(_startWorldPos.Y / 16) * 16); }
                    _isDrawing = true;
                }

                if (_isDrawing)
                {
                    
                    Vector2 start = _startWorldPos;
                    Vector2 end = toolInput.WorldPosition;

                    if (Mode) { end = new Vector2((float)Math.Floor(end.X / 16) * 16, (float)Math.Floor(end.Y / 16) * 16); }

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
                if (input.CurrentKeyboard.CapsLock) _startPos = Snap(_startPos);
                _isDrawing = true;
            }

            if (_isDrawing && input.CurrentMouse.LeftButton == ButtonState.Released)
            {
                Vector2 endPos = toolInput.WorldPosition;
                if (input.CurrentKeyboard.CapsLock) endPos = Snap(endPos);

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
                        Tag = toolInput.ActiveLayer.Type.ToString(),
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

            if (kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift))
                return ShapeOperation.Union;
            else if (kbs.IsKeyDown(Keys.LeftControl) || kbs.IsKeyDown(Keys.RightControl))
                return ShapeOperation.Intersection;
            else if (kbs.IsKeyDown(Keys.LeftAlt))
                return ShapeOperation.Difference;
            else
                return ShapeOperation.None;
        }
        private Vector2 Snap(Vector2 pos)
        {
            return new Vector2((float)Math.Floor(pos.X / 16) * 16, (float)Math.Floor(pos.Y / 16) * 16);
        }

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

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus,EditorState editorState)
        {
            _es = editorState;
            if (_es == null) return;

            if (!(editorState.UI.FocusedElement is UITextBox))
            {
                if (input.CurrentKeyboard.IsKeyDown(Keys.Delete) && input.PreviousKeyboard.IsKeyUp(Keys.Delete))
                {
                    if (CurrentSelect != null && toolInput.ActiveLayer is ObjectLayer objLayer)
                    {
                        eventBus.Publish(new RemoveObjectCommand(objLayer, CurrentSelect));
                        editorState.Selection.SelectedMapObject = null;
                        CurrentSelect = null;
                        _currentMode = InteractionMode.None;
                        return; // Exit early
                    }
                }
            }
            // --- STEP 1: SELECTION (Only on the frame the button is pressed) ---
            if (input.IsNewLeftClick)
            {

                // First, check handles on the EXISTING selection
                if (_es.Selection.SelectedMapObject != null)
                {
                    _activeHandle = GetHandleAt(_es.Selection.SelectedMapObject, input.MouseWorldPosition, input.Zoom);
                    if (_activeHandle != -1)
                    {
                        _currentMode = InteractionMode.Resizing;
                        var obj = _es.Selection.SelectedMapObject;
                        _initialPos = obj.Position;
                        _initialSize = GetObjectSize(obj);
                    }
                }

                // Second, if no handle was hit, try to select a NEW object
                if (_currentMode == InteractionMode.None)
                {
                    bool absoluteMode = input.CurrentKeyboard.IsKeyDown(Keys.LeftAlt);
                    var hit = FindTopObject(toolInput.ActiveLayer, input.MouseWorldPosition, absoluteMode);
                    editorState.Selection.SelectedMapObject = hit; // APPLY SELECTION TO STATE

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

            // --- STEP 2: INTERACTION (While mouse is held) ---
            // We use state.Selection.SelectedMapObject directly to ensure we have the latest ref
            
            if (CurrentSelect != null && input.LeftHold && _currentMode != InteractionMode.None)
            {
                if (_currentMode == InteractionMode.Moving)
                {
                    Vector2 newPos = input.MouseWorldPosition + _dragOffset;
                    if (input.CurrentKeyboard.CapsLock)
                        newPos = new Vector2((float)Math.Round(newPos.X / 16) * 16, (float)Math.Round(newPos.Y / 16) * 16);

                    Vector2 delta = newPos - CurrentSelect.Position;

                    // If it's a shape, we MUST move the vertices, not just the Position property
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
                    ApplyResize(CurrentSelect, input.MouseWorldPosition, _activeHandle, input.CurrentKeyboard.CapsLock);
                }
            }

            // --- STEP 3: KEYBOARD NUDGING ---
            if (CurrentSelect != null && _currentMode == InteractionMode.None)
            {
                HandleKeyboardMovement(CurrentSelect, input);
            }

            // --- STEP 4: RELEASE & COMMAND ---
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

        private void HandleKeyboardMovement(MapObject obj, EditorInputState input)
        {
            if (_es.UI.FocusedElement is UITextBox) return; // Don't move if typing!

            bool snapMode = input.CurrentKeyboard.CapsLock;
            float speed = snapMode ? 16f : 1f;
            Vector2 move = Vector2.Zero;

            bool up = snapMode ? (input.CurrentKeyboard.IsKeyDown(Keys.W) && input.PreviousKeyboard.IsKeyUp(Keys.W)) : input.CurrentKeyboard.IsKeyDown(Keys.W);
            bool down = snapMode ? (input.CurrentKeyboard.IsKeyDown(Keys.S) && input.PreviousKeyboard.IsKeyUp(Keys.S)) : input.CurrentKeyboard.IsKeyDown(Keys.S);
            bool left = snapMode ? (input.CurrentKeyboard.IsKeyDown(Keys.A) && input.PreviousKeyboard.IsKeyUp(Keys.A)) : input.CurrentKeyboard.IsKeyDown(Keys.A);
            bool right = snapMode ? (input.CurrentKeyboard.IsKeyDown(Keys.D) && input.PreviousKeyboard.IsKeyUp(Keys.D)) : input.CurrentKeyboard.IsKeyDown(Keys.D);

            if (up) move.Y -= speed;
            if (down) move.Y += speed;
            if (left) move.X -= speed;
            if (right) move.X += speed;

            if (move != Vector2.Zero)
            {
                if (obj is ShapeObject poly)
                {
                    poly.Shape.Offset(move);
                    poly.UpdateBoundsFromVertices();
                }
                else obj.Position += move;
            }
        }

        private int GetHandleAt(MapObject obj, Vector2 mouseWorld, float zoom)
        {
            float handleSize = 8f / zoom;
            RectangleF bounds = GetObjectBounds(obj);
            if (bounds.IsEmpty) return -1;

            // TL, TR, BR, BL
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
                // Complex Resize: We calculate the scale factor and apply it to vertices
                Vector2 pos = poly.Position;
                Vector2 size = poly.Size;
                Vector2 br = pos + size;

                // Calculate new bounds based on handle
                Vector2 newPos = pos;
                Vector2 newSize = size;

                switch (handle)
                {
                    case 0: newPos = mouseWorld; newSize = br - mouseWorld; break;
                    case 1: newPos.Y = mouseWorld.Y; newSize = new Vector2(mouseWorld.X - pos.X, br.Y - mouseWorld.Y); break;
                    case 2: newSize = mouseWorld - pos; break;
                    case 3: newPos.X = mouseWorld.X; newSize = new Vector2(br.X - mouseWorld.X, mouseWorld.Y - pos.Y); break;
                }

                // Apply scale to vertices relative to the anchor (the opposite corner)
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
                0 => br, // Anchor BR to resize TL
                1 => new Vector2(tl.X, br.Y), // Anchor BL to resize TR
                2 => tl, // Anchor TL to resize BR
                3 => new Vector2(br.X, tl.Y), // Anchor TR to resize BL
                _ => tl
            };
        }
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (_es == null) return;
            var selected = _es.Selection.SelectedMapObject;
            if (selected == null) return;

            RectangleF b = GetObjectBounds(selected);
            float hs = 6f / input.Zoom;

            // Draw base selection
            sb.DrawRectangle(b, GetObjectColor(selected.Type) * 0.4f, 1 / input.Zoom, 0);
            sb.FillRectangle(new RectangleF(b.Left - hs / 2, b.Top - hs / 2, hs, hs), Color.White);
            sb.FillRectangle(new RectangleF(b.Right - hs / 2, b.Top - hs / 2, hs, hs), Color.White);
            sb.FillRectangle(new RectangleF(b.Right - hs / 2, b.Bottom - hs / 2, hs, hs), Color.White);
            sb.FillRectangle(new RectangleF(b.Left - hs / 2, b.Bottom - hs / 2, hs, hs), Color.White);

            // --- NEW: DRAW LINK LINES ---
            if (selected.LinkedObjects.Count > 0)
            {
                Vector2 startPos = b.Center;

                foreach (string targetId in selected.LinkedObjects)
                {
                    var targetObj = FindObjectByID(targetId);
                    if (targetObj != null)
                    {
                        RectangleF targetBounds = GetObjectBounds(targetObj);

                        // Color Code based on the target's type!
                        Color linkColor = targetObj.Type switch
                        {
                            ObjectType.Prop => Color.LimeGreen,
                            ObjectType.Rectangle => Color.DeepSkyBlue,
                            ObjectType.Shape => Color.OrangeRed,
                            ObjectType.Point => Color.Yellow,
                            _ => Color.Cyan
                        };

                        // Draw Dashed Line
                        UIDrawExtensions.DrawDashedLine(sb, startPos, targetBounds.Center, linkColor, 2f / input.Zoom, 10f / input.Zoom, 5f / input.Zoom);

                        // Highlight Target Box
                        sb.DrawRectangle(targetBounds, linkColor * 0.8f, 2f / input.Zoom);
                    }
                }
            }
        }

        // Helper utilities
        private Color GetObjectColor(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Shape: return Color.Red;
                case ObjectType.Rectangle: return Color.Blue;
                default: return Color.White; // Fallback color
            }
        }
        public string GetShortcutHints() => "ALT: Pick Any Layer | DEL: Delete | CAPS: Grid Nudge"; private RectangleF GetObjectBounds(MapObject obj)
        {
            if (obj is RectangleObject r) return new RectangleF(r.Position, r.Size);
            if (obj is ShapeObject s) return new RectangleF(s.Position, s.Size);
            if (obj is PointObject p) return new RectangleF(p.Position.X - p.Radius, p.Position.Y - p.Radius, p.Radius * 2, p.Radius * 2);
            if (obj is MapObject o) return new RectangleF(o.Position, Vector2.Zero); 
            return RectangleF.Empty;
        }
        private Vector2 GetObjectSize(MapObject obj)
        {
            if (obj is RectangleObject r) return r.Size;
            if (obj is ShapeObject s) return s.Size;
            if (obj is PointObject p) return new Vector2(p.Radius, p.Radius);
            return Vector2.Zero;
        }
        private MapObject FindTopObject(Layer activeLayer, Vector2 mouseWorld, bool searchAllLayers)
        {
            // Define which layers we are allowed to look at
            var layersToSearch = searchAllLayers ? _es.ActiveMap.Layers.AsEnumerable() : new List<Layer> { activeLayer };

            // Loop backwards (top layer to bottom layer)
            foreach (var layer in layersToSearch.Reverse())
            {
                if (!layer.IsVisible || layer.IsLocked) continue;

                if (layer is ObjectLayer objLayer)
                {
                    // Reverse so top objects are selected first
                    for (int i = objLayer.Objects.Count - 1; i >= 0; i--)
                    {
                        var obj = objLayer.Objects[i];
                        if (obj is PropObject prop)
                        {
                            var prefab = _es.PrefabManager.GetPrefab(prop.PrefabID);
                            if (prefab != null)
                            {
                                RectangleF bounds = new RectangleF(prop.Position.X - prefab.Pivot.X, prop.Position.Y - prefab.Pivot.Y, prefab.SourceRect.Width, prefab.SourceRect.Height);
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
    public class TerrainBrushTool : ITool
    {
        public string Name => "TerrainBrush";
        public string IconName { get; set; } = "TerrainBrush";

        public float BrushRadius { get; set; } = 32f;
        public int TargetValue { get; set; } = 128; // The 0-255 value for the active channel
        public PaintChannel ActiveChannel { get; set; } = PaintChannel.B_Elevation;

        // "Normalized" elevation (0.0 to 1.0) so we can apply the Bell Curve math smoothly
        private float _normalizedElevation = 0.5f;

        private bool _isDrawing = false;
        private Texture2D _brushTexture;
        private bool _brushNeedsUpdate = true;
        private string _lastNoiseName = "None";

        private Dictionary<Point, Color[]> _strokeBefore;
        private HashSet<Point> _touchedChunksThisStroke;

        private static readonly BlendState PaintRed = new BlendState { ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceAlpha, ColorWriteChannels = ColorWriteChannels.Red };
        private static readonly BlendState PaintGreen = new BlendState { ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceAlpha, ColorWriteChannels = ColorWriteChannels.Green };
        private static readonly BlendState PaintBlue = new BlendState { ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.InverseSourceAlpha, ColorWriteChannels = ColorWriteChannels.Blue };
        private static readonly BlendState PaintAlpha = new BlendState { AlphaSourceBlend = Blend.SourceAlpha, AlphaDestinationBlend = Blend.InverseSourceAlpha, ColorWriteChannels = ColorWriteChannels.Alpha };

        private static readonly BlendState EraseChannel = new BlendState
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.Zero,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.Zero,
            AlphaBlendFunction = BlendFunction.Add
        };

        public void Update(ToolInput toolInput, EditorInputState input, EventBus eventBus, EditorState editorState)
        {
            if (!(toolInput.ActiveLayer is MaskLayer maskLayer)) return;

            var kbs = input.CurrentKeyboard;
            bool shift = kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift);
            bool isErasing = kbs.IsKeyDown(Keys.LeftControl) || kbs.IsKeyDown(Keys.RightControl);

            if (_lastNoiseName != editorState.noiseManager.ActiveNoiseName)
            {
                _lastNoiseName = editorState.noiseManager.ActiveNoiseName;
                _brushNeedsUpdate = true;
            }

            // --- CONTROLS ---
            if (kbs.IsKeyDown(Keys.OemCloseBrackets)) // ']'
            {
                if (shift) BrushRadius = MathHelper.Clamp(BrushRadius + 2f, 8f, 256f);
                else AdjustValue(true);
                _brushNeedsUpdate = true;
            }
            if (kbs.IsKeyDown(Keys.OemOpenBrackets)) // '['
            {
                if (shift) BrushRadius = MathHelper.Clamp(BrushRadius - 2f, 8f, 256f);
                else AdjustValue(false);
                _brushNeedsUpdate = true;
            }

            // --- STROKE TRACKING ---
            if (input.LeftHold)
            {
                if (!_isDrawing)
                {
                    _isDrawing = true;
                    _strokeBefore = new Dictionary<Point, Color[]>();
                    _touchedChunksThisStroke = new HashSet<Point>();
                }
                PaintOnMask(maskLayer, input.MouseWorldPosition, editorState, isErasing);
            }
            else if (_isDrawing)
            {
                _isDrawing = false;
                if (_touchedChunksThisStroke != null && _touchedChunksThisStroke.Count > 0)
                {
                    var strokeAfter = new Dictionary<Point, Color[]>();
                    foreach (var chunkCoord in _touchedChunksThisStroke)
                    {
                        Color[] data = new Color[MaskLayer.CHUNK_PIXEL_SIZE * MaskLayer.CHUNK_PIXEL_SIZE];
                        maskLayer.Chunks[chunkCoord].GetData(data);
                        strokeAfter[chunkCoord] = data;
                    }
                    eventBus.Publish(new PaintMaskCommand(maskLayer, _strokeBefore, strokeAfter, editorState._graphics));
                }
                _strokeBefore = null;
                _touchedChunksThisStroke = null;
            }
        }
        private float SmootherStep(float x) => x * x * x * (x * (x * 6 - 15) + 10);
        private void AdjustValue(bool increase)
        {
            if (ActiveChannel == PaintChannel.B_Elevation)
            {
                // Move the normalized value slightly
                _normalizedElevation += increase ? 0.005f : -0.005f;
                _normalizedElevation = MathHelper.Clamp(_normalizedElevation, 0f, 1f);

                // Apply a Sigmoid Curve (Bell Curve Integral)
                // This makes values near 0.5 expand slowly, and values near 0 or 1 snap quickly.
                float curve = SmootherStep(_normalizedElevation);
                TargetValue = (int)Math.Round(curve * 255f);
            }
            else
            {
                // Linear adjustment for Biomes, Spawn Types, etc.
                TargetValue = MathHelper.Clamp(TargetValue + (increase ? 1 : -1), 0, 255);
            }
        }
        private void PaintOnMask(MaskLayer layer, Vector2 worldPos, EditorState es, bool isErasing)
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

                        if (!_touchedChunksThisStroke.Contains(chunkCoord))
                        {
                            Color[] data = new Color[MaskLayer.CHUNK_PIXEL_SIZE * MaskLayer.CHUNK_PIXEL_SIZE];
                            chunkRT.GetData(data);
                            _strokeBefore[chunkCoord] = data;
                            _touchedChunksThisStroke.Add(chunkCoord);
                        }

                        Vector2 localPos = new Vector2(worldPos.X - (x * MaskLayer.CHUNK_PIXEL_SIZE), worldPos.Y - (y * MaskLayer.CHUNK_PIXEL_SIZE));
                        gd.SetRenderTarget(chunkRT);

                        // Select the correct Blend State based on the active channel
                        BlendState activeState = ActiveChannel switch
                        {
                            PaintChannel.R_Biome => PaintRed,
                            PaintChannel.G_SpawnType => PaintGreen,
                            PaintChannel.B_Elevation => PaintBlue,
                            PaintChannel.A_SpawnIndex => PaintAlpha,
                            _ => PaintBlue
                        };

                        // If erasing, we override the ColorWriteChannels to act as an eraser for the specific channel
                        if (isErasing)
                        {
                            activeState = new BlendState
                            {
                                ColorSourceBlend = Blend.Zero,
                                ColorDestinationBlend = Blend.Zero,
                                ColorBlendFunction = BlendFunction.Add,
                                AlphaSourceBlend = Blend.Zero,
                                AlphaDestinationBlend = Blend.Zero,
                                AlphaBlendFunction = BlendFunction.Add,
                                ColorWriteChannels = activeState.ColorWriteChannels // Preserve channel isolation!
                            };
                        }

                        sb.Begin(blendState: activeState);
                        int val = isErasing ? 0 : TargetValue;
                        Color paintColor = new Color(val, val, val, val);

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
            if (!_brushNeedsUpdate && _brushTexture != null) return;

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
            float noiseAmplitude = 50f; // How much the noise swings the value up or down

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Vector2.Distance(center, new Vector2(x, y));
                    if (dist <= radius)
                    {
                        int pixelValue = TargetValue;

                        if (noiseData != null && ActiveChannel == PaintChannel.B_Elevation)
                        {
                            int nx = x % noiseTex.Width;
                            int ny = y % noiseTex.Height;
                            float noiseVal = noiseData[nx + ny * noiseTex.Width].R / 255f;

                            // --- YOUR EXACT FORMULA ---
                            // b + 50 * (noise - 0.5)
                            float offset = (noiseVal - 0.5f) * noiseAmplitude;
                            pixelValue = (int)MathHelper.Clamp(TargetValue + offset, 0, 255);
                        }

                        // Alpha 255 ensures a hard overwrite using SourceAlpha blending.
                        data[x + y * diameter] = new Color(pixelValue, pixelValue, pixelValue, 255);
                    }
                    else
                    {
                        data[x + y * diameter] = Color.Transparent;
                    }
                }
            }

            _brushTexture.SetData(data);
            _brushNeedsUpdate = false;
        }
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, EditorInputState input)
        {
            if (!(toolInput.ActiveLayer is MaskLayer)) return;

            bool isErasing = input.CurrentKeyboard.IsKeyDown(Keys.LeftControl) || input.CurrentKeyboard.IsKeyDown(Keys.RightControl);

            // Change cursor color based on active channel!
            Color cursorColor = ActiveChannel switch
            {
                PaintChannel.R_Biome => Color.Red,
                PaintChannel.G_SpawnType => Color.LimeGreen,
                PaintChannel.B_Elevation => Color.DeepSkyBlue,
                PaintChannel.A_SpawnIndex => Color.White,
                _ => Color.Cyan
            };
            if (isErasing) cursorColor = Color.Magenta;

            sb.DrawCircle(input.MouseWorldPosition, BrushRadius, 32, cursorColor, 2f / input.Zoom);

            Vector2 textPos = input.MouseWorldPosition + new Vector2(BrushRadius + 10, -10) / input.Zoom;
            string modeText = isErasing ? "ERASE ALL" : $"{ActiveChannel}: {TargetValue}";

            sb.DrawString(UITheme.DefaultFont, modeText, textPos + Vector2.One, Color.Black, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
            sb.DrawString(UITheme.DefaultFont, modeText, textPos, cursorColor, 0f, Vector2.Zero, 1f / input.Zoom, SpriteEffects.None, 0f);
        }

        public string GetShortcutHints() => "[ / ]: Adjust Value | SHIFT + [ / ]: Radius | CTRL: Erase All Channels";
    }
}

