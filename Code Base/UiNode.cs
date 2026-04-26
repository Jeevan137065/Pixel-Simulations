using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System;
using System.Linq;

namespace Pixel_Simulations.Studio
{
    public class UINodeCanvas : UIElement
    {
        private readonly StudioState _state;

        private string _draggingNodeId = null;
        private Vector2 _dragGrabOffset;

        private string _linkingFromNodeId = null;
        private NodePort _linkingFromPort;
        private Vector2 _linkingCurrentPos;

        private const int SNAP_GRID = 10;
        private readonly Color _gridColor = Color.White * 0.05f;
        private Color[] _palette = { new Color(40, 40, 45), Color.DarkRed, Color.DarkGreen, Color.DarkSlateBlue, Color.DarkGoldenrod };

        public UINodeCanvas(StudioState state)
        {
            _state = state;
            ClipToBounds = true;
        }

        private Matrix GetTransformMatrix(AnimationStateMachine sm)
        {
            return Matrix.CreateTranslation(new Vector3(sm.GraphPanOffset, 0)) *
                   Matrix.CreateScale(sm.GraphZoom) *
                   Matrix.CreateTranslation(new Vector3(AbsoluteBounds.Location.ToVector2(), 0));
        }

        public override bool Update(EditorInputState input, EventBus bus = null)
        {
            if (!IsVisible || !AbsoluteBounds.Contains(input.MouseWindowPosition)) return false;

            var sm = _state.DataManager.CurrentStateMachine;
            if (sm == null) return true;

            Matrix transform = GetTransformMatrix(sm);
            Matrix inverseTransform = Matrix.Invert(transform);
            Vector2 mouseGraphPos = Vector2.Transform(input.MouseWindowPosition, inverseTransform);

            // --- ZOOMING (Scroll Wheel) ---
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float zoomDelta = scrollDelta > 0 ? 1.0f : -1.0f;
                float oldZoom = sm.GraphZoom;
                sm.GraphZoom = MathHelper.Clamp((float)Math.Round(sm.GraphZoom + zoomDelta), 1.0f, 5.0f);

                Vector2 mouseBefore = Vector2.Transform(input.MouseWindowPosition, Matrix.Invert(Matrix.CreateTranslation(new Vector3(sm.GraphPanOffset, 0)) * Matrix.CreateScale(oldZoom) * Matrix.CreateTranslation(new Vector3(AbsoluteBounds.Location.ToVector2(), 0))));
                Vector2 mouseAfter = Vector2.Transform(input.MouseWindowPosition, Matrix.Invert(GetTransformMatrix(sm)));

                sm.GraphPanOffset += (mouseAfter - mouseBefore) * sm.GraphZoom;
                return true;
            }

            // --- PANNING ---
            if (input.CurrentMouse.MiddleButton == ButtonState.Pressed || (input.CurrentKeyboard.IsKeyDown(Keys.Space) && input.LeftHold))
            {
                sm.GraphPanOffset += (input.CurrentMouse.Position.ToVector2() - input.PreviousMouse.Position.ToVector2());
                return true;
            }

