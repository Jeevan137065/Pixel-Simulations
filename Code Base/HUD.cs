using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.UI
{
    public enum TitleMode { TopCenter, TopRight }

    public class HudMessage
    {
        public string Text;
        public float Timer;
        public float MaxTimer;
        public Vector2 Position;
        public float TargetY;
        public float Scale;
        public float Alpha = 1f;
    }

    public class HudEmote
    {
        public Rectangle SourceRect;
        public Vector2 Position;
        public float Timer;
        public float MaxTimer;
        public float WobbleOffset;
        public float Scale;
        public float Alpha = 1f;
    }

    public class AreaTitle
    {
        public string MainText;
        public string SubText;
        public float Timer;
        public float MaxTimer;
        public TitleMode Mode;
        public float Alpha = 0f;
    }

    public class HUDManager
    {
        // --- Collections ---
        private List<HudMessage> _messages = new List<HudMessage>();
        private List<HudEmote> _emotes = new List<HudEmote>();
        private AreaTitle _currentTitle;

        // --- References ---
        private SpriteFont _font;
        private Texture2D _emoteTexture;
        private Random _random = new Random();
        private Texture2D _pixel;
        // --- Config ---
        private const int EMOTE_SIZE = 32; // Assuming the grid cells are 32x32
        private Vector2 _screenSize = new Vector2(1920, 1080); // Final output resolution

        // --- Dictionary for Rich Text Colors ---
        private readonly Dictionary<string, Color> _colorTags = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Red", Color.Tomato }, { "Green", Color.LimeGreen }, { "Blue", Color.CornflowerBlue },
            { "Yellow", Color.Gold }, { "Orange", Color.Orange }, { "Purple", Color.MediumPurple },
            { "Gray", Color.LightGray }, { "White", Color.White }
        };

        public void LoadContent(SpriteFont font, Texture2D emoteTexture, GraphicsDevice gd)
        {
            _font = font;
            _emoteTexture = emoteTexture;
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;

            // 1. UPDATE MESSAGES (Bottom Left)
            float currentTargetY = _screenSize.Y - 50; // Start near bottom
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                var msg = _messages[i];
                msg.Timer -= dt;

                // Fade out in the last second
                if (msg.Timer < 1f) msg.Alpha = Math.Max(0, msg.Timer);

                // Smoothly slide to target Y position (stacks them)
                msg.TargetY = currentTargetY;
                msg.Position.Y = MathHelper.Lerp(msg.Position.Y, msg.TargetY, dt * 10f);

                if (msg.Timer <= 0) _messages.RemoveAt(i);

                currentTargetY -= (_font.MeasureString("A").Y * msg.Scale) + 10; // Space for next message above
            }

            // 2. UPDATE EMOTES (Bottom Right -> Mid Right)
            for (int i = _emotes.Count - 1; i >= 0; i--)
            {
                var emote = _emotes[i];
                emote.Timer -= dt;

                // Float Upwards
                emote.Position.Y -= 150f * dt;
                // Sine wave wobble based on unique offset
                emote.Position.X += (float)Math.Sin((totalTime * 4f) + emote.WobbleOffset) * 2f;

                // Fade out smoothly
                emote.Alpha = MathHelper.Clamp(emote.Timer / (emote.MaxTimer * 0.5f), 0f, 1f);

                if (emote.Timer <= 0) _emotes.RemoveAt(i);
            }

            // 3. UPDATE TITLE DROP
            if (_currentTitle != null)
            {
                _currentTitle.Timer -= dt;

                // Fade In (First 1 second)
                if (_currentTitle.MaxTimer - _currentTitle.Timer < 1f)
                    _currentTitle.Alpha = MathHelper.Lerp(_currentTitle.Alpha, 1f, dt * 5f);
                // Fade Out (Last 1 second)
                else if (_currentTitle.Timer < 1f)
                    _currentTitle.Alpha = MathHelper.Lerp(_currentTitle.Alpha, 0f, dt * 5f);

                if (_currentTitle.Timer <= 0) _currentTitle = null;
            }
        }

        public void Draw(SpriteBatch sb)
        {
            // 1. Draw Messages
            foreach (var msg in _messages)
            {
                DrawRichText(sb, _font, msg.Text, msg.Position, Color.White * msg.Alpha, msg.Scale);
            }

            // 2. Draw Emotes
            if (_emoteTexture != null)
            {
                foreach (var emote in _emotes)
                {
                    sb.Draw(_emoteTexture, emote.Position, emote.SourceRect, Color.White * emote.Alpha, 0f,
                        new Vector2(EMOTE_SIZE / 2), emote.Scale, SpriteEffects.None, 0f);
                }
            }

            // 3. Draw Title Drop
            if (_currentTitle != null && _font != null)
            {
                float titleScale = 2.5f;
                float subScale = 1.0f;
                Vector2 titleSize = _font.MeasureString(_currentTitle.MainText) * titleScale;
                Vector2 subSize = string.IsNullOrEmpty(_currentTitle.SubText) ? Vector2.Zero : _font.MeasureString(_currentTitle.SubText) * subScale;

                Vector2 titlePos = Vector2.Zero;
                Vector2 subPos = Vector2.Zero;

                if (_currentTitle.Mode == TitleMode.TopCenter)
                {
                    titlePos = new Vector2((_screenSize.X - titleSize.X) / 2, 100);
                    subPos = new Vector2((_screenSize.X - subSize.X) / 2, titlePos.Y + titleSize.Y);
                }
                else if (_currentTitle.Mode == TitleMode.TopRight)
                {
                    titlePos = new Vector2(_screenSize.X - titleSize.X - 50, 100);
                    subPos = new Vector2(_screenSize.X - subSize.X - 50, titlePos.Y + titleSize.Y);
                }

                // Draw Text with Drop Shadow for readability
                sb.DrawString(_font, _currentTitle.MainText, titlePos + new Vector2(2, 2), Color.Black * (_currentTitle.Alpha * 0.5f), 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
                sb.DrawString(_font, _currentTitle.MainText, titlePos, Color.White * _currentTitle.Alpha, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

                if (!string.IsNullOrEmpty(_currentTitle.SubText))
                {
                    sb.DrawString(_font, _currentTitle.SubText, subPos + new Vector2(1, 1), Color.Black * (_currentTitle.Alpha * 0.5f), 0f, Vector2.Zero, subScale, SpriteEffects.None, 0f);
                    sb.DrawString(_font, _currentTitle.SubText, subPos, Color.LightGray * _currentTitle.Alpha, 0f, Vector2.Zero, subScale, SpriteEffects.None, 0f);
                }
            }
        }

        // --- API METHODS ---

        public void AddMessage(string text, float duration = 5f, float scale = 1.0f)
        {
            _messages.Insert(0, new HudMessage // Insert at 0 pushes older messages up
            {
                Text = text,
                Timer = duration,
                MaxTimer = duration,
                Position = new Vector2(50, _screenSize.Y + 50), // Start slightly offscreen bottom
                Scale = scale
            });
        }

        public void SpawnEmote(int column, int row, float scale = 2.0f)
        {
            _emotes.Add(new HudEmote
            {
                SourceRect = new Rectangle(column * EMOTE_SIZE, row * EMOTE_SIZE, EMOTE_SIZE, EMOTE_SIZE),
                Position = new Vector2(_screenSize.X - 100 + (_random.NextSingle() * 40 - 20), _screenSize.Y - 100),
                Timer = 4f,
                MaxTimer = 4f,
                WobbleOffset = _random.NextSingle() * MathHelper.TwoPi, // Randomize wobble start
                Scale = scale
            });
        }

        public void ShowTitleDrop(string mainText, string subText, TitleMode mode, float duration = 4f)
        {
            _currentTitle = new AreaTitle
            {
                MainText = mainText,
                SubText = subText,
                Mode = mode,
                Timer = duration,
                MaxTimer = duration
            };
        }

        // --- RICH TEXT PARSER ---
        // Parses strings like "You found a <c:Yellow>Gold Coin</c>!"
        private void DrawRichText(SpriteBatch sb, SpriteFont font, string text, Vector2 position, Color baseColor, float scale)
        {
            string[] parts = text.Split('<', '>');
            Vector2 currentPos = position;
            Color currentColor = baseColor;

            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) // It's a tag
                {
                    string tag = parts[i];
                    if (tag == "c" || tag == "/c") currentColor = baseColor; // Reset
                    else if (tag.StartsWith("c:"))
                    {
                        string colorName = tag.Substring(2);
                        if (_colorTags.TryGetValue(colorName, out Color newColor))
                        {
                            currentColor = newColor * (baseColor.A / 255f); // Maintain alpha
                        }
                    }
                }
                else // It's standard text
                {
                    if (string.IsNullOrEmpty(parts[i])) continue;

                    // Draw shadow
                    sb.DrawString(font, parts[i], currentPos + new Vector2(2, 2) * scale, Color.Black * (baseColor.A / 255f) * 0.7f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    // Draw text
                    sb.DrawString(font, parts[i], currentPos, currentColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                    currentPos.X += font.MeasureString(parts[i]).X * scale;
                }
            }
        }

        public void DrawInventory(SpriteBatch sb, GameState state)
        {
            var inv = state.Player.Inventory;
            // Scale everything up by 2x!
            int scaleFactor = 2;
            int slotSize = 48 * scaleFactor;
            int spacing = 4 * scaleFactor;

            int startX = (int)(_screenSize.X - (10 * slotSize + 9 * spacing)) / 2;
            int hotbarY = (int)(_screenSize.Y - slotSize - 20);

            var ms = Microsoft.Xna.Framework.Input.Mouse.GetState();
            Point mousePos = ms.Position;
            int hoveredIndex = -1; // Track which slot the mouse is over

            if (state.IsInventoryOpen)
            {
                sb.Draw(_pixel, new Rectangle(0, 0, (int)_screenSize.X, (int)_screenSize.Y), Color.Black * 0.4f);

                int fullInvY = (int)(_screenSize.Y - (3 * slotSize + 2 * spacing)) / 2;

                string weightText = $"Carry Weight: {inv.CurrentWeight:F1} / {inv.MaxWeight:F1} kg";
                sb.DrawString(_font, weightText, new Vector2(startX, fullInvY - 45), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);

                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 10; col++)
                    {
                        int slotIndex = row * 10 + col;
                        int x = startX + col * (slotSize + spacing);
                        int y = fullInvY + row * (slotSize + spacing);

                        if (new Rectangle(x, y, slotSize, slotSize).Contains(mousePos)) hoveredIndex = slotIndex;

                        DrawSlot(sb, inv, slotIndex, x, y, slotSize, state);
                    }
                }

                if (inv.HeldItem != null)
                {
                    DrawItemIcon(sb, inv.HeldItem, ms.X - slotSize / 2, ms.Y - slotSize / 2, slotSize, state);
                }
            }
            else if (!state.DebugPool[GameBool.IsPaused])
            {
                for (int col = 0; col < 10; col++)
                {
                    int x = startX + col * (slotSize + spacing);

                    if (new Rectangle(x, hotbarY, slotSize, slotSize).Contains(mousePos)) hoveredIndex = col;

                    DrawSlot(sb, inv, col, x, hotbarY, slotSize, state);
                }
            }

            // Draw Hover Tooltip if applicable and we aren't holding an item
            if (hoveredIndex != -1 && inv.Slots[hoveredIndex] != null && inv.HeldItem == null)
            {
                DrawTooltip(sb, inv.Slots[hoveredIndex], state, mousePos);
            }
        }
        private void DrawSlot(SpriteBatch sb, Inventory inv, int index, int x, int y, int size, GameState state)
        {
            Rectangle rect = new Rectangle(x, y, size, size);
            bool isSelected = index == inv.SelectedSlot;

            sb.Draw(_pixel, rect, inv.SlotColor);

            Color bColor = isSelected ? inv.HighlightColor : inv.BorderColor;
            int thick = (int)(isSelected ? inv.BorderThickness * 2f : inv.BorderThickness * 1.5f);

            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thick), bColor);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thick, rect.Width, thick), bColor);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, thick, rect.Height), bColor);
            sb.Draw(_pixel, new Rectangle(rect.Right - thick, rect.Y, thick, rect.Height), bColor);

            var stack = inv.Slots[index];
            if (stack != null && stack.Count > 0)
            {
                DrawItemIcon(sb, stack, x, y, size, state);
            }

            if (index < 10)
            {
                string num = index == 9 ? "0" : (index + 1).ToString();
                // Scaled up the hotkey number
                sb.DrawString(_font, num, new Vector2(x + 6, y + 6), Color.Gray, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
            }
        }

        private void DrawItemIcon(SpriteBatch sb, ItemStack stack, int x, int y, int slotSize, GameState state)
        {
            var def = state.ItemManager.GetItem(stack.ItemID);
            if (def != null)
            {
                var tex = state.Assets.GetAtlas(def.IconSource);
                if (tex != null)
                {
                    Rectangle srcRect = new Rectangle(def.Coord.X * 32, def.Coord.Y * 32, 32, 32);
                    float scale = (slotSize - 16f) / 32f; // Scale it perfectly inside the new 96px slot
                    Vector2 pos = new Vector2(x + 8, y + 8);

                    sb.Draw(tex, pos, srcRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    string fallback = def.Name.Substring(0, Math.Min(3, def.Name.Length)).ToUpper();
                    sb.DrawString(_font, fallback, new Vector2(x + 12, y + 12), Color.LightGray, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                }

                string countText = stack.Count.ToString();
                Vector2 cs = _font.MeasureString(countText) * 1.2f; // Scaled up text
                sb.DrawString(_font, countText, new Vector2(x + slotSize - cs.X - 6, y + slotSize - cs.Y - 4), Color.White, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
            }
        }

        private void DrawTooltip(SpriteBatch sb, ItemStack stack, GameState state, Point mousePos)
        {
            var def = state.ItemManager.GetItem(stack.ItemID);
            if (def == null) return;

            int width = 320;
            int height = 180;
            int padding = 15;

            // Position tooltip to the bottom-right of the mouse, but keep it on screen
            Vector2 pos = new Vector2(mousePos.X + 20, mousePos.Y + 20);
            if (pos.X + width > _screenSize.X) pos.X = mousePos.X - width - 10;
            if (pos.Y + height > _screenSize.Y) pos.Y = mousePos.Y - height - 10;

            // Draw Background
            Rectangle bgRect = new Rectangle((int)pos.X, (int)pos.Y, width, height);
            sb.Draw(_pixel, bgRect, Color.Black * 0.9f);
            sb.Draw(_pixel, new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, 2), Color.Gray);
            sb.Draw(_pixel, new Rectangle(bgRect.X, bgRect.Bottom - 2, bgRect.Width, 2), Color.Gray);
            sb.Draw(_pixel, new Rectangle(bgRect.X, bgRect.Y, 2, bgRect.Height), Color.Gray);
            sb.Draw(_pixel, new Rectangle(bgRect.Right - 2, bgRect.Y, 2, bgRect.Height), Color.Gray);

            float currentY = pos.Y + padding;

            // Item Name and Category
            sb.DrawString(_font, def.Name, new Vector2(pos.X + padding, currentY), Color.Yellow, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
            currentY += 25;
            sb.DrawString(_font, def.Category, new Vector2(pos.X + padding, currentY), Color.DarkGray, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            currentY += 25;

            // Weight and Count
            sb.DrawString(_font, $"Count: {stack.Count}", new Vector2(pos.X + padding, currentY), Color.White);
            sb.DrawString(_font, $"Total Weight: {stack.TotalWeight:F2} kg", new Vector2(pos.X + 150, currentY), Color.Cyan);
            currentY += 30;

            // Description (Word Wrapped)
            string wrappedDesc = WrapText(_font, def.Description, width - (padding * 2));
            sb.DrawString(_font, wrappedDesc, new Vector2(pos.X + padding, currentY), Color.LightGray);

            // Calculate space taken by description to start drawing tags
            currentY += _font.MeasureString(wrappedDesc).Y + 20;

            // Draw Tags using UI Pills
            float tagX = pos.X + padding;
            foreach (var tag in def.ItemTags)
            {
                Vector2 tSize = _font.MeasureString(tag);
                Rectangle pillRect = new Rectangle((int)tagX, (int)currentY, (int)tSize.X + 16, 24);

                // Wrap tags to next line if they exceed tooltip width
                if (pillRect.Right > pos.X + width - padding)
                {
                    tagX = pos.X + padding;
                    currentY += 30;
                    pillRect.X = (int)tagX;
                    pillRect.Y = (int)currentY;
                }

                UIDrawExtensions.DrawPill(sb, _font, pillRect, tag, Color.DarkSlateGray, Color.White);
                tagX += pillRect.Width + 8;
            }
        }

        // Simple text wrapper to ensure description fits in tooltip
        private string WrapText(SpriteFont font, string text, float maxLineWidth)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string[] words = text.Split(' ');
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            float lineWidth = 0f;
            float spaceWidth = font.MeasureString(" ").X;

            foreach (string word in words)
            {
                Vector2 size = font.MeasureString(word);
                if (lineWidth + size.X < maxLineWidth)
                {
                    sb.Append(word + " ");
                    lineWidth += size.X + spaceWidth;
                }
                else
                {
                    sb.Append("\n" + word + " ");
                    lineWidth = size.X + spaceWidth;
                }
            }
            return sb.ToString();
        }
    }
}