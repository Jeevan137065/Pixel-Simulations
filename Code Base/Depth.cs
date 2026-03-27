using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public class VolumetricDepthRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private Effect _depthEffect;
        private readonly BlendState WriteBlue = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Blue,
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.Zero
        };
        private readonly BlendState WriteRedAlpha = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Alpha,
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.InverseSourceAlpha
        };
        public void LoadContent(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _depthEffect = content.Load<Effect>("VolumeDepth");

            _depthEffect.Parameters["MaxAltitude"]?.SetValue(350f);
        }

        // --- 1. VOLUME ALTITUDE (RED) ---
        public void BeginVolumePass(SpriteBatch spriteBatch, Camera camera)
        {
            // Immediate Mode is REQUIRED so shader parameters update per-sprite!
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, _depthEffect, camera.SimFinal);
        }

        public void DrawVolumetricSprite(SpriteBatch spriteBatch, RenderableSprite sprite)
        {
            float spriteTopY = sprite.Position.Y - (sprite.Origin.Y * sprite.Scale.Y);
            float spriteBottomY = sprite.Position.Y + ((sprite.SourceRect.Height - sprite.Origin.Y) * sprite.Scale.Y);
            float vMin = (float)sprite.SourceRect.Top / sprite.Texture.Height;
            float vMax = (float)sprite.SourceRect.Bottom / sprite.Texture.Height;
            _depthEffect.Parameters["SpriteTopY"].SetValue(spriteTopY);
            _depthEffect.Parameters["SpriteBottomY"].SetValue(spriteBottomY);
            _depthEffect.Parameters["BaseWorldY"].SetValue(sprite.BaseWorldY);
            _depthEffect.Parameters["VMin"].SetValue(vMin);
            _depthEffect.Parameters["VMax"].SetValue(vMax);
            // Apply parameters for this specific sprite
            _depthEffect.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(sprite.Texture, sprite.Position, sprite.SourceRect, Color.White, sprite.Rotation, sprite.Origin, sprite.Scale, SpriteEffects.None, 0f);
        }
        public void DrawTerrainDepth(SpriteBatch spriteBatch, Dictionary<Point, Texture2D> maskChunks, RectangleF streamBounds, Camera camera)
        {
            if (maskChunks == null || maskChunks.Count == 0) return;

            // Additive Blending overwrites Blue without touching Red/Green
            spriteBatch.Begin(SpriteSortMode.Immediate, WriteBlue, SamplerState.PointClamp, null, null, null, camera.SimFinal);

            int chunkSize = 256; // MaskLayer.CHUNK_PIXEL_SIZE

            foreach (var kvp in maskChunks)
            {
                RectangleF chunkBounds = new RectangleF(kvp.Key.X * chunkSize, kvp.Key.Y * chunkSize, chunkSize, chunkSize);

                // STREAMING / CULLING: Only draw the chunk if it is within the camera's streaming bounds!
                if (streamBounds.Intersects(chunkBounds))
                {
                    spriteBatch.Draw(kvp.Value, chunkBounds.Position, Color.White);
                }
            }

            spriteBatch.End();
        }
        public void EndPass(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
        }

    }
}
