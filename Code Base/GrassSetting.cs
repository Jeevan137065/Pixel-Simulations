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
        // --- MASK & BIOME IDENTIFIER ---
        [JsonProperty("biomeMaskValue")] public int BiomeMaskValue { get; set; } = 10; // R-channel value in mask
        [JsonProperty("biomeThreshold")] public float BiomeThreshold { get; set; } = 0.30f;
        [JsonProperty("elevationScale")] public float ElevationScale { get; set; } = 45f; // Blue-channel Y displacement

        [JsonProperty("biomeTolerance")]    public int BiomeTolerance { get; set; } = 5;  // Allowed variation (useful if brushes use soft edges)
        // --- DENSITY & NOISE THRESHOLDS (GROWTH VARIATION) ---
        [JsonProperty("densityStep")] public float DensityStep { get; set; } = 11f;
        [JsonProperty("minThresh")] public float MinThreshold { get; set; } = 0.38f; // Below this = bare patch
        [JsonProperty("midThresh")] public float MidThreshold { get; set; } = 0.58f; // Threshold for standard tier
        [JsonProperty("maxThresh")] public float MaxThreshold { get; set; } = 0.78f; // Threshold for lush overgrown tier
        [JsonProperty("tuftMaxBlades")] public int TuftMaxBlades { get; set; } = 5;
        [JsonProperty("tuftRadius")] public float TuftRadius { get; set; } = 5.5f;

        // --- BLADE GEOMETRY ---
        [JsonProperty("baseHeight")] public float BaseHeight { get; set; } = 36f;
        [JsonProperty("bladeWidth")] public float BladeWidth { get; set; } = 3.2f;
        [JsonProperty("taper")] public float Taper { get; set; } = 0.85f;
        [JsonProperty("restingCurve")] public float RestingCurvature { get; set; } = 6f;
        [JsonProperty("segments")] public int Segments { get; set; } = 4;

        // --- TRAVELING WAVE WIND & CEL ANIMATION ---
        [JsonProperty("windAngle")] public float WindAngle { get; set; } = 35f;
        [JsonProperty("windSpeed")] public float WindSpeed { get; set; } = 2.4f;
        [JsonProperty("waveLength")] public float WaveLength { get; set; } = 180f;
        [JsonProperty("gustStrength")] public float GustStrength { get; set; } = 0.65f;
        [JsonProperty("flutterStr")] public float FlutterStr { get; set; } = 0.12f;
        [JsonProperty("stiffness")] public float Stiffness { get; set; } = 0.45f;
        [JsonProperty("animFPS")] public float AnimFPS { get; set; } = 12f; // Pixel-art cel stepping
        [JsonProperty("pixelSnap")] public bool PixelSnap { get; set; } = true;

        // --- COLORATION (3-POINT VERTICAL GRADIENT) ---
        [JsonProperty("cRoot"), JsonConverter(typeof(HexColorConverter))]
        public Color RootColor { get; set; } = new Color(18, 42, 18);

        [JsonProperty("cMid"), JsonConverter(typeof(HexColorConverter))]
        public Color MidColor { get; set; } = new Color(45, 106, 45);

        [JsonProperty("cTip"), JsonConverter(typeof(HexColorConverter))]
        public Color TipColor { get; set; } = new Color(86, 179, 73);

        // --- BOTANICAL FLORA TOPS & LIFE STAGES ---
        [JsonProperty("floraType")] public int FloraType { get; set; } = 3; // 0=None, 1=Dandelion, 2=Cattail, etc.
        [JsonProperty("floraStageMode")] public int FloraStageMode { get; set; } = -1; // -1=Random, 0=Bud, 1=Sprout, 2=Bloom, 3=Seed
        [JsonProperty("floraRotJitter")] public float FloraRotJitter { get; set; } = 45f; // Rotation jitter in degrees
        [JsonProperty("floraProb")] public float FloraProbability { get; set; } = 0.20f;
        [JsonProperty("floraMinSize")] public float FloraMinSize { get; set; } = 5f;
        [JsonProperty("floraMaxSize")] public float FloraMaxSize { get; set; } = 10f;

        [JsonProperty("cPetal"), JsonConverter(typeof(HexColorConverter))]
        public Color PetalColor { get; set; } = new Color(176, 106, 179);

        [JsonProperty("cCore"), JsonConverter(typeof(HexColorConverter))]
        public Color CoreColor { get; set; } = new Color(255, 211, 42);

        [JsonProperty("cCalyx"), JsonConverter(typeof(HexColorConverter))]
        public Color CalyxColor { get; set; } = new Color(39, 107, 47);

        // --- PLAYER INTERACTION ---
        public float PlayerPushRadius = 32f;
        public float PlayerPushStrength = 1.4f;
    }

    // Update the GrassVertex struct to include Flora Data
    public struct GrassVertex : IVertexType
    {
        public Vector3 RootPosition; // xy: world pos (with elevation offset), z: base lean
        public Vector2 T_Side;       // x: parametric t (0..1), y: side (-1, 1)
        public Vector2 Wind_Height;  // x: local phase, y: height multiplier
        public float Variation;      // hash variation
        public Vector4 FloraData;    // x: type, y: size, z: stage (0..3), w: rotJitter

        public GrassVertex(Vector3 root, Vector2 tSide, Vector2 windHeight, float var, Vector4 flora)
        {
            RootPosition = root;
            T_Side = tSide;
            Wind_Height = windHeight;
            Variation = var;
            FloraData = flora;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(28, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
