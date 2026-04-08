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

        public void LoadContent(SpriteFont font, Texture2D emoteTexture)
        {
            _font = font;
            _emoteTexture = emoteTexture;
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
    }
}