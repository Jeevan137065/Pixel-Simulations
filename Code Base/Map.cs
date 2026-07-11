using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pixel_Simulations.Data
{
    public class Chunk
    {
        public const int CHUNK_SIZE = 8; // Each chunk is 16x16 tiles

        [JsonProperty]
        public Point ChunkCoordinate { get; private set; }
        [JsonProperty]
        public TileInfo[,] Tiles { get; private set; }
        public Chunk(Point chunkCoordinate)
        {
            ChunkCoordinate = chunkCoordinate;
            // Initialize with nulls. We only store non-empty TileInfo.
            Tiles = new TileInfo[CHUNK_SIZE, CHUNK_SIZE];
        }
        // For deserialization
        public Chunk() { }
        /// Gets the tile at a local coordinate within this chunk (0-15).
        public TileInfo GetTileAt(int localX, int localY)
        {
            if (localX < 0 || localX >= CHUNK_SIZE || localY < 0 || localY >= CHUNK_SIZE)
                return null;
            return Tiles[localX, localY];
        }
        /// Places a tile at a local coordinate within this chunk (0-15).
        public void PlaceTile(int localX, int localY, TileInfo tileInfo)
        {
            if (localX < 0 || localX >= CHUNK_SIZE || localY < 0 || localY >= CHUNK_SIZE)
                return;
            Tiles[localX, localY] = tileInfo;
        }

        public void RemoveTile(int localX, int localY)
        {
            if (localX < 0 || localX >= CHUNK_SIZE || localY < 0 || localY >= CHUNK_SIZE)
                return;
            Tiles[localX, localY] = null;
            
        }
    }
    public class Map : IDisposable
    {
        [JsonProperty]
        public List<Layer> Layers { get; set; }

        public Map()
        {
            Layers = new List<Layer>();
            Layers.Add(new TileLayer("Ground"));
        }

        // --- Add Dispose to cascade down ---
        public void Dispose()
        {
            if (Layers != null)
            {
                foreach (var layer in Layers)
                {
                    layer.Dispose();
                }
            }
        }

        // Methods for manipulating layers. The EditorController will call these.
        public void AddLayerAbove(int index, Layer newLayer)
        {
            if (index > -1 && index < Layers.Count)
                Layers.Insert(index, newLayer);
            //else
                //Layers.Add(newLayer);
        }

        public void AddLayerBelow(int index, Layer newLayer)
        {
            if (index > 0 && index < Layers.Count - 1)
                Layers.Insert(index - 1, newLayer);
            //else
                //Layers.Add(newLayer);
        }

        public void DeleteLayer(int index)
        {
            if (Layers.Count > 1 && index >= 0 && index < Layers.Count)
                Layers.RemoveAt(index);
        }

        public void MoveLayerUp(int index)
        {
            if (index > 0 && index < Layers.Count){ 
                (Layers[index], Layers[index - 1]) = (Layers[index - 1], Layers[index]); }
        }

        public void MoveLayerDown(int index)
        {
            if (index > -1 && index < Layers.Count - 1)
            {
                (Layers[index], Layers[index + 1]) = (Layers[index + 1], Layers[index]);
            }
        }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class MapSaveData
    {
        // 1. Tracks IDs of base map objects (like trees) the player chopped down
        [JsonProperty] public HashSet<string> DestroyedBaseIDs { get; set; } = new HashSet<string>();

        // 2. Tracks everything the player has placed on the ground
        [JsonProperty] public List<PlacedItemObject> PlacedItems { get; set; } = new List<PlacedItemObject>();
    }

    public static class MapSerializer
    {
        private static readonly JsonSerializerSettings _jsonSettings;

        static MapSerializer()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                Converters = { new MapObjectConverter(), new Vector2Converter(), new ColorConverter(), new LayerConverter() },
                Formatting = Formatting.Indented
            };
        }
        #region Editor JSON Workflow
        public static void Save(Map map, string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            string json = JsonConvert.SerializeObject(map, _jsonSettings);
            File.WriteAllText(filePath, json);
            System.Diagnostics.Debug.WriteLine($"JSON Map Saved to: {filePath}");

        }
        public static Map Load(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Map>(json, _jsonSettings);
        }
        #endregion

        #region Game Binary Workflow
        public static void Export(Map map, string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using (var stream = File.Open(filePath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write("PMAP".ToCharArray());
                writer.Write(2);      // Version number

                writer.Write(map.Layers.Count);
                foreach (var layer in map.Layers)
                {
                    writer.Write((int)layer.Type);
                    writer.Write(layer.Name ?? " ");
                    writer.Write(layer.IsVisible);
                    writer.Write(layer.IsLocked);

                    switch (layer.Type)
                    {
                        case LayerType.Tile:
                            WriteTileLayer(writer, layer as TileLayer);
                            break;
                        case LayerType.Object:
                            WriteObjectLayer(writer, layer as ObjectLayer);
                            break;
                        case LayerType.Control:
                            WriteControlLayer(writer, layer as ControlLayer);
                            break;
                        case LayerType.Mask:
                            var maskL = layer as MaskLayer;
                            writer.Write(maskL.OffsetX);
                            writer.Write(maskL.OffsetY);
                            break;
                    }
                }
            }
        }
        public static Map Read(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var map = new Map();
            map.Layers.Clear();

            using (var stream = File.Open(filePath, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                string magic = new string(reader.ReadChars(4));
                if (magic != "PMAP") throw new System.Exception("Invalid map file format.");
                int version = reader.ReadInt32();

                int layerCount = reader.ReadInt32();
                for (int i = 0; i < layerCount; i++)
                {
                    LayerType type = (LayerType)reader.ReadInt32();
                    string name = reader.ReadString();
                    bool isVisible = reader.ReadBoolean();
                    bool isLocked = reader.ReadBoolean();

                    Layer newLayer = null;
                    switch (type)
                    {
                        case LayerType.Tile:
                            newLayer = ReadTileLayer(reader, name);
                            break;
                        case LayerType.Object:
                            newLayer = ReadObjectLayer(reader, name, version);
                            break;
                        case LayerType.Control:
                            newLayer = ReadControlLayer(reader, name, version);
                            break;
                        case LayerType.Mask:
                            var loadedMask = new MaskLayer(name);
                            loadedMask.OffsetX = reader.ReadInt32();
                            loadedMask.OffsetY = reader.ReadInt32();
                            newLayer = loadedMask;
                            break;
                    }
                    if (newLayer != null)
                    {
                        newLayer.IsVisible = isVisible;
                        newLayer.IsLocked = isLocked;
                        map.Layers.Add(newLayer);
                    }
                }
            }
            return map;
        }

        #endregion

        #region Binary Helper Methods
        private static void WriteTileLayer(BinaryWriter writer, TileLayer layer)
        {
            writer.Write(layer.Chunks.Count);
            foreach (var chunkPair in layer.Chunks)
            {
                writer.Write(chunkPair.Key.X);
                writer.Write(chunkPair.Key.Y);
                for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                    {
                        var tile = chunkPair.Value.Tiles[x, y];
                        if (tile == null)
                        {
                            writer.Write(false);
                        }
                        else
                        {
                            writer.Write(true);
                            writer.Write(tile.TilesetName);
                            writer.Write(tile.TileID);
                            writer.Write(tile.Rotation);
                        }
                    }
                }
            }
        }
        private static void WriteObjectLayer(BinaryWriter writer, ObjectLayer layer)
        {
            var props = layer.Objects.OfType<PropObject>().ToList();
            writer.Write(props.Count);
            foreach (var prop in props)
            {
                WriteMapObjectBase(writer, prop);
                writer.Write(prop.Position.X); writer.Write(prop.Position.Y);
                writer.Write(prop.PrefabID);
                writer.Write(prop.Scale.X); writer.Write(prop.Scale.Y);
                writer.Write(prop.Rotation);
            }
        }
        private static void WriteControlLayer(BinaryWriter writer, ControlLayer layer)
        {
            // Write Shapes
            writer.Write(layer.Shapes.Count);
            foreach (var shape in layer.Shapes)
            {
                WriteMapObjectBase(writer, shape);
                writer.Write(shape.Shape.Vertices.Count);
                foreach (var v in shape.Shape.Vertices) { writer.Write(v.X); writer.Write(v.Y); }

                // FIX: Defensive null check prevents truncated file exports
                int wpCount = shape.SpineWaypoints != null ? shape.SpineWaypoints.Count : 0;
                writer.Write(wpCount);
                if (shape.SpineWaypoints != null)
                {
                    foreach (var wp in shape.SpineWaypoints) { writer.Write(wp.X); writer.Write(wp.Y); }
                }
            }

            // Write Rectangles
            writer.Write(layer.Rectangles.Count);
            foreach (var rect in layer.Rectangles)
            {
                WriteMapObjectBase(writer, rect);
                writer.Write(rect.Position.X); writer.Write(rect.Position.Y);
                writer.Write(rect.Size.X); writer.Write(rect.Size.Y);
            }

            // Write Points
            writer.Write(layer.Points.Count);
            foreach (var pt in layer.Points)
            {
                WriteMapObjectBase(writer, pt);
                writer.Write(pt.Position.X); writer.Write(pt.Position.Y);
                writer.Write(pt.Radius);
            }
        }
        private static TileLayer ReadTileLayer(BinaryReader reader, string name)
        {
            var layer = new TileLayer(name);
            int chunkCount = reader.ReadInt32();
            for (int i = 0; i < chunkCount; i++)
            {
                var chunkCoord = new Point(reader.ReadInt32(), reader.ReadInt32());
                var chunk = new Chunk(chunkCoord);
                for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                    {
                        if (reader.ReadBoolean())
                        {
                            string tilesetName = reader.ReadString();
                            int tileId = reader.ReadInt32();
                            byte rotation = reader.ReadByte();
                            chunk.Tiles[x, y] = new TileInfo(tilesetName, tileId, rotation);
                        }
                    }
                }
                layer.Chunks[chunkCoord] = chunk;
            }
            return layer;
        }
        private static ObjectLayer ReadObjectLayer(BinaryReader reader, string name, int version)
        {
            var layer = new ObjectLayer(name);
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var prop = new PropObject();
                ReadMapObjectBase(reader, prop, version);
                prop.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                prop.PrefabID = reader.ReadString();
                prop.Scale = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                prop.Rotation = reader.ReadSingle();
                layer.Objects.Add(prop);
            }
            return layer;
        }
        private static ControlLayer ReadControlLayer(BinaryReader reader, string name, int version)
        {
            var layer = new ControlLayer(name);

            // Read Shapes
            int sCount = reader.ReadInt32();
            for (int i = 0; i < sCount; i++)
            {
                var s = new ShapeObject();
                ReadMapObjectBase(reader, s, version);
                int vCount = reader.ReadInt32();
                var verts = new List<Vector2>();
                for (int j = 0; j < vCount; j++) verts.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                s.Shape = new Polygon(verts);
                s.SpineWaypoints = new List<Vector2>();
                if (version >= 2)
                {
                    int wpCount = reader.ReadInt32();
                    for (int j = 0; j < wpCount; j++) s.SpineWaypoints.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
                s.UpdateBoundsFromVertices();
                layer.Shapes.Add(s);
            }

            // Read Rectangles
            int rCount = reader.ReadInt32();
            for (int i = 0; i < rCount; i++)
            {
                var r = new RectangleObject();
                ReadMapObjectBase(reader, r, version);
                r.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                r.Size = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                layer.Rectangles.Add(r);
            }

            // Read Points
            int pCount = reader.ReadInt32();
            for (int i = 0; i < pCount; i++)
            {
                var p = new PointObject();
                ReadMapObjectBase(reader, p, version);
                p.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                p.Radius = reader.ReadSingle();
                layer.Points.Add(p);
            }

            return layer;
        }
        #endregion
        private static void WriteMapObjectBase(BinaryWriter writer, MapObject obj)
        {
            writer.Write(obj.ID);
            writer.Write(obj.Name ?? "");

            // Write Links (Version 2 Format)
            writer.Write(obj.Links.Count);
            foreach (var link in obj.Links)
            {
                writer.Write(link.SourcePort ?? "Out");
                writer.Write(link.TargetID);
                writer.Write(link.TargetPort ?? "In");
            }

            writer.Write(obj.Tags.Count);
            foreach (var tag in obj.Tags) writer.Write(tag);

            writer.Write(obj.Properties.Count);
            foreach (var kvp in obj.Properties)
            {
                writer.Write(kvp.Key);
                writer.Write((int)kvp.Value.Type);
                writer.Write(kvp.Value.Value ?? "");
            }
        }

        private static void ReadMapObjectBase(BinaryReader reader, MapObject obj, int version)
        {
            obj.ID = reader.ReadString();
            obj.Name = reader.ReadString();

            int linkCount = reader.ReadInt32();
            obj.Links = new List<ObjectLink>();
            for (int i = 0; i < linkCount; i++)
            {
                if (version >= 2)
                {
                    obj.Links.Add(new ObjectLink
                    {
                        SourcePort = reader.ReadString(),
                        TargetID = reader.ReadString(),
                        TargetPort = reader.ReadString()
                    });
                }
                else
                {
                    // Backwards Compatibility: Upgrade old V1 String Links to ObjectLinks automatically.
                    obj.Links.Add(new ObjectLink { SourcePort = "Out", TargetID = reader.ReadString(), TargetPort = "In" });
                }
            }

            int tagCount = reader.ReadInt32();
            obj.Tags = new HashSet<int>();
            for (int i = 0; i < tagCount; i++) obj.Tags.Add(reader.ReadInt32());

            int propCount = reader.ReadInt32();
            obj.Properties = new Dictionary<string, MapProperty>();
            for (int i = 0; i < propCount; i++)
            {
                string key = reader.ReadString();
                PropertyType type = (PropertyType)reader.ReadInt32();
                string val = reader.ReadString();
                obj.Properties[key] = new MapProperty(type, val);
            }
        }
        public static void CaptureToImage(Map map, GraphicsDevice gd, Editor.EditorState state, string path)
        {
            // 1. Calculate the exact bounding box of the entire map
            var bounds = CalculateMapBounds(map, state.PrefabManager);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Capture Failed: Map is empty.");
                return;
            }

            // Add a tiny bit of padding (16 pixels) around the edges
            bounds.Inflate(16, 16);

            // 2. Create the Render Target (WARNING: Max size is usually 4096 or 8192 depending on GPU)
            int width = (int)System.Math.Min(bounds.Width, 10240);
            int height = (int)System.Math.Min(bounds.Height, 10240);

            var prevTargets = gd.GetRenderTargets();

            using (var target = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
            {
                gd.SetRenderTarget(target);
                gd.Clear(Color.Transparent); // Keep background transparent so you can overlay it in Photoshop!

                using (var sb = new SpriteBatch(gd))
                {
                    // 3. Create a camera matrix that shifts the map's top-left corner to (0,0)
                    var transform = Matrix.CreateTranslation(-bounds.X, -bounds.Y, 0);

                    sb.Begin(transformMatrix: transform, samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
                    for (int i = map.Layers.Count - 1; i >= 0; i--)
                    {
                        var layer = map.Layers[i];
                        if (!layer.IsVisible) continue;

                        if (layer is TileLayer tileLayer)
                        {
                            foreach (var chunkKvp in tileLayer.Chunks)
                            {
                                float cx = chunkKvp.Key.X * Chunk.CHUNK_SIZE * state.CELL_SIZE;
                                float cy = chunkKvp.Key.Y * Chunk.CHUNK_SIZE * state.CELL_SIZE;
                                Vector2 origin = new Vector2(8, 8);

                                for (int y = 0; y < Chunk.CHUNK_SIZE; y++)
                                {
                                    for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
                                    {
                                        var tile = chunkKvp.Value.Tiles[x, y];
                                        if (tile != null)
                                        {
                                            var tex = state.TilesetManager.GetTileTexture(tile);
                                            if (tex != null)
                                            {
                                                Vector2 pos = new Vector2(cx + (x * state.CELL_SIZE) + 8, cy + (y * state.CELL_SIZE) + 8);
                                                sb.Draw(tex, pos, null, Color.White, tile.Rotation * MathHelper.PiOver2, origin, 1f, SpriteEffects.None, 0f);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (layer is ObjectLayer objLayer && layer.Type == LayerType.Object)
                        {
                            foreach (var obj in objLayer.Objects)
                            {
                                if (obj is PropObject prop)
                                {
                                    var prefab = state.PrefabManager.GetPrefab(prop.PrefabID);
                                    if (prefab != null)
                                    {
                                        var tex = state.AssetLibrary.GetAtlas(prefab.AtlasName);
                                        if (tex != null)
                                        {
                                            sb.Draw(tex, prop.Position, prefab.SourceRect, Color.White, prop.Rotation, prefab.Pivot, prop.Scale, SpriteEffects.None, 0f);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    sb.End();
                }

                gd.SetRenderTargets(prevTargets);

                // 5. Save to Disk
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                using (var stream = System.IO.File.OpenWrite(path))
                {
                    target.SaveAsPng(stream, width, height);
                }
                System.Diagnostics.Debug.WriteLine($"Map Captured successfully to: {path}");
            }
        }

        private static RectangleF CalculateMapBounds(Map map, PrefabManager prefabManager)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool hasData = false;

            foreach (var layer in map.Layers)
            {
                if (layer is TileLayer tileLayer)
                {
                    foreach (var chunk in tileLayer.Chunks.Keys)
                    {
                        float x = chunk.X * Chunk.CHUNK_SIZE * 16;
                        float y = chunk.Y * Chunk.CHUNK_SIZE * 16;
                        float w = Chunk.CHUNK_SIZE * 16;

                        minX = System.Math.Min(minX, x);
                        minY = System.Math.Min(minY, y);
                        maxX = System.Math.Max(maxX, x + w);
                        maxY = System.Math.Max(maxY, y + w);
                        hasData = true;
                    }
                }
                else if (layer is ObjectLayer objLayer && layer.Type == LayerType.Object)
                {
                    foreach (var obj in objLayer.Objects)
                    {
                        if (obj is PropObject prop)
                        {
                            var prefab = prefabManager.GetPrefab(prop.PrefabID);
                            if (prefab != null)
                            {
                                float x = prop.Position.X - prefab.Pivot.X;
                                float y = prop.Position.Y - prefab.Pivot.Y;
                                float w = prefab.SourceRect.Width * prop.Scale.X;
                                float h = prefab.SourceRect.Height * prop.Scale.Y;

                                minX = System.Math.Min(minX, x);
                                minY = System.Math.Min(minY, y);
                                maxX = System.Math.Max(maxX, x + w);
                                maxY = System.Math.Max(maxY, y + h);
                                hasData = true;
                            }
                        }
                    }
                }
            }

            if (!hasData) return new RectangleF(0, 0, 0, 0);
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }
        public static void SaveMaskLayer(MaskLayer maskLayer, string imagePath, GraphicsDevice gd, EditorState state)
        {
            if (maskLayer == null || maskLayer.Chunks.Count == 0) return;

            // Calculate Bounds
            maskLayer.OffsetX = maskLayer.Chunks.Keys.Min(p => p.X);
            maskLayer.OffsetY = maskLayer.Chunks.Keys.Min(p => p.Y);

            int maxX = maskLayer.Chunks.Keys.Max(p => p.X);
            int maxY = maskLayer.Chunks.Keys.Max(p => p.Y);

            int width = (maxX - maskLayer.OffsetX + 1) * MaskLayer.CHUNK_PIXEL_SIZE;
            int height = (maxY - maskLayer.OffsetY + 1) * MaskLayer.CHUNK_PIXEL_SIZE;

            var prevRt = gd.GetRenderTargets();

            // 1. STITCH RAW MASK
            using (var masterRt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
            {
                gd.SetRenderTarget(masterRt);
                gd.Clear(new Color(0, 0, 0, 255)); // Clear to Opaque Black

                using (var sb = new SpriteBatch(gd))
                {
                    sb.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
                    foreach (var kvp in maskLayer.Chunks)
                    {
                        Vector2 pos = new Vector2(
                            (kvp.Key.X - maskLayer.OffsetX) * MaskLayer.CHUNK_PIXEL_SIZE,
                            (kvp.Key.Y - maskLayer.OffsetY) * MaskLayer.CHUNK_PIXEL_SIZE);
                        sb.Draw(kvp.Value, pos, Color.White);
                    }
                    sb.End();
                }

                // Save Raw Mask to Assets
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(imagePath));
                using (var stream = System.IO.File.OpenWrite(imagePath))
                {
                    masterRt.SaveAsPng(stream, width, height);
                }

                // 2. GENERATE HEIGHT-TO-NORMAL MAP
                string normalPath = imagePath.Replace("_mask.png", "_normals.png");
                var normalShader = state.normalShader;

                if (normalShader != null)
                {
                    using (var normalRt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
                    {
                        gd.SetRenderTarget(normalRt);
                        gd.Clear(Color.Transparent);

                        normalShader.Parameters["MaskTexture"]?.SetValue(masterRt);
                        normalShader.Parameters["TextureSize"]?.SetValue(new Vector2(width, height));
                        normalShader.Parameters["HeightScale"]?.SetValue(15.0f); // Adjusts terrain slope steepness

                        gd.BlendState = BlendState.Opaque;
                        gd.DepthStencilState = DepthStencilState.None;
                        gd.RasterizerState = RasterizerState.CullNone;
                        gd.SamplerStates[0] = SamplerState.PointClamp;

                        // Create a Full-Screen Quad
                        VertexPositionTexture[] quadVerts = new VertexPositionTexture[4];
                        quadVerts[0] = new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0));
                        quadVerts[1] = new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0));
                        quadVerts[2] = new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1));
                        quadVerts[3] = new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1));

                        short[] quadIndices = new short[] { 0, 1, 2, 1, 3, 2 };

                        foreach (var pass in normalShader.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, quadVerts, 0, 4, quadIndices, 0, 2);
                        }

                        // Save Normal Map to Assets
                        using (var stream = File.OpenWrite(normalPath))
                        {
                            normalRt.SaveAsPng(stream, width, height);
                        }
                    }
                }
                gd.SetRenderTargets(prevRt);
            }
        }
        public static void LoadMaskLayer(MaskLayer maskLayer, string imagePath, GraphicsDevice gd)
        {
            if (!System.IO.File.Exists(imagePath)) return;

            using (var stream = File.OpenRead(imagePath))
            using (var masterTex = Texture2D.FromStream(gd, stream))
            {
                int chunksX = masterTex.Width / MaskLayer.CHUNK_PIXEL_SIZE;
                int chunksY = masterTex.Height / MaskLayer.CHUNK_PIXEL_SIZE;

                var prevRt = gd.GetRenderTargets();
                using (var sb = new SpriteBatch(gd))
                {
                    for (int y = 0; y < chunksY; y++)
                    {
                        for (int x = 0; x < chunksX; x++)
                        {
                            // FIX B: Use the Offset saved inside the MaskLayer JSON!
                            Point chunkCoord = new Point(x + maskLayer.OffsetX, y + maskLayer.OffsetY);
                            var rt = maskLayer.GetOrCreateChunk(chunkCoord, gd);

                            Rectangle sourceRect = new Rectangle(x * MaskLayer.CHUNK_PIXEL_SIZE, y * MaskLayer.CHUNK_PIXEL_SIZE, MaskLayer.CHUNK_PIXEL_SIZE, MaskLayer.CHUNK_PIXEL_SIZE);

                            gd.SetRenderTarget(rt);
                            sb.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
                            sb.Draw(masterTex, Vector2.Zero, sourceRect, Color.White);
                            sb.End();
                        }
                    }
                }
                gd.SetRenderTargets(prevRt);
            }
        }
        public static Dictionary<Point, Texture2D> LoadGameChunks(string imagePath, GraphicsDevice gd, int offsetX, int offsetY)
        {
            var chunks = new Dictionary<Point, Texture2D>();
            if (!System.IO.File.Exists(imagePath)) return chunks;

            using (var stream = File.OpenRead(imagePath))
            using (var masterTex = Texture2D.FromStream(gd, stream))
            {
                int chunkPixels = MaskLayer.CHUNK_PIXEL_SIZE;
                int chunksX = masterTex.Width / chunkPixels;
                int chunksY = masterTex.Height / chunkPixels;

                Color[] masterData = new Color[masterTex.Width * masterTex.Height];
                masterTex.GetData(masterData);

                for (int y = 0; y < chunksY; y++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        Point coord = new Point(x + offsetX, y + offsetY); // Apply the parsed offset!
                        Texture2D chunkTex = new Texture2D(gd, chunkPixels, chunkPixels);
                        Color[] chunkData = new Color[chunkPixels * chunkPixels];

                        for (int cy = 0; cy < chunkPixels; cy++)
                        {
                            for (int cx = 0; cx < chunkPixels; cx++)
                            {
                                int masterX = (x * chunkPixels) + cx;
                                int masterY = (y * chunkPixels) + cy;
                                chunkData[cx + cy * chunkPixels] = masterData[masterX + masterY * masterTex.Width];
                            }
                        }

                        chunkTex.SetData(chunkData);
                        chunks[coord] = chunkTex;
                    }
                }
            }
            return chunks;
        }
        public static Dictionary<Point, Texture2D> LoadGameNormalChunks(string imagePath, GraphicsDevice gd, int offsetX, int offsetY)
        {
            var chunks = new Dictionary<Point, Texture2D>();
            if (!System.IO.File.Exists(imagePath)) return chunks;

            using (var stream = File.OpenRead(imagePath))
            using (var masterTex = Texture2D.FromStream(gd, stream))
            {
                int chunksX = masterTex.Width / MaskLayer.CHUNK_PIXEL_SIZE;
                int chunksY = masterTex.Height / MaskLayer.CHUNK_PIXEL_SIZE;
                Color[] masterData = new Color[masterTex.Width * masterTex.Height];
                masterTex.GetData(masterData);

                for (int y = 0; y < chunksY; y++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        Point coord = new Point(x + offsetX, y + offsetY);
                        Texture2D chunkTex = new Texture2D(gd, MaskLayer.CHUNK_PIXEL_SIZE, MaskLayer.CHUNK_PIXEL_SIZE);
                        Color[] chunkData = new Color[MaskLayer.CHUNK_PIXEL_SIZE * MaskLayer.CHUNK_PIXEL_SIZE];

                        for (int cy = 0; cy < MaskLayer.CHUNK_PIXEL_SIZE; cy++)
                        {
                            for (int cx = 0; cx < MaskLayer.CHUNK_PIXEL_SIZE; cx++)
                            {
                                int mX = (x * MaskLayer.CHUNK_PIXEL_SIZE) + cx;
                                int mY = (y * MaskLayer.CHUNK_PIXEL_SIZE) + cy;
                                chunkData[cx + cy * MaskLayer.CHUNK_PIXEL_SIZE] = masterData[mX + mY * masterTex.Width];
                            }
                        }
                        chunkTex.SetData(chunkData);
                        chunks[coord] = chunkTex;
                    }
                }
            }
            return chunks;
        }
        private static RectangleF CalculateMapBounds(Map map)
        {
            // Logic to find the Min/Max X and Y of every tile and object
            // Returns a rectangle covering the entire occupied area
            return new RectangleF(0, 0, 2048, 2048); // Placeholder
        }
    }

    public static class MapExporter
    {
        /// <summary>
        /// Carves a smooth, continuous waterbed valley directly into the Blue channel of the terrain mask.
        /// Step 1 of export.
        /// </summary>
        // Inside Map.cs -> MapExporter Class
        public static MaskLayer BakeWaterBedIntoMask(GraphicsDevice gd, Map map, MaskLayer terrainMask)
        {
            if (terrainMask == null || terrainMask.Chunks.Count == 0) return terrainMask;

            int minX = terrainMask.OffsetX * MaskLayer.CHUNK_PIXEL_SIZE;
            int minY = terrainMask.OffsetY * MaskLayer.CHUNK_PIXEL_SIZE;

            int maxChunkX = terrainMask.Chunks.Keys.Max(p => p.X);
            int maxChunkY = terrainMask.Chunks.Keys.Max(p => p.Y);

            int width = (maxChunkX - terrainMask.OffsetX + 1) * MaskLayer.CHUNK_PIXEL_SIZE;
            int height = (maxChunkY - terrainMask.OffsetY + 1) * MaskLayer.CHUNK_PIXEL_SIZE;

            Color[] bakedTerrainMask = new Color[width * height];
            ExtractContinuousTerrainHeightMap(gd, terrainMask, bakedTerrainMask, width, height, map);

            var waterShapes = map.Layers
                .OfType<ControlLayer>()
                .SelectMany(l => l.Shapes.Where(s => s.Tags.Contains(SystemTags.WATER)))
                .ToList();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pixelPos = new Vector2(x + minX, y + minY);
                    int idx = y * width + x;

                    foreach (var lake in waterShapes)
                    {
                        if (lake.Shape.Contains(pixelPos))
                        {
                            string projMode = lake.GetProperty("ProjectionMode", "spline");
                            string easingMode = lake.GetProperty("EasingMode", "linear");
                            float baseHeight = float.Parse(lake.GetProperty("BaseHeight", "160"));
                            float startOffset = float.Parse(lake.GetProperty("StartHeightOffset", "0"));
                            float endOffset = float.Parse(lake.GetProperty("EndHeightOffset", "-60"));
                            float channelHalfWidth = float.Parse(lake.GetProperty("RiverWidth", "8")) * 8f; // Scaled to pixels
                            bool isAsymmetric = lake.GetProperty("Asymmetric", "True") == "True";
                            bool doSnap = lake.GetProperty("Snap", "True") == "True";

                            float startHeight = baseHeight - startOffset;
                            float endHeight = baseHeight - endOffset;

                            float t = 0.0f;

                            if (projMode == "linear" && lake.Shape.Vertices.Count >= 2)
                            {
                                // Linear Slope projection utilizing vector projection from Anchor A to B
                                Vector2 anchorA = lake.Shape.Vertices[0];
                                Vector2 anchorB = lake.Shape.Vertices[1];

                                Vector2 ab = anchorB - anchorA;
                                Vector2 ap = pixelPos - anchorA;
                                float lenSq = ab.LengthSquared();
                                if (lenSq > 0)
                                {
                                    t = Vector2.Dot(ap, ab) / lenSq;
                                }
                                t = MathHelper.Clamp(t, 0.0f, 1.0f);
                            }
                            else if (projMode == "concentric")
                            {
                                // Concentric Basin projection centered on the centroid of the shape
                                Vector2 centroid = Vector2.Zero;
                                foreach (var v in lake.Shape.Vertices) centroid += v;
                                centroid /= lake.Shape.Vertices.Count;

                                float distEdge = GetDistanceToShoreline(pixelPos, lake);
                                float distCenter = Vector2.Distance(pixelPos, centroid);
                                if (distEdge + distCenter > 0)
                                {
                                    t = distEdge / (distEdge + distCenter);
                                }
                            }
                            else if (projMode == "spline" && lake.SpineWaypoints != null && lake.SpineWaypoints.Count >= 2)
                            {
                                // Winding Spline Longitudinal projection
                                var projection = GetSplineProjection(pixelPos, lake.SpineWaypoints);
                                float tLateral = MathHelper.Clamp(projection.Dist / channelHalfWidth, 0.0f, 1.0f);

                                // Asymmetric profile bends flow path around river bends
                                float profileCurve = MathF.Cos(tLateral * MathHelper.PiOver2);
                                if (isAsymmetric)
                                {
                                    float twistBias = projection.Curvature * 15f;
                                    float warpedLateral = MathHelper.Clamp((projection.Dist + twistBias) / channelHalfWidth, 0.0f, 1.0f);
                                    profileCurve = MathF.Cos(warpedLateral * MathHelper.PiOver2);
                                }

                                float baseLongitudinal = startHeight + projection.U * (endHeight - startHeight);
                                float calculatedDepth = baseLongitudinal - (profileCurve * (startHeight - endHeight));
                                t = MathHelper.Clamp(calculatedDepth / 255.0f, 0.0f, 1.0f);
                            }

                            // --- APPLY DESCENT CURVE EASING FUNCTIONS ---
                            float easedT = t;
                            if (easingMode == "easeIn")
                            {
                                easedT = t * t;
                            }
                            else if (easingMode == "easeOut")
                            {
                                easedT = t * (2f - t);
                            }
                            else if (easingMode == "easeInOut")
                            {
                                easedT = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
                            }
                            else if (easingMode == "zoned")
                            {
                                if (t < 0.3f)
                                {
                                    easedT = (t / 0.3f) * 0.12f;
                                }
                                else if (t < 0.7f)
                                {
                                    float nt = (t - 0.3f) / 0.4f;
                                    float ease = nt < 0.5f ? 2f * nt * nt : -1f + (4f - 2f * nt) * nt;
                                    //finalHeight = 0.12f + ease * 0.78f;
                                }
                                else
                                {
                                    easedT = 0.90f + ((t - 0.7f) / 0.3f) * 0.10f;
                                }
                            }

                            float heightValue = startHeight + easedT * (endHeight - startHeight);
                            if (projMode == "spline") heightValue = easedT * 255f;

                            // Enforce clamping constraint (Minimum value of 15 inside waterbeds)
                            int finalHeight = (int)MathHelper.Clamp(heightValue, 15f, 240f);

                            if (doSnap)
                            {
                                finalHeight = FindClosestDimetricLevel(finalHeight);
                            }

                            bakedTerrainMask[idx].B = (byte)finalHeight;
                        }
                    }
                }
            }

            WriteTerrainHeightMapToChunks(gd, terrainMask, bakedTerrainMask, width, height, map);
            return terrainMask;
        }

        private struct SplineProjectionResult
        {
            public float Dist;
            public float U;
            public float Curvature;
        }

        private static SplineProjectionResult GetSplineProjection(Vector2 pt, List<Vector2> spine)
        {
            float totalLength = 0;
            float[] segmentLengths = new float[spine.Count - 1];
            for (int i = 0; i < spine.Count - 1; i++)
            {
                float l = Vector2.Distance(spine[i], spine[i + 1]);
                segmentLengths[i] = l;
                totalLength += l;
            }

            float minD2 = float.MaxValue;
            float bestU = 0;
            float cumulativeLength = 0;
            int bestIdx = 0;

            for (int i = 0; i < spine.Count - 1; i++)
            {
                var res = distToSegmentSquared(pt, spine[i], spine[i + 1]);
                if (res.d2 < minD2)
                {
                    minD2 = res.d2;
                    float progressInSegment = res.t * segmentLengths[i];
                    bestU = totalLength > 0 ? (cumulativeLength + progressInSegment) / totalLength : 0;
                    bestIdx = i;
                }
                cumulativeLength += segmentLengths[i];
            }

            float curvature = 0;
            if (spine.Count > 2 && bestIdx > 0 && bestIdx < spine.Count - 1)
            {
                Vector2 prev = spine[bestIdx - 1];
                Vector2 curr = spine[bestIdx];
                Vector2 next = spine[bestIdx + 1];

                Vector2 v1 = curr - prev;
                Vector2 v2 = next - curr;

                if (v1.Length() > 0 && v2.Length() > 0)
                {
                    v1.Normalize();
                    v2.Normalize();
                    curvature = v1.X * v2.Y - v1.Y * v2.X;
                }
            }

            return new SplineProjectionResult
            {
                Dist = (float)Math.Sqrt(minD2),
                U = bestU,
                Curvature = curvature
            };
        }
        private struct SegmentProjection
        {
            public float d2;
            public float t;
        }
        private static SegmentProjection distToSegmentSquared(Vector2 p, Vector2 v, Vector2 w)
        {
            float l2 = Vector2.DistanceSquared(v, w);
            if (l2 == 0f) return new SegmentProjection { d2 = Vector2.DistanceSquared(p, v), t = 0f };
            float t = Vector2.Dot(p - v, w - v) / l2;
            t = MathHelper.Clamp(t, 0f, 1f);
            float d2 = Vector2.DistanceSquared(p, v + t * (w - v));
            return new SegmentProjection { d2 = d2, t = t };
        }
        private static int FindClosestDimetricLevel(int val)
        {
            int closest = 0;
            int minDiff = int.MaxValue;
            int[] levels = { 0, 15, 120, 128, 160, 192, 200, 240, 255 };

            for (int i = 0; i < levels.Length; i++)
            {
                int diff = Math.Abs(levels[i] - val);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = levels[i];
                }
            }
            return closest;
        }

        /// <summary>
        /// Bakes purely water surface attributes (R: Flow, G: Foam, B: Swells) into waterdata.png.
        /// Step 2 of export.
        /// </summary>
        public static Texture2D BakeWaterSurfaceData(GraphicsDevice gd, Map map, MaskLayer terrainMask)
        {
            int minX = terrainMask.OffsetX * MaskLayer.CHUNK_PIXEL_SIZE;
            int minY = terrainMask.OffsetY * MaskLayer.CHUNK_PIXEL_SIZE;

            int maxChunkX = terrainMask.Chunks.Keys.Max(p => p.X);
            int maxChunkY = terrainMask.Chunks.Keys.Max(p => p.Y);

            int width = (maxChunkX - terrainMask.OffsetX + 1) * MaskLayer.CHUNK_PIXEL_SIZE;
            int height = (maxChunkY - terrainMask.OffsetY + 1) * MaskLayer.CHUNK_PIXEL_SIZE;

            RenderTarget2D waterBakeTarget = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            Color[] bakedWaterData = new Color[width * height];

            var waterShapes = map.Layers
                .OfType<ControlLayer>()
                .SelectMany(l => l.Shapes.Where(s => s.Tags.Contains(SystemTags.WATER)))
                .ToList();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pixelPos = new Vector2(x + minX, y + minY);
                    int idx = y * width + x;

                    bakedWaterData[idx] = new Color(0, 0, 0, 0); // Transparent outside water

                    foreach (var lake in waterShapes)
                    {
                        if (lake.Shape.Contains(pixelPos))
                        {
                            // R Channel: Flow Vector Angle
                            float flowX = float.Parse(lake.GetProperty("FlowX", "0.0"));
                            float flowY = float.Parse(lake.GetProperty("FlowY", "0.0"));
                            float angleRad = (float)Math.Atan2(flowY, flowX);
                            if (angleRad < 0) angleRad += MathHelper.TwoPi;
                            byte rFlow = (byte)((angleRad / MathHelper.TwoPi) * 255f);

                            // G Channel: Foam Density
                            float distToShore = GetDistanceToShoreline(pixelPos, lake);
                            byte gFoam = (byte)MathHelper.Clamp(255f - (distToShore * 15f), 0, 255);

                            // B Channel: Wave characteristics
                            byte bCustom = 255;

                            bakedWaterData[idx] = new Color((int)rFlow, gFoam, bCustom, 255);
                            break;
                        }
                    }
                }
            }

            waterBakeTarget.SetData(bakedWaterData);
            return waterBakeTarget;
        }

        private static byte GetShorelineElevation(ShapeObject lake, Color[] terrainMask, int w, int h, int minX, int minY)
        {
            Vector2 startNode = lake.Shape.Vertices[0];
            int lx = (int)startNode.X - minX;
            int ly = (int)startNode.Y - minY;

            // Search outward from the edge node to bypass unpainted pixels and find real land elevation
            for (int r = 1; r < 6; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int sx = lx + dx * 8; // Scan in 8-pixel increments
                        int sy = ly + dy * 8;

                        if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                        {
                            byte elev = terrainMask[sy * w + sx].B;
                            if (elev > 40) // Found painted terrain (altitude > 40)
                            {
                                return elev;
                            }
                        }
                    }
                }
            }

            return 180; // Standard shoreline elevation fallback
        }

        private static float GetDistanceToShoreline(Vector2 point, ShapeObject lake)
        {
            float minDistance = float.MaxValue;
            int count = lake.Shape.Vertices.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 v1 = lake.Shape.Vertices[i];
                Vector2 v2 = lake.Shape.Vertices[(i + 1) % count];

                Vector2 ab = v2 - v1;
                Vector2 ap = point - v1;
                float t = MathHelper.Clamp(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab), 0.0f, 1.0f);
                Vector2 closestPoint = v1 + ab * t;

                float dist = Vector2.Distance(point, closestPoint);
                if (dist < minDistance) minDistance = dist;
            }

            return minDistance;
        }

        private static void ExtractContinuousTerrainHeightMap(GraphicsDevice gd, MaskLayer terrainMask, Color[] heightData, int w, int h, Map map)
        {
            // FIX A: Reference terrainMask's offsets directly to prevent coordinates drift
            int offsetX = terrainMask.OffsetX;
            int offsetY = terrainMask.OffsetY;

            for (int y = 0; y < h; y += 256)
            {
                for (int x = 0; x < w; x += 256)
                {
                    Point chunkCoord = new Point((x / 256) + offsetX, (y / 256) + offsetY);
                    if (terrainMask.Chunks.TryGetValue(chunkCoord, out var chunkRT))
                    {
                        Color[] chunkData = new Color[256 * 256];
                        chunkRT.GetData(chunkData);

                        for (int cy = 0; cy < 256 && y + cy < h; cy++)
                        {
                            for (int cx = 0; cx < 256 && x + cx < w; cx++)
                            {
                                int destIdx = (y + cy) * w + (x + cx);
                                int srcIdx = cy * 256 + cx;
                                heightData[destIdx] = chunkData[srcIdx];
                            }
                        }
                    }
                }
            }
        }

        // Inside Map.cs -> MapExporter Class
        private static void WriteTerrainHeightMapToChunks(GraphicsDevice gd, MaskLayer terrainMask, Color[] heightData, int w, int h, Map map)
        {
            int offsetX = terrainMask.OffsetX;
            int offsetY = terrainMask.OffsetY;

            for (int y = 0; y < h; y += 256)
            {
                for (int x = 0; x < w; x += 256)
                {
                    Point chunkCoord = new Point((x / 256) + offsetX, (y / 256) + offsetY);
                    if (terrainMask.Chunks.TryGetValue(chunkCoord, out var chunkRT))
                    {
                        // Preserve pre-existing Red (Biomes), Green (Spawns), and Alpha (Noise details)
                        Color[] chunkData = new Color[256 * 256];
                        chunkRT.GetData(chunkData);

                        for (int cy = 0; cy < 256 && y + cy < h; cy++)
                        {
                            for (int cx = 0; cx < 256 && x + cx < w; cx++)
                            {
                                int srcIdx = (y + cy) * w + (x + cx);
                                int destIdx = cy * 256 + cx;

                                // Overwrite ONLY the Blue Channel with baked slope heights
                                chunkData[destIdx].B = heightData[srcIdx].B;
                            }
                        }
                        chunkRT.SetData(chunkData);
                    }
                }
            }
        }
    }

}