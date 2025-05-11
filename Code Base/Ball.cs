using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simuations
{
    public class Ball
    {
        private Texture2D albedo;
        private Texture2D normal;
        private Vector2 position;

        public Ball(Vector2 pos, GraphicsDevice graphicsDevice)
        {
            position = pos;
            
        }

        public void LoadContent(ContentManager content)
        {
            albedo = content.Load<Texture2D>("RedBall");
            normal = content.Load<Texture2D>("RedBall_n");
        }

        public void Draw(SpriteBatch sb, Effect effect, Light light)
        {
            // apply light parameters once
            light.ApplyToEffect(effect);

            // ensure technique is correct
            effect.CurrentTechnique = effect.Techniques["Lighting"];

            // set albedo and normal maps if parameters exist
            var pAlbedo = effect.Parameters["u_AlbedoMap"];
            if (pAlbedo != null) pAlbedo.SetValue(albedo);
            var pNormal = effect.Parameters["u_NormalMap"];
            if (pNormal != null) pNormal.SetValue(normal);

            effect.CurrentTechnique.Passes[0].Apply();

            sb.Draw(albedo, position, Color.White);
        }
    }
}