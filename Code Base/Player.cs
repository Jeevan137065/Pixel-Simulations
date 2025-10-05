using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;


namespace Pixel_Simulations
{

    public class Player : IRenderable
    {
        private Texture2D _texture;
        private Vector2 _position;
        private float _speed = 150f; // Pixels per second

        // Public property to allow the Camera to get the player's position
        public Vector2 Position => _position;
        public float Depth => _position.Y + _texture.Height;

        // The world boundaries
        private readonly int _worldWidth;
        private readonly int _worldHeight;

        public Player(Vector2 startPosition, int worldWidth, int worldHeight)
        {
            _position = startPosition;
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
        }

        public void LoadContent(ContentManager content)
        {
            _texture = content.Load<Texture2D>("Player");
        }

        public void Update(GameTime gameTime)
        {
            var kstate = Keyboard.GetState();
            var moveDirection = Vector2.Zero;

            if (kstate.IsKeyDown(Keys.W) || kstate.IsKeyDown(Keys.Up))
                moveDirection.Y = -1;
            if (kstate.IsKeyDown(Keys.S) || kstate.IsKeyDown(Keys.Down))
                moveDirection.Y = 1;
            if (kstate.IsKeyDown(Keys.A) || kstate.IsKeyDown(Keys.Left))
                moveDirection.X = -1;
            if (kstate.IsKeyDown(Keys.D) || kstate.IsKeyDown(Keys.Right))
                moveDirection.X = 1;

            // Normalize the direction vector to prevent faster diagonal movement
            if (moveDirection != Vector2.Zero)
            {
                moveDirection.Normalize();
            }

            // Apply movement based on speed and elapsed time
            _position += moveDirection * _speed * (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Clamp the player's position to stay within the world boundaries
            _position.X = MathHelper.Clamp(_position.X, 0, _worldWidth - _texture.Width);
            _position.Y = MathHelper.Clamp(_position.Y, 0, _worldHeight - _texture.Height);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var drawPosition = new Vector2((int)Math.Round(_position.X), (int)Math.Round(_position.Y));
            spriteBatch.Draw(_texture, drawPosition, Color.White);
        }
    }
}
