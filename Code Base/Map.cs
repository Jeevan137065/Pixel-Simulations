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
                    writer.Write(layer.Type.ToString());
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
                            WriteObjectLayer(writer, layer as ObjectLayer); // Placeholder for future
                            break;
                        case LayerType.Collision:
                            WriteCollisionLayer(writer, layer as CollisionLayer);
                            break;
                        case LayerType.Navigation:
                            WriteNavigationLayer(writer, layer as NavigationLayer);
                            break;
                        case LayerType.Trigger:
                            WriteTriggerLayer(writer, layer as TriggerLayer);
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
                        case LayerType.Collision:
                            newLayer = ReadCollisionLayer(reader, name);
                            break;
                        case LayerType.Navigation:
                            newLayer = ReadNavigationLayer(reader, name);
                            break;
                            // Add cases for other layer types
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
                        }
                    }
                }
            }
        }
        private static void WriteObjectLayer(BinaryWriter writer, ObjectLayer layer)
        {
            // Filter only PropObjects to be safe
            var props = layer.Objects.OfType<PropObject>().ToList();
            writer.Write(props.Count);

            foreach (var prop in props)
            {
                writer.Write(prop.Name); // Unique Name
                writer.Write(prop.Position.X);  writer.Write(prop.Position.Y);
                writer.Write(prop.PrefabID); // Link to objects.json
                writer.Write(prop.Scale.X);     writer.Write(prop.Scale.Y);
                writer.Write(prop.Rotation);
            }
        }
        private static void WriteCollisionLayer(BinaryWriter writer, CollisionLayer layer)
        {
            writer.Write(layer.CollisionMesh.Count);
            foreach (var shape in layer.CollisionMesh)
            {
                writer.Write(shape.Name ?? "");
                writer.Write(shape.Shape.Vertices.Count);
                foreach (var v in shape.Shape.Vertices)
                {
                    writer.Write(v.X);
                    writer.Write(v.Y);
                }
            }
        }
        private static void WriteNavigationLayer(BinaryWriter writer, NavigationLayer layer)
        {
            writer.Write(layer.NavigationMesh.Count);
            foreach (var shape in layer.NavigationMesh)
            {
                writer.Write(shape.Name ?? "");
                writer.Write(shape.Shape.Vertices.Count);
                foreach (var v in shape.Shape.Vertices)
                {
                    writer.Write(v.X);
                    writer.Write(v.Y);
                }
            }
        }
        private static void WriteTriggerLayer(BinaryWriter writer, TriggerLayer layer)
        {
            writer.Write(layer.TriggerMesh.Count);
            foreach (var rect in layer.TriggerMesh)
            {
                writer.Write(rect.Position.X); writer.Write(rect.Position.Y);
                writer.Write(rect.Size.X); writer.Write(rect.Size.Y);
            }
            writer.Write(layer.PointTriggers.Count);
            foreach (var rect in layer.PointTriggers)
            {
                writer.Write(rect.Position.X); writer.Write(rect.Position.Y);
                writer.Write(rect.Radius); writer.Write(rect.Label);
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
                            chunk.Tiles[x, y] = new TileInfo(tilesetName, tileId);
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
                var prop = new PropObject
                {
                    Name = reader.ReadString(),
                    Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    PrefabID = reader.ReadString(),
                    Scale = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    Rotation = reader.ReadSingle()
                };
                layer.Objects.Add(prop);
            }
            return layer;
        }
        private static CollisionLayer ReadCollisionLayer(BinaryReader reader, string name)
        {
            var layer = new CollisionLayer(name);
            int shapeCount = reader.ReadInt32();
            for (int i = 0; i < shapeCount; i++)
            {
                var shape = new ShapeObject();
                shape.Name = reader.ReadString();
                int vertCount = reader.ReadInt32();
                var verts = new List<Vector2>();
                for (int j = 0; j < vertCount; j++)
                {
                    verts.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
                shape.Shape = new Polygon(verts);
                shape.UpdateBoundsFromVertices(); // Sync Position/Size for AABB checks
                layer.CollisionMesh.Add(shape);
            }
            return layer;
        }
        private static NavigationLayer ReadNavigationLayer(BinaryReader reader, string name)
        {
            var layer = new NavigationLayer(name);
            int shapeCount = reader.ReadInt32();
            for (int i = 0; i < shapeCount; i++)
            {
                var shape = new ShapeObject();
                shape.Name = reader.ReadString();
                int vertCount = reader.ReadInt32();
                var verts = new List<Vector2>();
                for (int j = 0; j < vertCount; j++)
                {
                    verts.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
                shape.Shape = new Polygon(verts);
                shape.UpdateBoundsFromVertices(); // Sync Position/Size for AABB checks
                layer.NavigationMesh.Add(shape);
            }
            return layer;
        }

        #endregion
    }

}

