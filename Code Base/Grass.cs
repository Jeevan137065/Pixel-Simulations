using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations.Code_Base
{
    public class Grass : IRenderable, ISwayable
    {
        private readonly Texture2D _texture;
        private readonly Texture2D _normalTexture;
        private Vector2 _position;
        private VertexPositionTexture[] _vertices = new VertexPositionTexture[4];
        private VertexBuffer _vertexBuffer;
        private Rectangle _sourceRect;
        private Vector2 _drawPosition;
        private GraphicsDevice _gd;
        // Physics Fields
        public float _swayValue { get; private set; }
        private float _maxShake;
        private bool _shakeLeft;
        private double _lastPushTime;
        // Properties
        public float Depth => _position.Y; // Sort by Y
        public Rectangle Bounds => new Rectangle((int)_position.X - 4, (int)_position.Y - 4, 8, 8);
        public SwayType SwayMode { get; set; } = SwayType.TriangleWave; // Default to Triangle

        public Grass(Vector2 tilePosition, Texture2D texture, Texture2D normalTexture, GraphicsDevice graphicsDevice, Random random)
        {
            _texture = texture;
            _normalTexture = normalTexture;
            _gd = graphicsDevice;
            // Pick one of the 4 variants (0, 1, 2, 3)
            // Texture is 24x96, so each sprite is 24x24.
            int variant = random.Next(0, 4);
            _sourceRect = new Rectangle(0, variant * 24, 24, 24);

            // Center the grass on the tile
            // Tile is 16px wide. Grass is 24px wide.
            // Tile Center = 8. Grass Center = 12. Offset = -4.
            // We add some randomness to the position so it looks natural.
            float randomOffsetX = random.Next(-4, 5);
            float randomOffsetY = random.Next(-4, 5);

            _position = new Vector2(
                tilePosition.X + 8 + randomOffsetX,
                tilePosition.Y + 16 + randomOffsetY // Bottom of the tile
            );

            // Calculate draw pos (Top-Left of the sprite)
            Vector2 origin = new Vector2(12, 24); // Bottom-Center of grass
            _drawPosition = _position - origin;

            // Setup Vertices
            _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), 4, BufferUsage.WriteOnly);
            SetupQuad();
        }

        private void SetupQuad()
        {
            float left = _drawPosition.X;
            float right = _drawPosition.X + 24;
            float top = _drawPosition.Y;
            float bottom = _drawPosition.Y + 24;

            // UVs
            Vector2 tl = new Vector2(0, (float)_sourceRect.Y / _texture.Height);
            Vector2 br = new Vector2(1, (float)_sourceRect.Bottom / _texture.Height);

            _vertices[0] = new VertexPositionTexture(new Vector3(left, top, 0), tl);
            _vertices[1] = new VertexPositionTexture(new Vector3(right, top, 0), new Vector2(br.X, tl.Y));
            _vertices[2] = new VertexPositionTexture(new Vector3(left, bottom, 0), new Vector2(tl.X, br.Y));
            _vertices[3] = new VertexPositionTexture(new Vector3(right, bottom, 0), br);

            _vertexBuffer.SetData(_vertices);
        }

        // --- ISwayable Implementation (Triangle Wave Logic) ---
        public void Push(Vector2 direction, float force)
        {
            if (Math.Abs(_maxShake) > 1.0f) return;
            _shakeLeft = direction.X < 0;
            _maxShake = Math.Min(10f, force); // Grass is lighter, maybe less force needed
        }

        public void UpdateSway(GameTime gameTime)
        {
            // Simple Triangle Wave Logic duplicated here for independence
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_maxShake > 0)
            {
                float rate = _maxShake * 15f * elapsed; // Faster sway for grass
                if (_shakeLeft) { _swayValue -= rate; if (_swayValue <= -_maxShake) _shakeLeft = false; }
                else { _swayValue += rate; if (_swayValue >= _maxShake) _shakeLeft = true; }
                _maxShake = Math.Max(0f, _maxShake - (1.0f * elapsed));
            }
            else { _swayValue = 0; }
        }

        public void UpdateVertices(float totalTime, float windAmount, float windSpeed)
        {
            // Grass creates a "ripple" effect, so we offset wind by position
            float windSway = (float)Math.Sin(totalTime * windSpeed + _position.X * 0.2f) * windAmount;
            float totalSway = _swayValue + windSway;

            // Apply Bend
            float topOffset = totalSway; // 100% at top

            _vertices[0].Position.X = _drawPosition.X + topOffset;
            _vertices[1].Position.X = _drawPosition.X + 24 + topOffset;

            _vertexBuffer.SetData(_vertices);
        }

        public void Draw(BasicEffect effect, IndexBuffer indexBuffer)
        {
            effect.Texture = _texture;
            _gd.SetVertexBuffer(_vertexBuffer);
            _gd.Indices = indexBuffer;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }

        public void DrawNormal(SpriteBatch spriteBatch, Effect normalEffect, IndexBuffer indexBuffer)
        {
            // Set the specific normal map for this object
            normalEffect.Parameters["NormalTexture"].SetValue(_normalTexture);

            // Vertices are already updated, just draw
            // Note: We use a custom Effect, not BasicEffect here
            _gd.SetVertexBuffer(_vertexBuffer);
            _gd.Indices = indexBuffer;

            foreach (var pass in normalEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }

        public void DrawDebugOutline(BasicEffect effect) {
            var debugVertices = new VertexPositionColor[5]; // 5 points to close the loop

            // Use the actual _vertices positions that are updated by physics
            debugVertices[0] = new VertexPositionColor(_vertices[0].Position, Color.Magenta); // TL
            debugVertices[1] = new VertexPositionColor(_vertices[1].Position, Color.Magenta); // TR
            debugVertices[2] = new VertexPositionColor(_vertices[3].Position, Color.Magenta); // BR
            debugVertices[3] = new VertexPositionColor(_vertices[2].Position, Color.Magenta); // BL
            debugVertices[4] = new VertexPositionColor(_vertices[0].Position, Color.Magenta); // Back to TL to close loop

            effect.TextureEnabled = false;
            effect.VertexColorEnabled = true;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                // Draw the line strip
                _gd.DrawUserPrimitives(PrimitiveType.LineStrip, debugVertices, 0, 4);
            }
        }

        public void DrawDepth(SpriteBatch spriteBatch,  Effect depthEffect, IndexBuffer indexBuffer)
        {
            // 1. Set Texture
            depthEffect.Parameters["SpriteTexture"].SetValue(_texture);

            // 2. Set Buffers
            _gd.SetVertexBuffer(_vertexBuffer);
            _gd.Indices = indexBuffer;

            // 3. Draw Quad
            foreach (var pass in depthEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }
        public void Draw(SpriteBatch sb) { } // Unused
    }
}
