using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using Pixel_Simulations.UI;
using System;

namespace Pixel_Simulations.Studio
{
    public class UISpriteCanvas : UIElement
    {
        private readonly StudioState _state;
        private Vector2 _panOffset = Vector2.Zero;
        private float _zoom = 2.0f;

        private Rectangle _hoveredGridCell;
        private readonly Color _gridColor = Color.White * 0.1f;

        public UISpriteCanvas(StudioState state)
        {
            _state = state;
            ClipToBounds = true;
        }

        private Matrix GetTransform() => Matrix.CreateTranslation(new Vector3(_panOffset, 0)) * Matrix.CreateScale(_zoom) * Matrix.CreateTranslation(new Vector3(AbsoluteBounds.Location.ToVector2(), 0));

        public override bool Update(EditorInputState input, EventBus bus = null)
        {
            if (!IsVisible || !AbsoluteBounds.Contains(input.MouseWindowPosition)) return false;

            var character = _state.DataManager.CurrentCharacter;
            if (character == null) return true;

            // --- ZOOMING (Integer Scaling, Centered on Mouse) ---
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float zoomDelta = scrollDelta > 0 ? 1.0f : -1.0f;
                float oldZoom = _zoom;
                _zoom = MathHelper.Clamp((float)Math.Round(_zoom + zoomDelta), 1.0f, 10.0f);

                Vector2 mouseBefore = Vector2.Transform(input.MouseWindowPosition, Matrix.Invert(Matrix.CreateTranslation(new Vector3(_panOffset, 0)) * Matrix.CreateScale(oldZoom) * Matrix.CreateTranslation(new Vector3(AbsoluteBounds.Location.ToVector2(), 0))));
                Vector2 mouseAfter = Vector2.Transform(input.MouseWindowPosition, Matrix.Invert(GetTransform()));
                _panOffset += (mouseAfter - mouseBefore) * _zoom;
                return true;
            }

            // --- PANNING (Middle Mouse OR Spacebar + Left Drag) ---
            if (input.CurrentMouse.MiddleButton == ButtonState.Pressed || (input.CurrentKeyboard.IsKeyDown(Keys.Space) && input.LeftHold))
            {
                _panOffset += (input.CurrentMouse.Position.ToVector2() - input.PreviousMouse.Position.ToVector2());
                return true;
            }

            // --- KEYBOARD PANNING (Arrow Keys) ---
            if (!(_state.UI.FocusedElement is UITextBox))
            {
                float speed = input.CurrentKeyboard.IsKeyDown(Keys.LeftShift) ? 20f : 5f;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Up)) _panOffset.Y += speed;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Down)) _panOffset.Y -= speed;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Left)) _panOffset.X += speed;
                if (input.CurrentKeyboard.IsKeyDown(Keys.Right)) _panOffset.X -= speed;
            }

            // Block drawing tools if holding spacebar
            if (input.CurrentKeyboard.IsKeyDown(Keys.Space)) return true;

            // --- GRID SELECTION ---
            Vector2 mouseLocal = Vector2.Transform(input.MouseWindowPosition, Matrix.Invert(GetTransform()));
            int gx = character.FrameSize.X; int gy = character.FrameSize.Y;

            _hoveredGridCell = new Rectangle((int)Math.Floor(mouseLocal.X / gx) * gx, (int)Math.Floor(mouseLocal.Y / gy) * gy, gx, gy);

            if (input.IsNewLeftClick && !string.IsNullOrEmpty(_state.SelectedNodeName) && !string.IsNullOrEmpty(_state.AssigningBodyPart))
            {
                string clipName = $"{_state.SelectedNodeName}_{_state.ActiveDirection}";
                if (!character.Clips.ContainsKey(clipName)) character.Clips[clipName] = new AnimationClip { Name = clipName };

                var clip = character.Clips[clipName];
                if (clip.Frames.Count == 0) clip.Frames.Add(new AnimFrame());

                int frameIdx = MathHelper.Clamp(_state.CurrentFrameIndex, 0, clip.Frames.Count - 1);
                clip.Frames[frameIdx].Parts[_state.AssigningBodyPart] = _hoveredGridCell;
            }

            return true;
        }
        public override void Draw(SpriteBatch sb, IUIContext context, UITheme theme)
        {
            if (!IsVisible) return;
            var character = _state.DataManager.CurrentCharacter;

            Rectangle prevScissor = sb.GraphicsDevice.ScissorRectangle;
            sb.End();
            sb.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true }, samplerState: SamplerState.PointClamp, transformMatrix: GetTransform());
            sb.GraphicsDevice.ScissorRectangle = AbsoluteBounds;

            sb.FillRectangle(new RectangleF(-10000, -10000, 20000, 20000), new Color(15, 15, 15));

            Texture2D atlas = string.IsNullOrEmpty(character?.AtlasName) ? null : _state.AssetLibrary?.GetAtlas(character.AtlasName);
            if (atlas != null)
            {
                sb.Draw(atlas, Vector2.Zero, Color.White);

                int gx = character.FrameSize.X; int gy = character.FrameSize.Y;
                for (float x = 0; x <= atlas.Width; x += gx) sb.DrawLine(x, 0, x, atlas.Height, Color.White * 0.1f, 1f / _zoom);
                for (float y = 0; y <= atlas.Height; y += gy) sb.DrawLine(0, y, atlas.Width, y, Color.White * 0.1f, 1f / _zoom);

                // Draw saved frames for CURRENT FRAME ONLY
                string activeClipName = $"{_state.SelectedNodeName}_{_state.ActiveDirection}";
                if (character.Clips.TryGetValue(activeClipName, out var clip) && clip.Frames.Count > _state.CurrentFrameIndex)
                {
                    foreach (var partKvp in clip.Frames[_state.CurrentFrameIndex].Parts)
                    {
                        bool isAssigning = partKvp.Key == _state.AssigningBodyPart;
                        Color boxColor = isAssigning ? Color.Goldenrod : Color.Cyan;

                        sb.FillRectangle(partKvp.Value, boxColor * 0.2f);
                        sb.DrawRectangle(partKvp.Value, boxColor, 2f / _zoom);

                        if (theme.Font != null) sb.DrawString(theme.Font, partKvp.Key, new Vector2(partKvp.Value.X + 2, partKvp.Value.Y + 2), boxColor, 0f, Vector2.Zero, 1f / _zoom, SpriteEffects.None, 0f);
                    }
                }

                if (!string.IsNullOrEmpty(_state.AssigningBodyPart))
                {
                    sb.FillRectangle(_hoveredGridCell, Color.Yellow * 0.3f);
                    sb.DrawRectangle(_hoveredGridCell, Color.Yellow, 2f / _zoom);
                }
            }

            sb.End();
            sb.GraphicsDevice.ScissorRectangle = prevScissor;
            sb.Begin();
        }
    }
}