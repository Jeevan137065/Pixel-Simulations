using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pixel_Simulations
{
    [JsonObject(MemberSerialization.OptIn)]
    public class MaskDataDef
    {
        [JsonProperty] public int Value { get; set; } // 0 - 255
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Category { get; set; } // "Forest", "Forage", "Fish", etc.
    }
    public class MaskDataManager
    {
        public List<MaskDataDef> Elevations { get; set; } = new List<MaskDataDef>();
        public List<MaskDataDef> Biomes { get; set; } = new List<MaskDataDef>();
        public List<MaskDataDef> Spawns { get; set; } = new List<MaskDataDef>();

        // We create quick-lookup dictionaries to avoid looping over lists during runtime!
        private Dictionary<int, MaskDataDef> _biomeMap = new Dictionary<int, MaskDataDef>();
        private Dictionary<int, MaskDataDef> _spawnMap = new Dictionary<int, MaskDataDef>();

        public void Save(string path)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(path, json);
        }

        public void Load(string path)
        {
            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"MaskData file not found at {path}. Expecting HTML export.");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<MaskDataManager>(json);
                if (loaded != null)
                {
                    Elevations = loaded.Elevations ?? new List<MaskDataDef>();
                    Biomes = loaded.Biomes ?? new List<MaskDataDef>();
                    Spawns = loaded.Spawns ?? new List<MaskDataDef>();

                    // Build quick lookups
                    _biomeMap = Biomes.ToDictionary(b => b.Value, b => b);
                    _spawnMap = Spawns.ToDictionary(s => s.Value, s => s);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse mask_data.json: {ex.Message}");
            }
        }
        public MaskDataDef GetBiome(int maskValue) => _biomeMap.TryGetValue(maskValue, out var b) ? b : null;
        public MaskDataDef GetSpawn(int maskValue) => _spawnMap.TryGetValue(maskValue, out var s) ? s : null;
    }

}
