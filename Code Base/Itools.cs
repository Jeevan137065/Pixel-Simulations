using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace Pixel_Simulations
{
    public interface ITool
    {
        string Name { get; }

        // Called once when the mouse button is first pressed
        void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush);

        // Called every frame the mouse is held down and moving
        void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush);

        // Called once when the mouse button is released
        void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush);

    }
}
