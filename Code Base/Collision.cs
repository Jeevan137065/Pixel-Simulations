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

        public void LoadMapData(Map map, PrefabManager prefabManager = null)
        {

            if (map == null) return;
            _collisionBounds.Clear();

            // 1. Control Layer Shapes
            foreach (var layer in map.Layers.OfType<ControlLayer>())
            {
                foreach (var shape in layer.Shapes.Where(s => s.Tags.Contains(SystemTags.SOLID)))
                {
                    if (shape != null) _collisionBounds.Add(shape.Shape.GetBounds());
                }
                foreach (var rect in layer.Rectangles.Where(r => r.Tags.Contains(SystemTags.SOLID)))
                {
                    _collisionBounds.Add(new RectangleF(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y));
                }
            }

            // 2. Automatic Building & Prop Solid Collision
            if (prefabManager != null)
            {
                foreach (var objLayer in map.Layers.OfType<ObjectLayer>().Where(l => l.Type == LayerType.Object))
                {
                    foreach (var prop in objLayer.Objects.OfType<PropObject>())
                    {
                        if (prop.Tags.Contains(SystemTags.SOLID))
                        {
                            var prefab = prefabManager.GetPrefab(prop.PrefabID);
                            if (prefab != null)
                            {
                                string type = prefab.Properties.TryGetValue("Type", out var pt) ? pt.Value : "Prop";

                                if (type == "Building")
                                {
                                    // Footprint collision: span the width, block the lower 32px, leave entrance gap
                                    float bw = prefab.CanvasSize.X * prop.Scale.X;
                                    float bh = 32f * prop.Scale.Y;
                                    float bx = prop.Position.X - prefab.Pivot.X * prop.Scale.X;
                                    float by = prop.Position.Y - bh;

                                    _collisionBounds.Add(new RectangleF(bx, by, bw, bh));
                                }
                                else
                                {
                                    // Standard prop box
                                    float pw = prefab.CanvasSize.X * 0.7f * prop.Scale.X;
                                    float ph = 16f * prop.Scale.Y;
                                    float px = prop.Position.X - (pw / 2f);
                                    float py = prop.Position.Y - ph;
                                    _collisionBounds.Add(new RectangleF(px, py, pw, ph));
                                }
                            }
                        }
                    }
                }
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
