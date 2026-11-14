// Inside Data/ModularBuildingRenderer.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations.Data
{
    public struct RatioSpec
    {
        public int Run;
        public int Rise;
        public float Slope;
        public int DefaultW;
        public int DefaultH;
    }

    public struct CelestialState
    {
        public bool IsDay;
        public Vector3 LightDirection;
        public Color LightColor;
        public float Ambient;
    }
    public static class ModularBuildingRenderer
    {
        public const int CELL_SIZE = 16;
        public const float STORY_HEIGHT_UNITS = 6.0f; // 6u = 96px
        private static Texture2D _missingCheckerTex;
        private class CachedPolygonTexture
        {
            public Texture2D Texture;
            public Vector2 DrawOffset;
        }

        private static readonly Dictionary<string, CachedPolygonTexture> _roofACache = new();
        private static readonly Dictionary<string, CachedPolygonTexture> _roofBCache = new();
        private static readonly Dictionary<string, CachedPolygonTexture> _slopeWallCache = new();
        private static readonly Dictionary<string, Texture2D> _exteriorProxyCache = new();
        private static readonly Dictionary<string, RectangleF> _tightBoundsCache = new();

        public static readonly Dictionary<string, RatioSpec> RatioSpecs = new(StringComparer.OrdinalIgnoreCase)
        {
            { "flat", new RatioSpec { Run = 1, Rise = 0, Slope = 0.0f,    DefaultW = 16, DefaultH = 16 } },
            { "1:1",  new RatioSpec { Run = 1, Rise = 1, Slope = 1.0f,    DefaultW = 16, DefaultH = 32 } },
            { "1:2",  new RatioSpec { Run = 1, Rise = 2, Slope = 2.0f,    DefaultW = 16, DefaultH = 48 } },
            { "2:1",  new RatioSpec { Run = 2, Rise = 1, Slope = 0.5f,    DefaultW = 32, DefaultH = 32 } },
            { "1:3",  new RatioSpec { Run = 1, Rise = 3, Slope = 3.0f,    DefaultW = 16, DefaultH = 64 } },
            { "3:1",  new RatioSpec { Run = 3, Rise = 1, Slope = 0.3333f, DefaultW = 48, DefaultH = 32 } },
            { "2:3",  new RatioSpec { Run = 2, Rise = 3, Slope = 1.5f,    DefaultW = 32, DefaultH = 64 } },
            { "3:2",  new RatioSpec { Run = 3, Rise = 2, Slope = 0.6667f, DefaultW = 48, DefaultH = 48 } }
        };

        // =====================================================================
        // 1. TIGHT BOUNDING BOX CALCULATOR (Ignores Empty Grid Cells)
        // =====================================================================
        public static RectangleF GetBuildingTightBounds(BuildingData blueprint)
        {
            if (blueprint == null || blueprint.floors == null || blueprint.floors.Count == 0)
                return new RectangleF(0, 0, 64, 64);

            if (_tightBoundsCache.TryGetValue(blueprint.id, out var cachedBounds))
                return cachedBounds;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool hasGeometry = false;

            void Expand(float x, float y)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                hasGeometry = true;
            }

            for (int fIdx = 0; fIdx < blueprint.floors.Count; fIdx++)
            {
                var floor = blueprint.floors[fIdx];
                float shiftY = fIdx * STORY_HEIGHT_UNITS * CELL_SIZE;

                foreach (var fp in floor.floorPlates)
                {
                    Expand(fp.x * CELL_SIZE, fp.y * CELL_SIZE - shiftY);
                    Expand((fp.x + fp.w) * CELL_SIZE, (fp.y + fp.h) * CELL_SIZE - shiftY);
                }

                foreach (var w in floor.edgeStrips)
                {
                    float x1 = w.x * CELL_SIZE;
                    float y1 = w.y * CELL_SIZE - shiftY;
                    float x2 = (w.x2 != 0 ? w.x2 : w.x + w.widthUnits) * CELL_SIZE;
                    float y2 = (w.y2 != 0 ? w.y2 : w.y) * CELL_SIZE - shiftY;
                    float hPx = w.heightUnits * CELL_SIZE;

                    Expand(x1, y1);
                    Expand(x2, y2);
                    Expand(x1, y1 - hPx);
                    Expand(x2, y2 - hPx);
                }

                foreach (var trim in floor.auxiliaries)
                {
                    float x1 = trim.x * CELL_SIZE;
                    float y1 = trim.y * CELL_SIZE - shiftY;
                    float x2 = (trim.x2 != 0 ? trim.x2 : trim.x + trim.widthUnits) * CELL_SIZE;
                    float y2 = (trim.y2 != 0 ? trim.y2 : trim.y) * CELL_SIZE - shiftY;
                    float hPx = trim.heightUnits * CELL_SIZE;

                    Expand(x1, y1);
                    Expand(x2, y2);
                    Expand(x1, y1 - hPx);
                    Expand(x2, y2 - hPx);
                }

                foreach (var r in floor.roofs)
                {
                    float baseZ = (r.roofY * CELL_SIZE) + shiftY;
                    var spec = RatioSpecs.TryGetValue(r.slopeRatio ?? "1:1", out var sp) ? sp : RatioSpecs["1:1"];

                    if (r.roofType == "A")
                    {
                        float x1 = r.x * CELL_SIZE;
                        float x2 = (r.x + r.w) * CELL_SIZE;
                        float y1 = r.y * CELL_SIZE;
                        float y2 = (r.y + r.h) * CELL_SIZE;
                        float deltaZ = r.w * spec.Slope * CELL_SIZE;

                        Expand(x1, y1 - baseZ - (r.tiltEdge == "E" ? deltaZ : 0));
                        Expand(x2, y1 - baseZ - (r.tiltEdge == "W" ? deltaZ : 0));
                        Expand(x1, y2 - baseZ - (r.tiltEdge == "E" ? deltaZ : 0));
                        Expand(x2, y2 - baseZ - (r.tiltEdge == "W" ? deltaZ : 0));
                    }
                    else if (r.roofType == "B" && r.points != null && r.points.Count > 0)
                    {
                        bool isSouth = r.tiltEdge == "S" && r.slopeRatio != "flat";
                        float maxRoofY = r.points.Max(pt => pt.y);
                        foreach (var pt in r.points)
                        {
                            float z = baseZ + (isSouth ? (maxRoofY - pt.y) * spec.Slope * CELL_SIZE : 0f);
                            Expand(pt.x * CELL_SIZE, (pt.y * CELL_SIZE) - z);
                        }
                    }
                }
            }

            RectangleF result = hasGeometry
                ? new RectangleF(minX, minY, maxX - minX, maxY - minY)
                : new RectangleF(0, 0, blueprint.gridDimension * CELL_SIZE, blueprint.gridDimension * CELL_SIZE);

            _tightBoundsCache[blueprint.id] = result;
            return result;
        }

        // =====================================================================
        // 2. FAST EXTERIOR DRAW (Used by Editor, Placement Preview & Captures)
        // =====================================================================
        public static void DrawBuildingExterior(SpriteBatch sb, BuildingData blueprint, Vector2 position, IAtlasProvider assets, Color tint)
        {
            if (blueprint == null || blueprint.floors.Count == 0) return;

            if (!_exteriorProxyCache.TryGetValue(blueprint.id, out var proxyTex) || proxyTex == null || proxyTex.IsDisposed)
            {
                proxyTex = BakeBuildingProxy(sb.GraphicsDevice, blueprint, assets);
                if (proxyTex != null) _exteriorProxyCache[blueprint.id] = proxyTex;
            }

            if (proxyTex != null)
            {
                int extraPixelH = (int)(blueprint.floors.Count * STORY_HEIGHT_UNITS * CELL_SIZE);
                sb.Draw(proxyTex, new Vector2(position.X, position.Y - extraPixelH), tint);
            }
        }

        public static void ClearCache()
        {
            foreach (var c in _roofACache.Values) c.Texture?.Dispose();
            _roofACache.Clear();

            foreach (var c in _roofBCache.Values) c.Texture?.Dispose();
            _roofBCache.Clear();

            foreach (var c in _slopeWallCache.Values) c.Texture?.Dispose();
            _slopeWallCache.Clear();

            foreach (var t in _exteriorProxyCache.Values) t?.Dispose();
            _exteriorProxyCache.Clear();

            _tightBoundsCache.Clear();
        }

        // =====================================================================
        // 3. FACADE PROXY BAKER (Renders ONLY Exterior R0 Walls, Trims & Roofs)
        // =====================================================================
        public static RenderTarget2D BakeBuildingProxy(GraphicsDevice gd, BuildingData building, IAtlasProvider assets)
        {
            if (building == null || building.floors.Count == 0) return null;

            int dim = building.gridDimension;
            int maxStories = building.floors.Count;
            int extraPixelH = (int)(maxStories * STORY_HEIGHT_UNITS * CELL_SIZE);

            int width = dim * CELL_SIZE;
            int height = (dim * CELL_SIZE) + extraPixelH;

            var proxyTarget = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            var prevTargets = gd.GetRenderTargets();

            gd.SetRenderTarget(proxyTarget);
            gd.Clear(Color.Transparent);

            using (var sb = new SpriteBatch(gd))
            {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                for (int fIdx = 0; fIdx < building.floors.Count; fIdx++)
                {
                    var floor = building.floors[fIdx];
                    float verticalShiftPixels = fIdx * STORY_HEIGHT_UNITS * CELL_SIZE;
                    float baseOffsetY = extraPixelH - verticalShiftPixels;

                    foreach (var wall in floor.edgeStrips.Where(w => w.depthRank == 0))
                        DrawWall(sb, wall, Vector2.Zero, baseOffsetY, assets);

                    foreach (var trim in floor.auxiliaries.Where(a => a.depthRank == 0))
                        DrawTrim(sb, trim, Vector2.Zero, baseOffsetY, assets);

                    foreach (var roof in floor.roofs)
                    {
                        if (roof.roofType == "A")
                            DrawRoofA(sb, roof, Vector2.Zero, baseOffsetY, assets, Color.White);
                        else
                            DrawRoofB(sb, roof, Vector2.Zero, baseOffsetY, assets, Color.White);
                    }
                }

                sb.End();
            }

            gd.SetRenderTargets(prevTargets);
            return proxyTarget;
        }

        // =====================================================================
        // 4. WALLS (Supports Sheared Sloped Gables + Flat Walls)
        // =====================================================================
        public static void DrawWall(SpriteBatch sb, WallComponentData wall, Vector2 origin, float baseOffsetY, IAtlasProvider assets)
        {
            var tex = assets.GetAtlas(wall.atlas ?? "walls") ?? assets.GetAtlas("Building");
            if (tex == null || wall.rect == null) return;

            if (!wall.isSlope)
            {
                float x1 = origin.X + (wall.x * CELL_SIZE);
                float y1 = origin.Y + (wall.y * CELL_SIZE) + baseOffsetY;
                float currentHeight = wall.heightUnits * CELL_SIZE;
                float sw = wall.widthUnits * CELL_SIZE;

                Rectangle src = wall.rect.ToRectangle();
                Vector2 pos = new Vector2(x1, y1 - currentHeight);
                SpriteEffects flip = wall.isMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                sb.Draw(tex, new Rectangle((int)pos.X, (int)pos.Y, (int)sw, (int)currentHeight), src, Color.White, 0f, Vector2.Zero, flip, 0f);
            }
            else
            {
                // Polygon-clipped sloped wall
                string cacheKey = $"{wall.id}_{wall.x}_{wall.y}_{wall.x2}_{wall.y2}_{wall.heightUnits}_{wall.isMirrored}";
                if (!_slopeWallCache.TryGetValue(cacheKey, out var cached) || cached.Texture == null || cached.Texture.IsDisposed)
                {
                    cached = BakeSlopedWallTexture(sb.GraphicsDevice, wall, tex);
                    if (cached != null) _slopeWallCache[cacheKey] = cached;
                }

                if (cached != null && cached.Texture != null)
                {
                    sb.Draw(cached.Texture, origin + new Vector2(cached.DrawOffset.X, cached.DrawOffset.Y + baseOffsetY), Color.White);
                }
            }
        }

        private static CachedPolygonTexture BakeSlopedWallTexture(GraphicsDevice gd, WallComponentData s, Texture2D atlas)
        {
            float x1 = s.x * CELL_SIZE, y1 = s.y * CELL_SIZE;
            float x2 = (s.x2 != 0 ? s.x2 : s.x + s.widthUnits) * CELL_SIZE;
            float y2 = (s.y2 != 0 ? s.y2 : s.y) * CELL_SIZE;
            float h = s.heightUnits * CELL_SIZE;

            var verts = new List<Vector2>
            {
                new Vector2(x1, y1),
                new Vector2(x2, y2),
                new Vector2(x2, y2 - h),
                new Vector2(x1, y1 - h)
            };

            float minX = verts.Min(v => v.X);
            float maxX = verts.Max(v => v.X);
            float minY = verts.Min(v => v.Y);
            float maxY = verts.Max(v => v.Y);

            int width = Math.Max(1, (int)Math.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)Math.Ceiling(maxY - minY));

            var localVerts = verts.Select(v => new Vector2(v.X - minX, v.Y - minY)).ToList();

            Rectangle src = s.rect.ToRectangle();
            Color[] srcPixels = new Color[src.Width * src.Height];
            atlas.GetData(0, src, srcPixels, 0, srcPixels.Length);

            Color[] outPixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsPointInPolygon(x + 0.5f, y + 0.5f, localVerts))
                    {
                        int sx = (int)((x / (float)width) * src.Width);
                        int sy = (int)((y / (float)height) * src.Height);
                        if (s.isMirrored) sx = (src.Width - 1) - sx;

                        sx = Math.Clamp(sx, 0, src.Width - 1);
                        sy = Math.Clamp(sy, 0, src.Height - 1);
                        outPixels[y * width + x] = srcPixels[sy * src.Width + sx];
                    }
                }
            }

            Texture2D tex = new Texture2D(gd, width, height);
            tex.SetData(outPixels);
            return new CachedPolygonTexture { Texture = tex, DrawOffset = new Vector2(minX, minY) };
        }

        // =====================================================================
        // 5. FIXED ROOF A: SHEARED PARALLELOGRAM CLIPPING (Matches BuilderRender.js)
        // =====================================================================
        public static void DrawRoofA(SpriteBatch sb, RoofComponentData roof, Vector2 origin, float baseOffsetY, IAtlasProvider assets, Color tint)
        {
            var fam = BuildingManager.Instance.GetRoofFamily(roof.family);
            var atlas = assets.GetAtlas("Roofs") ?? assets.GetAtlas("Building");
            string cacheKey = $"{roof.id}_{roof.family}_{roof.x}_{roof.y}_{roof.w}_{roof.h}_{roof.roofY}_{roof.tiltEdge}_{roof.slopeRatio}_{roof.fillPattern}_{(atlas != null ? 1 : 0)}";
            if (!_roofACache.TryGetValue(cacheKey, out var cached) || cached.Texture == null || cached.Texture.IsDisposed)
            {
                cached = BakeRoofAParallelogramTexture(sb.GraphicsDevice, roof, fam, atlas);
                if (cached != null) _roofACache[cacheKey] = cached;
            }

            if (cached != null && cached.Texture != null)
            {
                sb.Draw(cached.Texture, origin + new Vector2(cached.DrawOffset.X, cached.DrawOffset.Y + baseOffsetY), tint);
            }
        }

        private static CachedPolygonTexture BakeRoofAParallelogramTexture(GraphicsDevice gd, RoofComponentData roof, RoofFamilyData fam, Texture2D atlas)
        {
            string ratioKey = roof.slopeRatio ?? "1:1";
            var spec = RatioSpecs.TryGetValue(ratioKey, out var s) ? s : RatioSpecs["1:1"];

            float baseZ = roof.roofY * CELL_SIZE;
            float deltaZ = roof.w * spec.Slope * CELL_SIZE;
            float x1 = roof.x * CELL_SIZE, y1 = roof.y * CELL_SIZE;
            float w = roof.w * CELL_SIZE, h = roof.h * CELL_SIZE;

            float zNW = baseZ, zNE = baseZ, zSE = baseZ, zSW = baseZ;
            if (roof.tiltEdge == "W") { zNE += deltaZ; zSE += deltaZ; }
            else if (roof.tiltEdge == "E") { zNW += deltaZ; zSW += deltaZ; }

            // Exact parallelogram clipping polygon matching BuilderRender.js
            var screenVerts = new List<Vector2>
            {
                new Vector2(x1,     y1 - zNW),
                new Vector2(x1 + w, y1 - zNE),
                new Vector2(x1 + w, y1 + h - zSE),
                new Vector2(x1,     y1 + h - zSW)
            };

            float minX = screenVerts.Min(v => v.X);
            float maxX = screenVerts.Max(v => v.X);
            float minY = screenVerts.Min(v => v.Y);
            float maxY = screenVerts.Max(v => v.Y);

            int width = Math.Max(1, (int)Math.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)Math.Ceiling(maxY - minY));

            var localVerts = screenVerts.Select(v => new Vector2(v.X - minX, v.Y - minY)).ToList();

            string slotKeyA = $"angled_{ratioKey}_A";
            string slotKeyB = $"angled_{ratioKey}_B";
            bool hasB = fam.angleConfig != null && fam.angleConfig.ContainsKey(ratioKey) && fam.angleConfig[ratioKey].hasB;

            RectangleData pieceA = fam.pieces.TryGetValue(slotKeyA, out var pA) ? pA : null;
            RectangleData pieceB = fam.pieces.TryGetValue(slotKeyB, out var pB) ? pB : null;
            if (pieceA == null) return null;

            Color[] pixelsA = new Color[pieceA.w * pieceA.h];
            atlas.GetData(0, pieceA.ToRectangle(), pixelsA, 0, pixelsA.Length);

            Color[] pixelsB = null;
            if (pieceB != null)
            {
                pixelsB = new Color[pieceB.w * pieceB.h];
                atlas.GetData(0, pieceB.ToRectangle(), pixelsB, 0, pixelsB.Length);
            }

            Color[] outPixels = new Color[width * height];
            bool isEast = (roof.tiltEdge == "E");
            int strideU = spec.Run;
            int pieceH = spec.DefaultH;
            int extraOverhangUnits = (int)Math.Ceiling(pieceH / 16.0) + 1;

            int colIndex = 0;
            for (float u = 0; u < roof.w; u += strideU)
            {
                int rowIndex = 0;
                for (float v = -extraOverhangUnits; v <= roof.h + extraOverhangUnits; v++)
                {
                    float z = baseZ;
                    if (roof.tiltEdge == "W") z += u * spec.Slope * CELL_SIZE;
                    else if (roof.tiltEdge == "E") z += (roof.w - u - strideU) * spec.Slope * CELL_SIZE;

                    int destX = (int)Math.Round(((roof.x + u) * CELL_SIZE) - minX);
                    int destY = (int)Math.Round(((roof.y + v) * CELL_SIZE) - z - minY);

                    bool useB = false;
                    if (hasB && pixelsB != null)
                    {
                        if (roof.fillPattern == "aligned") useB = (colIndex % 2 == 1);
                        else if (roof.fillPattern == "staggered") useB = ((colIndex + rowIndex) % 2 == 1);
                    }

                    var activePiece = useB ? pieceB : pieceA;
                    var activePixels = useB ? pixelsB : pixelsA;

                    // Blit pixels clipped to the parallelogram
                    for (int py = 0; py < activePiece.h; py++)
                    {
                        int outY = destY + py;
                        if (outY < 0 || outY >= height) continue;

                        for (int px = 0; px < activePiece.w; px++)
                        {
                            int outX = destX + px;
                            if (outX < 0 || outX >= width) continue;

                            if (IsPointInPolygon(outX + 0.5f, outY + 0.5f, localVerts))
                            {
                                int srcX = isEast ? (activePiece.w - 1 - px) : px;
                                Color c = activePixels[py * activePiece.w + srcX];
                                if (c.A > 0) outPixels[outY * width + outX] = c;
                            }
                        }
                    }

                    rowIndex++;
                }
                colIndex++;
            }

            Texture2D tex = new Texture2D(gd, width, height);
            tex.SetData(outPixels);
            return new CachedPolygonTexture { Texture = tex, DrawOffset = new Vector2(minX, minY) };
        }

        // =====================================================================
        // 6. FIXED ROOF B: POLYGON CLIPPED & EXACT TILING ALIGNMENT
        // =====================================================================
        public static void DrawRoofB(SpriteBatch sb, RoofComponentData roof, Vector2 origin, float baseOffsetY, IAtlasProvider assets, Color tint)
        {
            if (roof.points == null || roof.points.Count < 3) return;

            var fam = BuildingManager.Instance.GetRoofFamily(roof.family);
            var atlas = assets.GetAtlas("Roofs") ?? assets.GetAtlas("Building");
            var flatPiece = fam?.flatPieces?.Find(fp => fp.id == roof.flatPieceId) ?? fam?.flatPieces?.FirstOrDefault();

            string cacheKey = $"{roof.id}_{roof.family}_{roof.flatPieceId}_{roof.roofY}_{roof.tiltEdge}_{roof.slopeRatio}_{roof.points.Count}";

            if (!_roofBCache.TryGetValue(cacheKey, out var cached) || cached.Texture == null || cached.Texture.IsDisposed)
            {
                cached = BakeRoofBPolygonTexture(sb.GraphicsDevice, roof, flatPiece, atlas);
                if (cached != null) _roofBCache[cacheKey] = cached;
            }

            if (cached != null && cached.Texture != null)
            {
                sb.Draw(cached.Texture, origin + new Vector2(cached.DrawOffset.X, cached.DrawOffset.Y + baseOffsetY), tint);
            }
        }

        private static CachedPolygonTexture BakeRoofBPolygonTexture(GraphicsDevice gd, RoofComponentData roof, FlatPieceData flatPiece, Texture2D atlas)
        {
            bool isSouth = roof.tiltEdge == "S" && roof.slopeRatio != "flat";
            float slope = isSouth && RatioSpecs.TryGetValue(roof.slopeRatio ?? "1:1", out var r) ? r.Slope : 0f;
            float baseZ = roof.roofY * CELL_SIZE;
            float maxY = roof.points.Max(pt => pt.y);

            var screenVerts = new List<Vector2>();
            foreach (var pt in roof.points)
            {
                float z = baseZ + (isSouth ? (maxY - pt.y) * slope * CELL_SIZE : 0f);
                screenVerts.Add(new Vector2(pt.x * CELL_SIZE, (pt.y * CELL_SIZE) - z));
            }

            float minX = screenVerts.Min(v => v.X);
            float maxX = screenVerts.Max(v => v.X);
            float minY = screenVerts.Min(v => v.Y);
            float maxYScreen = screenVerts.Max(v => v.Y);

            int width = Math.Max(1, (int)Math.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)Math.Ceiling(maxYScreen - minY));

            var localVerts = screenVerts.Select(v => new Vector2(v.X - minX, v.Y - minY)).ToList();

            Color[] pieceData = null;
            int pw = flatPiece?.w ?? 16;
            int ph = flatPiece?.h ?? 16;

            if (atlas != null && flatPiece != null && flatPiece.isBound)
            {
                Rectangle src = new Rectangle(flatPiece.x, flatPiece.y, pw, ph);
                if (src.Right <= atlas.Width && src.Bottom <= atlas.Height)
                {
                    pieceData = new Color[pw * ph];
                    atlas.GetData(0, src, pieceData, 0, pieceData.Length);
                }
            }

            Color[] outPixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsPointInPolygon(x + 0.5f, y + 0.5f, localVerts))
                    {
                        if (pieceData != null)
                        {
                            // Tiling anchored directly to the top-left of the roof bounding box (Matches BuilderRender.js)
                            int tx = x % pw;
                            int ty = y % ph;
                            outPixels[y * width + x] = pieceData[ty * pw + tx];
                        }
                        else
                        {
                            bool mag = ((x / 4) + (y / 4)) % 2 == 0;
                            outPixels[y * width + x] = mag ? Color.Magenta : Color.Black;
                        }
                    }
                }
            }

            Texture2D tex = new Texture2D(gd, width, height);
            tex.SetData(outPixels);
            return new CachedPolygonTexture { Texture = tex, DrawOffset = new Vector2(minX, minY) };
        }

        private static bool IsPointInPolygon(float px, float py, List<Vector2> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].Y > py) != (poly[j].Y > py)) &&
                    (px < (poly[j].X - poly[i].X) * (py - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // =====================================================================
        // 7. TRIMS & FLOOR PLATES
        // =====================================================================
        public static void DrawTrim(SpriteBatch sb, AuxComponentData trim, Vector2 origin, float baseOffsetY, IAtlasProvider assets)
        {
            var tex = assets.GetAtlas(trim.atlas ?? "walls") ?? assets.GetAtlas("Building");
            if (tex == null || trim.rect == null) return;

            float x1 = origin.X + (trim.x * CELL_SIZE);
            float y1 = origin.Y + (trim.y * CELL_SIZE) + baseOffsetY;
            float currentHeight = trim.heightUnits * CELL_SIZE;
            float sw = trim.widthUnits * CELL_SIZE;

            Rectangle src = trim.rect.ToRectangle();
            Vector2 pos = new Vector2(x1, y1 - currentHeight);
            sb.Draw(tex, new Rectangle((int)pos.X, (int)pos.Y, (int)sw, (int)currentHeight), src, Color.White);
        }

        public static void DrawFloorPlates(SpriteBatch sb, FloorData floor, Vector2 origin, float baseOffsetY, IAtlasProvider assets)
        {
            var tex = assets.GetAtlas("floors");

            foreach (var fp in floor.floorPlates)
            {
                float x = origin.X + (fp.x * CELL_SIZE);
                float y = origin.Y + (fp.y * CELL_SIZE) + baseOffsetY;
                float w = fp.w * CELL_SIZE;
                float h = fp.h * CELL_SIZE;
                Rectangle dest = new Rectangle((int)x, (int)y, (int)w, (int)h);

                if (tex == null)
                {
                    DrawTiledChecker(sb, dest, Color.White);
                    continue;
                }

                for (float py = y; py < y + h; py += 32)
                {
                    for (float px = x; px < x + w; px += 32)
                    {
                        float drawW = Math.Min(32, (x + w) - px);
                        float drawH = Math.Min(32, (y + h) - py);
                        sb.Draw(tex, new Rectangle((int)px, (int)py, (int)drawW, (int)drawH), new Rectangle(0, 0, (int)drawW, (int)drawH), Color.White);
                    }
                }
            }
        }

        // 8. 24-HOUR CELESTIAL LIGHTING CALCULATOR (Matches BuilderRender.js)
        public static CelestialState GetCelestialState(float timeOfDay)
        {
            bool isDay = (timeOfDay >= 6f && timeOfDay < 18f);
            float lx = 0f, ly = 0.25f, lz = 1.0f;
            Color lightColor = new Color(255, 245, 220);
            float ambient = 0.35f;

            if (isDay)
            {
                float prog = (timeOfDay - 6f) / 12f;
                float angle = prog * MathHelper.Pi;
                lx = -MathF.Cos(angle);
                lz = Math.Max(0.1f, MathF.Sin(angle));

                // Dawn / Dusk Golden Hour tint
                if (prog < 0.15f || prog > 0.85f)
                {
                    lightColor = new Color(255, 140, 80);
                    ambient = 0.20f;
                }
            }
            else
            {
                float prog = (timeOfDay < 6f ? timeOfDay + 6f : timeOfDay - 18f) / 12f;
                float angle = prog * MathHelper.Pi;
                lx = -MathF.Cos(angle);
                lz = Math.Max(0.1f, MathF.Sin(angle));
                lightColor = new Color(140, 180, 255);
                ambient = 0.14f;
            }

            Vector3 dir = Vector3.Normalize(new Vector3(lx, ly, lz));
            return new CelestialState { IsDay = isDay, LightDirection = dir, LightColor = lightColor, Ambient = ambient };
        }

        public static Color GetFacetTint(Vector3 normal, CelestialState celestial, float shadowExp = 1.0f, float alpha = 1.0f)
        {
            float diffuse = Math.Max(0f, Vector3.Dot(normal, celestial.LightDirection));
            float maxDrop = 0.25f * MathF.Pow(2f, shadowExp);
            float minBrightness = Math.Max(0.05f, 1.0f - maxDrop);

            float brightness = celestial.IsDay
                ? Math.Max(minBrightness, 1.0f - (maxDrop * (1.0f - diffuse)))
                : minBrightness * 0.75f;

            brightness = Math.Clamp(brightness, 0.05f, 1.0f);

            // Blend gently with sun/moon color
            Vector3 colorVec = Vector3.Lerp(Vector3.One, celestial.LightColor.ToVector3(), celestial.IsDay ? 0.30f : 0.55f);
            return new Color(colorVec * brightness) * alpha;
        }

        private static Vector3 GetRoofNormal(RoofComponentData roof)
        {
            var spec = RatioSpecs.TryGetValue(roof.slopeRatio ?? "1:1", out var s) ? s : RatioSpecs["1:1"];
            float rad = MathF.Atan(spec.Slope);
            float sinTilt = MathF.Sin(rad);
            float cosTilt = MathF.Cos(rad);

            if (roof.roofType == "B")
            {
                return roof.tiltEdge == "S" ? new Vector3(0f, sinTilt, cosTilt) : Vector3.UnitZ;
            }
            else
            {
                return roof.tiltEdge == "E" ? new Vector3(sinTilt, 0f, cosTilt) : new Vector3(-sinTilt, 0f, cosTilt);
            }
        }
        // 9. RUNTIME GAME RENDERER: FLOOR PLATES (Drawn on Albedo Ground Layer)

        public static void DrawBuildingFloorPlates(SpriteBatch sb, ModularBuildingInstance bldg, IAtlasProvider assets)
        {
            var blueprint = bldg.BlueprintRef;
            if (blueprint == null || blueprint.floors.Count == 0) return;

            int storyIdx = Math.Clamp(bldg.ActiveStory, 0, blueprint.floors.Count - 1);
            DrawFloorPlates(sb, blueprint.floors[storyIdx], bldg.Position, 0f, assets);
        }

        // =====================================================================
        // 10. RUNTIME GAME RENDERER: DYNAMIC WALLS, TRIMS & CELESTIAL ROOFS
        // =====================================================================
        public static void DrawBuildingDynamicGame(SpriteBatch sb, ModularBuildingInstance bldg, IAtlasProvider assets, CelestialState celestial, float playerFootY)
        {
            var blueprint = bldg.BlueprintRef;
            if (blueprint == null || blueprint.floors.Count == 0)
            {
                // Draw a 64x64 Black & Pink checkerboard if the blueprint itself wasn't found!
                DrawTiledChecker(sb, new Rectangle((int)bldg.Position.X, (int)bldg.Position.Y, 64, 64), Color.White);
                return;
            }

            // 1. IF FULLY OUTSIDE (Fast 1-Draw-Call Path with Celestial Ambient Tint)
            if (!bldg.IsPlayerInside && bldg.RoofAlpha >= 0.99f)
            {
                // Render multi-story shell with live celestial shading per roof/wall facet
                for (int fIdx = 0; fIdx < blueprint.floors.Count; fIdx++)
                {
                    var floor = blueprint.floors[fIdx];
                    float shiftY = -(fIdx * STORY_HEIGHT_UNITS * CELL_SIZE);

                    // Exterior Rank 0 Walls (Normal points South: +Y)
                    Color wallTint = GetFacetTint(new Vector3(0f, 0.85f, 0.52f), celestial);
                    foreach (var wall in floor.edgeStrips.Where(w => w.depthRank == 0))
                        DrawWallTinted(sb, wall, bldg.Position, shiftY, assets, wallTint);

                    foreach (var trim in floor.auxiliaries.Where(a => a.depthRank == 0))
                        DrawTrimTinted(sb, trim, bldg.Position, shiftY, assets, wallTint);

                    // Roofs with real-time directional Sun/Moon facet shading!
                    foreach (var roof in floor.roofs)
                    {
                        Color roofTint = GetFacetTint(GetRoofNormal(roof), celestial);
                        if (roof.roofType == "A")
                            DrawRoofA(sb, roof, bldg.Position, shiftY, assets, roofTint);
                        else
                            DrawRoofB(sb, roof, bldg.Position, shiftY, assets, roofTint);
                    }
                }
                return;
            }

            // 2. INSIDE OR FADING TRANSITION: Render Active Story Interior (Rank 1+) + Fading Roof
            int storyIdx = Math.Clamp(bldg.ActiveStory, 0, blueprint.floors.Count - 1);
            var activeFloor = blueprint.floors[storyIdx];
            Color intWallTint = GetFacetTint(new Vector3(0f, 0.7f, 0.7f), celestial);

            // Interior Walls (Rank 1+)
            foreach (var wall in activeFloor.edgeStrips.Where(w => w.depthRank > 0))
                DrawWallTinted(sb, wall, bldg.Position, 0f, assets, intWallTint);

            // Interior Trims (Rank 1+)
            foreach (var trim in activeFloor.auxiliaries.Where(a => a.depthRank > 0))
                DrawTrimTinted(sb, trim, bldg.Position, 0f, assets, intWallTint);

            // Fading Exterior Roofs & Rank 0 Fronts
            if (bldg.RoofAlpha > 0.02f)
            {
                Color extWallTint = GetFacetTint(new Vector3(0f, 0.85f, 0.52f), celestial, 1.0f, bldg.RoofAlpha);
                foreach (var wall in activeFloor.edgeStrips.Where(w => w.depthRank == 0))
                    DrawWallTinted(sb, wall, bldg.Position, 0f, assets, extWallTint);

                foreach (var roof in activeFloor.roofs)
                {
                    Color roofTint = GetFacetTint(GetRoofNormal(roof), celestial, 1.0f, bldg.RoofAlpha);
                    if (roof.roofType == "A")
                        DrawRoofA(sb, roof, bldg.Position, 0f, assets, roofTint);
                    else
                        DrawRoofB(sb, roof, bldg.Position, 0f, assets, roofTint);
                }
            }
        }

        // =====================================================================
        // 11. RUNTIME NORMAL BUFFER RENDERER (Supports _n Atlases + Flat Fallbacks)
        // =====================================================================
        private static Texture2D _normalPixel;
        public static void DrawBuildingNormals(SpriteBatch sb, ModularBuildingInstance bldg, IAtlasProvider assets)
        {
            var blueprint = bldg.BlueprintRef;
            if (blueprint == null || blueprint.floors.Count == 0 || bldg.IsPlayerInside) return;

            if (_normalPixel == null)
            {
                _normalPixel = new Texture2D(sb.GraphicsDevice, 1, 1);
                _normalPixel.SetData(new[] { Color.White });
            }

            for (int fIdx = 0; fIdx < blueprint.floors.Count; fIdx++)
            {
                var floor = blueprint.floors[fIdx];
                float shiftY = -(fIdx * STORY_HEIGHT_UNITS * CELL_SIZE);

                // 1. Exterior Wall Normals (South-facing Green-Blue: RGB(128, 255, 128))
                foreach (var wall in floor.edgeStrips.Where(w => w.depthRank == 0))
                {
                    var normTex = assets.GetNormalAtlas(wall.atlas ?? "walls") ?? assets.GetNormalAtlas("Building");
                    float x1 = bldg.Position.X + (wall.x * CELL_SIZE);
                    float y1 = bldg.Position.Y + (wall.y * CELL_SIZE) + shiftY;
                    float hPx = wall.heightUnits * CELL_SIZE;
                    float wPx = wall.widthUnits * CELL_SIZE;
                    Rectangle dest = new Rectangle((int)x1, (int)(y1 - hPx), (int)wPx, (int)hPx);

                    if (normTex != null && wall.rect != null)
                    {
                        SpriteEffects flip = wall.isMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        sb.Draw(normTex, dest, wall.rect.ToRectangle(), Color.White, 0f, Vector2.Zero, flip, 0f);
                    }
                    else
                    {
                        sb.Draw(_normalPixel, dest, new Color(128, 255, 128));
                    }
                }

                // 2. Roof Normals (Mathematical slope normal encoded to RGB)
                foreach (var roof in floor.roofs)
                {
                    Vector3 n = GetRoofNormal(roof);
                    Color normColor = new Color(
                        (byte)(((n.X + 1f) * 0.5f) * 255f),
                        (byte)(((n.Y + 1f) * 0.5f) * 255f),
                        (byte)(((n.Z + 1f) * 0.5f) * 255f)
                    );

                    var normAtlas = assets.GetNormalAtlas("roofs") ?? assets.GetNormalAtlas("Building");
                    // Draw into Normal target using encoded normal color if no _n texture exists
                    if (roof.roofType == "A")
                    {
                        var fam = BuildingManager.Instance.GetRoofFamily(roof.family);
                        string key = $"angled_{roof.slopeRatio ?? "1:1"}_A";
                        if (normAtlas != null && fam?.pieces != null && fam.pieces.ContainsKey(key))
                            DrawRoofA(sb, roof, bldg.Position, shiftY, assets, Color.White); // Uses normal atlas if loaded
                        else
                        {
                            float z = roof.roofY * CELL_SIZE;
                            sb.Draw(_normalPixel, new Rectangle((int)(bldg.Position.X + roof.x * CELL_SIZE), (int)(bldg.Position.Y + roof.y * CELL_SIZE + shiftY - z), (int)(roof.w * CELL_SIZE), (int)(roof.h * CELL_SIZE)), normColor);
                        }
                    }
                }
            }
        }

        // Helper methods supporting Color tinting
        private static void DrawWallTinted(SpriteBatch sb, WallComponentData wall, Vector2 origin, float baseOffsetY, IAtlasProvider assets, Color tint)
        {
            var tex = assets.GetAtlas(wall.atlas ?? "walls");
            float x1 = origin.X + (wall.x * CELL_SIZE);
            float y1 = origin.Y + (wall.y * CELL_SIZE) + baseOffsetY;
            float currentHeight = wall.heightUnits * CELL_SIZE;
            float sw = wall.widthUnits * CELL_SIZE;
            Rectangle dest = new Rectangle((int)x1, (int)(y1 - currentHeight), (int)sw, (int)currentHeight);

            // If atlas or crop is missing -> Draw Black & Pink Checkerboard!
            if (tex == null || wall.rect == null || wall.rect.w <= 0 || wall.rect.h <= 0)
            {
                DrawTiledChecker(sb, dest, tint);
                return;
            }

            if (!wall.isSlope)
            {
                Rectangle src = wall.rect.ToRectangle();
                SpriteEffects flip = wall.isMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                sb.Draw(tex, dest, src, tint, 0f, Vector2.Zero, flip, 0f);
            }
            else
            {
                string cacheKey = $"{wall.id}_{wall.x}_{wall.y}_{wall.x2}_{wall.y2}_{wall.heightUnits}_{wall.isMirrored}";
                if (!_slopeWallCache.TryGetValue(cacheKey, out var cached) || cached.Texture == null || cached.Texture.IsDisposed)
                {
                    cached = BakeSlopedWallTexture(sb.GraphicsDevice, wall, tex);
                    if (cached != null) _slopeWallCache[cacheKey] = cached;
                }
                if (cached?.Texture != null)
                    sb.Draw(cached.Texture, origin + new Vector2(cached.DrawOffset.X, cached.DrawOffset.Y + baseOffsetY), tint);
            }
        }

        private static void DrawTrimTinted(SpriteBatch sb, AuxComponentData trim, Vector2 origin, float baseOffsetY, IAtlasProvider assets, Color tint)
        {
            var tex = assets.GetAtlas(trim.atlas ?? "walls");
            float x1 = origin.X + (trim.x * CELL_SIZE);
            float y1 = origin.Y + (trim.y * CELL_SIZE) + baseOffsetY;
            float currentHeight = trim.heightUnits * CELL_SIZE;
            float sw = trim.widthUnits * CELL_SIZE;
            Rectangle dest = new Rectangle((int)x1, (int)(y1 - currentHeight), (int)sw, (int)currentHeight);

            if (tex == null || trim.rect == null || trim.rect.w <= 0 || trim.rect.h <= 0)
            {
                DrawTiledChecker(sb, dest, tint);
                return;
            }

            sb.Draw(tex, dest, trim.rect.ToRectangle(), tint);
        }

        public static Texture2D GetMissingCheckerTexture(GraphicsDevice gd)
        {
            if (_missingCheckerTex == null || _missingCheckerTex.IsDisposed)
            {
                _missingCheckerTex = new Texture2D(gd, 16, 16);
                Color[] data = new Color[16 * 16];
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        bool isPink = ((x / 4) + (y / 4)) % 2 == 0;
                        data[y * 16 + x] = isPink ? Color.Magenta : Color.Black;
                    }
                }
                _missingCheckerTex.SetData(data);
            }
            return _missingCheckerTex;
        }

        private static void DrawTiledChecker(SpriteBatch sb, Rectangle destRect, Color tint)
        {
            var checker = GetMissingCheckerTexture(sb.GraphicsDevice);
            for (int y = destRect.Top; y < destRect.Bottom; y += 16)
            {
                for (int x = destRect.Left; x < destRect.Right; x += 16)
                {
                    int w = Math.Min(16, destRect.Right - x);
                    int h = Math.Min(16, destRect.Bottom - y);
                    sb.Draw(checker, new Rectangle(x, y, w, h), new Rectangle(0, 0, w, h), tint);
                }
            }
        }
    }
}