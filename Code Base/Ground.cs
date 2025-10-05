using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Pixel_Simulations
{
    public class Ground
    {
        int h, H, W, w, s = 0;
        Texture2D GroundTexture;
        Texture2D perlinNoiseTexture;

        public Ground(GraphicsDevice gb)
        {
            s = 16;
            H = gb.Viewport.Height; h = (H - (H % s)) / s;
            W = gb.Viewport.Width; w = (W - (W % s)) / s;
            h = h - 2; w = w - 2;
            GenerateAndOverlayPerlinNoise(gb);
        }

        public void Load(ContentManager content, String path)
        {
            GroundTexture = content.Load<Texture2D>(path);
        }

        public void Draw(SpriteBatch sb)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    sb.Draw(GroundTexture, new Rectangle((x + 1) * s, (y + 1) * s, s, s), Color.White);
                }
            }

            if (perlinNoiseTexture != null)
            {
                sb.Draw(perlinNoiseTexture, new Vector2(0, 0), Color.White);
            }

        }

        private float Perlin(float x, float y)
        {
            int xi = (int)x & 255;
            int yi = (int)y & 255;
            float xf = x - (int)x;
            float yf = y - (int)y;

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = Permutation[Permutation[xi] + yi];
            int ab = Permutation[Permutation[xi] + yi + 1];
            int ba = Permutation[Permutation[xi + 1] + yi];
            int bb = Permutation[Permutation[xi + 1] + yi + 1];

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

            return (Lerp(x1, x2, v) + 1) / 2; // Normalize to [0,1]
        }

        private float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private float Lerp(float a, float b, float t) => a + t * (b - a);
        private float Grad(int hash, float x, float y)
        {
            int h = hash & 3;
            float u = h < 2 ? x : y;
            float v = h < 2 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        // Permutation table for Perlin noise
        private static readonly int[] Permutation = new int[512] { 151, 160, 137,  91,  90,  15, 131,  13, 201,  95,  96,  53, 194, 233,   7, 225,
                      140,  36, 103,  30,  69, 142,   8,  99,  37, 240,  21,  10,  23, 190,   6, 148,
                      247, 120, 234,  75,   0,  26, 197,  62,  94, 252, 219, 203, 117,  35,  11,  32,
                       57, 177,  33,  88, 237, 149,  56,  87, 174,  20, 125, 136, 171, 168,  68, 175,
                       74, 165,  71, 134, 139,  48,  27, 166,  77, 146, 158, 231,  83, 111, 229, 122,
                       60, 211, 133, 230, 220, 105,  92,  41,  55,  46, 245,  40, 244, 102, 143,  54,
                       65,  25,  63, 161,   1, 216,  80,  73, 209,  76, 132, 187, 208,  89,  18, 169,
                      200, 196, 135, 130, 116, 188, 159,  86, 164, 100, 109, 198, 173, 186,   3,  64,
                       52, 217, 226, 250, 124, 123,   5, 202,  38, 147, 118, 126, 255,  82,  85, 212,
                      207, 206,  59, 227,  47,  16,  58,  17, 182, 189,  28,  42, 223, 183, 170, 213,
                      119, 248, 152,   2,  44, 154, 163,  70, 221, 153, 101, 155, 167,  43, 172,   9,
                      129,  22,  39, 253,  19,  98, 108, 110,  79, 113, 224, 232, 178, 185, 112, 104,
                      218, 246,  97, 228, 251,  34, 242, 193, 238, 210, 144,  12, 191, 179, 162, 241,
                       81,  51, 145, 235, 249,  14, 239, 107,  49, 192, 214,  31, 181, 199, 106, 157,
                      184,  84, 204, 176, 115, 121,  50,  45, 127,   4, 150, 254, 138, 236, 205,  93,
                      222, 114,  67,  29,  24,  72, 243, 141, 128, 195,  78,  66, 215,  61, 156, 180, 151, 160, 137,  91,  90,  15, 131,  13, 201,  95,  96,  53, 194, 233,   7, 225,
                      140,  36, 103,  30,  69, 142,   8,  99,  37, 240,  21,  10,  23, 190,   6, 148,
                      247, 120, 234,  75,   0,  26, 197,  62,  94, 252, 219, 203, 117,  35,  11,  32,
                       57, 177,  33,  88, 237, 149,  56,  87, 174,  20, 125, 136, 171, 168,  68, 175,
                       74, 165,  71, 134, 139,  48,  27, 166,  77, 146, 158, 231,  83, 111, 229, 122,
                       60, 211, 133, 230, 220, 105,  92,  41,  55,  46, 245,  40, 244, 102, 143,  54,
                       65,  25,  63, 161,   1, 216,  80,  73, 209,  76, 132, 187, 208,  89,  18, 169,
                      200, 196, 135, 130, 116, 188, 159,  86, 164, 100, 109, 198, 173, 186,   3,  64,
                       52, 217, 226, 250, 124, 123,   5, 202,  38, 147, 118, 126, 255,  82,  85, 212,
                      207, 206,  59, 227,  47,  16,  58,  17, 182, 189,  28,  42, 223, 183, 170, 213,
                      119, 248, 152,   2,  44, 154, 163,  70, 221, 153, 101, 155, 167,  43, 172,   9,
                      129,  22,  39, 253,  19,  98, 108, 110,  79, 113, 224, 232, 178, 185, 112, 104,
                      218, 246,  97, 228, 251,  34, 242, 193, 238, 210, 144,  12, 191, 179, 162, 241,
                       81,  51, 145, 235, 249,  14, 239, 107,  49, 192, 214,  31, 181, 199, 106, 157,
                      184,  84, 204, 176, 115, 121,  50,  45, 127,   4, 150, 254, 138, 236, 205,  93,
                      222, 114,  67,  29,  24,  72, 243, 141, 128, 195,  78,  66, 215,  61, 156, 180
        };

        // Generate and overlay Perlin noise
        public void GenerateAndOverlayPerlinNoise(GraphicsDevice graphicsDevice)
        {
            Color[] noiseColors = new Color[W * H];
            float scale = 0.07f; // Lower = smoother noise

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float noise = Perlin(x * scale, y * scale);
                    // Map noise: <0.5 = black, >0.5 = white
                    byte value = (byte)(noise > 0.5f ? 255 : 0);
                    noiseColors[y * W + x] = new Color((int)value, (int)value, (int)value, 128); // semi-transparent
                }
            }

            perlinNoiseTexture = new Texture2D(graphicsDevice, W, H);
            perlinNoiseTexture.SetData(noiseColors);
        }
    }
}
