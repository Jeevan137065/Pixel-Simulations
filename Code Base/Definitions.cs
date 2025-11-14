using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;


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
    public class PixelTexture {

        private static GraphicsDevice gd;
        private static Texture2D pixelTexture, GridTexture;

        public PixelTexture(GraphicsDevice _gd, int size) {
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

        public Texture2D GetPixelTexture(bool A) {
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

    public enum TilesetType
    {
        Layout,
        Terrain, // 100-piece organic set
        Normal,  // 16-piece structured set
        Object,  // Chunk/prefab set
        Animated // Future use
    }
    public enum LayerType { 
        Tile, Object }

    // A private helper class to structure our grid data for serialization
    public class GridEntry
    {
        public Point Position;
        public TileInfo Tile;
    }
    public class LayerConverter : JsonConverter<Layer>
    {
        

        private const string TypePropertyName = "$type";

        public override void WriteJson(JsonWriter writer, Layer value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(TypePropertyName);
            writer.WriteValue(value.Type.ToString());

            writer.WritePropertyName("Name"); writer.WriteValue(value.Name);
            writer.WritePropertyName("IsVisible"); writer.WriteValue(value.IsVisible);
            writer.WritePropertyName("IsLocked"); writer.WriteValue(value.IsLocked);

            if (value is TileLayer tileLayer)
            {
                writer.WritePropertyName("Grid");
                // *** THE FIX (WRITING): Convert the dictionary to a list of GridEntry objects ***
                var gridList = tileLayer.Grid.Select(kvp => new GridEntry { Position = kvp.Key, Tile = kvp.Value });
                serializer.Serialize(writer, gridList);
            }
            else if (value is ObjectLayer objectLayer)
            {
                writer.WritePropertyName("Objects");
                serializer.Serialize(writer, objectLayer.Objects);
            }
            writer.WriteEndObject();
        }

        public override Layer ReadJson(JsonReader reader, Type objectType, Layer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var obj = JObject.Load(reader);
            var typeString = obj[TypePropertyName]?.ToString();
            Layer layer = null;

            if (Enum.TryParse(typeString, out LayerType layerType))
            {
                switch (layerType)
                {
                    case LayerType.Tile:
                        var tileLayer = new TileLayer(obj["Name"]?.ToString());
                        tileLayer.IsVisible = obj["IsVisible"]?.Value<bool>() ?? true;
                        tileLayer.IsLocked = obj["IsLocked"]?.Value<bool>() ?? false;

                        // *** THE FIX (READING): Deserialize the array into a list of GridEntry objects ***
                        var gridList = obj["Grid"]?.ToObject<List<GridEntry>>(serializer);
                        if (gridList != null)
                        {
                            // Convert the list back into the runtime dictionary
                            tileLayer.Grid = new Dictionary<Point, TileInfo>();
                            foreach (var entry in gridList)
                            {
                                tileLayer.Grid[entry.Position] = entry.Tile;
                            }
                        }
                        layer = tileLayer;
                        break;

                    case LayerType.Object:
                        var objectLayer = new ObjectLayer(obj["Name"]?.ToString());
                        objectLayer.IsVisible = obj["IsVisible"]?.Value<bool>() ?? true;
                        objectLayer.IsLocked = obj["IsLocked"]?.Value<bool>() ?? false;
                        var objects = obj["Objects"]?.ToObject<List<MapObject>>(serializer);
                        if (objects != null) objectLayer.Objects = objects;
                        layer = objectLayer;
                        break;
                }
            }
            return layer;
        }
    }
    // You'll also need a converter for MapObject to handle RectangleObject etc.
    // This is a bit more involved, typically using JsonDerivedTypeAttribute.
    // For simplicity in this outline, we'll assume TypeNameHandling.Auto can handle it
    // with the right setup in JsonSerializerSettings.
}

