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
        // --- State Properties ---
        public Vector2 Position { get; private set; }
        public float Zoom { get; private set; } = 1.0f;

        private float _targetZoom = 1.0f;
        private Vector2 _primaryTarget;
        private Vector2 _secondaryTarget;
        private float _focusStrength = 0f;

        // Standard zoom levels (Production)
        private readonly float[] _standardZoomLevels = { 1.0f, 2.0f };
        // Extended developer zoom levels (0.5x = 1080p canvas, 4.0x = extreme close-up)
        private readonly float[] _developerZoomLevels = {0.1f,0.66f, 0.5f, 0.75f, 1.0f, 1.5f, 2.0f, 3.0f, 4.0f };

        public bool AllowExtendedZoom { get; set; } = false;
        public float MoveLerpSpeed { get; set; } = 15.0f;
        public float ZoomLerpSpeed { get; set; } = 8.0f;

        // Base canvas dimensions at 1.0x
        public const int BASE_SIM_WIDTH = 960;
        public const int BASE_SIM_HEIGHT = 540;
        public const int BASE_NATIVE_WIDTH = 480;
        public const int BASE_NATIVE_HEIGHT = 270;

        // Dynamic (A x B) Viewports
        public Rectangle SimViewportRect { get; private set; }
        public Rectangle NativeViewportRect { get; private set; }
        public Rectangle ScreenRect { get; private set; } = new Rectangle(0, 0, 1920, 1080);

        // Transformation Matrices
        public Matrix NativeTransform { get; private set; }
        public Matrix SimTransform { get; private set; }
        public Matrix ScreenTransform { get; private set; }

        public Matrix NativeWVP { get; private set; }
        public Matrix SimWVP { get; private set; }
        public Matrix ScreenWVP { get; private set; }

        public void Setcamera(Rectangle nativeRect, Rectangle simRect, Rectangle finalRect)
        {
            ScreenRect = finalRect;
            UpdateMatrices();
        }

        public void SetFocusPoint(Vector2 point, float strength)
        {
            _secondaryTarget = point;
            _focusStrength = MathHelper.Clamp(strength, 0f, 1f);
        }

        public void Update(GameTime gameTime, Vector2 playerPos)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _primaryTarget = playerPos;

            Vector2 finalTarget = Vector2.Lerp(_primaryTarget, _secondaryTarget, _focusStrength);

            Position = Vector2.Lerp(Position, finalTarget, MoveLerpSpeed * dt);
            Zoom = MathHelper.Lerp(Zoom, _targetZoom, ZoomLerpSpeed * dt);

            if (Math.Abs(Zoom - _targetZoom) < 0.001f) Zoom = _targetZoom;
            if (Vector2.Distance(Position, finalTarget) < 0.1f) Position = finalTarget;

            UpdateMatrices();
        }

        public void ChangeZoomStep(bool increase)
        {
            float[] levels = AllowExtendedZoom ? _developerZoomLevels : _standardZoomLevels;

            if (increase)
            {
                _targetZoom = levels.FirstOrDefault(z => z > _targetZoom + 0.01f);
                if (_targetZoom == 0) _targetZoom = levels.Last();
            }
            else
            {
                _targetZoom = levels.LastOrDefault(z => z < _targetZoom - 0.01f);
                if (_targetZoom == 0) _targetZoom = levels.First();
            }
        }

        public void SnapTo(Vector2 target, float zoom)
        {
            Position = target;
            _targetZoom = zoom;
            Zoom = zoom;
            UpdateMatrices();
        }

        private void UpdateMatrices()
        {
            // 1. Calculate Active Canvas Dimensions (A x B)
            // Zoom 1.0 => Sim: 960x540, Native: 480x270
            // Zoom 2.0 => Sim: 480x270, Native: 240x135
            // Zoom 0.5 => Sim: 1920x1080, Native: 960x540
            int simA = (int)Math.Round(BASE_SIM_WIDTH / Zoom);
            int simB = (int)Math.Round(BASE_SIM_HEIGHT / Zoom);

            // Keep dimensions even to avoid 0.5px texture misalignment
            simA = Math.Clamp(simA + (simA % 2), 240, 1920);
            simB = Math.Clamp(simB + (simB % 2), 135, 1080);

            SimViewportRect = new Rectangle(0, 0, simA, simB);
            NativeViewportRect = new Rectangle(0, 0, simA / 2, simB / 2);

            // 2. Integer pixel translation prevents sub-pixel shimmer
            Vector2 roundedPos = new Vector2(
                MathF.Round(Position.X),
                MathF.Round(Position.Y)
            );

            Matrix translation = Matrix.CreateTranslation(-roundedPos.X, -roundedPos.Y, 0);

            // --- NATIVE TRANSFORM (Albedo / Tiles: 1x world unit) ---
            Matrix nativeCenter = Matrix.CreateTranslation(NativeViewportRect.Width / 2f, NativeViewportRect.Height / 2f, 0);
            NativeTransform = translation * nativeCenter;
            NativeWVP = NativeTransform * Matrix.CreateOrthographicOffCenter(0, NativeViewportRect.Width, NativeViewportRect.Height, 0, -100, 100);

            // --- SIMULATION TRANSFORM (Entities / Normals / Depth: 2x world unit) ---
            Matrix simScale = Matrix.CreateScale(2f, 2f, 1f);
            Matrix simCenter = Matrix.CreateTranslation(SimViewportRect.Width / 2f, SimViewportRect.Height / 2f, 0);
            SimTransform = translation * simScale * simCenter;
            SimWVP = SimTransform * Matrix.CreateOrthographicOffCenter(0, SimViewportRect.Width, SimViewportRect.Height, 0, -100, 100);

            // --- SCREEN TRANSFORM (1080p HUD & Debug overlay) ---
            float screenScaleX = (float)ScreenRect.Width / NativeViewportRect.Width;
            float screenScaleY = (float)ScreenRect.Height / NativeViewportRect.Height;
            Matrix screenCenter = Matrix.CreateTranslation(ScreenRect.Width / 2f, ScreenRect.Height / 2f, 0);
            ScreenTransform = translation * Matrix.CreateScale(screenScaleX, screenScaleY, 1f) * screenCenter;
            ScreenWVP = ScreenTransform * Matrix.CreateOrthographicOffCenter(0, ScreenRect.Width, ScreenRect.Height, 0, -100, 100);
        }

        // Visible World Bounds
        public Rectangle SimViewBounds => new Rectangle(
            (int)(Position.X - (SimViewportRect.Width / 4f)),
            (int)(Position.Y - (SimViewportRect.Height / 4f)),
            SimViewportRect.Width / 2,
            SimViewportRect.Height / 2
        );

        public Rectangle NativeViewBounds => SimViewBounds;

        public Vector2 ScreenToWorld(Vector2 screenPosition, Rectangle viewportRect)
        {
            float normX = screenPosition.X / viewportRect.Width;
            float normY = screenPosition.Y / viewportRect.Height;

            float canvasX = normX * SimViewportRect.Width;
            float canvasY = normY * SimViewportRect.Height;

            float worldX = Position.X + (canvasX - (SimViewportRect.Width / 2f)) / 2f;
            float worldY = Position.Y + (canvasY - (SimViewportRect.Height / 2f)) / 2f;

            return new Vector2(worldX, worldY);
        }

        public RectangleF GetVisibleWorldBounds(Rectangle viewportBounds)
        {
            float worldWidth = SimViewportRect.Width / 2f;
            float worldHeight = SimViewportRect.Height / 2f;
            return new RectangleF(Position.X - (worldWidth / 2f), Position.Y - (worldHeight / 2f), worldWidth, worldHeight);
        }

        public void Follow(Vector2 target, float zoom, GameTime gt) => Update(gt, target);
        public void Follow(NewPlayer player, float scale) => SnapTo(player.Position, scale);
    }


    public class EditorCamera
    {
        public Vector2 Position { get; set; }
        public float Zoom { get; set; }
        public Matrix Transform { get; private set; }

        private const float MIN_ZOOM = 0.125f;
        private const float MAX_ZOOM = 4.0f;
        private const float STEP_ZOOM = 0.25f;

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


