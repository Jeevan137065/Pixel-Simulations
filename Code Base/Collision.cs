using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;
using Pixel_Simulations.Data;
using Pixel_Simulations.Code_Base;

namespace Pixel_Simulations
{
    public class PhysicsManager
    {
        private List<ShapeObject> _collisionShapes = new List<ShapeObject>();


        /// <summary>
        /// Reads the map and caches all collision shapes for fast lookups.
        /// </summary>
        public void LoadMapData(Map map)
        {
            if (map == null) return;
            _collisionShapes.Clear();

            foreach (var layer in map.Layers)
            {
                if (layer is CollisionLayer colLayer)
                {
                    _collisionShapes.AddRange(colLayer.CollisionMesh);
                }
            }
        }

        /// <summary>
        /// Attempts to move an entity by a requested velocity. 
        /// Returns the actual velocity allowed after collision resolution.
        /// </summary>
        public Vector2 ResolveMovement(RectangleF entityBounds, Vector2 requestedVelocity)
        {
            // A simple "slide" collision resolution.
            // Test X and Y movement separately.

            Vector2 finalVelocity = requestedVelocity;

            // 1. Test X Movement
            RectangleF testBoundsX = new RectangleF(entityBounds.X + requestedVelocity.X, entityBounds.Y, entityBounds.Width, entityBounds.Height);
            if (IsColliding(testBoundsX))
            {
                finalVelocity.X = 0; // Stop X movement
            }

            // 2. Test Y Movement
            RectangleF testBoundsY = new RectangleF(entityBounds.X, entityBounds.Y + requestedVelocity.Y, entityBounds.Width, entityBounds.Height);
            if (IsColliding(testBoundsY))
            {
                finalVelocity.Y = 0; // Stop Y movement
            }

            return finalVelocity;
        }

        private bool IsColliding(RectangleF bounds)
        {
            // 1. Broad-phase AABB check (Fast)
            var possibleCollisions = _collisionShapes.Where(s => s.Shape.GetBounds().Intersects(bounds));

            // 2. Narrow-phase Polygon check (Slow, only done if AABB intersects)
            foreach (var shapeObj in possibleCollisions)
            {
                // This is a complex geometric problem.
                // Checking if an AABB intersects an arbitrary Polygon requires checking 
                // if any line segment of the AABB intersects any line segment of the Polygon,
                // OR if the AABB is entirely inside the Polygon,
                // OR if the Polygon is entirely inside the AABB.

                // For a simple, robust start, if we assume our collision shapes are mostly
                // rectangular or convex, we can check if the four corners of our bounding box
                // are inside the polygon.

                Vector2 topLeft = new Vector2(bounds.Left, bounds.Top);
                Vector2 topRight = new Vector2(bounds.Right, bounds.Top);
                Vector2 bottomLeft = new Vector2(bounds.Left, bounds.Bottom);
                Vector2 bottomRight = new Vector2(bounds.Right, bounds.Bottom);

                if (shapeObj.Shape.Contains(topLeft) ||
                    shapeObj.Shape.Contains(topRight) ||
                    shapeObj.Shape.Contains(bottomLeft) ||
                    shapeObj.Shape.Contains(bottomRight))
                {
                    return true;
                }

                // Note: A true robust solution requires a library like Velcro Physics or 
                // writing a Separating Axis Theorem (SAT) implementation.
            }

            return false;
        }
    }
}
