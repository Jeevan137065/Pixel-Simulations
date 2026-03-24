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
            // You can load your 14 textures here. Example:
            Noises["Perlin"] = content.Load<Texture2D>("Noise/Perlin/Perlin01");
            Noises["Cracks"] = content.Load<Texture2D>("Noise/Cracks/Crack01");
        }

        public Texture2D GetActiveNoise()
        {
            if (ActiveNoiseName == "None" || !Noises.ContainsKey(ActiveNoiseName)) return null;
            return Noises[ActiveNoiseName];
        }
    }
}
