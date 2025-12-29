using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.Editor;
using System;

namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public class Camera
    {
        public Vector2 Position { get; private set; }

        // Matrix for drawing to Low-Res Targets (Albedo/Ground)
        // Coordinates: 0 to 480
        public Matrix SimulationMatrix { get; private set; }

        // Matrix for drawing to High-Res Targets (Dynamic, Depth, Normal)
        // Coordinates: 0 to 1920 (Scaled up)
        public Matrix RenderMatrix { get; private set; }
        private Vector3 lowResCenter, highResCenter;

        public void Setcamera(Rectangle nativeRect, Rectangle highResRect)
        {
            // 1. Calculate Center Offsets
             lowResCenter = new Vector3(nativeRect.Width / 2f, nativeRect.Height / 2f, 0);
             highResCenter = new Vector3(highResRect.Width / 2f, highResRect.Height / 2f, 0);

            
        }

        public void Follow(Player player, float scale)
        {
            Position = player.Position;

            // 2. Base View (Move world so player is at 0,0)
            // We round the position to prevent sub-pixel jitter in the low-res art
            var view = Matrix.CreateTranslation(
                -MathF.Round(Position.X),
                -MathF.Round(Position.Y),
                0);

            // 3. Create Simulation Matrix (Low Res)
            // Just centering the view in the small window
            SimulationMatrix = view * Matrix.CreateTranslation(lowResCenter);

            // 4. Create Render Matrix (High Res)
            // Move World -> Scale Up -> Center in Big Window
            RenderMatrix = view * Matrix.CreateScale(scale) * Matrix.CreateTranslation(highResCenter);

        }
    }

    public class EditorCamera
    {
        public Vector2 Position { get; private set; }
        public float Zoom { get; private set; }
        public Matrix Transform { get; private set; }

        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 4.0f;
        private const float STEP_ZOOM = 0.5f;

        public EditorCamera()
        {
            Position = Vector2.Zero;
            Zoom = 1.0f;
            UpdateTransform();
        }

        public void Update(InputState input, Rectangle viewportBounds)
        {
            // --- Panning ---
            if (input.CurrentMouse.MiddleButton == ButtonState.Pressed && viewportBounds.Contains(input.MouseWindowPosition))
            {
                Vector2 delta = input.CurrentMouse.Position.ToVector2() - input.PreviousMouse.Position.ToVector2();
                Position -= delta / Zoom;
            }

            // --- STEPPED ZOOMING ---
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            if (scrollDelta != 0 && viewportBounds.Contains(input.MouseWindowPosition))
            {
                Vector2 mouseRelative = input.MouseWindowPosition - viewportBounds.Location.ToVector2();
                Vector2 mouseWorldPosBeforeZoom = ScreenToWorld(mouseRelative);

                // Calculate the new zoom level
                float newZoom = Zoom;
                if (scrollDelta > 0)
                {
                    // Zooming In
                    if (Zoom < 1.0f) newZoom += STEP_ZOOM;
                    else if(Zoom == 1) { newZoom += 1.0f; }
                    else newZoom += 2.0f; // Jump by whole numbers at 1x zoom and above
                }
                else
                {
                    // Zooming Out
                    if (Zoom <= 1.0f) newZoom -= STEP_ZOOM;
                    else if (Zoom == 2) { newZoom -= 1.0f; }
                    else newZoom -= 2.0f;
                }

                // Clamp to predefined min/max
                Zoom = MathHelper.Clamp(newZoom, MIN_ZOOM, MAX_ZOOM);

                UpdateTransform();

                Vector2 mouseWorldPosAfterZoom = ScreenToWorld(mouseRelative);
                Position += mouseWorldPosBeforeZoom - mouseWorldPosAfterZoom;
            }

            UpdateTransform();
        }

        public void Update(InputState input, Rectangle viewportBounds,bool x)
        {
            // --- Panning ---
            if (input.CurrentMouse.MiddleButton == ButtonState.Pressed && viewportBounds.Contains(input.MouseWindowPosition))
            {
                // Panning logic is correct, but we need to account for the upscale factor.
                // The delta is in screen pixels, but the camera moves in world pixels.
                Vector2 delta = input.CurrentMouse.Position.ToVector2() - input.PreviousMouse.Position.ToVector2();

                // Let's assume a hardcoded scale factor of 2 for the editor viewport for now.
                // A better solution would be to pass this from the LayoutManager.
                float upscaleFactor = 2.0f;

                Position -= delta / (Zoom * upscaleFactor);
            }

            // --- STEPPED ZOOMING ---
            int scrollDelta = input.CurrentMouse.ScrollWheelValue - input.PreviousMouse.ScrollWheelValue;
            if (scrollDelta != 0 && viewportBounds.Contains(input.MouseWindowPosition))
            {
                // *** THE FIX IS HERE ***
                // We need the mouse position relative to the NATIVE (low-res) canvas, not the final window.
                Vector2 mouseInViewport = input.MouseWindowPosition - viewportBounds.Location.ToVector2();
                Vector2 mouseNative = mouseInViewport / 2f; // Divide by the upscale factor

                Vector2 mouseWorldPosBeforeZoom = ScreenToWorld(mouseNative);

                float newZoom = Zoom;
                if (scrollDelta > 0)
                {
                    // Zooming In
                    if (Zoom < 1.0f) newZoom += STEP_ZOOM;
                    else if (Zoom == 1) { newZoom += 1.0f; }
                    else newZoom += 2.0f; // Jump by whole numbers at 1x zoom and above
                }
                else
                {
                    // Zooming Out
                    if (Zoom <= 1.0f) newZoom -= STEP_ZOOM;
                    else if (Zoom == 2) { newZoom -= 1.0f; }
                    else newZoom -= 2.0f;
                }

                Zoom = MathHelper.Clamp(newZoom, MIN_ZOOM, MAX_ZOOM);

                UpdateTransform();

                Vector2 mouseWorldPosAfterZoom = ScreenToWorld(mouseNative);

                Position += mouseWorldPosBeforeZoom - mouseWorldPosAfterZoom;
            }

            UpdateTransform();
        }

        private void UpdateTransform()
        {
            Transform = Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
                        Matrix.CreateScale(Zoom, Zoom, 1);
        }

        // --- Coordinate Conversion Methods ---
        /// Converts a screen position (relative to the viewport) to a world position.
        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return Vector2.Transform(screenPosition, Matrix.Invert(Transform));
        }
        /// Gets the visible area of the world in world coordinates.
        public RectangleF GetVisibleWorldBounds(Rectangle viewportBounds)
        {
            Vector2 worldTopLeft = ScreenToWorld(Vector2.Zero);
            Vector2 worldBottomRight = ScreenToWorld(viewportBounds.Size.ToVector2());

            return new RectangleF(worldTopLeft, worldBottomRight - worldTopLeft);
        }
    }
}


