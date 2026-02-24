using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Timers;
using Pixel_Simulations.Editor;
using System;
using System.Linq;

namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public class Camera
    {
        // --- State ---
        public Vector2 Position { get; private set; }
        public float Zoom { get; private set; } = 1f;



        // --- Settings ---
        private readonly float[] _zoomLevels = { 0.125f, 0.25f, 0.5f, 1f, 2f, 4f };
        private float _moveLerpSpeed = 5.0f; // Higher is faster
        private float _zoomLerpSpeed = 3.0f;

        // --- Matrices ---
        public Matrix NativeFinal { get; private set; }
        public Matrix NativeView { get; private set; }
        public Matrix SimFinal { get; private set; }
        public Matrix SimView { get; private set; }

        private Rectangle _nativeRect;
        private Rectangle _simRect;
        public Rectangle CameraView { get; private set; }
        public void Setcamera(Rectangle nativeRect, Rectangle highResRect)
        {
            _nativeRect = nativeRect;
            _simRect = highResRect;
        }
        /// <summary>
        /// Call this every frame to process smoothing.
        /// </summary>
        public void Update(GameTime gameTime, Vector2 targetPos, float targetZoom, bool smooth = false)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (smooth)
            {
                // 1. Smoothly interpolate position
                Position = Vector2.Lerp(Position, targetPos, _moveLerpSpeed * dt);

                // 2. Smoothly interpolate zoom
                Zoom = MathHelper.Lerp(Zoom, targetZoom, _zoomLerpSpeed * dt);
            }
            else
            {
                Position = targetPos;
                Zoom = targetZoom;
            }

            UpdateMatrices();
        }
        public float GetNearestZoomStep(float current, bool increase)
        {
            if (increase)
                return _zoomLevels.FirstOrDefault(z => z > current + 0.01f, _zoomLevels.Last());
            else
                return _zoomLevels.LastOrDefault(z => z < current - 0.01f, _zoomLevels.First());
        }

        private void UpdateMatrices()
        {

            // --- THE BASE VIEW ---
            Matrix translation = Matrix.CreateTranslation(
                 -System.MathF.Round(Position.X),
                 -System.MathF.Round(Position.Y),
                 0);

            // --- A. NATIVE MATRICES (480x270) ---
            Matrix nativeScale = Matrix.CreateScale(Zoom, Zoom, 1);
            Matrix nativeCenter = Matrix.CreateTranslation(_nativeRect.Width / 2f, _nativeRect.Height / 2f, 0);

            NativeFinal = translation * nativeScale * nativeCenter;
            //NativeFinal = NativeView * Matrix.CreateOrthographicOffCenter(0, _nativeRect.Width, _nativeRect.Height, 0, 0, 1);

            // --- B. SIMULATION MATRICES (960x540) ---
            float simScaleFactor = (float)_simRect.Width / _nativeRect.Width; // Usually 2.0x
            Matrix simScale = Matrix.CreateScale(Zoom * simScaleFactor, Zoom * simScaleFactor, 1);
            Matrix simCenter = Matrix.CreateTranslation(_simRect.Width / 2f, _simRect.Height / 2f, 0);

            SimFinal = translation * simScale * simCenter;
            //SimFinal = SimView * Matrix.CreateOrthographicOffCenter(0, _simRect.Width, _simRect.Height, 0, 0, 1);

            // Update visible bounds for culling
            float invZoom = 1f / Zoom;
            CameraView = new Rectangle(
                (int)(Position.X - (_nativeRect.Width * invZoom) / 2f),
                (int)(Position.Y - (_nativeRect.Height * invZoom) / 2f),
                (int)(_nativeRect.Width * invZoom),
                (int)(_nativeRect.Height * invZoom)
            );
        }

        public void Follow(Vector2 target, float zoom, GameTime gt) => Update(gt, target, zoom);
        public void SnapTo(Vector2 target, float zoom)
        {
            Position = target;
            Zoom = zoom;
            UpdateMatrices();
        }
        public void Follow(Vector2 target, float scale)
        {
            Position = target;
            Zoom = scale;

            // --- THE BASE VIEW ---
            // Move world so target is at 0,0. (Rounded for pixel art)
            Matrix view = Matrix.CreateTranslation(-MathF.Round(Position.X), -MathF.Round(Position.Y), 0);

            // --- A. NATIVE MATRICES (480x270) ---
            // Background is drawn at 1x scale relative to world coords
            Matrix nativeCenter = Matrix.CreateTranslation(_nativeRect.Width / 2f, _nativeRect.Height / 2f, 0);
            NativeView = view * nativeCenter;
            NativeFinal = NativeView * Matrix.CreateOrthographicOffCenter(0, _nativeRect.Width, _nativeRect.Height, 0, 0, 1);

            // --- B. SIMULATION MATRICES (960x540) ---
            // Grass/Player are drawn at 2x scale (SimScale) relative to 480p coords
            // NOTE: We do NOT use 'scale' (User Zoom) here yet, as Simulation is fixed at 960p.
            float simScaleFactor = 2.0f;
            Matrix simZoom = Matrix.CreateScale(simScaleFactor);
            Matrix simCenter = Matrix.CreateTranslation(_simRect.Width / 2f, _simRect.Height / 2f, 0);

            SimView = view * simZoom * simCenter;
            SimFinal = SimView * Matrix.CreateOrthographicOffCenter(0, _simRect.Width, _simRect.Height, 0, 0, 1);

            CameraView = new Rectangle(
                (int)(Position.X - _nativeRect.Width / 2f),
                (int)(Position.Y - _nativeRect.Height / 2f),
                _nativeRect.Width,
                _nativeRect.Height
            );
        }

        // In MyGame/Camera.cs

        public void Follow(NewPlayer target, float userZoom = 1f)
        {
            Position = target.Position;
            Zoom = userZoom;

            // 1. Center the view on the target. We round to prevent sub-pixel jitter.
            Matrix translation = Matrix.CreateTranslation(
                -System.MathF.Round(Position.X),
                -System.MathF.Round(Position.Y),
                0);

            // --- A. NATIVE MATRICES (for 480x270 RenderTarget) ---
            // The scale is just the user's zoom (default 1x).
            Matrix nativeScale = Matrix.CreateScale(Zoom, Zoom, 1);
            // Center the origin within the 480x270 canvas.
            Matrix nativeCenter = Matrix.CreateTranslation(_nativeRect.Width / 2f, _nativeRect.Height / 2f, 0);

            NativeFinal = translation * nativeScale * nativeCenter;


            // --- B. SIMULATION MATRICES (for 960x540 RenderTarget) ---
            // The scale must be multiplied by the factor between Native and Sim (960/480 = 2).
            float simScaleFactor = (float)_simRect.Width / _nativeRect.Width; // Should be 2.0f
            Matrix simScale = Matrix.CreateScale(Zoom * simScaleFactor, Zoom * simScaleFactor, 1);
            // Center the origin within the 960x540 canvas.
            Matrix simCenter = Matrix.CreateTranslation(_simRect.Width / 2f, _simRect.Height / 2f, 0);

            SimFinal = translation * simScale * simCenter;


            // Culling view calculation (based on native resolution for simplicity)
            CameraView = new Rectangle(
                (int)(Position.X - _nativeRect.Width / (2f * Zoom)),
                (int)(Position.Y - _nativeRect.Height / (2f * Zoom)),
                (int)(_nativeRect.Width / Zoom),
                (int)(_nativeRect.Height / Zoom)
            );
        }

        public void Follow(Player player, float scale) => Follow(player._position, scale);

        public void Follow(TestPlayer player, float scale) => Follow(player.Position, scale);

        //public void Follow(NewPlayer player, float scale) => Follow(player.Position, scale);
    }

    public class EditorCamera
    {
        public Vector2 Position { get; set; }
        public float Zoom { get; set; }
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

        public void Pan(Vector2 delta)
        {
            Position += delta;
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


