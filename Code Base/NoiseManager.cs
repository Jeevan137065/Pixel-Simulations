using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
{
    public class NoiseManager
    {
        public Dictionary<string, Texture2D> Noises { get; private set; } = new Dictionary<string, Texture2D>();
        public string ActiveNoiseName { get; set; } = "None"; // "None" = solid brush

        public void LoadContent(ContentManager content)
        {
            //Noise Textures we have are (all are 256*256)
            // 14 Cracks Noise, 14 Craters,, 14 Gabor ,  14 Grainy, 14 Manifold, 14 Marble, 14 Melt, 14 Milky,
            //14 Perlin noise, 13 Super Perlin noise, 14 Super Noise, 14 Spokes, 14 Streaks, 14 Swirl, 14 Techno, 14 Turbulence, 14 Vein, 14 Voronoi
            // We will need to add blue, ign and other noise in future
            //
            Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin01");
            Noises["Streak"] = content.Load<Texture2D>("Noise/Streak/Streak01");
            Noises["Gabor"] = content.Load<Texture2D>("Noise/Gabor/Gabor01");
            Noises["Crater"] = content.Load<Texture2D>("Noise/Craters/Craters01");
            Noises["Grainy"] = content.Load<Texture2D>("Noise/Grainy/Grainy01");
            Noises["Cracks"] = content.Load<Texture2D>("Noise/Cracks/Cracks01");
            Noises["Super_Perlin"] = content.Load<Texture2D>("Noise/Super Perlin/Super_Perlin01");
            Noises["Spokes"] = content.Load<Texture2D>("Noise/Spokes/Spokes01");
            Noises["Melt"] = content.Load<Texture2D>("Noise/Melt/Melt01");
        }

        public Texture2D GetActiveNoise()
        {
            if (ActiveNoiseName == "None" || !Noises.ContainsKey(ActiveNoiseName)) return null;
            return Noises[ActiveNoiseName];
        }
    }
}
