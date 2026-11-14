using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public struct PhysicalBarrier
    {
        public RectangleF Bounds;
        public bool IsOneWay;
        public Vector2 AllowedDirection; // Direction player can pass through (Vaulting)
    }
    public class PhysicsManager
    {
        public List<PhysicalBarrier> _barriers = new List<PhysicalBarrier>();

        public void LoadMapData(Map map, PrefabManager prefabManager = null)
        {
            if (map == null) return;
            _barriers.Clear();

            // ==============================================================
            // 1. RECTANGLES WITH EDGE-SELECTIVE SIDES
            // ==============================================================
            foreach (var layer in map.Layers.OfType<ControlLayer>())
            {
                foreach (var rectObj in layer.Rectangles.Where(r => r.HasTag(SystemTags.SOLID)))
                {
                    GenerateRectangleBarriers(rectObj);
                }

                // Polygons / Shapes (Trees, Octagons, Diagonal Fences)
                foreach (var shapeObj in layer.Shapes.Where(s => s.HasTag(SystemTags.SOLID)))
                {
                    GenerateShapeBarriers(shapeObj);
                }
            }

            // ==============================================================
            // 2. AUTOMATIC SOLID PROPS & BUILDINGS
            // ==============================================================
            if (prefabManager != null)
            {
                foreach (var objLayer in map.Layers.OfType<ObjectLayer>().Where(l => l.Type == LayerType.Object))
                {
                    foreach (var prop in objLayer.Objects.OfType<PropObject>().Where(p => p.HasTag(SystemTags.SOLID)))
                    {
                        var prefab = prefabManager.GetPrefab(prop.PrefabID);
                        if (prefab == null) continue;

                        string type = prefab.GetProperty("Type", "Prop");

                        if (type == "Building")
                        {
                            // Automatic bottom footing barrier (leaves top 75% walkable for roof cutaway exploration)
                            float bw = prefab.CanvasSize.X * prop.Scale.X;
                            float bh = 16f * prop.Scale.Y; // 16px bottom footing
                            float bx = prop.Position.X - (prefab.Pivot.X * prop.Scale.X);
                            float by = prop.Position.Y - bh;

                            _barriers.Add(new PhysicalBarrier { Bounds = new RectangleF(bx, by, bw, bh) });
                        }
                        else
                        {
                            // Tree / Rock: circular/octagonal foot collision
                            float pw = prefab.CanvasSize.X * 0.6f * prop.Scale.X;
                            float ph = 12f * prop.Scale.Y;
                            float px = prop.Position.X - (pw / 2f);
                            float py = prop.Position.Y - ph;

                            _barriers.Add(new PhysicalBarrier { Bounds = new RectangleF(px, py, pw, ph) });
                        }
                    }
                }
            }
        }

        private void GenerateRectangleBarriers(RectangleObject rect)
        {
            string sides = rect.GetProperty("CollisionSides", "All").Trim();
            float thick = rect.GetFloat("EdgeThickness", 8f);
            bool canVault = rect.GetBool("CanVault", false);
            string vaultDirStr = rect.GetProperty("VaultDir", "South");

            Vector2 vaultDir = vaultDirStr.ToLower() switch
            {
                "south" => new Vector2(0, 1),
                "north" => new Vector2(0, -1),
                "east" => new Vector2(1, 0),
                "west" => new Vector2(-1, 0),
                _ => Vector2.Zero
            };

            float x = rect.Position.X;
            float y = rect.Position.Y;
            float w = rect.Size.X;
            float h = rect.Size.Y;

            // Full solid box
            if (sides.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                _barriers.Add(new PhysicalBarrier
                {
                    Bounds = new RectangleF(x, y, w, h),
                    IsOneWay = canVault,
                    AllowedDirection = vaultDir
                });
                return;
            }

            // Edge-Selective Slices
            var sideList = sides.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var side in sideList)
            {
                if (side.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
                {
                    // Barrier along the bottom edge (Walk behind the wall)
                    _barriers.Add(new PhysicalBarrier
                    {
                        Bounds = new RectangleF(x, y + h - thick, w, thick),
                        IsOneWay = canVault,
                        AllowedDirection = vaultDir
                    });
                }
                else if (side.Equals("Top", StringComparison.OrdinalIgnoreCase))
                {
                    _barriers.Add(new PhysicalBarrier
                    {
                        Bounds = new RectangleF(x, y, w, thick),
                        IsOneWay = canVault,
                        AllowedDirection = vaultDir
                    });
                }
                else if (side.Equals("Left", StringComparison.OrdinalIgnoreCase))
                {
                    _barriers.Add(new PhysicalBarrier
                    {
                        Bounds = new RectangleF(x, y, thick, h),
                        IsOneWay = canVault,
                        AllowedDirection = vaultDir
                    });
                }
                else if (side.Equals("Right", StringComparison.OrdinalIgnoreCase))
                {
                    _barriers.Add(new PhysicalBarrier
                    {
                        Bounds = new RectangleF(x + w - thick, y, thick, h),
                        IsOneWay = canVault,
                        AllowedDirection = vaultDir
                    });
                }
            }
        }

        private void GenerateShapeBarriers(ShapeObject shape)
        {
            // For custom polygons (e.g. tree base octagon), bind tight bounding box
            var b = shape.Shape.GetBounds();
            string sides = shape.GetProperty("CollisionSides", "All");
            float thick = shape.GetFloat("EdgeThickness", 8f);

            if (sides.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
            {
                _barriers.Add(new PhysicalBarrier { Bounds = new RectangleF(b.X, b.Bottom - thick, b.Width, thick) });
            }
            else
            {
                _barriers.Add(new PhysicalBarrier { Bounds = b });
            }
        }

        public Vector2 ResolveMovement(RectangleF entityBounds, Vector2 requestedVelocity)
        {
            Vector2 finalVelocity = requestedVelocity;

            // 1. Test Horizontal (X) Movement
            RectangleF testBoundsX = new RectangleF(entityBounds.X + requestedVelocity.X, entityBounds.Y, entityBounds.Width, entityBounds.Height);
            if (IsColliding(testBoundsX, new Vector2(requestedVelocity.X, 0)))
                finalVelocity.X = 0;

            // 2. Test Vertical (Y) Movement
            RectangleF testBoundsY = new RectangleF(entityBounds.X, entityBounds.Y + requestedVelocity.Y, entityBounds.Width, entityBounds.Height);
            if (IsColliding(testBoundsY, new Vector2(0, requestedVelocity.Y)))
                finalVelocity.Y = 0;

            return finalVelocity;
        }
        public void LoadModularBuildingBarriers(ModularBuildingInstance building)
        {
            if (building?.BlueprintRef == null) return;

            var groundFloor = building.BlueprintRef.floors.FirstOrDefault(f => f.level == 0);
            if (groundFloor == null) return;

            Vector2 bldgOrigin = building.Position;

            foreach (var wall in groundFloor.edgeStrips)
            {
                // Only exterior walls (Rank 0) and non-doors block the player
                if (wall.depthRank == 0 && wall.type != "door")
                {
                    float x1 = bldgOrigin.X + (wall.x * 16f);
                    float y1 = bldgOrigin.Y + (wall.y * 16f);
                    float x2 = bldgOrigin.X + ((wall.x2 != 0 ? wall.x2 : wall.x + wall.widthUnits) * 16f);
                    float y2 = bldgOrigin.Y + ((wall.y2 != 0 ? wall.y2 : wall.y) * 16f);

                    // Create a physical barrier along the base of the wall (8px thick)
                    float minX = Math.Min(x1, x2);
                    float maxX = Math.Max(x1, x2);
                    float minY = Math.Min(y1, y2);
                    float maxY = Math.Max(y1, y2);

                    _barriers.Add(new PhysicalBarrier
                    {
                        Bounds = new RectangleF(minX, maxY - 8f, Math.Max(16f, maxX - minX), 8f)
                    });
                }
            }
        }
        private bool IsColliding(RectangleF bounds, Vector2 moveDir)
        {
            for (int i = 0; i < _barriers.Count; i++)
            {
                var barrier = _barriers[i];
                if (barrier.Bounds.Intersects(bounds))
                {
                    // If vaulting barrier, check if moving in the allowed direction
                    if (barrier.IsOneWay && barrier.AllowedDirection != Vector2.Zero)
                    {
                        float dot = Vector2.Dot(Vector2.Normalize(moveDir), barrier.AllowedDirection);
                        if (dot > 0.7f)
                        {
                            return false; // Permitted to pass (Vaulting over fence)
                        }
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
