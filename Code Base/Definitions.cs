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

   
}

