using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.UI
{
    public enum StackDirection { Vertical, Horizontal }
    // 1. ADD THIS INTERFACE so elements know how to draw icons and check focus without needing EditorUI
    public interface IUIContext
    {
        void DrawIcon(SpriteBatch sb, Rectangle destination, string iconName, Color color);
        void SetFocus(UIElement element);
        void CheckFocusClick(UIElement root, EditorInputState input);
        UIElement FocusedElement { get; }
    }
    // Defines the look of the UI so the Game and Editor can look different.
    public class UITheme
    {
        public static SpriteFont DefaultFont { get; set; }
        public SpriteFont Font { get; set; }
        public Color PanelBackground { get; set; } = Color.Black;
        public Color ButtonNormal { get; set; } = Color.Black * 0.6f;
        public Color ButtonHover { get; set; } = Color.Gray;
        public Color ButtonPressed { get; set; } = Color.DarkGray;
        public Color TextColor { get; set; } = Color.White;
        public Color BorderColor { get; set; } = Color.Black;
    }

    // A generic input wrapper so the UI isn't tied to the EditorInputState
    public class UIInputState
    {
        public MouseState CurrentMouse;
        public MouseState PreviousMouse;
        public KeyboardState CurrentKeyboard;
        public KeyboardState PreviousKeyboard;

        public bool IsLeftClick => CurrentMouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
        public bool IsLeftDown => CurrentMouse.LeftButton == ButtonState.Pressed;
        public Vector2 MousePos => CurrentMouse.Position.ToVector2();
    }

    // The base class for absolutely everything in the UI
    public abstract partial class UIElement
    {
        public Vector2 LocalPosition { get; set; }
        public Vector2 Size { get; set; }
        public UIElement Parent { get; set; }
        public List<UIElement> Children { get; } = new List<UIElement>();
        public bool IsFocused { get; set; }
        public bool ClipToBounds { get; set; } = false;
        public Vector2 ScrollOffset { get; set; } = Vector2.Zero;
        public string DebugName { get; set; } = "UIElement"; // For the debug screen
        public Action OnGotFocus; // To trigger events when clicked/focused
        public Color? BackgroundColor { get; set; }
        public Color? BorderColor { get; set; }
        public Color? TextColor { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsHovered { get; internal set; }

        // Calculates absolute screen position by walking up the tree
        public Rectangle AbsoluteBounds
        {
            get
            {
                float px = Parent != null ? Parent.AbsoluteBounds.X - Parent.ScrollOffset.X : 0;
                float py = Parent != null ? Parent.AbsoluteBounds.Y - Parent.ScrollOffset.Y : 0;
                return new Rectangle((int)(LocalPosition.X + px), (int)(LocalPosition.Y + py), (int)Size.X, (int)Size.Y);
            }
        }


        public void AddChild(UIElement child)
        {
            child.Parent = this;
            Children.Add(child);
            UpdateLayout();
        }

        public void RemoveChild(UIElement child)
        {
            child.Parent = null;
            Children.Remove(child);
            UpdateLayout();
        }

        public virtual void ClearChildren()
        {
            foreach (var child in Children) child.Parent = null;
            Children.Clear();
            UpdateLayout();
        }

        // Called when children are added/removed to recalculate bounds (overridden by StackPanels)
        public virtual void UpdateLayout()
        {
            foreach (var child in Children) child.UpdateLayout();
        }

        // Returns true if this element (or a child) consumed the input
        public virtual bool Update(EditorInputState input, EventBus bus = null)
        {
            if (!IsVisible) return false;
            bool consumed = false;
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                if (Children[i].Update(input, bus)) consumed = true;
            }
            return consumed;
        }
        public virtual void Draw(SpriteBatch sb, IUIContext context, UITheme theme)
        {
            if (!IsVisible) return;

            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            if (ClipToBounds)
            {
                sb.End();
                sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
                var clipRect = Rectangle.Intersect(AbsoluteBounds, sb.GraphicsDevice.Viewport.Bounds);
                if (clipRect.Width > 0 && clipRect.Height > 0) sb.GraphicsDevice.ScissorRectangle = clipRect;
            }

            foreach (var child in Children)
            {
                child.LocalPosition -= ScrollOffset;
                child.Draw(sb, context, theme);
                child.LocalPosition += ScrollOffset;
            }

            if (ClipToBounds)
            {
                sb.End();
                sb.GraphicsDevice.ScissorRectangle = prevScissor;
                sb.Begin();
            }
        }
    }

    // A simple colored background container
    public class UIPanel : UIElement
    {

        public override void Draw(SpriteBatch sb, IUIContext ui, UITheme theme)
        {
            if (!IsVisible) return;

            // Use local override, or fallback to theme!
            Color bg = BackgroundColor ?? theme.PanelBackground;
            Color border = BorderColor ?? theme.BorderColor;

            if (bg != Color.Transparent) sb.FillRectangle(AbsoluteBounds, bg);
            if (border != Color.Transparent) sb.DrawRectangle(AbsoluteBounds, border, 1);

            base.Draw(sb, ui, theme);
        }
    }

    public class UIStackPanel : UIPanel
    {
        public StackDirection Direction { get; set; } = StackDirection.Vertical;
        public float Spacing { get; set; } = 5f;
        public float Padding { get; set; } = 5f;
        public bool AutoSize { get; set; } = true;
        public Color PanelBackground { get; set; } = Color.Transparent;
        public override void UpdateLayout()
        {
            float currentPos = Padding;
            float maxCrossSize = 0;

            foreach (var child in Children)
            {
                if (!child.IsVisible) continue;

                if (Direction == StackDirection.Vertical)
                {
                    child.LocalPosition = new Vector2(Padding, currentPos);
                    currentPos += child.Size.Y + Spacing;
                    maxCrossSize = Math.Max(maxCrossSize, child.Size.X);
                }
                else
                {
                    child.LocalPosition = new Vector2(currentPos, Padding);
                    currentPos += child.Size.X + Spacing;
                    maxCrossSize = Math.Max(maxCrossSize, child.Size.Y);
                }
                child.UpdateLayout();
            }

            if (AutoSize)
            {
                if (Direction == StackDirection.Vertical)
                    Size = new Vector2(maxCrossSize + (Padding * 2), currentPos - Spacing + Padding);
                else
                    Size = new Vector2(currentPos - Spacing + Padding, maxCrossSize + (Padding * 2));
            }
        }
    }

    public class UILabel : UIElement
    {
        private string _text = "";
        public Color? ColorOverride { get; set; }
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                // Calculate size IMMEDIATELY when text changes
                if (UITheme.DefaultFont != null && !string.IsNullOrEmpty(_text))
                    Size = UITheme.DefaultFont.MeasureString(_text);
                else
                    Size = Vector2.Zero;
            }
        }
        public override void Draw(SpriteBatch sb, IUIContext ui, UITheme theme)
        {
            if (!IsVisible || string.IsNullOrEmpty(Text)) return;
            sb.DrawString(UITheme.DefaultFont ?? theme.Font, Text, AbsoluteBounds.Location.ToVector2(), TextColor ?? theme.TextColor);
            base.Draw(sb, ui, theme);
        }
    }
    // A panel that wraps its children to the next row when it runs out of horizontal space!
    public class UIFlowPanel : UIPanel
    {
        public float SpacingX { get; set; } = 5f;
        public float SpacingY { get; set; } = 5f;
        public float Padding { get; set; } = 5f;

        public override void UpdateLayout()
        {
            float currentX = Padding;
            float currentY = Padding;
            float currentRowHeight = 0;

            foreach (var child in Children)
            {
                if (!child.IsVisible) continue;

                // If this element exceeds the panel width, wrap to the next line!
                if (currentX + child.Size.X > (Size.X - Padding) && currentX > Padding)
                {
                    currentX = Padding;
                    currentY += currentRowHeight + SpacingY;
                    currentRowHeight = 0; // Reset row height
                }

                child.LocalPosition = new Vector2(currentX, currentY);
                child.UpdateLayout(); // Update child's internals

                currentX += child.Size.X + SpacingX;
                currentRowHeight = System.Math.Max(currentRowHeight, child.Size.Y);
            }

            // Auto-expand the Y size to fit all rows
            Size = new Vector2(Size.X, currentY + currentRowHeight + Padding);
        }
    }
    public class UIButton : UIElement
    {
        public string Text { get; set; }
        public string IconName { get; set; }

        public ICommand Command { get; set; } // Left Click Command
        public ICommand RightCommand { get; set; } // Right Click Command

        public Action OnClick;
        public Action OnRightClick;

        public bool IsSelected { get; set; }
        private bool _isPressed;
        public override bool Update(EditorInputState input, EventBus bus)
        {
            if (!IsVisible) return false;

            // 1. CRITICAL FIX: Let children update first! 
            // This allows nested buttons (like Delete inside a Row) to work correctly.
            bool consumedByChild = base.Update(input, bus);
            if (consumedByChild) return true;

            // 2. Handle this button's interaction
            IsHovered = AbsoluteBounds.Contains(input.MouseWindowPosition);

            if (IsHovered)
            {
                if (input.LeftHold || input.RightHold) _isPressed = true;

                if (input.IsNewLeftClick)
                {
                    if (Command != null) bus.Publish(Command);
                    OnClick?.Invoke();
                    _isPressed = false;
                    return true; // Click consumed!
                }
                else if (input.IsNewRightClick)
                {
                    if (RightCommand != null) bus.Publish(RightCommand);
                    OnRightClick?.Invoke();
                    _isPressed = false;
                    return true;
                }
            }
            else _isPressed = false;

            return IsHovered;
        }

        public override void Draw(SpriteBatch sb, IUIContext ui, UITheme theme)
        {
            if (!IsVisible) return;

            // Button specific color logic, overriding with BackgroundColor if provided
            Color baseBg = BackgroundColor ?? theme.ButtonNormal;
            Color bgColor = _isPressed ? theme.ButtonPressed : (IsHovered ? theme.ButtonHover : baseBg);
            if (IsSelected) bgColor = Color.Goldenrod * 0.5f;

            Color border = BorderColor ?? theme.BorderColor;

            if (bgColor != Color.Transparent) sb.FillRectangle(AbsoluteBounds, bgColor);
            sb.DrawRectangle(AbsoluteBounds, IsSelected ? Color.Yellow : border, IsSelected ? 2 : 1);

            if (!string.IsNullOrEmpty(IconName))
            {
                ui.DrawIcon(sb, AbsoluteBounds, IconName, Color.White);
            }
            else if (!string.IsNullOrEmpty(Text))
            {
                Vector2 textSize = theme.Font.MeasureString(Text);
                Vector2 textPos = AbsoluteBounds.Center.ToVector2() - (textSize / 2);
                sb.DrawString(theme.Font, Text, textPos, TextColor ?? theme.TextColor);
            }
            base.Draw(sb, ui, theme);
        }
    }

    public class UITextBox : UIElement
    {
        public string Text { get; set; } = "";
        public string Placeholder { get; set; } = "Type here...";
        public Action<string> OnTextChanged;
        public Action<string> OnSubmit;
        private int _backspaceFrames = 0; // Frame counter for holding backspace
        private bool _wasFocused = false;
        public override bool Update(EditorInputState input, EventBus bus)
        {
            if (!IsVisible) return false;

            IsHovered = AbsoluteBounds.Contains(input.MouseWindowPosition);

            // Request Focus on Click
            if (IsHovered && input.IsNewLeftClick)
            {
                // We will handle global focus setting in EditorUI
                return true;
            }
            if (IsFocused) HandleTyping(input);
            if (_wasFocused && !IsFocused)
            {
                OnSubmit?.Invoke(Text);
            }
            _wasFocused = IsFocused;

            return IsHovered;
        }

        private void HandleTyping(EditorInputState input)
        {
            var kbs = input.CurrentKeyboard;
            var prevKbs = input.PreviousKeyboard;

            // 1. Defocus on Enter
            if (kbs.IsKeyDown(Keys.Enter) && prevKbs.IsKeyUp(Keys.Enter))
            {
                IsFocused = false;
                return;
            }

            // 2. Continuous Backspace Logic
            if (kbs.IsKeyDown(Keys.Back))
            {
                _backspaceFrames++;
                // Fire on first frame, then wait 30 frames (0.5s), then fire every 2 frames (super fast)
                if (_backspaceFrames == 1 || (_backspaceFrames > 30 && _backspaceFrames % 2 == 0))
                {
                    if (Text.Length > 0)
                    {
                        Text = Text.Substring(0, Text.Length - 1);
                        OnTextChanged?.Invoke(Text);
                    }
                }
            }
            else _backspaceFrames = 0; // Reset when released

            // 3. Handle Typing Letters (with CapsLock support)
            bool shift = kbs.IsKeyDown(Keys.LeftShift) || kbs.IsKeyDown(Keys.RightShift);
            bool caps = kbs.CapsLock;
            bool isUpper = shift ^ caps; // XOR: Shift reverses CapsLock

            foreach (var key in kbs.GetPressedKeys())
            {
                if (prevKbs.IsKeyUp(key)) // Only trigger on initial press for normal typing
                {
                    char c = GetCharFromKey(key, isUpper, shift); // Pass shift separately for symbols
                    if (c != '\0')
                    {
                        Text += c;
                        OnTextChanged?.Invoke(Text);
                    }
                }
            }
        }

        private char GetCharFromKey(Keys key, bool isUpper, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z) return isUpper ? (char)key : char.ToLower((char)key);
            if (key >= Keys.D0 && key <= Keys.D9) return shift ? GetSymbol(key) : (char)key;
            if (key == Keys.Space) return ' ';
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            if (key == Keys.OemComma) return ',';
            return '\0';
        }
        private char GetSymbol(Keys key)
        {
            return key switch
            {
                Keys.D3 => '#', // Shift+3 = #
                _ => '\0'
            };
        }
        public override void Draw(SpriteBatch sb, IUIContext ui, UITheme theme)
        {
            if (!IsVisible) return;

            sb.FillRectangle(AbsoluteBounds, IsFocused ? Color.White : Color.Gray);
            sb.DrawRectangle(AbsoluteBounds, IsFocused ? Color.Yellow : theme.BorderColor, IsFocused ? 2 : 1);

            string displayText = string.IsNullOrEmpty(Text) ? Placeholder : Text;
            Color textColor = string.IsNullOrEmpty(Text) ? Color.DarkGray : Color.Black;

            if (IsFocused && (System.DateTime.Now.Millisecond % 1000) < 500) displayText += "|";

            sb.DrawString(theme.Font, displayText, AbsoluteBounds.Location.ToVector2() + new Vector2(5, 5), textColor);
            base.Draw(sb, ui, theme);
        }
    }

    // --- CUSTOM OPTIMIZED GRID ELEMENT ---
    // This lives inside the panel to tightly couple with its specific data needs, 
    // while perfectly respecting the UIFramework's scrolling and clipping rules.
    public class UIGridView : UIElement
    {
        private readonly EditorState _es;
        private readonly TilesetPanel _parentPanel;
        private const int TILE_SIZE = 32;

        public UIGridView(EditorState es, TilesetPanel parentPanel)
        {
            _es = es;
            _parentPanel = parentPanel;
        }

        public override bool Update(EditorInputState input, EventBus bus)
        {
            if (!AbsoluteBounds.Contains(input.MouseWindowPosition)) return false;

            // Handle Scrolling
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            if (scrollDelta != 0)
                ScrollOffset = new Vector2(0, Math.Max(0, ScrollOffset.Y - scrollDelta * 0.5f));

            // Handle Clicks based on active tab
            if (input.IsNewLeftClick)
            {
                if (_parentPanel._activeTab == Tab.Tiles) HandleTileClick(input, bus);
                else HandleObjectClick(input, bus);
            }

            return true; // Consume mouse
        }

        private void HandleTileClick(EditorInputState input, EventBus bus)
        {
            var activeTs = _es.ActiveTileSets.FirstOrDefault(ts => ts.Name == _es.TilesetPanel.ActiveTilesetName);
            if (activeTs == null) return;

            int tilesPerRow = (int)Size.X / TILE_SIZE;
            int relX = (int)input.MouseWindowPosition.X - AbsoluteBounds.X;
            int relY = (int)input.MouseWindowPosition.Y - AbsoluteBounds.Y + (int)ScrollOffset.Y;

            int col = relX / TILE_SIZE;
            int row = relY / TILE_SIZE;
            int tileIndex = row * tilesPerRow + col;

            if (tileIndex >= 0 && tileIndex < activeTs.SlicedAtlas.Count)
            {
                int tileId = activeTs.SlicedAtlas.Keys.ElementAt(tileIndex);
                bus.Publish(new SelectTileCommand { TilesetName = activeTs.Name, TileID = tileId });
            }
        }

        private void HandleObjectClick(EditorInputState input, EventBus bus)
        {
            int slotSize = 64, spacing = 6;
            int x = AbsoluteBounds.X + spacing;
            int y = AbsoluteBounds.Y + spacing - (int)ScrollOffset.Y;

            foreach (var prefab in _es.PrefabManager.Prefabs.Values)
            {
                Rectangle slotRect = new Rectangle(x, y, slotSize, slotSize);
                if (slotRect.Contains(input.MouseWindowPosition))
                {
                    bus.Publish(new SelectPrefabCommand { PrefabID = prefab.ID });
                    return;
                }

                x += slotSize + spacing;
                if (x + slotSize > AbsoluteBounds.Right) { x = AbsoluteBounds.X + spacing; y += slotSize + spacing; }
            }
        }

        public override void Draw(SpriteBatch sb, IUIContext ui, UITheme theme)
        {
            if (!IsVisible) return;

            // The base UIElement Draw handles ScissorRect clipping, so we call it
            // and do our drawing inside the clipped context.
            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            if (ClipToBounds)
            {
                sb.End();
                sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true });
                sb.GraphicsDevice.ScissorRectangle = AbsoluteBounds;
            }

            if (_parentPanel._activeTab == Tab.Tiles) DrawTiles(sb);
            else DrawObjects(sb);

            if (ClipToBounds)
            {
                sb.End();
                sb.GraphicsDevice.ScissorRectangle = prevScissor;
                sb.Begin();
            }
        }

        private void DrawTiles(SpriteBatch sb)
        {
            var activeTs = _es.ActiveTileSets.FirstOrDefault(ts => ts.Name == _es.TilesetPanel.ActiveTilesetName);
            if (activeTs == null) return;

            int tilesPerRow = (int)Size.X / TILE_SIZE;
            int index = 0;

            foreach (var tilePair in activeTs.SlicedAtlas.OrderBy(p => p.Key))
            {
                int col = index % tilesPerRow;
                int row = index / tilesPerRow;

                var destRect = new Rectangle(
                    AbsoluteBounds.X + col * TILE_SIZE,
                    AbsoluteBounds.Y + row * TILE_SIZE - (int)ScrollOffset.Y,
                    TILE_SIZE, TILE_SIZE);

                if (destRect.Bottom > AbsoluteBounds.Top && destRect.Top < AbsoluteBounds.Bottom)
                {
                    sb.Draw(tilePair.Value, destRect, Color.White);

                    if (tilePair.Key == _es.TilesetPanel.SelectedTileID)
                        sb.DrawRectangle(destRect, Color.Yellow, 2);
                    else
                        sb.DrawRectangle(destRect, Color.Black * 0.2f, 1);
                }
                index++;
            }
        }

        private void DrawObjects(SpriteBatch sb)
        {
            int slotSize = 64, spacing = 6;
            int x = AbsoluteBounds.X + spacing;
            int y = AbsoluteBounds.Y + spacing - (int)ScrollOffset.Y;

            foreach (var prefab in _es.PrefabManager.Prefabs.Values)
            {
                Rectangle slotRect = new Rectangle(x, y, slotSize, slotSize);
                bool isSelected = _es.Selection.ActivePrefab?.ID == prefab.ID;

                if (slotRect.Bottom > AbsoluteBounds.Top && slotRect.Top < AbsoluteBounds.Bottom)
                {
                    sb.FillRectangle(slotRect, Color.Black * 0.3f);
                    sb.DrawRectangle(slotRect, isSelected ? Color.Yellow : Color.White * 0.2f, 1);

                    var tex = _es.AssetLibrary.GetAtlas(prefab.AtlasName);
                    if (tex != null)
                    {
                        float scale = (slotSize - 10f) / Math.Max(prefab.SourceRect.Width, prefab.SourceRect.Height);
                        Vector2 pos = new Vector2(
                            slotRect.Center.X - (prefab.SourceRect.Width * scale) / 2,
                            slotRect.Center.Y - (prefab.SourceRect.Height * scale) / 2);
                        sb.Draw(tex, pos, prefab.SourceRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    }
                }

                x += slotSize + spacing;
                if (x + slotSize > AbsoluteBounds.Right) { x = AbsoluteBounds.X + spacing; y += slotSize + spacing; }
            }
        }
    }

    public static class UIDrawExtensions
    {
        private static Texture2D _circleTexture;
        private static Texture2D _pixelTexture;

        private static void EnsureTextures(GraphicsDevice gd)
        {
            if (_pixelTexture == null)
            {
                _pixelTexture = new Texture2D(gd, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
            if (_circleTexture == null)
            {
                int diam = 64;
                _circleTexture = new Texture2D(gd, diam, diam);
                Color[] data = new Color[diam * diam];
                for (int x = 0; x < diam; x++)
                    for (int y = 0; y < diam; y++)
                        data[x + y * diam] = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) <= 32 ? Color.White : Color.Transparent;
                _circleTexture.SetData(data);
            }
        }
        public static void DrawDashedLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness = 2f, float dashLength = 5f, float spaceLength = 5f)
        {
            EnsureTextures(sb.GraphicsDevice);
            float distance = Vector2.Distance(start, end);
            Vector2 direction = Vector2.Normalize(end - start);
            float currentPos = 0;
            bool draw = true;

            while (currentPos < distance)
            {
                float length = System.Math.Min(dashLength, distance - currentPos);
                if (draw)
                {
                    sb.DrawLine(start + direction * currentPos, start + direction * (currentPos + length), color, thickness);
                }
                currentPos += draw ? dashLength : spaceLength;
                draw = !draw; // Toggle
            }
        }
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

        public static void DrawBezierCurve(SpriteBatch sb, SpriteFont font, Vector2 start, Vector2 end, Pixel_Simulations.Studio.NodePort startPort, Pixel_Simulations.Studio.NodePort endPort, Color color, float thickness, Pixel_Simulations.Studio.LineStyle style, string label)
        {
            EnsureTextures(sb.GraphicsDevice);

            float distance = Vector2.Distance(start, end);
            float controlOffset = System.Math.Max(distance * 0.5f, 30f);

            Vector2 cp1 = GetControlPoint(start, startPort, controlOffset);
            Vector2 cp2 = GetControlPoint(end, endPort, controlOffset);

            int segments = 40;
            Vector2 previousPoint = start;
            float distanceTraveled = 0;
            float dashLength = style == Pixel_Simulations.Studio.LineStyle.Dashed ? 10f : 4f;

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float u = 1 - t;
                Vector2 p = (u * u * u * start) + (3 * u * u * t * cp1) + (3 * u * t * t * cp2) + (t * t * t * end);

                distanceTraveled += Vector2.Distance(previousPoint, p);

                bool shouldDraw = true;
                if (style == Pixel_Simulations.Studio.LineStyle.Dashed || style == Pixel_Simulations.Studio.LineStyle.Dotted)
                {
                    shouldDraw = (distanceTraveled % (dashLength * 2)) < dashLength;
                }

                if (shouldDraw) sb.DrawLine(previousPoint, p, color, thickness);
                previousPoint = p;
            }

            // Draw Arrowhead
            Vector2 dir = Vector2.Normalize(end - cp2);
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            Vector2 arrowBase = end - (dir * 12f);
            sb.DrawLine(end, arrowBase + (perp * 6f), color, thickness + 1);
            sb.DrawLine(end, arrowBase - (perp * 6f), color, thickness + 1);

            // Draw Label
            if (!string.IsNullOrEmpty(label) && font != null)
            {
                Vector2 midPoint = (start + cp1 + cp2 + end) * 0.25f;
                Vector2 textSize = font.MeasureString(label);
                Rectangle labelBounds = new Rectangle((int)midPoint.X - (int)textSize.X / 2 - 10, (int)midPoint.Y - (int)textSize.Y / 2 - 5, (int)textSize.X + 20, (int)textSize.Y + 10);
                DrawPill(sb, font, labelBounds, label, Color.Black * 0.8f, color);
            }
        }

        public static void FillCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
        {
            EnsureTextures(sb.GraphicsDevice);
            float scale = (radius * 2f) / _circleTexture.Width;
            sb.Draw(_circleTexture, center, null, color, 0f, new Vector2(32, 32), scale, SpriteEffects.None, 0f);
        }

        public static void DrawPill(SpriteBatch sb, SpriteFont font, Rectangle bounds, string text, Color pillColor, Color textColor)
        {
            EnsureTextures(sb.GraphicsDevice);
            int radius = bounds.Height / 2;
            int diam = radius * 2;
            float scale = (float)diam / _circleTexture.Width;
            sb.Draw(_circleTexture, new Vector2(bounds.X, bounds.Y), null, pillColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.Draw(_circleTexture, new Vector2(bounds.Right - diam, bounds.Y), null, pillColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.Draw(_pixelTexture, new Rectangle(bounds.X + radius, bounds.Y, bounds.Width - diam, bounds.Height), pillColor);

            if (!string.IsNullOrEmpty(text) && font != null)
            {
                Vector2 tSize = font.MeasureString(text);
                sb.DrawString(font, text, new Vector2(bounds.Center.X - tSize.X / 2, bounds.Center.Y - tSize.Y / 2), textColor);
            }
        }
    }


    public class UISlider : UIElement
    {
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 1f;
        public float Value { get; set; } = 0.5f;
        public Action<float> OnValueChanged;

        private bool _isDragging = false;

        public override bool Update(EditorInputState input, EventBus bus)
        {
            if (!IsVisible) return false;

            IsHovered = AbsoluteBounds.Contains(input.MouseWindowPosition);

            // Start drag
            if (IsHovered && input.IsNewLeftClick)
            {
                _isDragging = true;
                // Optionally set focus here if you want to lock it
            }

            // Process drag
            if (_isDragging)
            {
                if (!input.LeftHold)
                {
                    _isDragging = false;
                }
                else
                {
                    // Calculate value based on mouse X relative to bounds
                    float localX = input.MouseWindowPosition.X - AbsoluteBounds.X;
                    float percentage = MathHelper.Clamp(localX / AbsoluteBounds.Width, 0f, 1f);

                    float newValue = Min + (percentage * (Max - Min));
                    if (Value != newValue)
                    {
                        Value = newValue;
                        OnValueChanged?.Invoke(Value);
                    }
                }
            }

            return IsHovered || _isDragging;
        }

        public override void Draw(SpriteBatch sb, IUIContext ui, UITheme theme)
        {
            if (!IsVisible) return;

            // Draw Track Background
            sb.FillRectangle(new Rectangle(AbsoluteBounds.X, AbsoluteBounds.Center.Y - 2, AbsoluteBounds.Width, 4), Color.Black);

            // Draw Filled Track
            float percentage = (Value - Min) / (Max - Min);
            int filledWidth = (int)(AbsoluteBounds.Width * percentage);
            sb.FillRectangle(new Rectangle(AbsoluteBounds.X, AbsoluteBounds.Center.Y - 2, filledWidth, 4), Color.LimeGreen);

            // Draw Handle
            int handleX = AbsoluteBounds.X + filledWidth - 5;
            Rectangle handleRect = new Rectangle(handleX, AbsoluteBounds.Center.Y - 8, 10, 16);
            sb.FillRectangle(handleRect, _isDragging ? Color.White : Color.LightGray);
            sb.DrawRectangle(handleRect, Color.Black, 1);

            base.Draw(sb, ui, theme);
        }
    }














}