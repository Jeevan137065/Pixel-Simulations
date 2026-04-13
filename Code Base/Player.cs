using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Pixel_Simulations
{

    public class Ball 
    {
        public Vector2 Position { get; private set; }

        private Texture2D _albedoTexture;
        private Texture2D _normalTexture;
        private float _speed = 300f; // Speed in high-resolution pixels per second
        private readonly int _scale;

        private Vector2 _albedoOrigin;
        private Vector2 _normalOrigin;

        public Ball(Texture2D albedo, Texture2D normal, Vector2 startPosition, int upscaleFactor)
        {
            _albedoTexture = albedo;
            _normalTexture = normal;
            Position = startPosition;
            _scale = upscaleFactor;

            // Pre-calculate origins for centering
            _albedoOrigin = new Vector2(_albedoTexture.Width / 2f, _albedoTexture.Height / 2f);
            _normalOrigin = new Vector2(_normalTexture.Width / 2f, _normalTexture.Height / 2f);
        }

        public void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var velocity = Vector2.Zero;

            if (keyboard.IsKeyDown(Keys.W)) velocity.Y -= 1;
            if (keyboard.IsKeyDown(Keys.S)) velocity.Y += 1;
            if (keyboard.IsKeyDown(Keys.A)) velocity.X -= 1;
            if (keyboard.IsKeyDown(Keys.D)) velocity.X += 1;

            if (velocity != Vector2.Zero)
            {
                velocity.Normalize();
                Position += velocity * _speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
        }

        // Draws the low-res color texture to the low-res render target
        public void DrawAlbedo(SpriteBatch spriteBatch)
        {
            // We divide the high-res position by the scale to get the correct low-res position
            Vector2 drawPosition = Position / _scale;
            spriteBatch.Draw(_albedoTexture, drawPosition, null, Color.White, 0f, _albedoOrigin, 1.0f, SpriteEffects.None, 0);
        }

        // Draws the high-res normal map to the high-res render target
        public void DrawNormals(SpriteBatch spriteBatch)
        {
            // We use the position directly as this is the high-res buffer
            spriteBatch.Draw(_normalTexture, Position, null, Color.White, 0f, _normalOrigin, 1.0f, SpriteEffects.None, 0);
        }
        public void DrawOcclusionShape(SpriteBatch spriteBatch)
        {
            // We use the high-res position and a scale factor to draw the albedo
            // texture at the correct size on the high-res render target.
            float highResScale = (float)_normalTexture.Width / _albedoTexture.Width;
            spriteBatch.Draw(_albedoTexture, Position, null, Color.White, 0f, _albedoOrigin, highResScale, SpriteEffects.None, 0);
        }

    }
    public class Player : IRenderable
    {
        public Texture2D _texture;
        private Texture2D _normalTexture;
        public Vector2 _position;
        private int _speed = 160;
        public bool isMoving = false;
        public Vector2 Velocity { get; private set; }
        public Rectangle Bounds => new Rectangle((int)_position.X, (int)_position.Y, _texture.Width, _texture.Height);

        // Public property to allow the Camera to get the player's position
        public Vector2 Position => _position;
        public float SubmergedAmount = 0.0f;
        public float Depth => _position.Y + _texture.Height;
        public Vector2 Foot => new Vector2(_position.X,Depth);
        public Rectangle FootBounds => new Rectangle((int)Foot.X, (int)Foot.Y - 8, _texture.Width, 8);
        public Inventory Inventory { get; }
        public Vector2 InteractionCenter;
        // The world boundaries
        private readonly int _worldWidth;
        private readonly int _worldHeight;
        GraphicsDevice gd;
        public List<VertexPositionTexture> _playerVertices;
        public VertexBuffer _playerBuffer;
        public Effect playerShader;

        public Player(Vector2 startPosition, int worldWidth, int worldHeight)
        {
            _position = startPosition;
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            Inventory = new Inventory();
        }

        public void LoadContent(ContentManager content, GraphicsDevice _gd)
        {
            gd = _gd;
            _texture = content.Load<Texture2D>("Player");
            _normalTexture = content.Load<Texture2D>("Player_n");
            playerShader = content.Load<Effect>("PlayerEffect");
            _playerVertices = new List<VertexPositionTexture>();
            InitializePlayerQuad(_texture.Width, _texture.Height,gd);
        }
        void InitializePlayerQuad(int w, int h,GraphicsDevice device)
        {
            _playerVertices.Clear();
            // Define a static quad from (0,0) to (Width, Height)
            // Triangle 1
            _playerVertices.Add(new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 0)));
            _playerVertices.Add(new VertexPositionTexture(new Vector3(w, 0, 0), new Vector2(1, 0)));
            _playerVertices.Add(new VertexPositionTexture(new Vector3(0, h, 0), new Vector2(0, 1)));
            _playerVertices.Add(new VertexPositionTexture(new Vector3(0, h, 0), new Vector2(0, 1)));
            _playerVertices.Add(new VertexPositionTexture(new Vector3(w, 0, 0), new Vector2(1, 0)));
            _playerVertices.Add(new VertexPositionTexture(new Vector3(w, h, 0), new Vector2(1, 1)));

            _playerBuffer = new VertexBuffer(device, typeof(VertexPositionTexture), 6, BufferUsage.WriteOnly);
            _playerBuffer.SetData(_playerVertices.ToArray());
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
            if (kstate.IsKeyDown(Keys.D1)) Inventory.SelectSlot(0);
            if (kstate.IsKeyDown(Keys.D2)) Inventory.SelectSlot(1);
            if (kstate.IsKeyDown(Keys.D3)) Inventory.SelectSlot(2);
            if (kstate.IsKeyDown(Keys.D4)) Inventory.SelectSlot(3);
            if (kstate.IsKeyDown(Keys.D5)) Inventory.SelectSlot(4);
            if (kstate.IsKeyDown(Keys.D6)) Inventory.SelectSlot(5);
            // Normalize the direction vector to prevent faster diagonal movement
            

            if (moveDirection != Vector2.Zero)
            {
                moveDirection.Normalize();
                Velocity = moveDirection * _speed;
                isMoving = true;
            }
            else
            {
                Velocity = Vector2.Zero;
                isMoving = false;
            }

            _position += Velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Clamp the player's position to stay within the world boundaries
            //_position.X = MathHelper.Clamp(_position.X, 0, _worldWidth);
            //_position.Y = MathHelper.Clamp(_position.Y, 0, _worldHeight);
            InteractionCenter = new Vector2(_position.X + _texture.Width / 2f, _position.Y + _texture.Height / 2f);
        }

        public void Sink(float targetDepth)
        {
            float lerpSpeed = 0.05f;

            // Standard Lerp
            SubmergedAmount = MathHelper.Lerp(SubmergedAmount, targetDepth, lerpSpeed);

            // FIX: Snap to target if we are close enough to avoid e-15 or 0.4999999
            if (Math.Abs(SubmergedAmount - targetDepth) < 0.001f)
            {
                SubmergedAmount = targetDepth;
            }

            // Ensure it never goes below zero (fixes the e-negative issue)
            if (SubmergedAmount < 0.0001f) SubmergedAmount = 0.0f;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            var drawPosition = new Vector2((int)Math.Round(_position.X), (int)Math.Round(_position.Y));
            spriteBatch.Draw(_texture, drawPosition, Color.White);
        }
        public void DrawPlayerManual(Matrix finalMatrix)
        {
            // 1. Create the 6 vertices for the player Quad
            // 1. Create the vertices using MonoGame's BUILT-IN type
            VertexPositionTexture[] verts = new VertexPositionTexture[6];

            Vector2 pos = _position;
            Vector2 origin = Foot;
            float w = _texture.Width;
            float h = _texture.Height;

            // The logic coordinates
            float left = pos.X - origin.X;
            float top = pos.Y - origin.Y;
            float right = left + w;
            float bottom = top + h;

            // We store the World Y in the 'Z' component of the Position
            // This is a safe way to pass depth to the shader
            float z = pos.Y;

            verts[0] = new VertexPositionTexture(new Vector3(left, top, z), new Vector2(0, 0));
            verts[1] = new VertexPositionTexture(new Vector3(right, top, z), new Vector2(1, 0));
            verts[2] = new VertexPositionTexture(new Vector3(left, bottom, z), new Vector2(0, 1));
            verts[3] = new VertexPositionTexture(new Vector3(left, bottom, z), new Vector2(0, 1));
            verts[4] = new VertexPositionTexture(new Vector3(right, top, z), new Vector2(1, 0));
            verts[5] = new VertexPositionTexture(new Vector3(right, bottom, z), new Vector2(1, 1));

            // 2. Set Parameters with NULL CHECKS
            // This prevents the 'ArgumentException' if the parameter was optimized out
            var wvpParam = playerShader.Parameters["WorldViewProjection"];
            if (wvpParam != null) wvpParam.SetValue(finalMatrix);

            var texParam = playerShader.Parameters["PlayerTexture"];
            if (texParam != null) texParam.SetValue(_texture);

            // 3. Draw
            foreach (var pass in playerShader.CurrentTechnique.Passes)
            {
                pass.Apply(); // This is now safe
                gd.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    verts, 0, 2
                );
            }
        }
        public void Draw(Matrix RenderFinal, bool x)
        {

            playerShader.Parameters["WorldViewProjection"].SetValue(RenderFinal);
            playerShader.Parameters["PlayerPos"].SetValue(_position);
            //playerShader.Parameters["PlayerFoot"].SetValue(Depth);
            playerShader.Parameters["PlayerOrigin"].SetValue(new Vector2(0, 0));
            playerShader.Parameters["PlayerTexture"].SetValue(_texture);
            foreach (var pass in playerShader.CurrentTechnique.Passes)
            {
                pass.Apply(); // This applies the parameters once
                gd.SetVertexBuffer(_playerBuffer);
                gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);
            }
        }

        public void DrawMod(Matrix RenderFinal, bool x)
        {
            playerShader.Parameters["WorldViewProjection"].SetValue(RenderFinal);
            playerShader.Parameters["SubmergedAmount"].SetValue(SubmergedAmount); // NEW
            playerShader.Parameters["PlayerOrigin"].SetValue(new Vector2(0, 0));
            playerShader.Parameters["PlayerPos"].SetValue(_position);
            playerShader.Parameters["PlayerTexture"].SetValue(_texture);

            foreach (var pass in playerShader.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.SetVertexBuffer(_playerBuffer);
                gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);
            }
        }
        public void Draw(SpriteBatch spriteBatch, Matrix hiResProj)
        {

            spriteBatch.Begin(
                SpriteSortMode.BackToFront,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.Default, // MUST match the Grass
                null, null,
                hiResProj); // Use the View-only matrix as discussed

            spriteBatch.Draw(
                _texture,
                _position,
                null,
                Color.White,
                0,
                Vector2.Zero,
                1.0f,
                SpriteEffects.None,
                Depth / 10000.0f); // Depth value

            spriteBatch.DrawRectangle(FootBounds, Color.White);
            spriteBatch.End();
        }

        public void Draw(SpriteBatch spriteBatch, Color color)
        {
            // Calculate position logic...
            // (Assuming you aren't using the matrix in Pass 2, but you ARE)
            // Since Pass 2/3 uses a scaled matrix, draw at standard position.

            // NOTE: Check your Pass 2 player logic. 
            // If you draw using a matrix, draw at _position.

            var drawPosition = new Vector2((int)Math.Round(_position.X), (int)Math.Round(_position.Y));
            spriteBatch.Draw(_texture, drawPosition, color);
        }
    }

}
