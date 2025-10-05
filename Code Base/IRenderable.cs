using Microsoft.Xna.Framework.Graphics;

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
}