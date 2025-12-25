using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;


namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public interface IRenderable
    {
        // The "depth" of the object, used for sorting. Higher Y-values are drawn on top.
        // This should typically be the Y-coordinate of the object's "feet".
        float Depth { get; }

        // The method to draw the object to the screen.
        void Draw(SpriteBatch spriteBatch);
        void DrawNormal(SpriteBatch spriteBatch, Effect normalEffect, IndexBuffer indexBuffer);
    }

    public enum SwayType
    {
        PhysicsSpring, // The elastic, spring-based movement
        TriangleWave   // The decaying linear movement (Stardew style)
    }

    public class Decoration : IRenderable
    {
        private readonly Texture2D _texture;
        private readonly Vector2 _position;
        public Rectangle _sourceRect;
        public float Depth => _position.Y + _sourceRect.Height;

        public Decoration(Texture2D texture, Vector2 position, Rectangle sourceRect)
        {
            _texture = texture;
            _position = position;
            _sourceRect = sourceRect;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Round the position to prevent jitter.
            var drawPosition = new Vector2((int)Math.Round(_position.X), (int)Math.Round(_position.Y));
            spriteBatch.Draw(_texture, drawPosition, _sourceRect, Color.White);
        }

        public void DrawNormal(SpriteBatch spriteBatch, Effect normalEffect, IndexBuffer indexBuffer) { }
    }

    public interface ISwayable : IRenderable
    {
        // The collision area of the object, usually at its base.
        Rectangle Bounds { get; }

        // The current horizontal sway value. Negative is left, positive is right.
        float _swayValue { get; }
        void UpdateVertices(float totalTime, float windAmount, float windSpeed);

        // Apply a force to the object, causing it to sway.
        void Push(Vector2 direction, float force);

        // Update the sway physics (like damping/decay) each frame.
        void UpdateSway(GameTime gameTime);

        void Draw(BasicEffect effect, IndexBuffer indexBuffer);
        void DrawDepth(SpriteBatch spriteBatch, Effect depthEffect, IndexBuffer indexBuffer);
        void DrawDebugOutline( BasicEffect effect);

    }
}


