using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public class Camera
    {
        private Matrix _transform;
        public Matrix Transform => _transform;

        public void Follow(Player target, int windowWidth, int windowHeight, int worldWidth, int worldHeight)
        {
            // Center the camera on the player
            var cameraPosition = target.Position - new Vector2(windowWidth / 2, windowHeight / 2);

            // Clamp the camera's position to the world boundaries so we don't see "outside" the world
            float clampedX = MathHelper.Clamp(cameraPosition.X, 0, worldWidth - windowWidth);
            float clampedY = MathHelper.Clamp(cameraPosition.Y, 0, worldHeight - windowHeight);

            // Create a transform matrix that translates the world opposite to the camera's clamped position
            // This makes it look like the camera is moving.
            _transform = Matrix.CreateTranslation(-clampedX, -clampedY, 0);
        }
    }


    public class EditorCamera
    {
        public Vector2 Position { get; private set; }
        public float Zoom { get; private set; }

        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 3.0f;

        public Matrix Transform { get; private set; }

        public EditorCamera()
        {
            Zoom = 1.0f;
            Position = Vector2.Zero;
            UpdateTransform();
        }

        public void Pan(Vector2 delta)
        {
            Position -= delta / Zoom;
        }

        public void AdjustZoom(float zoomAdjustment, Vector2 mousePositionInNativeSpace)
        {
            Vector2 mouseWorldPosBeforeZoom = ScreenToWorld(mousePositionInNativeSpace);

            Zoom = MathHelper.Clamp(Zoom + zoomAdjustment, MIN_ZOOM, MAX_ZOOM);

            UpdateTransform();

            Vector2 mouseWorldPosAfterZoom = ScreenToWorld(mousePositionInNativeSpace);

            Position += mouseWorldPosBeforeZoom - mouseWorldPosAfterZoom;
        }

        public void UpdateTransform()
        {
            // *** THE FIX ***
            // This is a much simpler, standard 2D camera transform.
            // It no longer tries to center the view, which was causing the offset.
            Transform = Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
                        Matrix.CreateScale(Zoom, Zoom, 1);
        }

        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return Vector2.Transform(worldPosition, Transform);
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return Vector2.Transform(screenPosition, Matrix.Invert(Transform));
        }

        public Rectangle GetVisibleWorldBounds(int nativeViewportWidth, int nativeViewportHeight)
        {
            Vector2 worldTopLeft = ScreenToWorld(Vector2.Zero);
            Vector2 worldBottomRight = ScreenToWorld(new Vector2(nativeViewportWidth, nativeViewportHeight));

            return new Rectangle(
                (int)worldTopLeft.X,
                (int)worldTopLeft.Y,
                (int)(worldBottomRight.X - worldTopLeft.X),
                (int)(worldBottomRight.Y - worldTopLeft.Y)
            );
        }
    }
}


