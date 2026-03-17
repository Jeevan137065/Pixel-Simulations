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
                    case LayerType.Tile:
                        DrawTileLayer(sb, layer as TileLayer);
                        break;

                    case LayerType.Object:
                        DrawObjectLayer(sb, layer as ObjectLayer);
                        break;

                    case LayerType.Control:
                         DrawControlLayer(sb, layer as ControlLayer);
                        break;

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
                    // Case-sensitive check
                    if (!_editorState.PrefabManager.Prefabs.TryGetValue(prop.PrefabID, out var prefab))
                    {
                        // DIAGNOSTIC: If it's missing, draw a bright box so we know it exists but link is broken
                        sb.DrawRectangle(new RectangleF(prop.Position.X - 8, prop.Position.Y - 8, 16, 16), Color.Magenta, 1f);
                        continue;
                    }

                    var tex = _editorState.AssetLibrary.GetAtlas(prefab.AtlasName);
                    if (tex != null)
                    {
                        sb.Draw(tex, prop.Position, prefab.SourceRect, Color.White,
                                prop.Rotation, prefab.Pivot, prop.Scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }
        private void DrawControlLayer(SpriteBatch sb, ControlLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f / _editorState.camera.Zoom;


            foreach (var shapeObj in layer.Shapes)
            {

                if (shapeObj == null || shapeObj.Shape == null) continue;
                // 1. Draw the filled area (using bounds for a simple transparent tint)
                var bounds = shapeObj.Shape.GetBounds();
                sb.FillRectangle(bounds, shapeObj.DebugColor * 0.2f);

                // 2. DRAW THE ACTUAL POLYGON EDGES (The complex "Plus" shape)
                var verts = shapeObj.Shape.Vertices;
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector2 start = verts[i];
                    Vector2 end = verts[(i + 1) % verts.Count]; // Loop back to start
                    sb.DrawLine(start, end, shapeObj.DebugColor, lineThickness * 2);
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
                            DrawObjectLayer(sb, layer as ObjectLayer, streamBounds);
                            break;
                        case LayerType.Control:
                            DrawControlLayer(sb, layer as ControlLayer);
                            break;
                    }
                }
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
        public List<RenderableSprite> CollectDynamicSprites()
        {
            var sprites = new List<RenderableSprite>();
            var _map = _gameState.CurrentMap;
            if (_map == null) return sprites;

            RectangleF streamBounds = _gameState.GetStreamingBounds(_nativeScreenWidth, _nativeScreenHeight);

            // 1. COLLECT MAP OBJECTS
            for (int i = _map.Layers.Count - 1; i >= 0; i--)
            {
                var layer = _map.Layers[i];
                if (layer.IsVisible && layer.Type == LayerType.Object)
                {
                    var objLayer = layer as ObjectLayer;
                    foreach (var obj in objLayer.Objects)
                    {
                        if (obj is PropObject prop)
                        {
                            var prefab = _gameState.PrefabManager.GetPrefab(prop.PrefabID);
                            if (prefab == null) continue;

                            RectangleF objectBounds = new RectangleF(prop.Position.X, prop.Position.Y, prefab.SourceRect.Width, prefab.SourceRect.Height);

                            if (streamBounds.Intersects(objectBounds))
                            {
                                var tex = _gameState.Assets.GetAtlas(prefab.AtlasName);
                                if (tex != null)
                                {
                                    // The physical bottom of the object
                                    float distanceFromPivotToBottom = (prefab.SourceRect.Height - prefab.Pivot.Y) * prop.Scale.Y;
                                    float bottomY = prop.Position.Y + distanceFromPivotToBottom;

                                    sprites.Add(new RenderableSprite
                                    {
                                        Texture = tex,
                                        Position = prop.Position,
                                        SourceRect = prefab.SourceRect,
                                        Origin = prefab.Pivot,
                                        Scale = prop.Scale,
                                        Rotation = prop.Rotation,
                                        DrawDepth = DepthUtil.Calculate(bottomY), // Standard 0-1 depth
                                        BaseWorldY = bottomY                      // Exact world altitude
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // 2. COLLECT THE PLAYER
            // Note: Adjust the property names based on how your NewPlayer class is actually set up!
            if (_gameState.Player != null)
            {
                var P = _gameState.Player;
                // Calculate where the player's feet touch the ground.
                // If origin is centered, it's Position.Y + (Height / 2). 
                // Adjust this formula to match your player's exact pivot point.
                float playerFeetY = _gameState.Player.Position.Y + P.CurrentFrameRect.Height;

                sprites.Add(new RenderableSprite
                {
                    Texture = P.CompositeTexture, // Assuming Player has a Texture property
                    Position = P.Position,
                    SourceRect = P.CurrentFrameRect, // Assuming Player has animation frame
                    Origin = P.Origin, // e.g., new Vector2(Width/2, Height)
                    Scale = Vector2.One,
                    Rotation = 0f,
                    DrawDepth = DepthUtil.Calculate(playerFeetY),
                    BaseWorldY = playerFeetY
                });
            }

            return sprites;
        }
        private void DrawObjectLayer(SpriteBatch sb, ObjectLayer layer, RectangleF streamBounds)
        {
            if (layer == null) return;
            foreach (var obj in layer.Objects)
            {
                // We are looking for PropObjects (trees, buildings, etc.)
                if (obj is PropObject prop)
                {
                    // 1. Look up the prefab definition from the PrefabManager
                    var prefab = _gameState.PrefabManager.GetPrefab(prop.PrefabID);

                    if (prefab == null)
                    {
                        // Diagnostic: Draw a magenta box if the prefab is missing
                        sb.DrawRectangle(new RectangleF(prop.Position.X - 8, prop.Position.Y - 8, 16, 16), Color.Red, 1f);
                        continue;
                    }

                    RectangleF objectBounds = new RectangleF(prop.Position.X, prop.Position.Y, prefab.SourceRect.Width, prefab.SourceRect.Height);

                    if (streamBounds.Intersects(objectBounds))
                    {
                        var tex = _gameState.Assets.GetAtlas(prefab.AtlasName);
                        if (tex != null)
                        {
                            // Calculate Depth based on the bottom Y (Y + Height)
                            float bottomY = prop.Position.Y + prefab.SourceRect.Height;
                            float depth = DepthUtil.Calculate(bottomY);

                            sb.Draw(tex, prop.Position, prefab.SourceRect, Color.White,
                                    prop.Rotation, prefab.Pivot, prop.Scale, SpriteEffects.None, depth);
                        }
                    }
                }
            }

        }
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
