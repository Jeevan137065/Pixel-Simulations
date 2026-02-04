using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using MonoGame.Extended;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Clipper2Lib;


namespace Pixel_Simulations
{
    public enum GameState { Playing, InventoryOpen }
    public static class ListExtensions
    {
        private static Random _random = new Random();

        // Fisher-Yates shuffle algorithm
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public static class PathHelper
    {
        private static string _solutionRoot;

        public static string GetSolutionRoot()
        {
            if (!string.IsNullOrEmpty(_solutionRoot))
                return _solutionRoot;

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            // Go up from bin/Debug/net8.0 etc. until we find a .sln file or a specific folder
            while (currentDir != null && !Directory.GetFiles(currentDir, "*.sln").Any() && !Directory.Exists(Path.Combine(currentDir, "Pixel Simulations")))
            {
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            _solutionRoot = currentDir;
            return _solutionRoot;
        }

        /// <summary>
        /// Gets the absolute path to the shared Assets folder.
        /// </summary>
        public static string GetAssetsPath()
        {
            string root = GetSolutionRoot();
            if (string.IsNullOrEmpty(root))
                return null; // Or throw an exception

            // Your desired path
            return Path.Combine(root, "Pixel Simulations", "Assets");
        }
    }
    public struct ParticleVertex : IVertexType
    {
        public Vector2 Position; // The particle's current position
        public Vector2 Velocity; // The particle's current velocity
        public Vector4 Metadata; // Store extra data: .x=Lifetime, .y=Type (0=rain, 1=splash), .zw=RandomSeed

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
            new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
            new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.Position, 1),
            new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
    //create 1,1 or size, size texture with edges, at any where
    public class PixelTexture
    {

        private static GraphicsDevice gd;
        private static Texture2D pixelTexture, GridTexture;

        public PixelTexture(GraphicsDevice _gd, int size)
        {
            gd = _gd;
            pixelTexture = new Texture2D(_gd, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            GridTexture = new Texture2D(_gd, size, size);
            var colorData = new Color[size * size];
            for (int i = 0; i < colorData.Length; i++)
            {
                int x = i % size;
                int y = i / size;
                if (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                {
                    colorData[i] = Color.White; // Border
                }
                else
                {
                    colorData[i] = Color.Transparent;
                }
            }
            GridTexture.SetData(colorData);
        }

        public Texture2D GetPixelTexture(bool A)
        {
            if (A) return GridTexture;
            else return pixelTexture;
        }

    }
    public class Vector2Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            // This converter is only responsible for the Vector2 type.
            return objectType == typeof(Vector2);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Load the JSON object from the reader.
            JObject jObject = JObject.Load(reader);

            // Extract the 'X' and 'Y' properties as floats.
            float x = jObject["X"].Value<float>();
            float y = jObject["Y"].Value<float>();

            // Create and return the new Vector2.
            return new Vector2(x, y);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // (We don't need to save presets, but this is how you would implement it.)
            var vec = (Vector2)value;
            writer.WriteStartObject();
            writer.WritePropertyName("X");
            writer.WriteValue(vec.X);
            writer.WritePropertyName("Y");
            writer.WriteValue(vec.Y);
            writer.WriteEndObject();
        }
    }
    public class MapObjectConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(MapObject).IsAssignableFrom(objectType);
        public override bool CanWrite => false; // We will let the default serializer write, as it's simpler.

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            string typeString = jo["Type"]?.Value<string>(); // Use the "Type" enum property

            MapObject target = null;
            if (Enum.TryParse(typeString, out ObjectType type))
            {
                switch (type)
                {
                    case ObjectType.Rectangle: target = new RectangleObject(); break;
                    case ObjectType.Point: target = new PointObject(); break;
                    case ObjectType.Shape: target = new ShapeObject(); break; // ADDED THIS
                    //case ObjectType.Prop: target = new PropObject(); break;   // ADDED THIS
                }
                if (target != null)
                {
                    serializer.Populate(jo.CreateReader(), target);
                }
            }
            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException(); // We disabled writing
        }
    }
    public class ColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            // Save the color as a simple RGBA string, e.g., "255,0,0,255"
            writer.WriteValue($"{value.R},{value.G},{value.B},{value.A}");
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var parts = ((string)reader.Value).Split(',');
                if (parts.Length == 4)
                {
                    return new Color(
                        int.Parse(parts[0]),
                        int.Parse(parts[1]),
                        int.Parse(parts[2]),
                        int.Parse(parts[3])
                    );
                }
            }
            return Color.Magenta; // Return a default error color if parsing fails
        }
    }
    public class LayerConverter : JsonConverter
    {
        public override bool CanWrite => true;
        public override bool CanConvert(Type objectType) => typeof(Layer).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var layer = (Layer)value;
            writer.WriteStartObject();

            // Write common properties
            writer.WritePropertyName("$type"); writer.WriteValue(layer.Type.ToString());
            writer.WritePropertyName("Name"); writer.WriteValue(layer.Name);
            writer.WritePropertyName("IsVisible"); writer.WriteValue(layer.IsVisible);
            writer.WritePropertyName("IsLocked"); writer.WriteValue(layer.IsLocked);

            // Write type-specific properties
            switch (layer.Type)
            {
                case LayerType.Tile:
                    var tileLayer = (TileLayer)layer;
                    writer.WritePropertyName("Chunks");
                    serializer.Serialize(writer, tileLayer.Chunks.Select(kvp => new { Key = kvp.Key, Value = kvp.Value }));
                    break;
                case LayerType.Object:
                    var objectLayer = (ObjectLayer)layer;
                    writer.WritePropertyName("Objects");
                    serializer.Serialize(writer, objectLayer.Objects);
                    break;
                case LayerType.Collision:
                    var collisionLayer = (CollisionLayer)layer;
                    writer.WritePropertyName("CollisionMesh");
                    serializer.Serialize(writer, collisionLayer.CollisionMesh);
                    break;
                case LayerType.Navigation:
                    var navLayer = (NavigationLayer)layer;
                    writer.WritePropertyName("NavigationMesh");
                    serializer.Serialize(writer, navLayer.NavigationMesh);
                    break;
                case LayerType.Trigger:
                    var triggerLayer = (TriggerLayer)layer;
                    writer.WritePropertyName("TriggerMesh");
                    serializer.Serialize(writer, triggerLayer.TriggerMesh);
                    writer.WritePropertyName("PointTriggers");
                    serializer.Serialize(writer, triggerLayer.PointTriggers);
                    break;
            }

            writer.WriteEndObject();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            string typeString = jo["$type"]?.Value<string>();

            Layer target = null;
            if (Enum.TryParse(typeString, out LayerType type))
            {
                // Create the correct concrete class
                switch (type)
                {
                    case LayerType.Tile: target = new TileLayer(); break;
                    case LayerType.Object: target = new ObjectLayer(); break;
                    case LayerType.Collision: target = new CollisionLayer(); break;
                    case LayerType.Navigation: target = new NavigationLayer(); break;
                    case LayerType.Trigger: target = new TriggerLayer(); break;
                    default: return null;
                }
                target.Name = jo["Name"]?.Value<string>() ?? "Unnamed Layer";
                target.IsVisible = jo["IsVisible"]?.Value<bool>() ?? true;
                target.IsLocked = jo["IsLocked"]?.Value<bool>() ?? false;

                switch (target.Type)
                {
                    case LayerType.Tile:
                        var tileLayer = (TileLayer)target;
                        var chunksToken = jo["Chunks"];
                        if (chunksToken != null && chunksToken.Type == JTokenType.Array)
                        {
                            tileLayer.Chunks = new Dictionary<Point, Chunk>();
                            foreach (var item in chunksToken.Children<JObject>())
                            {
                                Point key = item["Key"].ToObject<Point>(serializer);
                                Chunk chunk = item["Value"].ToObject<Chunk>(serializer);
                                tileLayer.Chunks[key] = chunk;
                            }
                        }
                        break;
                    case LayerType.Object:
                        var objLayer = (ObjectLayer)target;
                        objLayer.Objects = jo["Objects"]?.ToObject<List<MapObject>>(serializer) ?? new List<MapObject>();
                        break;
                    case LayerType.Collision:
                        var colLayer = (CollisionLayer)target;
                        colLayer.CollisionMesh = jo["CollisionMesh"]?.ToObject<List<ShapeObject>>(serializer) ?? new List<ShapeObject>();
                        break;
                    case LayerType.Navigation:
                        var navLayer = (NavigationLayer)target;
                        navLayer.NavigationMesh = jo["NavigationMesh"]?.ToObject<List<ShapeObject>>(serializer) ?? new List<ShapeObject>();
                        break;
                    case LayerType.Trigger:
                        var trigLayer = (TriggerLayer)target;
                        trigLayer.TriggerMesh = jo["TriggerMesh"]?.ToObject<List<RectangleObject>>(serializer) ?? new List<RectangleObject>();
                        trigLayer.PointTriggers = jo["PointTriggers"]?.ToObject<List<PointObject>>(serializer) ?? new List<PointObject>();
                        break;
                }
            }
            return target;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public partial class Polygon
    {
        [JsonProperty]
        public List<Vector2> Vertices { get; set; } = new List<Vector2>();
        private const double CLIPPER_SCALE = 1000.0; // Scale to preserve 3 decimal places

        public Polygon(List<Vector2> vertices) => Vertices = vertices;

        // Parameterless constructor for deserialization
        public Polygon()    {}
        /// Creates a rectangular polygon from two corner points.
        public static Polygon FromRectangle(Vector2 p1, Vector2 p2)
        {
            float minX = System.Math.Min(p1.X, p2.X);
            float minY = System.Math.Min(p1.Y, p2.Y);
            float maxX = System.Math.Max(p1.X, p2.X);
            float maxY = System.Math.Max(p1.Y, p2.Y);

            return new Polygon(new List<Vector2>
            {
                new Vector2(minX, minY),
                new Vector2(maxX, minY),
                new Vector2(maxX, maxY),
                new Vector2(minX, maxY)
            });
        }
        public static Polygon FromVertices(List<Vector2> vertices) => new Polygon(new List<Vector2>(vertices));

        // --- Clipper2 Integration ---
        public Path64 ToClipperPath()
        {
            Path64 path = new Path64();
            foreach (var v in Vertices)
                path.Add(new Point64(v.X * CLIPPER_SCALE, v.Y * CLIPPER_SCALE));
            return path;
        }
        public static List<Vector2> FromClipperPath(Path64 path)
        {
            return path.Select(p => new Vector2((float)(p.X / CLIPPER_SCALE), (float)(p.Y / CLIPPER_SCALE))).ToList();
        }
        public RectangleF GetBounds()
        {
            if (Vertices.Count == 0) return RectangleF.Empty;
            float minX = Vertices.Min(v => v.X);
            float minY = Vertices.Min(v => v.Y);
            float maxX = Vertices.Max(v => v.X);
            float maxY = Vertices.Max(v => v.Y);
            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public bool Contains(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
            {
                if (((Vertices[i].Y > p.Y) != (Vertices[j].Y > p.Y)) &&
                    (p.X < (Vertices[j].X - Vertices[i].X) * (p.Y - Vertices[i].Y) / (Vertices[j].Y - Vertices[i].Y) + Vertices[i].X))
                    inside = !inside;
            }
            return inside;
        }

        public void Offset(Vector2 delta)
        {
            for (int i = 0; i < Vertices.Count; i++) Vertices[i] += delta;
        }

        public void Scale(Vector2 scale, Vector2 origin)
        {
            for (int i = 0; i < Vertices.Count; i++)
                Vertices[i] = origin + (Vertices[i] - origin) * scale;
        }
        public void UpdateVertices(Vector2 oldPos, Vector2 oldSize, Vector2 newPos, Vector2 newSize)
        {
            if (oldSize.X == 0 || oldSize.Y == 0) return;

            for (int i = 0; i < Vertices.Count; i++)
            {
                // 1. Convert to 0.0 - 1.0 local space based on old bounds
                Vector2 local = (Vertices[i] - oldPos) / oldSize;
                // 2. Map back to world space based on new bounds
                Vertices[i] = newPos + (local * newSize);
            }
        }
    }


}