            // --- KEYBOARD MOVEMENT ---
            if (!(_state.UI.FocusedElement is UITextBox))
            {
                float speed = input.CurrentKeyboard.IsKeyDown(Keys.LeftShift) ? 20f : 5f;
                Vector2 moveDir = Vector2.Zero;

                if (input.CurrentKeyboard.IsKeyDown(Keys.Up)) moveDir.Y -= 1;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Down)) moveDir.Y += 1;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Left)) moveDir.X -= 1;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Right)) moveDir.X += 1;

                if (moveDir != Vector2.Zero)
                {
                    // Move node if selected, otherwise pan canvas
                    if (_state.SelectedNodeName != null && sm.States.TryGetValue(_state.SelectedNodeName, out var selNode))
                        selNode.GraphPosition += moveDir * speed;
                    else
                        sm.GraphPanOffset -= moveDir * speed;
                }
            }

            // --- CLICK DETECTION ---
            if (input.IsNewLeftClick)
            {
                if (HandleFloatingMenuClick(input, bus, sm, transform)) return true;

                _state.SelectedNodeName = null;
                _state.SelectedTransitionID = null;

                // Check Nodes & Ports
                foreach (var node in sm.States.Values.Reverse())
                {
                    RectangleF bounds = new RectangleF(node.GraphPosition.X, node.GraphPosition.Y, node.Size.X, node.Size.Y);

                    var ports = GetPortPositions(node.GraphPosition, node.Size);
                    for (int i = 0; i < 4; i++)
                    {
                        if (Vector2.Distance(mouseGraphPos, ports[i]) < 15f) // Generous hit box
                        {
                            _linkingFromNodeId = node.Name;
                            _linkingFromPort = (NodePort)i;
                            _linkingCurrentPos = mouseGraphPos;
                            _state.SelectedNodeName = node.Name;
                            return true;
                        }
                    }

                    if (bounds.Contains(mouseGraphPos))
                    {
                        _state.SelectedNodeName = node.Name;
                        _draggingNodeId = node.Name;
                        _dragGrabOffset = node.GraphPosition - mouseGraphPos;
                        return true;
                    }
                }

                // Check Transitions
                foreach (var node in sm.States.Values)
                {
                    foreach (var trans in node.Transitions)
                    {
                        if (sm.States.TryGetValue(trans.TargetState, out var targetNode))
                        {
                            Vector2 p1 = GetPortPositions(node.GraphPosition, node.Size)[(int)trans.SourcePort];
                            Vector2 p2 = GetPortPositions(targetNode.GraphPosition, targetNode.Size)[(int)trans.TargetPort];

                            // Calculate approximate midpoint of the Bezier Curve
                            Vector2 cp1 = GetControlPoint(p1, trans.SourcePort, System.Math.Max(Vector2.Distance(p1, p2) * 0.5f, 30f));
                            Vector2 cp2 = GetControlPoint(p2, trans.TargetPort, System.Math.Max(Vector2.Distance(p1, p2) * 0.5f, 30f));
                            Vector2 midPoint = (p1 + cp1 + cp2 + p2) * 0.25f;

                            // MASSIVE Hitbox for ease of clicking (45 pixels)
                            if (Vector2.Distance(mouseGraphPos, midPoint) < 45f)
                            {
                                _state.SelectedTransitionID = trans.ID;
                                return true;
                            }
                        }
                    }
                }
            }

            // --- DRAGGING ---
            if (input.LeftHold)
            {
                if (_draggingNodeId != null && sm.States.TryGetValue(_draggingNodeId, out var mNode))
                {
                    Vector2 rawTarget = mouseGraphPos + _dragGrabOffset;
                    mNode.GraphPosition = new Vector2(
                        (float)System.Math.Round(rawTarget.X / SNAP_GRID) * SNAP_GRID,
                        (float)System.Math.Round(rawTarget.Y / SNAP_GRID) * SNAP_GRID
                    );
                }
                else if (_linkingFromNodeId != null) _linkingCurrentPos = mouseGraphPos;
            }
            else // --- RELEASE LINK ---
            {
                if (_linkingFromNodeId != null)
                {
                    // Inflate the bounds slightly to make dropping links extremely forgiving
                    var targetNode = sm.States.Values.LastOrDefault(n =>
                        new RectangleF(n.GraphPosition.X - 20, n.GraphPosition.Y - 20, n.Size.X + 40, n.Size.Y + 40).Contains(mouseGraphPos));

                    if (targetNode != null && targetNode.Name != _linkingFromNodeId)
                    {
                        var sourceNode = sm.States[_linkingFromNodeId];
                        NodePort bestTargetPort = GetClosestPort(targetNode, mouseGraphPos);

                        // Only block if a transition with the exact same ports exists
                        if (!sourceNode.Transitions.Any(t => t.TargetState == targetNode.Name && t.SourcePort == _linkingFromPort && t.TargetPort == bestTargetPort))
                        {
                            var trans = new StateTransition { TargetState = targetNode.Name, SourcePort = _linkingFromPort, TargetPort = bestTargetPort };
                            bus?.Publish(new AddTransitionCommand(sourceNode, trans));
                            _state.SelectedTransitionID = trans.ID;
                            _state.SelectedNodeName = null;
                        }
                    }
                    _linkingFromNodeId = null;
                }
                _draggingNodeId = null;
            }

            // --- RIGHT CLICK (Spawn Node) ---
            if (input.IsNewRightClick)
            {
                Vector2 snapPos = new Vector2((float)System.Math.Round(mouseGraphPos.X / SNAP_GRID) * SNAP_GRID, (float)System.Math.Round(mouseGraphPos.Y / SNAP_GRID) * SNAP_GRID);
                bus?.Publish(new AddAnimNodeCommand(sm, new AnimState { Name = "State_" + System.Guid.NewGuid().ToString().Substring(0, 4), GraphPosition = snapPos }));
            }

            return true;
        }

        private bool HandleFloatingMenuClick(EditorInputState input, EventBus bus, AnimationStateMachine sm, Matrix transform)
        {
            Matrix inverse = Matrix.Invert(transform);
            Vector2 mouseGraphPos = Vector2.Transform(input.MouseWindowPosition, inverse);

            if (_state.SelectedNodeName != null && sm.States.TryGetValue(_state.SelectedNodeName, out var node))
            {
                Vector2 menuPos = node.GraphPosition + new Vector2(node.Size.X / 2 - 50, -45);
                RectangleF rectColor = new RectangleF(menuPos.X, menuPos.Y, 32, 32);
                RectangleF rectShape = new RectangleF(menuPos.X + 35, menuPos.Y, 32, 32);
                RectangleF rectDelete = new RectangleF(menuPos.X + 70, menuPos.Y, 32, 32);

                if (rectColor.Contains(mouseGraphPos)) { node.NodeColor = _palette[(System.Array.IndexOf(_palette, node.NodeColor) + 1) % _palette.Length]; return true; }
                if (rectShape.Contains(mouseGraphPos)) { node.Shape = node.Shape == NodeShape.Rectangle ? NodeShape.Pill : NodeShape.Rectangle; return true; }
                if (rectDelete.Contains(mouseGraphPos)) { bus.Publish(new RemoveAnimNodeCommand(sm, node)); _state.SelectedNodeName = null; return true; }
            }
            else if (_state.SelectedTransitionID != null)
            {
                foreach (var n in sm.States.Values)
                {
                    var trans = n.Transitions.FirstOrDefault(t => t.ID == _state.SelectedTransitionID);
                    if (trans != null && sm.States.TryGetValue(trans.TargetState, out var target))
                    {
                        Vector2 p1 = GetPortPositions(n.GraphPosition, n.Size)[(int)trans.SourcePort];
                        Vector2 p2 = GetPortPositions(target.GraphPosition, target.Size)[(int)trans.TargetPort];
                        Vector2 menuPos = ((p1 + p2) * 0.5f) + new Vector2(-50, -45);

                        RectangleF rectColor = new RectangleF(menuPos.X, menuPos.Y, 32, 32);
                        RectangleF rectStyle = new RectangleF(menuPos.X + 35, menuPos.Y, 32, 32);
                        RectangleF rectDelete = new RectangleF(menuPos.X + 70, menuPos.Y, 32, 32);

                        if (rectColor.Contains(mouseGraphPos)) { trans.LineColor = trans.LineColor == Color.Cyan ? Color.Magenta : (trans.LineColor == Color.Magenta ? Color.LimeGreen : Color.Cyan); return true; }
                        if (rectStyle.Contains(mouseGraphPos)) { trans.Style = trans.Style == LineStyle.Solid ? LineStyle.Dashed : LineStyle.Solid; return true; }
                        if (rectDelete.Contains(mouseGraphPos)) { bus.Publish(new RemoveTransitionCommand(n, trans)); _state.SelectedTransitionID = null; return true; }
                    }
                }
            }
            return false;
        }

        private Vector2[] GetPortPositions(Vector2 pos, Vector2 size) => new Vector2[]
        {
            new Vector2(pos.X + size.X / 2, pos.Y),           // Top
            new Vector2(pos.X + size.X / 2, pos.Y + size.Y),  // Bottom
            new Vector2(pos.X, pos.Y + size.Y / 2),           // Left
            new Vector2(pos.X + size.X, pos.Y + size.Y / 2)   // Right
        };
        private static Vector2 GetControlPoint(Vector2 pos, Pixel_Simulations.Studio.NodePort port, float offset)
        {
            return port switch
            {
                Pixel_Simulations.Studio.NodePort.Top => pos + new Vector2(0, -offset),
                Pixel_Simulations.Studio.NodePort.Bottom => pos + new Vector2(0, offset),
                Pixel_Simulations.Studio.NodePort.Left => pos + new Vector2(-offset, 0),
                Pixel_Simulations.Studio.NodePort.Right => pos + new Vector2(offset, 0),
                _ => pos
            };
        }
        private NodePort GetClosestPort(AnimState node, Vector2 mousePos)
        {
            var ports = GetPortPositions(node.GraphPosition, node.Size);
            int best = 0; float minDist = float.MaxValue;
            for (int i = 0; i < 4; i++) { float d = Vector2.Distance(mousePos, ports[i]); if (d < minDist) { minDist = d; best = i; } }
            return (NodePort)best;
        }

        public override void Draw(SpriteBatch sb, IUIContext context, UITheme theme)
        {
            if (!IsVisible) return;
            var sm = _state.DataManager.CurrentStateMachine;
            if (sm == null) return;

            Matrix transform = GetTransformMatrix(sm);

            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            sb.End();

            // Apply Camera Transform (Zooming and Panning) directly to the SpriteBatch!
            sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true }, samplerState: SamplerState.PointClamp, transformMatrix: transform);
            sb.GraphicsDevice.ScissorRectangle = AbsoluteBounds;

            // 1. Draw Infinite Grid (Scaled down to world size)
            // We use inverse transform to find what part of the world the screen covers
            Matrix inverse = Matrix.Invert(transform);
            Vector2 tl = Vector2.Transform(AbsoluteBounds.Location.ToVector2(), inverse);
            Vector2 br = Vector2.Transform(new Vector2(AbsoluteBounds.Right, AbsoluteBounds.Bottom), inverse);

            sb.FillRectangle(new RectangleF(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y), new Color(15, 15, 18));

            int vGrid = 20;
            float startX = (float)System.Math.Floor(tl.X / vGrid) * vGrid;
            float startY = (float)System.Math.Floor(tl.Y / vGrid) * vGrid;

            for (float x = startX; x < br.X; x += vGrid) sb.DrawLine(x, tl.Y, x, br.Y, _gridColor, 1f / sm.GraphZoom);
            for (float y = startY; y < br.Y; y += vGrid) sb.DrawLine(tl.X, y, br.X, y, _gridColor, 1f / sm.GraphZoom);

            // 2. Draw Transitions
            foreach (var node in sm.States.Values)
            {
                foreach (var trans in node.Transitions)
                {
                    if (sm.States.TryGetValue(trans.TargetState, out var targetNode))
                    {
                        Vector2 startPos = GetPortPositions(node.GraphPosition, node.Size)[(int)trans.SourcePort];
                        Vector2 endPos = GetPortPositions(targetNode.GraphPosition, targetNode.Size)[(int)trans.TargetPort];

                        bool isSel = _state.SelectedTransitionID == trans.ID;
                        string label = !string.IsNullOrEmpty(trans.CustomLabel) ? trans.CustomLabel : (trans.Conditions.Count > 0 ? "*" : "");

                        // FIX: Draw a white glow BEHIND the selected line so the color is still visible!
                        if (isSel) UIDrawExtensions.DrawBezierCurve(sb, null, startPos, endPos, trans.SourcePort, trans.TargetPort, Color.White, 5f / sm.GraphZoom, trans.Style, "");
                        UIDrawExtensions.DrawBezierCurve(sb, theme.Font, startPos, endPos, trans.SourcePort, trans.TargetPort, trans.LineColor, isSel ? 3f : 2f, trans.Style, label);
                    }
                }
            }

            // 3. Draw Active Line
            if (_linkingFromNodeId != null && sm.States.TryGetValue(_linkingFromNodeId, out var linkSrc))
            {
                Vector2 startPos = GetPortPositions(linkSrc.GraphPosition, linkSrc.Size)[(int)_linkingFromPort];
                UIDrawExtensions.DrawBezierCurve(sb, theme.Font, startPos, _linkingCurrentPos, _linkingFromPort, NodePort.Left, Color.White, 2f, LineStyle.Dashed, "");
            }

            // 4. Draw Nodes
            foreach (var node in sm.States.Values)
            {
                Rectangle r = new Rectangle((int)node.GraphPosition.X, (int)node.GraphPosition.Y, (int)node.Size.X, (int)node.Size.Y);
                bool isSel = _state.SelectedNodeName == node.Name;
                bool isDef = sm.DefaultState == node.Name;
                Color bCol = isSel ? Color.Cyan : (isDef ? Color.Orange : Color.Gray);

                if (node.Shape == NodeShape.Pill) UIDrawExtensions.DrawPill(sb, theme.Font, r, "", node.NodeColor, Color.White);
                else sb.FillRectangle(r, node.NodeColor);

                if (node.Shape == NodeShape.Rectangle) sb.DrawRectangle(r, bCol, isSel ? 2f / sm.GraphZoom : 1f / sm.GraphZoom);

                if (theme.Font != null)
                {
                    Vector2 tSize = theme.Font.MeasureString(node.Name);
                    sb.DrawString(theme.Font, node.Name, new Vector2(r.Center.X - tSize.X / 2, r.Center.Y - tSize.Y / 2), Color.White);
                }

                // Draw Ports
                if (isSel || _linkingFromNodeId != null)
                {
                    foreach (var p in GetPortPositions(node.GraphPosition, node.Size))
                    {
                        UIDrawExtensions.FillCircle(sb, p, 6f / sm.GraphZoom, Color.DarkGray);
                        sb.DrawCircle(p, 6f / sm.GraphZoom, 12, Color.White, 1f / sm.GraphZoom);
                    }
                }
                if (_state.CurrentMode == StudioMode.Animator)
                {
                    var character = _state.DataManager.CurrentCharacter;
                    var atlas = _state.AssetLibrary?.GetAtlas(character?.AtlasName);

                    if (character != null && atlas != null)
                    {
                        string targetClip = $"{node.Name}_{_state.ActiveDirection}";
                        SpriteEffects flip = SpriteEffects.None;

                        // --- GOAL D: WEST MIRRORS EAST FALLBACK ---
                        if (_state.ActiveDirection == "West" && !character.Clips.ContainsKey(targetClip))
                        {
                            targetClip = $"{node.Name}_East";
                            flip = SpriteEffects.FlipHorizontally;
                        }

                        if (character.Clips.TryGetValue(targetClip, out var clip) && clip.Frames.Count > 0)
                        {
                            int frameIdx = MathHelper.Clamp(_state.CurrentFrameIndex, 0, clip.Frames.Count - 1);
                            var frame = clip.Frames[frameIdx];

                            var drawOrderDict = character.DrawOrders.ContainsKey(_state.ActiveDirection) ?
                                                character.DrawOrders[_state.ActiveDirection] :
                                                new System.Collections.Generic.Dictionary<string, int>();

                            var sortedParts = character.BodyParts.OrderBy(p => drawOrderDict.ContainsKey(p) ? drawOrderDict[p] : 0).ToList();

                            Vector2 previewCenter = new Vector2(r.Center.X, r.Bottom + (character.FrameSize.Y * 2f));

                            foreach (var partName in sortedParts)
                            {
                                if (frame.Parts.TryGetValue(partName, out Rectangle partRect))
                                {
                                    Vector2 partOrigin = new Vector2(partRect.Width / 2f, partRect.Height);

                                    // Let the node graph's integer zoom handle the scaling cleanly!
                                    sb.Draw(atlas, previewCenter, partRect, Color.White, 0f, partOrigin, 2f, flip, 0f);
                                }
                            }
                        }
                    }
                }
            }

            // 5. Draw Floating Menus (In world space, so they zoom with the graph)
            if (_state.SelectedNodeName != null && sm.States.TryGetValue(_state.SelectedNodeName, out var selNode))
            {
                Vector2 menuPos = selNode.GraphPosition + new Vector2(selNode.Size.X / 2 - 50, -45);
                DrawFloatingMenu(sb, context, menuPos, new[] { "Icon_Color", "Icon_Shape", "Icon_Delete" }, sm.GraphZoom);
            }
            else if (_state.SelectedTransitionID != null)
            {
                foreach (var n in sm.States.Values)
                {
                    var trans = n.Transitions.FirstOrDefault(t => t.ID == _state.SelectedTransitionID);
                    if (trans != null && sm.States.TryGetValue(trans.TargetState, out var target))
                    {
                        Vector2 p1 = GetPortPositions(n.GraphPosition, n.Size)[(int)trans.SourcePort];
                        Vector2 p2 = GetPortPositions(target.GraphPosition, target.Size)[(int)trans.TargetPort];
                        Vector2 menuPos = ((p1 + p2) * 0.5f) + new Vector2(-50, -45);
                        DrawFloatingMenu(sb, context, menuPos, new[] { "Icon_Color", "Icon_Style", "Icon_Delete" }, sm.GraphZoom);
                        break;
                    }
                }
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = prevScissor;
            sb.Begin();
        }

        private void DrawFloatingMenu(SpriteBatch sb, IUIContext context, Vector2 pos, string[] icons, float zoom)
        {
            Rectangle bg = new Rectangle((int)pos.X - 5, (int)pos.Y - 5, (icons.Length * 35) + 5, 42);
            UIDrawExtensions.DrawPill(sb, null, bg, "", Color.Black * 0.8f, Color.White);
            sb.DrawRectangle(bg, Color.White * 0.5f, 1f / zoom);

            for (int i = 0; i < icons.Length; i++)
            {
                Rectangle btn = new Rectangle((int)pos.X + (i * 35), (int)pos.Y, 32, 32);
                sb.FillRectangle(btn, Color.DarkSlateGray);
                context.DrawIcon(sb, btn, icons[i], Color.White);
            }
        }
    }
}