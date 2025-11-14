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
    }

    public struct PlantDefinition
    {
        public Tool SeedType; // What tool plants this?
        public bool CanSpawnNaturally;
        public Rectangle SourceRect;
        public Vector2 Origin;
    }
}


