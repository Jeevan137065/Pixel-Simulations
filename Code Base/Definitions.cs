using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;


namespace Pixel_Simulations
{
    public static class ListExtensions
    {
        private static Random _random = new Random();

        // Fisher-Yates shuffle algorithm
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }

    public class Decoration : IRenderable
    {
        private readonly Texture2D _texture;
        private readonly Vector2 _position;
        private readonly Rectangle _sourceRect;

        // The depth is the bottom of the decoration's sprite.
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
    }
}
