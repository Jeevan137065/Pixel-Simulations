using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations.Data
{

    public class MapRenderer
    {

        public int CELL_SIZE;
        private readonly EditorState _editorState;
        private TilesetManager _tilesetManager { get; set; }
        public MapRenderer(EditorState editorState)
        {
            _editorState = editorState;
            _tilesetManager = _editorState.TilesetManager;
            CELL_SIZE = _editorState.CELL_SIZE;
        }
        public void Draw(SpriteBatch sb)
        {
            var _map = _editorState.ActiveMap;
            if (_map == null) return;
            for (int i = _map.Layers.Count - 1; i > -1; i--)
            {
                var layer = _map.Layers[i];
                if (!layer.IsVisible) continue;

                switch (layer.Type)
                {
                    case LayerType.Tile: DrawTileLayer(sb, layer as TileLayer); break;
                    case LayerType.Object: DrawObjectLayer(sb, layer as ObjectLayer); break;
                    case LayerType.Control: DrawControlLayer(sb, layer as ControlLayer); break;
                    case LayerType.Mask: DrawMaskLayer(sb, layer as MaskLayer); DrawMaskLayer(sb, layer as MaskLayer); break; // Add this!
                }
            }
        }
        private void DrawTileLayer(SpriteBatch sb, TileLayer layer)
        {
            // Get the visible area from the camera
            RectangleF visibleWorld = _editorState.camera.GetVisibleWorldBounds(_editorState._layoutmanager.ViewportPanel);

            // Determine which chunk coordinates are visible
            int minChunkX = (int)Math.Floor(visibleWorld.Left / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int maxChunkX = (int)Math.Floor(visibleWorld.Right / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int minChunkY = (int)Math.Floor(visibleWorld.Top / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int maxChunkY = (int)Math.Floor(visibleWorld.Bottom / (Chunk.CHUNK_SIZE * CELL_SIZE));


            if (!layer.IsVisible || !(layer is TileLayer tileLayer)) return;

            for (int y = minChunkY; y <= maxChunkY; y++)
            {
                for (int x = minChunkX; x <= maxChunkX; x++)
                {
                    if (layer.Chunks.TryGetValue(new Point(x, y), out var chunk))
                    {
                        DrawChunk(sb, chunk);
                    }
                }
            }
        }
        private void DrawObjectLayer(SpriteBatch sb, ObjectLayer layer)
        {
            foreach (var obj in layer.Objects)
            {
                if (obj is PropObject prop)
                {
                    if (!_editorState.PrefabManager.Prefabs.TryGetValue(prop.PrefabID, out var prefab))
                    {
                        sb.DrawRectangle(new RectangleF(prop.Position.X - 8, prop.Position.Y - 8, 16, 16), Color.Magenta, 1f);
                        continue;
                    }

                    Vector2 topLeft = prop.Position - prefab.Pivot;

                    bool isTree = false;
                    if (prop.Properties.TryGetValue("Type", out var pType)) isTree = pType.Value == "Tree";
                    else if (prefab.Properties.TryGetValue("Type", out var pTypeB)) isTree = pTypeB.Value == "Tree";

                    if (isTree)
                    {
                        // ONLY draw the trunk and the base in the Editor to prevent clutter
                        if (prefab.AlternateStates.TryGetValue("State_Base", out var bLayer) && _editorState.AssetLibrary.GetAtlas(bLayer.AtlasName) != null)
                            sb.Draw(_editorState.AssetLibrary.GetAtlas(bLayer.AtlasName), topLeft + bLayer.Offset.ToVector2(), bLayer.SourceRect, Color.White);

                        if (prefab.AlternateStates.TryGetValue("State_Trunk", out var tLayer) && _editorState.AssetLibrary.GetAtlas(tLayer.AtlasName) != null)
                            sb.Draw(_editorState.AssetLibrary.GetAtlas(tLayer.AtlasName), topLeft + tLayer.Offset.ToVector2(), tLayer.SourceRect, Color.White);
                    }
                    else
                    {
                        // Check if we should draw an alternate state instead of the base
                        bool drewLayer = false;
                        foreach (var kvp in prefab.AlternateStates)
                        {
                            var t = _editorState.AssetLibrary.GetAtlas(kvp.Value.AtlasName);
                            if (t != null)
                            {
                                sb.Draw(t, topLeft + kvp.Value.Offset.ToVector2(), kvp.Value.SourceRect, Color.White);
                                drewLayer = true;
                            }
                        }

                        // Draw Base as fallback
                        if (!drewLayer && _editorState.AssetLibrary.GetAtlas(prefab.AtlasName) != null)
                        {
                            sb.Draw(_editorState.AssetLibrary.GetAtlas(prefab.AtlasName), prop.Position, prefab.SourceRect, Color.White, prop.Rotation, prefab.Pivot, prop.Scale, SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }
        private void DrawControlLayer(SpriteBatch sb, ControlLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f / _editorState.camera.Zoom;

            Color GetObjColor(MapObject obj)
            {
                if (obj.Tags != null && obj.Tags.Count > 0)
                {
                    // Just grab the first tag to determine the color
                    var tagDef = _editorState.TagManager.GetTag(obj.Tags.First());
                    if (tagDef != null) return tagDef.TagColor;
                }
                return Color.Gray; // Default if no tags!
            }
            foreach (var shapeObj in layer.Shapes)
            {
                if (shapeObj == null || shapeObj.Shape == null) continue;

                var bounds = shapeObj.Shape.GetBounds();
                Color objColor = GetObjColor(shapeObj);
                // --- GRADIENT Light Source PREVIEW ---
                if (shapeObj.Tags.Contains(200))
                {
                    var falloffShape = layer.Shapes.FirstOrDefault(s => s.Tags.Contains(200) && shapeObj.Links.Any(l => l.TargetID == s.ID));

                    if (falloffShape != null && shapeObj.Shape.Vertices.Count == falloffShape.Shape.Vertices.Count)
                    {
                        // 2. Draw lines connecting the vertices (The "Light Shafts")
                        var srcVerts = shapeObj.Shape.Vertices;
                        var fallVerts = falloffShape.Shape.Vertices;

                        for (int i = 0; i < srcVerts.Count; i++)
                        {
                            sb.DrawLine(srcVerts[i], fallVerts[i], Color.Yellow * 0.3f, lineThickness);
                            sb.DrawLine(srcVerts[i], srcVerts[(i + 1) % srcVerts.Count], Color.White, lineThickness * 2);
                            sb.DrawLine(fallVerts[i], fallVerts[(i + 1) % fallVerts.Count], Color.Yellow * 0.2f, lineThickness);
                        }
                    }
                    else
                    {
                        sb.DrawRectangle(bounds, Color.Yellow, lineThickness * 2);
                    }
                }
                else if (shapeObj.Tags.Contains(200))
                {
                    // We skip drawing the falloff explicitly, because the #light_source draws the connecting lines to it!
                    // We just draw a faint outline so you can still click it to select it.
                    sb.DrawRectangle(bounds, Color.Orange * 0.2f, lineThickness);
                }
                // --- GRADIENT REFLECTION PREVIEW ---
                if (shapeObj.Tags.Contains(201))
                {
                    bool isNegative = true; // Default to floor reflection
                    if (shapeObj.Properties.TryGetValue("Direction", out var dirProp))
                        isNegative = (dirProp.Value == "True");

                    // Draw Gradient (using horizontal slices for performance in editor)
                    int slices = 15;
                    float sliceHeight = bounds.Height / slices;

                    for (int i = 0; i < slices; i++)
                    {
                        float factor = (float)i / (slices - 1);

                        // If Negative (Floor), fade to dark at the BOTTOM (factor increases downwards).
                        // If Positive (Mirror), fade to dark at the TOP (factor decreases downwards).
                        float darkness = isNegative ? (1f - factor) : factor;

                        Color sliceColor = objColor * (darkness * 0.5f); // Max 50% opacity
                        RectangleF sliceRect = new RectangleF(bounds.X, bounds.Y + (i * sliceHeight), bounds.Width, sliceHeight);
                        sb.FillRectangle(sliceRect, sliceColor);
                    }

                    // Draw Outer Bounds
                    sb.DrawRectangle(bounds, objColor, lineThickness * 2);
                }
                else
                {
                    // Standard Shape
                    sb.FillRectangle(bounds, objColor * 0.2f);
                    var verts = shapeObj.Shape.Vertices;
                    for (int i = 0; i < verts.Count; i++)
                        sb.DrawLine(verts[i], verts[(i + 1) % verts.Count], objColor, lineThickness * 2);
                }
            }
            foreach (var rectObj in layer.Rectangles)
            {
                Color objColor = GetObjColor(rectObj);
                var bounds = new RectangleF(rectObj.Position, rectObj.Size);
                sb.FillRectangle(bounds, objColor * 0.4f);
                sb.DrawRectangle(bounds, objColor, lineThickness);
            }
            foreach (var pointObj in layer.Points)
            {
                Color objColor = GetObjColor(pointObj);
                sb.DrawCircle(pointObj.Position, pointObj.Radius, 32, pointObj.DebugColor, lineThickness);
                sb.DrawLine(pointObj.Position - new Vector2(4, 0), pointObj.Position + new Vector2(4, 0), objColor * 0.4f, lineThickness);
                sb.DrawLine(pointObj.Position - new Vector2(0, 4), pointObj.Position + new Vector2(0, 4), objColor, lineThickness);
            }
        }
        private void DrawMaskLayer(SpriteBatch sb, MaskLayer layer)
        {
            // --- 1. DETERMINE WHICH CHANNELS TO SHOW ---
            ColorWriteChannels writeChannels = ColorWriteChannels.None;
            if (_editorState.ShowMaskRed) writeChannels |= ColorWriteChannels.Red;
            if (_editorState.ShowMaskGreen) writeChannels |= ColorWriteChannels.Green;
            if (_editorState.ShowMaskBlue) writeChannels |= ColorWriteChannels.Blue;

            if (writeChannels == ColorWriteChannels.None) return;

            // --- 2. DRAW RAW DATA MASK ---
            // Additive Blending makes differences in values highly visible! 
            var dataBlendState = new BlendState
            {
                ColorSourceBlend = Blend.SourceAlpha,
                ColorDestinationBlend = Blend.One, // Additive!
                ColorWriteChannels = writeChannels
            };

            sb.End();
            sb.Begin(blendState: dataBlendState, samplerState: SamplerState.PointClamp, transformMatrix: _editorState.camera.Transform);

            // Tint chunks slightly so they don't blow out the screen to pure white
            Color dataTint = Color.White * 0.8f;

            foreach (var kvp in layer.Chunks)
            {
                Vector2 worldPos = new Vector2(kvp.Key.X * MaskLayer.CHUNK_PIXEL_SIZE, kvp.Key.Y * MaskLayer.CHUNK_PIXEL_SIZE);
                sb.Draw(kvp.Value, worldPos, dataTint);
            }

            // Restore default SpriteBatch state for the rest of the Editor
            sb.End();
            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _editorState.camera.Transform);
        }
        private void DrawChunk(SpriteBatch sb, Chunk chunk)
        {
            float chunkWorldX = chunk.ChunkCoordinate.X * Chunk.CHUNK_SIZE * CELL_SIZE;
            float chunkWorldY = chunk.ChunkCoordinate.Y * Chunk.CHUNK_SIZE * CELL_SIZE;
            Vector2 origin = new Vector2(8, 8); // Half of 16x16

            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            {
                for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                {
                    var tile = chunk.Tiles[x, y];
                    if (tile != null)
                    {
                        var tex = _tilesetManager.GetTileTexture(tile);
                        if (tex != null)
                        {
                            Vector2 pos = new Vector2(
                                chunkWorldX + (x * CELL_SIZE) + 8, // Add 8 to offset the origin
                                chunkWorldY + (y * CELL_SIZE) + 8
                            );

                            float rotationRadians = tile.Rotation * MathHelper.PiOver2;
                            sb.Draw(tex, pos, null, Color.White, rotationRadians, origin, 1f, SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }
    }

    public class GameMapRenderer
    {
        private const int CELL_SIZE = 16;
        // Hold a reference to the central state to access all needed managers
        private readonly GameState _gameState;
        private readonly int _nativeScreenWidth;
        private readonly int _nativeScreenHeight;

        //Debug Valus
        public int Object_count = 0;

        public GameMapRenderer(GameState gameState, int nativeWidth, int nativeHeight)
        {
            _gameState = gameState;
            _nativeScreenWidth = nativeWidth;
            _nativeScreenHeight = nativeHeight;
        }
        public void Draw(SpriteBatch sb, LayerType typeToDraw)
        {
            var _map = _gameState.CurrentMap;
            if (_map == null) return;

            RectangleF streamBounds = _gameState.GetStreamingBounds(_nativeScreenWidth, _nativeScreenHeight);
            // Loop backwards for correct visual order (top layers in list are drawn last)
            for (int i = _map.Layers.Count - 1; i >= 0; i--)
            {
                var layer = _map.Layers[i];
                if (layer.IsVisible && layer.Type == typeToDraw)
                {
                    switch (layer.Type)
                    {
                        case LayerType.Tile:
                            DrawTileLayer(sb, layer as TileLayer, streamBounds);
                            break;
                        case LayerType.Object:
                            break;
                        case LayerType.Control:
                            DrawControlLayer(sb, layer as ControlLayer);
                            break;
                    }
                }
            }
        }
        public void Draw(SpriteBatch sb, LayerType typeToDraw, string skipLayerName = null)
        {
            var _map = _gameState.CurrentMap;
            if (_map == null) return;

            RectangleF streamBounds = _gameState.GetStreamingBounds(_nativeScreenWidth, _nativeScreenHeight);

            // FIX: Loop FORWARDS so bottom layers draw first!
            for (int i = 0; i < _map.Layers.Count; i++)
            {
                var layer = _map.Layers[i];
                if (layer.IsVisible && layer.Type == typeToDraw && layer.Name != skipLayerName)
                {
                    switch (layer.Type)
                    {
                        case LayerType.Tile: DrawTileLayer(sb, layer as TileLayer, streamBounds); break;
                        //case LayerType.Object: DrawObjectLayer(sb, layer as ObjectLayer, streamBounds); break;
                        case LayerType.Control: DrawControlLayer(sb, layer as ControlLayer); break;
                    }
                }
            }
        }

        public void DrawLayerByName(SpriteBatch sb, string layerName)
        {
            var layer = _gameState.CurrentMap.Layers.FirstOrDefault(l => l.Name == layerName);
            if (layer != null && layer.IsVisible && layer is TileLayer tileLayer)
            {
                RectangleF streamBounds = _gameState.GetStreamingBounds(_nativeScreenWidth, _nativeScreenHeight);
                DrawTileLayer(sb, tileLayer, streamBounds);
            }
        }
        private void DrawTileLayer(SpriteBatch sb, TileLayer layer, RectangleF streamBounds)
        {
            if (layer == null) return;

            // Convert the continuous bounding box into chunk coordinates
            int minChunkX = (int)System.Math.Floor(streamBounds.Left / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int maxChunkX = (int)System.Math.Ceiling(streamBounds.Right / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int minChunkY = (int)System.Math.Floor(streamBounds.Top / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int maxChunkY = (int)System.Math.Ceiling(streamBounds.Bottom / (Chunk.CHUNK_SIZE * CELL_SIZE));
            
            for (int y = minChunkY; y <= maxChunkY; y++)
            {
                for (int x = minChunkX; x <= maxChunkX; x++)
                {
                    if (layer.Chunks.TryGetValue(new Point(x, y), out var chunk))
                    {
                        DrawChunk(sb, chunk);
                    }
                }
            }
        }
       // GAME MAP RENDERER ONLY DRAWS TILES IN GAME, AND CONTROL LAYER FOR DEBUG PURPOSE
        //private void DrawObjectLayer(SpriteBatch sb, ObjectLayer layer, RectangleF streamBounds)
        //{
        //    if (layer == null) return;
        //    foreach (var obj in layer.Objects)
        //    {
        //        // We are looking for PropObjects (trees, buildings, etc.)
        //        if (obj is PropObject prop)
        //        {
        //            // 1. Look up the prefab definition from the PrefabManager
        //            var prefab = _gameState.PrefabManager.GetPrefab(prop.PrefabID);

        //            if (prefab == null)
        //            {
        //                // Diagnostic: Draw a magenta box if the prefab is missing
        //                sb.DrawRectangle(new RectangleF(prop.Position.X - 8, prop.Position.Y - 8, 16, 16), Color.Red, 1f);
        //                continue;
        //            }

        //            RectangleF objectBounds = new RectangleF(prop.Position.X, prop.Position.Y, prefab.SourceRect.Width, prefab.SourceRect.Height);

        //            if (streamBounds.Intersects(objectBounds))
        //            {
        //                var tex = _gameState.Assets.GetAtlas(prefab.AtlasName);
        //                if (tex != null)
        //                {
        //                    // Calculate Depth based on the bottom Y (Y + Height)
        //                    float bottomY = prop.Position.Y + prefab.SourceRect.Height;
        //                    float depth = DepthUtil.Calculate(bottomY);

        //                    sb.Draw(tex, prop.Position, prefab.SourceRect, Color.White,
        //                            prop.Rotation, prefab.Pivot, prop.Scale, SpriteEffects.None, depth);
        //                }
        //            }
        //        }
        //    }

        //}
        private void DrawControlLayer(SpriteBatch sb, ControlLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f;
            foreach (var shapeObj in layer.Shapes)
            {
                shapeObj.DebugColor = Color.Red;
                // Draw Fill (Optional)
                sb.FillRectangle(shapeObj.Shape.GetBounds(), shapeObj.DebugColor * 0.3f);

                // Draw Polygon Edges
                var verts = shapeObj.Shape.Vertices;
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector2 v1 = verts[i];
                    Vector2 v2 = verts[(i + 1) % verts.Count];
                    sb.DrawLine(v1, v2, shapeObj.DebugColor, lineThickness);
                }
            }
            foreach (var rectObj in layer.Rectangles)
            {
                var bounds = new RectangleF(rectObj.Position, rectObj.Size);
                sb.FillRectangle(bounds, rectObj.DebugColor * 0.4f);
                sb.DrawRectangle(bounds, rectObj.DebugColor, lineThickness);
            }
            foreach (var pointObj in layer.Points)
            {
                sb.DrawCircle(pointObj.Position, pointObj.Radius, 32, pointObj.DebugColor, lineThickness);
                sb.DrawLine(pointObj.Position - new Vector2(4, 0), pointObj.Position + new Vector2(4, 0), pointObj.DebugColor * 0.4f, lineThickness);
                sb.DrawLine(pointObj.Position - new Vector2(0, 4), pointObj.Position + new Vector2(0, 4), pointObj.DebugColor, lineThickness);
            }
        }
        private void DrawChunk(SpriteBatch sb, Chunk chunk)
        {
            float chunkWorldX = chunk.ChunkCoordinate.X * Chunk.CHUNK_SIZE * CELL_SIZE;
            float chunkWorldY = chunk.ChunkCoordinate.Y * Chunk.CHUNK_SIZE * CELL_SIZE;
            Vector2 origin = new Vector2(8, 8); // Center of 16x16 tile

            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            {
                for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                {
                    var tile = chunk.Tiles[x, y];
                    if (tile != null)
                    {
                        var texture = _gameState.TilesetManager.GetTileTexture(tile);
                        if (texture != null)
                        {
                            // Offset position by 8,8 to account for center origin
                            var position = new Vector2(
                                chunkWorldX + (x * CELL_SIZE) + 8,
                                chunkWorldY + (y * CELL_SIZE) + 8
                            );

                            float rotationRadians = tile.Rotation * MathHelper.PiOver2;

                            sb.Draw(texture, position, null, Color.White, rotationRadians, origin, 1f, SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }
    }
}
