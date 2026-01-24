using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    public struct BladeData
    {
        public Vector2 Pos;
        public float Wind;
        public float Height;
        public float Var;
        public float Lean; // NEW: Natural rest angle (-5 to 5 pixels)
    }
    public struct GrassVertex : IVertexType
    {
        public Vector3 RootPosition; // 12 bytes
        public Vector2 T_Side;       // 8 bytes
        public Vector2 Wind_Height;  // 8 bytes
        public float Variation;      // 4 bytes
        public Color Color;          // 4 bytes

        // 7-Argument Constructor
        public GrassVertex(Vector2 root, float t, float side, float wind, float height, float var, float lean, Color col)
        {
            RootPosition = new Vector3(root.X, root.Y, lean);
            T_Side = new Vector2(t, side);
            Wind_Height = new Vector2(wind, height);
            Variation = var;
            Color = col;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
         new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
         new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
         new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
         new VertexElement(28, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),
         new VertexElement(32, VertexElementFormat.Color, VertexElementUsage.Color, 0)
     );
        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
    public struct GrassSettings
    {
        // Geometry
        //Grass Blade Height for a global average.
        public float GlobalHeightBase;   // 1.0 - 5.0
        //The thickness value at thickness
        public float BladeThickness;     // 0.1 - 5.0

        public int Segments;             // 2 (Standard) to 4 (High End)
        //Bending shape. -ve for left, +ve for right
        public float RestingCurvature;   // 0.0 (straight) to 10.0 (heavy droop)

        // Physics
        public float WindSpeed;          // 1.0 to 5.0
        public float WindIntensity;      // 2.0 to 10.0
        //Strength against wind
        public float Stiffness;          // 0.1 (floppy) to 0.9 (stiff)
        public float PlayerPushRadius;   // 20.0 to 40.0
        public float PlayerPushStrength; // 1.0 to 2.0

        // Visuals
        public Color RootColor;
        public Color TipColor;

        // Distribution
        //WARNING. CAN NOT BE LESS THAN 1.0f
        public float DensityStep;        // 1.0 (dense) to 5.0 (sparse)
        //Min < Mid < Max < 0.8f
        public float MinThreshold;   // Start of sparse grass (e.g., 0.3)
        public float MidThreshold;   // Start of thick grass (e.g., 0.5)
        public float MaxThreshold;   // Start of overgrown/tall clumps (e.g., 0.8)

        //Probability of spawn in sparse zone
        public float SparseDensity;  //(0.0 - 1.0)
        public float LushCount;      // How many extra blades in Max zones (1 - 3)
        //Control How many times to draw over a clump
        public int TuftSize;             // 1 to 2 blades per clump
        //Controls how much Tips spread
        public float TuftSpread;         // 1.0f to 30.0f
        //Control the thickness of Tip of blade
        public float BladeTaper;         // 0.5 (fat tip) to 1.0 (needle tip)
        
    }
    public enum GrassPreset
    {
        Lawn,           // Short, uniform, bright
        Marsh,          // Your current look: tall, floppy, dark
        ForestFloor,    // Patchy, wide blades, dark greens
        DesertScrub,    // Very sparse, dry, stiff
        WheatField,     // Tall, golden, highly synchronized
        Highlands,      // Hilly, wild, extreme height variation
        DeadMeadow      // Grey-brown, brittle
    }
    public static class GrassLibrary
    {
        public static GrassSettings GetPreset(GrassPreset type)
        {
            switch (type)
            {
                case GrassPreset.Lawn:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 1.5f,
                        BladeThickness = 1.5f,
                        Segments = 3,
                        RestingCurvature = 0.25f,

                        WindSpeed = 0.3f,
                        WindIntensity = 0.3f,
                        Stiffness = 0.5f,
                        PlayerPushRadius = 15f,
                        PlayerPushStrength = 0.0f,

                        DensityStep = 3.0f,
                        MinThreshold = 0.20f,
                        MidThreshold = 0.91f,
                        MaxThreshold = 0.92f,
                        SparseDensity = 0.4f,
                        LushCount = 1,
                        TuftSize = 1,
                        TuftSpread = 10.0f,
                        BladeTaper = 0.9f,
                        RootColor = new Color(5, 40, 5),
                        TipColor = new Color(40, 150, 40)
                    };

                case GrassPreset.Marsh:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 5.0f, // Max height
                        BladeThickness = 2.0f,
                        Segments = 8,
                        RestingCurvature = 2.0f,

                        WindSpeed = 0.5f,
                        WindIntensity = 0.1f,
                        Stiffness = 0.1f,
                        PlayerPushRadius = 0.1f,
                        PlayerPushStrength = 0.1f,

                        DensityStep = 2.5f,
                        MinThreshold = 0.6f,
                        MidThreshold = 0.75f,
                        MaxThreshold = 0.95f,
                        SparseDensity = 0.8f,
                        LushCount = 2,
                        TuftSize = 1,
                        TuftSpread = 40.0f,
                        BladeTaper = 0.5f,
                        RootColor = new Color(40, 75, 30),
                        TipColor = new Color(75, 75, 40)
                    };

                case GrassPreset.ForestFloor:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 2.5f,
                        BladeThickness = 1.5f,
                        Segments = 2,
                        RestingCurvature = 1.0f,

                        WindSpeed = 0.8f,
                        WindIntensity = 0.5f,
                        Stiffness = 0.5f,
                        PlayerPushRadius = 15f,
                        PlayerPushStrength = 2.0f,

                        DensityStep = 2.8f,
                        MinThreshold = 0.5f,
                        MidThreshold = 0.7f,
                        MaxThreshold = 0.8f,
                        SparseDensity = 0.4f,
                        LushCount = 1,
                        TuftSize = 2,
                        TuftSpread = 20.0f,
                        BladeTaper = 0.85f,
                        RootColor = new Color(15, 30, 10),
                        TipColor = new Color(50, 120, 40)
                    };

                case GrassPreset.DeadMeadow:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 3.0f,
                        BladeThickness = 1.2f,
                        Segments = 2,
                        RestingCurvature = 2.0f,

                        WindSpeed = 0.1f,
                        WindIntensity = 0.5f,
                        Stiffness = 0.4f,
                        PlayerPushRadius = 20f,
                        PlayerPushStrength = 1.0f,
                        
                        DensityStep = 2.0f,
                        MinThreshold = 0.5f,
                        MidThreshold = 0.8f,
                        MaxThreshold = 1.0f,
                        SparseDensity = 0.4f,
                        LushCount = 2,
                        TuftSize = 2,
                        TuftSpread = 20.0f,
                        BladeTaper = 0.5f,

                        RootColor = new Color(10, 10, 10),
                        TipColor = new Color(80, 80, 80)
                    };

                case GrassPreset.WheatField:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 4.0f,
                        BladeThickness = 0.8f,
                        Segments = 2,
                        RestingCurvature = 0.5f,

                        WindSpeed = 0.01f,
                        WindIntensity = 0.1f,
                        Stiffness = 0.95f,
                        PlayerPushRadius = 30f,
                        PlayerPushStrength = 1.5f,
                        
                        DensityStep = 2.0f,
                        MinThreshold = 0.15f,
                        MidThreshold = 0.4f,
                        MaxThreshold = 0.7f,
                        SparseDensity = 0.9f,
                        LushCount = 1,
                        TuftSize = 2,
                        TuftSpread = 2.0f,
                        BladeTaper = 0.5f,

                        RootColor = new Color(50, 45, 10),
                        TipColor = new Color(220, 180, 50)
                    };

                case GrassPreset.DesertScrub:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 3.0f,
                        BladeThickness = 1.0f,
                        Segments = 2,
                        RestingCurvature = 1.5f,

                        WindSpeed = 0.5f,
                        WindIntensity = 0.3f,
                        Stiffness = 0.75f,
                        PlayerPushRadius = 15f,
                        PlayerPushStrength = 0.4f,

                        DensityStep = 6.0f,
                        MinThreshold = 0.6f,
                        MidThreshold = 0.8f,
                        MaxThreshold = 0.95f,
                        SparseDensity = 0.2f,
                        LushCount = 1,
                        TuftSize = 2,
                        TuftSpread = 40.0f,
                        BladeTaper = 0.75f,

                        RootColor = new Color(60, 60, 60),
                        TipColor = new Color(180, 180, 140)
                    };

                case GrassPreset.Highlands:
                    return new GrassSettings
                    {
                        GlobalHeightBase = 5.0f, // Max height
                        BladeThickness = 1.0f,
                        Segments = 5,
                        RestingCurvature = 1.5f,

                        WindSpeed = 0.5f,
                        WindIntensity = 0.1f,
                        Stiffness = 0.1f,
                        PlayerPushRadius = 1.0f,
                        PlayerPushStrength = 0.5f,

                        DensityStep = 1.5f,
                        MinThreshold = 0.5f,
                        MidThreshold = 0.6f,
                        MaxThreshold = 0.7f,
                        SparseDensity = 0.8f,
                        LushCount = 2,
                        TuftSize = 1,
                        TuftSpread = 10.0f,
                        BladeTaper = 0.75f,
                        RootColor = new Color(40, 75, 30),
                        TipColor = new Color(75, 75, 40)
                    };
            }
            return GetPreset(GrassPreset.ForestFloor); // Default
        }
    }
}
