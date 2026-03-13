using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations
{
    public struct VertexPositionColorTextureHeight : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector2 TextureCoordinate;
        public float NormalizedHeight; // 1.0 = Top, 0.0 = Bottom

        // This tells the GPU exactly how to read our struct
        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            // We pass NormalizedHeight via TEXCOORD1
            new VertexElement(24, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

        public VertexPositionColorTextureHeight(Vector3 position, Color color, Vector2 texCoord, float normalizedHeight)
        {
            Position = position;
            Color = color;
            TextureCoordinate = texCoord;
            NormalizedHeight = normalizedHeight;
        }
    }
    public class ParallaxRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private Effect _effect;

        // Batching buffers
        private VertexPositionColorTextureHeight[] _vertices;
        private short[] _indices;
        private int _spriteCount;
        private Texture2D _currentTexture;
        private const int MAX_SPRITES = 2048; // Can draw 2048 sprites before forcing a GPU flush

        public float parallaxAmount = 0.15f;
        public bool enableLighting = false;
        public ParallaxRenderer(GraphicsDevice graphicsDevice, Effect parallaxEffect)
        {
            _graphicsDevice = graphicsDevice;
            _effect = parallaxEffect;

            _vertices = new VertexPositionColorTextureHeight[MAX_SPRITES * 4];
            _indices = new short[MAX_SPRITES * 6];

            // Pre-calculate indices (Standard Quad pattern: 0,1,2, 1,3,2)
            for (int i = 0; i < MAX_SPRITES; i++)
            {
                _indices[i * 6 + 0] = (short)(i * 4 + 0);
                _indices[i * 6 + 1] = (short)(i * 4 + 1);
                _indices[i * 6 + 2] = (short)(i * 4 + 2);
                _indices[i * 6 + 3] = (short)(i * 4 + 1);
                _indices[i * 6 + 4] = (short)(i * 4 + 3);
                _indices[i * 6 + 5] = (short)(i * 4 + 2);
            }
        }

        public void Begin(Camera camera, Vector2 viewportSize)
        {
            // Replicate SpriteBatch's internal matrix math
            Matrix projection = Matrix.CreateOrthographicOffCenter(0, viewportSize.X, viewportSize.Y, 0, 0, 1);

            // NOTE: Depending on your exact Camera class implementation, use NativeFinal or SimFinal or Transform here.
            Matrix viewProjection = camera.SimFinal * projection;

            _effect.Parameters["MatrixTransform"]?.SetValue(viewProjection);
            _effect.Parameters["CameraPosition"]?.SetValue(camera.Position);
            _effect.Parameters["ParallaxAmount"]?.SetValue(parallaxAmount); // Tweak for strength!
            _effect.Parameters["EnableLighting"]?.SetValue(enableLighting ? 1.0f : 0.0f);
            _spriteCount = 0;
            _currentTexture = null;

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        }

        public void Draw(RenderableSprite sprite)
        {
            if (sprite.Texture == null) return;

            // Flush if we hit the limit or the texture changes (Texture Atlas switching)
            if (_spriteCount >= MAX_SPRITES || (_currentTexture != null && _currentTexture != sprite.Texture))
            {
                Flush();
            }

            _currentTexture = sprite.Texture;

            // --- QUAD MATH ---
            float texW = 1f / _currentTexture.Width;
            float texH = 1f / _currentTexture.Height;

            // Texture Coordinates
            float u0 = sprite.SourceRect.X * texW;
            float v0 = sprite.SourceRect.Y * texH;
            float u1 = (sprite.SourceRect.X + sprite.SourceRect.Width) * texW;
            float v1 = (sprite.SourceRect.Y + sprite.SourceRect.Height) * texH;

            // Local Corner Positions (Pre-rotation)
            float left = -sprite.Origin.X * sprite.Scale.X;
            float right = (sprite.SourceRect.Width - sprite.Origin.X) * sprite.Scale.X;
            float top = -sprite.Origin.Y * sprite.Scale.Y;
            float bottom = (sprite.SourceRect.Height - sprite.Origin.Y) * sprite.Scale.Y;

            // Apply Rotation
            float cos = 1f, sin = 0f;
            if (sprite.Rotation != 0f)
            {
                cos = (float)Math.Cos(sprite.Rotation);
                sin = (float)Math.Sin(sprite.Rotation);
            }

            Vector3 tl = new Vector3(sprite.Position.X + (left * cos - top * sin), sprite.Position.Y + (left * sin + top * cos), 0);
            Vector3 tr = new Vector3(sprite.Position.X + (right * cos - top * sin), sprite.Position.Y + (right * sin + top * cos), 0);
            Vector3 bl = new Vector3(sprite.Position.X + (left * cos - bottom * sin), sprite.Position.Y + (left * sin + bottom * cos), 0);
            Vector3 br = new Vector3(sprite.Position.X + (right * cos - bottom * sin), sprite.Position.Y + (right * sin + bottom * cos), 0);

            // Add vertices to array (Notice NormalizedHeight: Top = 1f, Bottom = 0f)
            int vIndex = _spriteCount * 4;
            _vertices[vIndex + 0] = new VertexPositionColorTextureHeight(tl, Color.White, new Vector2(u0, v0), 1f); // Top Left
            _vertices[vIndex + 1] = new VertexPositionColorTextureHeight(tr, Color.White, new Vector2(u1, v0), 1f); // Top Right
            _vertices[vIndex + 2] = new VertexPositionColorTextureHeight(bl, Color.White, new Vector2(u0, v1), 0f); // Bottom Left
            _vertices[vIndex + 3] = new VertexPositionColorTextureHeight(br, Color.White, new Vector2(u1, v1), 0f); // Bottom Right

            _spriteCount++;
        }

        public void End()
        {
            Flush();
        }

        private void Flush()
        {
            if (_spriteCount == 0 || _currentTexture == null) return;

            _effect.Parameters["SpriteTexture"]?.SetValue(_currentTexture);
            _effect.CurrentTechnique.Passes[0].Apply();

            _graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _vertices, 0, _spriteCount * 4,
                _indices, 0, _spriteCount * 2
            );

            _spriteCount = 0;
        }
    }
}
