using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;
using Pixel_Simulations.Data;

namespace Pixel_Simulations
{
    public class PhysicsManager
    {
        // For this test, we will use simple AABB (Axis-Aligned Bounding Box) collision for performance
        private List<RectangleF> _collisionBounds = new List<RectangleF>();

        public void LoadMapData(Map map)
        {
            if (map == null) return;
            _collisionBounds.Clear();

            // Grab all control layers
            foreach (var layer in map.Layers.OfType<ControlLayer>())
            {
                // Cache Polygons (Shapes)
                    foreach (var shape in layer.Shapes.Where(s => s.Tags.Contains(2)))
                {

                    if (shape != null) _collisionBounds.Add(shape.Shape.GetBounds());
                }

                // Cache Rectangles
                //foreach (var rect in layer.Rectangles)
                //    _collisionBounds.Add(new RectangleF(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y));
            }
        }

        public Vector2 ResolveMovement(RectangleF entityBounds, Vector2 requestedVelocity)
        {
            Vector2 finalVelocity = requestedVelocity;

            // 1. Test X Movement
            RectangleF testBoundsX = new RectangleF(entityBounds.X + requestedVelocity.X, entityBounds.Y, entityBounds.Width, entityBounds.Height);
            if (IsColliding(testBoundsX)) finalVelocity.X = 0;

            // 2. Test Y Movement
            RectangleF testBoundsY = new RectangleF(entityBounds.X, entityBounds.Y + requestedVelocity.Y, entityBounds.Width, entityBounds.Height);
            if (IsColliding(testBoundsY)) finalVelocity.Y = 0;

            return finalVelocity;
        }

        private bool IsColliding(RectangleF bounds)
        {
            foreach (var colBounds in _collisionBounds)
            {
                if (colBounds.Intersects(bounds)) return true;
            }
            return false;
        }
    }
}
