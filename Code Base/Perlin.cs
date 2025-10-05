using System;

namespace Pixel_Simulations
{ // <-- Make sure this matches your project's namespace

    public static class Perlin
    {
        private static readonly int[] _p = new int[512];

        static Perlin()
        {
            // Initialize with a permutation of 0-255
            var permutation = new int[256];
            for (int i = 0; i < 256; i++)
            {
                permutation[i] = i;
            }

            // Shuffle the permutation array
            var random = new Random(0); // Using a fixed seed for predictable noise
            for (int i = 0; i < 256; i++)
            {
                int source = random.Next(256);
                (permutation[i], permutation[source]) = (permutation[source], permutation[i]);
            }

            for (int i = 0; i < 512; i++)
            {
                _p[i] = permutation[i & 255];
            }
        }

        public static double Noise(double x, double y)
        {
            int xi = (int)x & 255;
            int yi = (int)y & 255;
            double xf = x - (int)x;
            double yf = y - (int)y;
            double u = Fade(xf);
            double v = Fade(yf);

            int a = _p[xi] + yi;
            int b = _p[xi + 1] + yi;

            double g1 = Grad(_p[a], xf, yf);
            double g2 = Grad(_p[b], xf - 1, yf);
            double g3 = Grad(_p[a + 1], xf, yf - 1);
            double g4 = Grad(_p[b + 1], xf - 1, yf - 1);

            double lerpX1 = Lerp(u, g1, g2);
            double lerpX2 = Lerp(u, g3, g4);
            double result = Lerp(v, lerpX1, lerpX2);

            // Return value in range [0, 1]
            return (result + 1) / 2;
        }

        private static double Fade(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double Lerp(double t, double a, double b)
        {
            return a + t * (b - a);
        }

        private static double Grad(int hash, double x, double y)
        {
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : h == 12 || h == 14 ? x : 0;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}