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

        // NEW: A separate transform for parallax effects that has NO translation.
        public Matrix ParallaxTransform { get; private set; }

        // NEW: A public property to get the camera's top-left position.
        // This is what our GPU simulation needs.
        public Vector2 Position { get; private set; }


        public void Follow(Player target, int windowWidth, int windowHeight, int worldWidth, int worldHeight)
        {
            // Calculate the desired top-left position of the camera.
            var cameraPosition = target.Position - new Vector2(windowWidth / 2f, windowHeight / 2f);

            // Clamp the camera's position to the world boundaries.
            float clampedX = MathHelper.Clamp(cameraPosition.X, 0, worldWidth - windowWidth);
            float clampedY = MathHelper.Clamp(cameraPosition.Y, 0, worldHeight - windowHeight);

            // Store the final, clamped position.
            Position = new Vector2(clampedX, clampedY);

            // Create the main transform matrix that moves the world.
            _transform = Matrix.CreateTranslation(-Position.X, -Position.Y, 0);

            // Create the parallax transform matrix. Since there is no zoom in this camera,
            // the parallax transform is just the Identity matrix (it does nothing).
            // If you were to add zoom later, the scale matrix would go here.
            ParallaxTransform = Matrix.Identity;
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


