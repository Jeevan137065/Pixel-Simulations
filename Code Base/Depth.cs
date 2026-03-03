using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations
{
    public class VolumetricDepthRenderer
    {
        private Effect _depthEffect;
        public Effect _depthChannel;
        public void LoadContent(ContentManager content)
        {
            _depthEffect = content.Load<Effect>("VolumeDepth");
            _depthChannel = content.Load<Effect>("DepthChannel");
            // Set default terrain elevation (can be changed dynamically if you have hills/valleys)
            _depthEffect.Parameters["TerrainElevation"]?.SetValue(0f);
            _depthEffect.Parameters["MaxAltitude"]?.SetValue(750f);
        }
        public void BeginPass(SpriteBatch spriteBatch, Camera camera)
        {
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, null, null, _depthEffect, camera.SimFinal);
        }
        public void DrawVolumetricSprite(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, Rectangle sourceRect, Vector2 origin, Vector2 scale, float baseWorldY, float drawDepth)
        {
            // Calculate the exact world bounds of this specific sprite
            float spriteTopY = position.Y - (origin.Y * scale.Y);
            float spriteBottomY = position.Y + ((sourceRect.Height - origin.Y) * scale.Y);

            // Pass to shader
            _depthEffect.Parameters["SpriteTopY"]?.SetValue(spriteTopY);
            _depthEffect.Parameters["SpriteBottomY"]?.SetValue(spriteBottomY);
            _depthEffect.Parameters["BaseWorldY"]?.SetValue(baseWorldY);
            _depthEffect.Parameters["DrawDepth"]?.SetValue(drawDepth);

            _depthEffect.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(texture, position, sourceRect, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        public void EndPass(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
        }
    }
}
