using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;

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
    public class Map
    {

        [JsonProperty]
        public List<Layer> Layers { get; set; }

        public Map()
        {
            Layers = new List<Layer>();
            // Every new map starts with a default, unlocked Ground layer.
            Layers.Add(new TileLayer("Ground"));
            
        }

        // Json.NET requires a parameterless constructor for deserialization
        //public Map() { }

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
                // --- FILE HEADER ---
                writer.Write("PMAP".ToCharArray()); // 4-byte magic string to identify our file type
                writer.Write(1);      // Version number

                // --- LAYER DATA ---
                writer.Write(map.Layers.Count);
                foreach (var layer in map.Layers)
                {
                    writer.Write((int)layer.Type);
                    writer.Write(layer.Name ?? " ");
                    writer.Write(layer.IsVisible);
                    writer.Write(layer.IsLocked);

                    // Write type-specific data
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
                            // Do nothing! The base layer info (Name, Visible, Locked) is already written.
                            // The chunk pixel data is handled by the PNG exporter.
                            break;
                    }
                }
            }
        }
        public static Map Read(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            var map = new Map();
            map.Layers.Clear(); // Clear the default "Ground" layer

            using (var stream = File.Open(filePath, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                // --- FILE HEADER ---
                string magic = new string(reader.ReadChars(4));
                if (magic != "PMAP") throw new System.Exception("Invalid map file format.");
                int version = reader.ReadInt32();

                // --- LAYER DATA ---
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
                            newLayer = ReadObjectLayer(reader, name);
                            break;
                        case LayerType.Control:
                            newLayer = ReadControlLayer(reader, name);
                            break;
                        case LayerType.Mask:
                            // Do nothing! The base layer info (Name, Visible, Locked) is already written.
                            newLayer = new MaskLayer(name);
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
                            writer.Write(false); // Indicates no tile here
                        }
                        else
                        {
                            writer.Write(true); // Indicates a tile follows
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
                WriteMapObjectBase(writer, prop); // <--- Replaces writer.Write(prop.Name)
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
                WriteMapObjectBase(writer, shape); // Saves ID, Links, Tags, Name
                writer.Write(shape.Shape.Vertices.Count);
                foreach (var v in shape.Shape.Vertices) { writer.Write(v.X); writer.Write(v.Y); }
            }

            // Write Rectangles
            writer.Write(layer.Rectangles.Count);
            foreach (var rect in layer.Rectangles)
            {
                WriteMapObjectBase(writer, rect); // Saves ID, Links, Tags, Name
                writer.Write(rect.Position.X); writer.Write(rect.Position.Y);
                writer.Write(rect.Size.X); writer.Write(rect.Size.Y);
            }

            // Write Points
            writer.Write(layer.Points.Count);
            foreach (var pt in layer.Points)
            {
                WriteMapObjectBase(writer, pt); // Saves ID, Links, Tags, Name
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
                        if (reader.ReadBoolean()) // Check if a tile exists
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
        private static ObjectLayer ReadObjectLayer(BinaryReader reader, string name)
        {
            var layer = new ObjectLayer(name);
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var prop = new PropObject();
                ReadMapObjectBase(reader, prop); // <--- Replaces Name = reader.ReadString()
                prop.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                prop.PrefabID = reader.ReadString();
                prop.Scale = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                prop.Rotation = reader.ReadSingle();
                layer.Objects.Add(prop);
            }
            return layer;
        }
        private static ControlLayer ReadControlLayer(BinaryReader reader, string name)
        {
            var layer = new ControlLayer(name);

            // Read Shapes
            int sCount = reader.ReadInt32();
            for (int i = 0; i < sCount; i++)
            {
                var s = new ShapeObject();
                ReadMapObjectBase(reader, s); // Loads ID, Links, Tags, Name
                int vCount = reader.ReadInt32();
                var verts = new List<Vector2>();
                for (int j = 0; j < vCount; j++) verts.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                s.Shape = new Polygon(verts);
                s.UpdateBoundsFromVertices();
                layer.Shapes.Add(s);
            }

            // Read Rectangles
            int rCount = reader.ReadInt32();
            for (int i = 0; i < rCount; i++)
            {
                var r = new RectangleObject();
                ReadMapObjectBase(reader, r); // Loads ID, Links, Tags, Name
                r.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                r.Size = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                layer.Rectangles.Add(r);
            }

            // Read Points
            int pCount = reader.ReadInt32();
            for (int i = 0; i < pCount; i++)
            {
                var p = new PointObject();
                ReadMapObjectBase(reader, p); // Loads ID, Links, Tags, Name
                p.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                p.Radius = reader.ReadSingle();
                layer.Points.Add(p);
            }

            return layer;
        }
        #endregion
        private static void WriteMapObjectBase(BinaryWriter writer, MapObject obj)
        {
            writer.Write(obj.ID); // Save the unique ID
            writer.Write(obj.Name ?? "");

            // Write Links
            writer.Write(obj.LinkedObjects.Count);
            foreach (var link in obj.LinkedObjects) writer.Write(link);

            // Write Tags
            writer.Write(obj.Tags.Count);
            foreach (var tag in obj.Tags) writer.Write(tag);
            // Write Properties
            writer.Write(obj.Properties.Count);
            foreach (var kvp in obj.Properties)
            {
                writer.Write(kvp.Key);
                writer.Write((int)kvp.Value.Type);  // Save type as an int
                writer.Write(kvp.Value.Value ?? ""); // Save value as string
            }
        }
        private static void ReadMapObjectBase(BinaryReader reader, MapObject obj)
        {
            obj.ID = reader.ReadString(); // Load the unique ID
            obj.Name = reader.ReadString();

            // Read Links
            int linkCount = reader.ReadInt32();
            obj.LinkedObjects = new List<string>();
            for (int i = 0; i < linkCount; i++) obj.LinkedObjects.Add(reader.ReadString());

            // Read Tags
            int tagCount = reader.ReadInt32();
            obj.Tags = new HashSet<int>();
            for (int i = 0; i < tagCount; i++) obj.Tags.Add(reader.ReadInt32());

            // --- NEW: READ PROPERTIES ---
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
        public static void CaptureToImage(Map map, GraphicsDevice gd,Editor.EditorState state, string path)
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
            int width = (int)System.Math.Min(bounds.Width, 4096);
            int height = (int)System.Math.Min(bounds.Height, 4096);

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
        public static void SaveMaskLayer(MaskLayer maskLayer, string imagePath, GraphicsDevice gd, Editor.EditorState state)
        {
            if (maskLayer == null || maskLayer.Chunks.Count == 0) return;

            // 1. Calculate Bounds and save them directly to the MaskLayer instance!
            maskLayer.OffsetX = maskLayer.Chunks.Keys.Min(p => p.X);
            maskLayer.OffsetY = maskLayer.Chunks.Keys.Min(p => p.Y);

            int maxX = maskLayer.Chunks.Keys.Max(p => p.X);
            int maxY = maskLayer.Chunks.Keys.Max(p => p.Y);

            int width = (maxX - maskLayer.OffsetX + 1) * MaskLayer.CHUNK_PIXEL_SIZE;
            int height = (maxY - maskLayer.OffsetY + 1) * MaskLayer.CHUNK_PIXEL_SIZE;

            var prevRt = gd.GetRenderTargets();

            // --- 2. STITCH RAW MASK ---
            using (var masterRt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
            {
                gd.SetRenderTarget(masterRt);
                gd.Clear(new Color(0, 0, 0, 255));

                using (var sb = new SpriteBatch(gd))
                {
                    sb.Begin(blendState: BlendState.Opaque, samplerState: SamplerState.PointClamp);
                    foreach (var kvp in maskLayer.Chunks)
                    {
                        Vector2 pos = new Vector2((kvp.Key.X - maskLayer.OffsetX) * MaskLayer.CHUNK_PIXEL_SIZE, (kvp.Key.Y - maskLayer.OffsetY) * MaskLayer.CHUNK_PIXEL_SIZE);
                        sb.Draw(kvp.Value, pos, Color.White);
                    }
                    sb.End();
                }

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(imagePath));
                using (var stream = System.IO.File.OpenWrite(imagePath)) { masterRt.SaveAsPng(stream, width, height); }

                // --- 3. GENERATE NORMAL MAP ---
                string normalPath = imagePath.Replace("_mask.png", "_normals.png");
                var normalShader = state.normalShader;
                if (normalShader != null)
                {
                    using (var normalRt = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None))
                    {
                        gd.SetRenderTarget(normalRt);
                        gd.Clear(Color.Transparent); // Clear to empty

                        // 1. Pass the Master Mask Texture to the Shader
                        normalShader.Parameters["MaskTexture"]?.SetValue(masterRt);
                        normalShader.Parameters["TextureSize"]?.SetValue(new Vector2(width, height));
                        normalShader.Parameters["HeightScale"]?.SetValue(15.0f); // Tweak bumpiness here!

                        // 2. Setup GPU State for a raw Full-Screen Quad Draw
                        gd.BlendState = BlendState.Opaque; // Ignore alpha blending, just write the exact pixel
                        gd.DepthStencilState = DepthStencilState.None;
                        gd.RasterizerState = RasterizerState.CullNone;
                        gd.SamplerStates[0] = SamplerState.PointClamp;

                        // 3. Create a Full-Screen Quad in Clip Space (-1.0 to 1.0)
                        // This perfectly covers the entire RenderTarget without needing a Camera Matrix!
                        VertexPositionTexture[] quadVerts = new VertexPositionTexture[4];
                        quadVerts[0] = new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0));  // Top Left
                        quadVerts[1] = new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0));   // Top Right
                        quadVerts[2] = new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)); // Bottom Left
                        quadVerts[3] = new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1));  // Bottom Right

                        short[] quadIndices = new short[] { 0, 1, 2, 1, 3, 2 };

                        // 4. Draw the Quad!
                        foreach (var pass in normalShader.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, quadVerts, 0, 4, quadIndices, 0, 2);
                        }

                        // 5. Save the resulting target to PNG
                        using (var stream = System.IO.File.OpenWrite(normalPath))
                        {
                            normalRt.SaveAsPng(stream, width, height);
                        }
                    }
                }
                gd.SetRenderTargets(prevRt);
            }
            // (Notice: We completely deleted the .meta saving code!)
        }
        public static void LoadMaskLayer(MaskLayer maskLayer, string imagePath, GraphicsDevice gd)
        {
            if (!System.IO.File.Exists(imagePath)) return;

            using (var stream = System.IO.File.OpenRead(imagePath))
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

            using (var stream = System.IO.File.OpenRead(imagePath))
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

            using (var stream = System.IO.File.OpenRead(imagePath))
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

}

