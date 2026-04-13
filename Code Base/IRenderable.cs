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


    public struct RenderableSprite
    {
        public string AtlasName;
        public Texture2D Texture;
        public Vector2 Position;
        public Rectangle SourceRect;
        public Vector2 Origin;
        public Vector2 Scale;
        public float Rotation;

        // --- DEPTH DATA ---
        // Used for standard SpriteBatch Y-Sorting (0.0 to 1.0)
        public float DrawDepth;

        // Used for Volumetric Fog/Shadows. The exact physical Y coordinate where it touches the ground.
        public float BaseWorldY;
        // 1.0f = Affected by Parallax (Trees/Props)
        // 0.0f = Ignores Parallax (Player/NPCs)
        public float ParallaxMask;

    }
}


