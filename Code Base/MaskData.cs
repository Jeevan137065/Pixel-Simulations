using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Pixel_Simulations
{
    public class MaskDataDef
    {
        public string Name { get; set; }
        public int Value { get; set; } // 0 - 255
    }
    public class MaskDataManager
    {
        public List<MaskDataDef> Elevations { get; set; } = new List<MaskDataDef>();
        public List<MaskDataDef> Biomes { get; set; } = new List<MaskDataDef>();
        public List<MaskDataDef> Spawns { get; set; } = new List<MaskDataDef>();

        public void Save(string path)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(path, json);
        }

        public void Load(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                // Initialize Defaults if file is missing!
                Elevations.Add(new MaskDataDef { Name = "Sea", Value = 0 });
                Elevations.Add(new MaskDataDef { Name = "Ground", Value = 128 });
                Elevations.Add(new MaskDataDef { Name = "Hill", Value = 192 });
                Elevations.Add(new MaskDataDef { Name = "Peak", Value = 255 });
                Save(path);
                return;
            }

            string json = System.IO.File.ReadAllText(path);
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<MaskDataManager>(json);
            if (loaded != null)
            {
                Elevations = loaded.Elevations ?? new List<MaskDataDef>();
                Biomes = loaded.Biomes ?? new List<MaskDataDef>();
                Spawns = loaded.Spawns ?? new List<MaskDataDef>();
            }
        }
    }

}
