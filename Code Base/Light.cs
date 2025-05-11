// File: Light.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Pixel_Simuations
{
    public class Light
    {
        public Vector2 Position { get; set; }
        public float Intensity { get; set; } = 1f;
        public float Radius { get; set; } = 20f;
        public Color Color { get; set; } = Color.Blue;

        private const int RayCount = 360 / 30;
        private readonly Vector2[] rayEndpoints = new Vector2[RayCount];

        public void CastRays(Func<Vector2, Vector2> clampFunc)
        {
            for (int i = 0; i < RayCount; i++)
            {
                float angle = MathHelper.ToRadians(i * 30);
                Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                rayEndpoints[i] = clampFunc(Position + dir * Radius);
            }
        }

        public void ApplyToEffect(Effect effect)
        {
            effect.Parameters["u_LightPos"].SetValue(new Vector3(Position, 0));
            effect.Parameters["u_LightRadius"].SetValue(Radius);
            effect.Parameters["u_LightIntensity"].SetValue(Intensity);
            effect.Parameters["u_LightColor"].SetValue(Color.ToVector3());
        }

        public void DrawWireframe(SpriteBatch sb, Texture2D pixel)
        {
            for (int i = 0; i < RayCount; i++)
            {
                Vector2 end = rayEndpoints[i];
                float length = Vector2.Distance(Position, end);
                float rotation = (float)Math.Atan2(end.Y - Position.Y, end.X - Position.X);
                sb.Draw(pixel, new Rectangle((int)Position.X, (int)Position.Y, (int)length, 1), null,
                        Color.Yellow, rotation, Vector2.Zero, SpriteEffects.None, 0);
            }
        }
    }
}