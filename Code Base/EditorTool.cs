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
  
    public interface ITool
    {
        public string Name { get; }
        public string IconName { get; }
        void Update(ToolInput toolInput, InputState input , EventBus bus, EditorState editorState);
        void DrawPreview(ToolInput toolInput, SpriteBatch spriteBatch, InputState input);
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
        public bool _isDrawing => false;
        private EditorState _es;
        public void Update(ToolInput toolInput, InputState input,EventBus eventBus, EditorState editorState) // Add EventBus parameter
        {
            var selection = toolInput.ActiveBrush;
            _es = editorState;
            if (selection == null) return;

            // Handle Rotation Input
            if (input.CurrentKeyboard.IsKeyDown(Keys.E) && input.PreviousKeyboard.IsKeyUp(Keys.E))
            {
                selection.Rotation = (byte)((selection.Rotation + 1) % 4);
            }
            if (input.CurrentKeyboard.IsKeyDown(Keys.Q) && input.PreviousKeyboard.IsKeyUp(Keys.Q))
            {
                selection.Rotation = (byte)((selection.Rotation + 3) % 4); // Anti-clockwise
            }
            if (toolInput.CurrentMouse.LeftButton == ButtonState.Pressed)
            {
                if (toolInput.ActiveLayer is TileLayer tileLayer && (toolInput.ActiveLayer.Type == LayerType.Tile) &&toolInput.ActiveBrush != null)
                {
                    var cell = input.MouseGridCell;
                    var existing = tileLayer.GetTileAt(cell);

                    // Check if rotation or ID is different before placing
                    if (existing == null || existing.TileID != selection.TileID || existing.Rotation != selection.Rotation)
                    {
                        eventBus.Publish(new PlaceTileCommand(tileLayer, cell,
                            new TileInfo(selection.TilesetName, selection.TileID, selection.Rotation)));
                    }
                }
            }
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, InputState input)
        {
            if (toolInput.ActiveBrush == null) return;

            // Calculate grid-snapped position
            Vector2 gridPos = new Vector2(
                input.MouseGridCell.X * 16 + 8, // +8 for center origin
                input.MouseGridCell.Y * 16 + 8
            );

            // Get the texture from the manager
            // Note: You may need to pass TilesetManager or EditorState to the tool
            var tex = _es.TilesetManager.GetTileTexture(toolInput.ActiveBrush);
            if (tex != null)
            {
                float rotationRadians = toolInput.ActiveBrush.Rotation * MathHelper.PiOver2;
                Vector2 origin = new Vector2(8, 8);

                // Draw with 40% opacity
                sb.Draw(tex, gridPos, null, Color.White * 0.4f, rotationRadians, origin, 1f, SpriteEffects.None, 0f);
            }
        }
    }
    public class EraserTool : ITool
    {
        public string Name => "Eraser";
        public string IconName => Name;
        public bool _isDrawing = false;
        public void Update(ToolInput toolInput, InputState input, EventBus eventBus, EditorState editorState)
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

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, InputState input)
        {
            // A brush might draw a semi-transparent preview of the tile under the cursor.
            // For now, we'll leave it empty.
        }
    }
    public class FreeRectangleTool : ITool
    {
        public bool _isDrawing = false;
        public bool Mode = false;
        public string Name => "FreeRectangle";
        public string IconName { get; set; } = "GridRectangle";
        private Vector2 _startWorldPos;

        private float Zoom = 1f;
        private LayerType type;
        public void Update(ToolInput toolInput, InputState input, EventBus eventBus, EditorState editorState)
        {

            if (input.CurrentKeyboard.CapsLock) { Mode = true; }    else { Mode = false; }

            input.Drawing = _isDrawing;
            Zoom = input.Zoom;
            type = toolInput.ActiveLayer.Type;
            if (!(type == LayerType.Trigger)) 
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
                case LayerType.Trigger: return Color.LimeGreen;
                default: return Color.White;
            }
        }
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, InputState input)
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
        public void Update(ToolInput toolInput, InputState input, EventBus eventBus, EditorState editorState)
        {
            var selectedShape = editorState.Selection.SelectedMapObject as ShapeObject;
            op = HandleKeys(input);
            editorState.ToolState.ActiveBooleanOp = op;
            if (op == ShapeOperation.Union) IconName = "ShapeUnion";
            else if (op == ShapeOperation.Intersection) IconName = "ShapeIntersection";
            else if (op == ShapeOperation.Difference) IconName = "ShapeDifference";
            else IconName = "ShapeTool";
            type = toolInput.ActiveLayer.Type;
            if(type == LayerType.Tile || type == LayerType.Object || type == LayerType.Trigger) { return; }
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
        private ShapeOperation HandleKeys(InputState input)
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

        private bool IsValidLayer(Layer l) => l is CollisionLayer || l is NavigationLayer;

        private Color GetLayerColor(LayerType type)
        {
            switch (type)
            {
                case LayerType.Collision: return Color.Red;
                case LayerType.Navigation: return Color.Blue;
                default: return Color.White; // Fallback color
            }
        }
        private int GetObjectCount(Layer ActiveLayer)
        {
            if(ActiveLayer is CollisionLayer col) { return col.CollisionMesh.Count; }
            if (ActiveLayer is NavigationLayer nav) { return nav.NavigationMesh.Count; }
            else return -1;
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, InputState input)
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

        public void Update(ToolInput toolInput, InputState input, EventBus eventBus,EditorState editorState)
        {
            var state = editorState;
            if (state == null) return;

            // --- STEP 1: SELECTION (Only on the frame the button is pressed) ---
            if (input.IsNewLeftClick)
            {

                // First, check handles on the EXISTING selection
                if (state.Selection.SelectedMapObject != null)
                {
                    _activeHandle = GetHandleAt(state.Selection.SelectedMapObject, input.MouseWorldPosition, input.Zoom);
                    if (_activeHandle != -1)
                    {
                        _currentMode = InteractionMode.Resizing;
                        var obj = state.Selection.SelectedMapObject;
                        _initialPos = obj.Position;
                        _initialSize = GetObjectSize(obj);
                    }
                }

                // Second, if no handle was hit, try to select a NEW object
                if (_currentMode == InteractionMode.None)
                {
                    var hit = FindTopObject(toolInput.ActiveLayer, input.MouseWorldPosition);
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

        private void HandleKeyboardMovement(MapObject obj, InputState input)
        {
            float speed = input.CurrentKeyboard.IsKeyDown(Keys.LeftShift) ? 16f : 1f;
            Vector2 move = Vector2.Zero;

            // Use IsKeyDown for smooth nudging, or combine with IsKeyUp for step-by-step
            //Removed isKeyup to make movement smooth while holding keys.
            if (input.CurrentKeyboard.IsKeyDown(Keys.W)) move.Y -= speed;
            if (input.CurrentKeyboard.IsKeyDown(Keys.S)) move.Y += speed;
            if (input.CurrentKeyboard.IsKeyDown(Keys.A)) move.X -= speed;
            if (input.CurrentKeyboard.IsKeyDown(Keys.D)) move.X += speed;

            if (move != Vector2.Zero)
            {
                if (obj is ShapeObject poly)
                {
                    // IMPORTANT: Move vertices, then update the bounding box
                    poly.Shape.Offset(move);
                    poly.UpdateBoundsFromVertices();
                }
                else
                {
                    // Standard move for Rectangles/Points
                    obj.Position += move;
                }
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
        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, InputState input)
        {
            // Draw the white stroke first
            // Then draw small squares (Handles) at corners

            if (input.mapObject == null) return;

            RectangleF b = GetObjectBounds(input.mapObject);
            float hs = 6f / input.Zoom; // Handle Screen Size
            sb.DrawRectangle(b,GetObjectColor(input.mapObject.Type)*0.4f, 1, 0);
            // Corner handles
            sb.FillRectangle(new RectangleF(b.Left - hs / 2, b.Top - hs / 2, hs, hs), Color.White);
            sb.FillRectangle(new RectangleF(b.Right - hs / 2, b.Top - hs / 2, hs, hs), Color.White);
            sb.FillRectangle(new RectangleF(b.Right - hs / 2, b.Bottom - hs / 2, hs, hs), Color.White);
            sb.FillRectangle(new RectangleF(b.Left - hs / 2, b.Bottom - hs / 2, hs, hs), Color.White);
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

        private RectangleF GetObjectBounds(MapObject obj)
        {
            if (obj is RectangleObject r) return new RectangleF(r.Position, r.Size);
            if (obj is ShapeObject s) return new RectangleF(s.Position, s.Size);
            if (obj is PointObject p) return new RectangleF(p.Position.X - p.Radius, p.Position.Y - p.Radius, p.Radius * 2, p.Radius * 2);
            return RectangleF.Empty;
        }
        private Vector2 GetObjectSize(MapObject obj)
        {
            if (obj is RectangleObject r) return r.Size;
            if (obj is ShapeObject s) return s.Size;
            if (obj is PointObject p) return new Vector2(p.Radius, p.Radius);
            return Vector2.Zero;
        }
        private void SnapPosition(MapObject obj) => obj.Position = new Vector2((float)Math.Round(obj.Position.X / 16) * 16, (float)Math.Round(obj.Position.Y / 16) * 16);
        private MapObject FindTopObject(Layer layer, Vector2 mouseWorld)
        {
            if (layer is ObjectLayer objLayer && layer.Type == LayerType.Object)
            {
                return objLayer.Objects.LastOrDefault(o =>
                    new RectangleF(o.Position - new Vector2(8), new Vector2(16)).Contains(mouseWorld));
            }

            // 2. Handle Collision/Navigation (Polygons)
            if (layer is CollisionLayer col)
                return col.CollisionMesh.LastOrDefault(s => s.Shape.Contains(mouseWorld));
            if (layer is NavigationLayer nav)
                return nav.NavigationMesh.LastOrDefault(s => s.Shape.Contains(mouseWorld));

            // 3. Handle Triggers (Rects/Points)
            if (layer is TriggerLayer trig)
            {
                var rect = trig.TriggerMesh.LastOrDefault(r => new RectangleF(r.Position, r.Size).Contains(mouseWorld));
                if (rect != null) return rect;
                return trig.PointTriggers.LastOrDefault(p => Vector2.Distance(p.Position, mouseWorld) <= p.Radius);
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

        public void Update(ToolInput toolInput, InputState input, EventBus eventBus, EditorState editorState)
        {
            input.Drawing = _isDrawing;

            // This tool ONLY works on TriggerLayers.
            if (!(toolInput.ActiveLayer is TriggerLayer triggerLayer))
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
                        eventBus.Publish<IUndoableCommand>(new AddPointCommand(triggerLayer, pointObject));
                    }
                    _isDrawing = false;
                }
            }
        }

        public void DrawPreview(ToolInput toolInput, SpriteBatch sb, InputState input)
        {
            if (!_isDrawing) return;

            // Draw a circle and crosshair for the preview.
            sb.DrawCircle(_startWorldPos, _currentRadius, 32, Color.Yellow, 1f / input.Zoom);
            sb.DrawLine(_startWorldPos - new Vector2(4, 0), _startWorldPos + new Vector2(4, 0), Color.Yellow, 1f / input.Zoom);
            sb.DrawLine(_startWorldPos - new Vector2(0, 4), _startWorldPos + new Vector2(0, 4), Color.Yellow, 1f / input.Zoom);
        }
    }
}
