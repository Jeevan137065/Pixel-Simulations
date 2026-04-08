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
        // --- Current State ---
        public Vector2 Position { get; private set; }
        public float Zoom { get; private set; } = 1f;

        // --- Multi-Target Logic ---
        private float _targetZoom = 1f;
        private Vector2 _primaryTarget;    // Usually Player
        private Vector2 _secondaryTarget;  // Object or Mouse
        private float _focusStrength = 0f; // 0 = 100% Player, 1 = 100% Object

        // --- Settings ---
        //private readonly float[] _zoomLevels = { 0.25f, 0.5f, 1f, 2f, 4f };
        private readonly float[] _zoomLevels = {0.5f, 1f, 2f};
        public float MoveLerpSpeed { get; set; } = 15.0f;
        public float ZoomLerpSpeed { get; set; } = 5.0f;

        // --- Matrices ---
        public Matrix NativeTransform { get; private set; } // 480x270
        public Matrix SimTransform { get; private set; }    // 960x540
        public Matrix ScreenTransform { get; private set; } // 1920x1080 (Final Output)

        // --- Matrices for HLSL Custom Shaders (WVP) ---
        public Matrix NativeWVP { get; private set; }
        public Matrix SimWVP { get; private set; }
        public Matrix ScreenWVP { get; private set; }

        private Rectangle _nativeRect;
        private Rectangle _simRect;
        private Rectangle _screenRect; // NEW!

        public void Setcamera(Rectangle nativeRect, Rectangle simRect, Rectangle finalRect)
        {
            _nativeRect = nativeRect;
            _simRect = simRect;
            _screenRect = finalRect;
        }
        public void SetFocusPoint(Vector2 point, float strength)
        {
            _secondaryTarget = point;
            _focusStrength = MathHelper.Clamp(strength, 0, 1);
        }
        public void Update(GameTime gameTime, Vector2 playerPos)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _primaryTarget = playerPos;

            // 1. Calculate the blended target position
            // This pulls the camera between the player and the focus point
            Vector2 finalTarget = Vector2.Lerp(_primaryTarget, _secondaryTarget, _focusStrength);

            // 2. Smoothly lerp actual camera position and zoom
            Position = Vector2.Lerp(Position, finalTarget, MoveLerpSpeed * dt);
            Zoom = MathHelper.Lerp(Zoom, _targetZoom, ZoomLerpSpeed * dt);
            // 4. Snap logic: If we are extremely close to the target, just set it.
            // This prevents floating point "creeping" (0.9999999)
            if (Math.Abs(Zoom - _targetZoom) < 0.001f) Zoom = _targetZoom;
            if (Vector2.Distance(Position, finalTarget) < 0.1f) Position = finalTarget;
            UpdateMatrices();
        }
        private void UpdateMatrices()
        {
            // Round to prevent sub-pixel jitter
            Vector2 roundedPos = new Vector2(
                (float)System.Math.Round(Position.X),
                (float)System.Math.Round(Position.Y)
            );

            // Base World Translation
            Matrix translation = Matrix.CreateTranslation(-roundedPos.X, -roundedPos.Y, 0);

            // --- 1. NATIVE MATRICES (480x270) ---
            Matrix nativeScale = Matrix.CreateScale(Zoom, Zoom, 1);
            Matrix nativeCenter = Matrix.CreateTranslation(_nativeRect.Width / 2f, _nativeRect.Height / 2f, 0);
            NativeTransform = translation * nativeScale * nativeCenter;

            Matrix nativeProjection = Matrix.CreateOrthographicOffCenter(0, _nativeRect.Width, _nativeRect.Height, 0, -100, 100);
            NativeWVP = NativeTransform * nativeProjection;

            // --- 2. SIMULATION MATRICES (960x540) ---
            float simFactor = (float)_simRect.Width / _nativeRect.Width;
            Matrix simScale = Matrix.CreateScale(Zoom * simFactor, Zoom * simFactor, 1);
            Matrix simCenter = Matrix.CreateTranslation(_simRect.Width / 2f, _simRect.Height / 2f, 0);
            SimTransform = translation * simScale * simCenter;

            Matrix simProjection = Matrix.CreateOrthographicOffCenter(0, _simRect.Width, _simRect.Height, 0, -100, 100);
            SimWVP = SimTransform * simProjection;

            // --- 3. SCREEN MATRICES (1920x1080 Final Output) ---
            float screenFactor = (float)_screenRect.Width / _nativeRect.Width;
            Matrix screenScale = Matrix.CreateScale(Zoom * screenFactor, Zoom * screenFactor, 1);
            Matrix screenCenter = Matrix.CreateTranslation(_screenRect.Width / 2f, _screenRect.Height / 2f, 0);
            ScreenTransform = translation * screenScale * screenCenter;

            Matrix screenProjection = Matrix.CreateOrthographicOffCenter(0, _screenRect.Width, _screenRect.Height, 0, -100, 100);
            ScreenWVP = ScreenTransform * screenProjection;

        }
        public Rectangle NativeViewBounds => new Rectangle(
    (int)(Position.X - (_nativeRect.Width / (2f * Zoom))),
    (int)(Position.Y - (_nativeRect.Height / (2f * Zoom))),
    (int)(_nativeRect.Width / Zoom),
    (int)(_nativeRect.Height / Zoom)
);

        // We use this for all Physics, Parallax, and Dynamic rendering!
        public Rectangle SimViewBounds => new Rectangle(
            (int)(Position.X - (_simRect.Width / (2f * Zoom))),
            (int)(Position.Y - (_simRect.Height / (2f * Zoom))),
            (int)(_simRect.Width / Zoom),
            (int)(_simRect.Height / Zoom)
        );
        public void ChangeZoomStep(bool increase)
        {
            if (increase)
            {
                // Find the first value in the array strictly greater than our current target
                _targetZoom = _zoomLevels.FirstOrDefault(z => z > _targetZoom + 0.001f);
                if (_targetZoom == 0) _targetZoom = _zoomLevels.Last(); // Fallback if at end
            }
            else
            {
                // Find the last value in the array strictly less than our current target
                _targetZoom = _zoomLevels.LastOrDefault(z => z < _targetZoom - 0.001f);
                if (_targetZoom == 0) _targetZoom = _zoomLevels.First(); // Fallback if at start
            }
        }

        public void SnapTo(Vector2 target, float zoom)
        {
            Position = target;
            Zoom = zoom;
            UpdateMatrices();
        }
        public void Follow(Vector2 target, float zoom, GameTime gt) => Update(gt, target);
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
            NativeTransform = view * nativeCenter* Matrix.CreateOrthographicOffCenter(0, _nativeRect.Width, _nativeRect.Height, 0, 0, 1);

            // --- B. SIMULATION MATRICES (960x540) ---
            // Grass/Player are drawn at 2x scale (SimScale) relative to 480p coords
            // NOTE: We do NOT use 'scale' (User Zoom) here yet, as Simulation is fixed at 960p.
            float simScaleFactor = 2.0f;
            Matrix simZoom = Matrix.CreateScale(simScaleFactor);
            Matrix simCenter = Matrix.CreateTranslation(_simRect.Width / 2f, _simRect.Height / 2f, 0);

            SimTransform = view * simZoom * simCenter * Matrix.CreateOrthographicOffCenter(0, _simRect.Width, _simRect.Height, 0, 0, 1);

        }
        public void Follow(Player player, float scale) => Follow(player._position, scale);
        /// <summary>
        /// Converts a screen coordinate (e.g., from the 960x540 Simulation RenderTarget) to a World coordinate.
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 screenPosition, Rectangle viewportRect)
        {
            // 1. Find the offset from the center of the screen
            Vector2 screenCenter = new Vector2(viewportRect.Width / 2f, viewportRect.Height / 2f);
            Vector2 offsetFromCenter = screenPosition - screenCenter;

            // 2. Adjust for camera zoom
            offsetFromCenter /= Zoom;

            // 3. Add to the camera's world position
            return Position + offsetFromCenter;
        }
        /// <summary>
        /// Returns a highly accurate RectangleF of the exact world area currently visible on screen.
        /// </summary>
        public RectangleF GetVisibleWorldBounds(Rectangle viewportBounds)
        {
            // Calculate the actual world-space width and height based on the zoom level
            float worldWidth = viewportBounds.Width / Zoom;
            float worldHeight = viewportBounds.Height / Zoom;

            // Because Camera.Position is the exact center of the screen, 
            // the top-left corner is half the width/height up and to the left.
            float worldX = Position.X - (worldWidth / 2f);
            float worldY = Position.Y - (worldHeight / 2f);

            return new RectangleF(worldX, worldY, worldWidth, worldHeight);
        }
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

        public void Update(EditorInputState input, Rectangle viewportBounds)
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

        public void Update(EditorInputState input, Rectangle viewportBounds,bool x)
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


