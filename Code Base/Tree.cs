using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pixel_Simulations;
using System;

namespace Pixel_Simulations
{

    public class Tree : IRenderable
    {
        private readonly Texture2D _texture;
        private readonly Vector2 _position; // Top-left position for drawing
        private readonly Rectangle _sourceRect;
        private readonly Vector2 _hotspot; // Bottom-center position for placement and depth

        public float Depth => _hotspot.Y;

        public Tree(Texture2D texture, Vector2 position, Rectangle sourceRect)
        {
            _texture = texture;
            _position = position;
            _sourceRect = sourceRect;

            // Calculate the bottom-center hotspot
            _hotspot = new Vector2(
                _position.X + _sourceRect.Width / 2f,
                _position.Y + _sourceRect.Height
            );
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Round the position to prevent jitter
            var drawPosition = new Vector2((int)Math.Round(_position.X), (int)Math.Round(_position.Y));
            spriteBatch.Draw(_texture, drawPosition, _sourceRect, Color.White);
        }

        public void DrawNormal(SpriteBatch spriteBatch, Effect normalEffect, IndexBuffer indexBuffer) { }
    }
}