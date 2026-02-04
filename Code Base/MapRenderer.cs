using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Pixel_Simulations.Editor;
using MonoGame.Extended;
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

                    case LayerType.Collision:
                        DrawCollisionLayer(sb, layer as CollisionLayer);
                        break;

                    case LayerType.Navigation:
                        DrawNavigationLayer(sb, layer as NavigationLayer);
                        break;

                    case LayerType.Trigger:
                        DrawTriggerLayer(sb, layer as TriggerLayer);
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
            // The camera zoom is needed to keep outlines a consistent 1px thickness
            float lineThickness = 1f / _editorState.camera.Zoom;

            foreach (var mapObject in layer.Objects)
            {
                // Draw RectangleObjects
                if (mapObject is RectangleObject rectObj)
                {
                    var rectBounds = new RectangleF(rectObj.Position, rectObj.Size);
                    // Draw a semi-transparent fill so it's not obstructive
                    sb.FillRectangle(rectBounds, rectObj.DebugColor * 0.4f);
                    // Draw a solid outline to clearly see its bounds
                    sb.DrawRectangle(rectBounds, rectObj.DebugColor, lineThickness);
                }
            }
        }
        private void DrawCollisionLayer(SpriteBatch sb, CollisionLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f / _editorState.camera.Zoom;

            foreach (var shapeObj in layer.CollisionMesh)
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
        }
        private void DrawNavigationLayer(SpriteBatch sb, NavigationLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f / _editorState.camera.Zoom;

            foreach (var shapeObj in layer.NavigationMesh)
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
        }
        private void DrawTriggerLayer(SpriteBatch sb, TriggerLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f / _editorState.camera.Zoom;

            // Draw Rectangle Triggers
            foreach (var rectObj in layer.TriggerMesh)
            {
                var bounds = new RectangleF(rectObj.Position, rectObj.Size);
                sb.FillRectangle(bounds, rectObj.DebugColor * 0.4f);
                sb.DrawRectangle(bounds, rectObj.DebugColor, lineThickness);
            }
            foreach (var pointObj in layer.PointTriggers)
            {
                sb.DrawCircle(pointObj.Position, pointObj.Radius, 32, pointObj.DebugColor, lineThickness);
                sb.DrawLine(pointObj.Position - new Vector2(4, 0), pointObj.Position + new Vector2(4, 0), pointObj.DebugColor * 0.4f, lineThickness);
                sb.DrawLine(pointObj.Position - new Vector2(0, 4), pointObj.Position + new Vector2(0, 4), pointObj.DebugColor, lineThickness);
            }
        }
        private void DrawChunk(SpriteBatch sb, Chunk chunk)
        {
            // Calculate the top-left world position of the chunk
            float chunkWorldX = chunk.ChunkCoordinate.X * Chunk.CHUNK_SIZE * CELL_SIZE;
            float chunkWorldY = chunk.ChunkCoordinate.Y * Chunk.CHUNK_SIZE * CELL_SIZE;

            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            {
                for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                {
                    var tileInfo = chunk.Tiles[x, y];
                    if (tileInfo != null)
                    {
                        var texture = _tilesetManager.GetTileTexture(tileInfo);
                        if (texture != null)
                        {
                            var position = new Vector2(
                                chunkWorldX + x * CELL_SIZE,
                                chunkWorldY + y * CELL_SIZE
                            );
                            sb.Draw(texture, position, Color.White);
                        }
                    }
                }
            }
        }
    }

    public class GameMapRenderer
    {
        private const int CELL_SIZE = 16;
        private readonly Map _map;
        private readonly TilesetManager _tilesetManager;
        private readonly Camera _camera;
        private readonly int _nativeScreenWidth;
        private readonly int _nativeScreenHeight;

        public GameMapRenderer(Map map, TilesetManager tilesetManager, Camera camera, int nativeWidth, int nativeHeight)
        {
            _map = map;
            _tilesetManager = tilesetManager;
            _camera = camera;
            _nativeScreenWidth = nativeWidth;
            _nativeScreenHeight = nativeHeight;
        }
        public void Draw(SpriteBatch sb, LayerType typeToDraw)
        {
            if (_map == null) return;
            // Loop backwards for correct visual order (top layers in list are drawn last)
            for (int i = _map.Layers.Count - 1; i >= 0; i--)
            {
                var layer = _map.Layers[i];
                if (layer.IsVisible && layer.Type == typeToDraw)
                {
                    switch (layer.Type)
                    {
                        case LayerType.Tile:
                            DrawTileLayer(sb, layer as TileLayer);
                            break;
                        case LayerType.Object:
                            DrawObjectLayer(sb, layer as ObjectLayer);
                            break;
                        case LayerType.Collision:
                            DrawCollisionLayer(sb, layer as CollisionLayer);
                            break;
                        case LayerType.Navigation:
                            DrawNavigationLayer(sb, layer as NavigationLayer);
                            break;
                        case LayerType.Trigger:
                            DrawTriggerLayer(sb, layer as TriggerLayer);
                            break;
                    }
                }
            }
        }

        private void DrawTileLayer(SpriteBatch sb, TileLayer layer)
        {
            if (layer == null) return;

            // Calculate the visible world area based on the game camera's position.
            RectangleF visibleWorld = new RectangleF(_camera.Position, new Vector2(_nativeScreenWidth, _nativeScreenHeight));

            // Determine which chunks are visible on screen for culling
            int minChunkX = (int)System.Math.Floor(visibleWorld.Left / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int maxChunkX = (int)System.Math.Ceiling(visibleWorld.Right / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int minChunkY = (int)System.Math.Floor(visibleWorld.Top / (Chunk.CHUNK_SIZE * CELL_SIZE));
            int maxChunkY = (int)System.Math.Ceiling(visibleWorld.Bottom / (Chunk.CHUNK_SIZE * CELL_SIZE));

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
            if (layer == null) return;

            // Get the unified list of shapes from the layer
            List<MapObject> shapes = null;
            if (layer is ObjectLayer ol) shapes = ol.Objects;
            
            if (shapes == null) return;

            float lineThickness = 1f / _camera.Zoom;

            foreach (var mapObject in shapes)
            {
                if (mapObject is ShapeObject shapeObj)
                {
                    //DrawPolygon(sb, shapeObj.Shape, shapeObj.DebugColor, lineThickness);
                }
                else if (mapObject is PointObject pointObj)
                {
                    // Draw a circle for the radius and a crosshair at the center
                    sb.DrawCircle(pointObj.Position, pointObj.Radius, 32, pointObj.DebugColor, lineThickness);
                    sb.DrawLine(pointObj.Position - new Vector2(4 / _camera.Zoom, 0), pointObj.Position + new Vector2(4 / _camera.Zoom, 0), pointObj.DebugColor, lineThickness);
                    sb.DrawLine(pointObj.Position - new Vector2(0, 4 / _camera.Zoom), pointObj.Position + new Vector2(0, 4 / _camera.Zoom), pointObj.DebugColor, lineThickness);
                }
            }
        }
        private void DrawCollisionLayer(SpriteBatch sb, CollisionLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f;
            foreach (var shapeObj in layer.CollisionMesh)
            {
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
        }
        private void DrawNavigationLayer(SpriteBatch sb, NavigationLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f;
            foreach (var shapeObj in layer.NavigationMesh)
            {
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
        }
        private void DrawTriggerLayer(SpriteBatch sb, TriggerLayer layer)
        {
            if (layer == null) return;
            float lineThickness = 1f;

            // Draw Rectangle Triggers
            foreach (var rectObj in layer.TriggerMesh)
            {
                var bounds = new RectangleF(rectObj.Position, rectObj.Size);
                sb.FillRectangle(bounds, rectObj.DebugColor * 0.4f);
                sb.DrawRectangle(bounds, rectObj.DebugColor, lineThickness);
            }
            foreach (var pointObj in layer.PointTriggers)
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

            for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
            {
                for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                {
                    var tileInfo = chunk.Tiles[x, y];
                    if (tileInfo != null)
                    {
                        var texture = _tilesetManager.GetTileTexture(tileInfo);
                        if (texture != null)
                        {
                            var position = new Vector2(chunkWorldX + x * CELL_SIZE, chunkWorldY + y * CELL_SIZE);
                            sb.Draw(texture, position, Color.White);
                        }
                    }
                }
            }
        }
    }
}
