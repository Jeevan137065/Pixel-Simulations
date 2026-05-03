using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    public class HexColorConverter : JsonConverter<Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string hex = (string)reader.Value;
            if (hex != null && hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new Color((int)r, (int)g, (int)b, (int)255);
            }
            return Color.White;
        }
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer) => throw new NotImplementedException();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class GrassSettings
    {
        // Geometry
        [JsonProperty("height")] public float GlobalHeightBase { get; set; } = 120f;
        [JsonProperty("width")] public float BladeThickness { get; set; } = 10f;
        [JsonProperty("taper")] public float Taper { get; set; } = 0.8f;
        [JsonProperty("curve")] public float RestingCurvature { get; set; } = 15f;

        // Colors (Using our custom Hex converter!)
        [JsonProperty("cRoot"), JsonConverter(typeof(HexColorConverter))]
        public Color RootColor { get; set; } = new Color(10, 42, 10);

        [JsonProperty("cTip"), JsonConverter(typeof(HexColorConverter))]
        public Color TipColor { get; set; } = new Color(46, 140, 46);

        // Thresholds
        [JsonProperty("densityStep")] public float DensityStep { get; set; } = 10f;
        [JsonProperty("minThresh")] public float MinThreshold { get; set; } = 0.4f;
        [JsonProperty("midThresh")] public float MidThreshold { get; set; } = 0.6f;
        [JsonProperty("maxThresh")] public float MaxThreshold { get; set; } = 0.8f;

        // Physics
        [JsonProperty("stiff")] public float Stiffness { get; set; } = 0.4f;
        [JsonProperty("wSpeed")] public float WindSpeed { get; set; } = 2.0f;
        [JsonProperty("wInt")] public float WindIntensity { get; set; } = 0.5f;

        // Flora Data
        [JsonProperty("flowerType")] public int FlowerType { get; set; } = 0;
        [JsonProperty("fProb")] public float FlowerProbability { get; set; } = 0.1f;

        [JsonProperty("cFlower"), JsonConverter(typeof(HexColorConverter))]
        public Color FlowerColor { get; set; } = new Color(155, 89, 182);

        [JsonProperty("cFlower2"), JsonConverter(typeof(HexColorConverter))]
        public Color FlowerColor2 { get; set; } = new Color(224, 170, 255);

        [JsonProperty("fMin")] public float FlowerMinSize { get; set; } = 10f;
        [JsonProperty("fMax")] public float FlowerMaxSize { get; set; } = 15f;

        // Player Interaction (Not exported by HTML, so we set defaults)
        public float PlayerPushRadius = 30f;
        public float PlayerPushStrength = 1.5f;
        public int Segments = 4; // Hardcoded segments for performance consistency
    }

    // Update the GrassVertex struct to include Flora Data
    public struct GrassVertex : IVertexType
    {
        public Vector3 RootPosition;
        public Vector2 T_Side;
        public Vector2 Wind_Height;
        public float Variation;
        public Color Color;
        public Vector4 FloraData;    // NEW: x=Type, y=Size, z=Empty, w=Empty

        public GrassVertex(Vector2 root, float t, float side, float wind, float height, float var, float lean, Color col, int fType, float fSize)
        {
            RootPosition = new Vector3(root.X, root.Y, lean);
            T_Side = new Vector2(t, side);
            Wind_Height = new Vector2(wind, height);
            Variation = var;
            Color = col;
            FloraData = new Vector4(fType, fSize, 0, 0);
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(28, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3) // FloraData
        );
        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
