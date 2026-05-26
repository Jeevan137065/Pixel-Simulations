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
            Noises["Perlin1"] = content.Load<Texture2D>("Noise/Perlin/Perlin02");
            Noises["Perlin2"] = content.Load<Texture2D>("Noise/Perlin/Perlin03");

            Noises["Streak"] = content.Load<Texture2D>("Noise/Streak/Streak01");
            Noises["Streak1"] = content.Load<Texture2D>("Noise/Streak/Streak02");
            Noises["Streak2"] = content.Load<Texture2D>("Noise/Streak/Streak03");

            Noises["Gabor"] = content.Load<Texture2D>("Noise/Gabor/Gabor01");
            Noises["Gabor1"] = content.Load<Texture2D>("Noise/Gabor/Gabor01");
            Noises["Gabor2"] = content.Load<Texture2D>("Noise/Gabor/Gabor03");

            Noises["Crater"] = content.Load<Texture2D>("Noise/Craters/Craters01");
            Noises["Crater1"] = content.Load<Texture2D>("Noise/Craters/Craters02");
            Noises["Crater2"] = content.Load<Texture2D>("Noise/Craters/Craters03");

            Noises["Grainy"] = content.Load<Texture2D>("Noise/Grainy/Grainy01");
            Noises["Grainy1"] = content.Load<Texture2D>("Noise/Grainy/Grainy02");
            Noises["Grainy2"] = content.Load<Texture2D>("Noise/Grainy/Grainy03");

            Noises["Cracks"] = content.Load<Texture2D>("Noise/Cracks/Cracks01");
            Noises["Cracks1"] = content.Load<Texture2D>("Noise/Cracks/Cracks02");
            Noises["Cracks2"] = content.Load<Texture2D>("Noise/Cracks/Cracks03");

            Noises["Super_Perlin"] = content.Load<Texture2D>("Noise/Super Perlin/Super_Perlin01");
            Noises["Super_Perlin1"] = content.Load<Texture2D>("Noise/Super Perlin/Super_Perlin02");
            Noises["Super_Perlin2"] = content.Load<Texture2D>("Noise/Super Perlin/Super_Perlin03");

            Noises["Spokes"] = content.Load<Texture2D>("Noise/Spokes/Spokes01");
            Noises["Spokes1"] = content.Load<Texture2D>("Noise/Spokes/Spokes02");
            Noises["Spokes2"] = content.Load<Texture2D>("Noise/Spokes/Spokes03");

            Noises["Melt"] = content.Load<Texture2D>("Noise/Melt/Melt01");
            Noises["Melt1"] = content.Load<Texture2D>("Noise/Melt/Melt02");
            Noises["Melt2"] = content.Load<Texture2D>("Noise/Melt/Melt03");

        }

        public Texture2D GetActiveNoise()
        {
            if (ActiveNoiseName == "None" || !Noises.ContainsKey(ActiveNoiseName)) return null;
            return Noises[ActiveNoiseName];
        }
    }
}
