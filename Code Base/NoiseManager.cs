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
            // 14 Crack Noise, 14 Crater,, 14 Gabor ,  14 Grainy, 14 Manifold, 14 Marble, 14 Melt, 14 Milky,
            //14 Perlin noise, 13 Super Perlin noise, 14 Super Noise, 14 Spokes, 14 Streaks, 14 Swirl, 14 Techno, 14 Turbulence, 14 Vein, 14 Voronoi
            // We will need to add blue, ign and other noise in future
            //
            //Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin01");
            //Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin02");
            //Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin03");
            //Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin04");
            //Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin05");
            //Noises["Cracks"] = content.Load<Texture2D>("Noise/Cracks/Crack01");
        }

        public Texture2D GetActiveNoise()
        {
            if (ActiveNoiseName == "None" || !Noises.ContainsKey(ActiveNoiseName)) return null;
            return Noises[ActiveNoiseName];
        }
    }
}
