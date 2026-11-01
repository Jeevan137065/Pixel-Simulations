using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Pixel_Simulations;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
    {
    public enum AtlasType { Tile, Object,Normal, Universal }
    public class AtlasMetadata
    {
        public string Name { get; set; }
        public Texture2D Texture { get; set; }
        public AtlasType Type { get; set; }
    }
    public class EditorLibrary
        {
        public readonly Dictionary<string, AtlasMetadata> _library = new Dictionary<string, AtlasMetadata>(StringComparer.OrdinalIgnoreCase);
        private readonly ContentManager _content;
        
        public EditorLibrary(ContentManager content) => _content = content;

        public void LoadAtlas(string assetName, AtlasType type)
        {
            string cleanName = Path.GetFileNameWithoutExtension(assetName);
            if (!_library.ContainsKey(cleanName))
            {
                try
                {
                    _library[cleanName] = new AtlasMetadata
                    {
                        Name = cleanName,
                        Texture = _content.Load<Texture2D>(cleanName),
                        Type = type
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EditorLibrary] Failed to load '{assetName}': {ex.Message}");
                }
            }
        }

        public Texture2D GetAtlas(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string cleanName = Path.GetFileNameWithoutExtension(name);
            return _library.TryGetValue(cleanName, out var meta) ? meta.Texture : null;
        }

        public List<string> GetNamesByType(AtlasType type)
        {
            return _library.Values
                .Where(m => m.Type == type || m.Type == AtlasType.Universal)
                .Select(m => m.Name).ToList();
        }
    }
    public class AssetLibrary
    {
        private readonly Dictionary<string, AtlasMetadata> _library = new Dictionary<string, AtlasMetadata>(StringComparer.OrdinalIgnoreCase);
        private ContentManager _content;
        public SpriteFont customFont;

        public void LoadAtlas(string assetName, AtlasType type)
        {
            string cleanName = Path.GetFileNameWithoutExtension(assetName);
            if (!_library.ContainsKey(cleanName))
            {
                try
                {
                    _library[cleanName] = new AtlasMetadata
                    {
                        Name = cleanName,
                        Texture = _content.Load<Texture2D>(cleanName),
                        Type = type
                    };
                }
                catch (ContentLoadException)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Could not load texture '{cleanName}'.");
                }
            }
        }

        public void LoadCoreContent(ContentManager content)
        {
            _content = content;
            // Tiles
            LoadAtlas("Base", AtlasType.Tile);
            LoadAtlas("BaseWorld", AtlasType.Tile);
            LoadAtlas("Wild", AtlasType.Tile);
            LoadAtlas("Farm", AtlasType.Tile);

            // Objects & Facades
            LoadAtlas("Trees", AtlasType.Object);
            LoadAtlas("Trees_t", AtlasType.Object);
            LoadAtlas("Trees_b", AtlasType.Object);
            LoadAtlas("Trees_l", AtlasType.Object);
            LoadAtlas("Trees_s", AtlasType.Object);
            LoadAtlas("Trees_n", AtlasType.Normal);

            LoadAtlas("Building", AtlasType.Object);
            LoadAtlas("Building_n", AtlasType.Normal);
            LoadAtlas("Prototype_Building", AtlasType.Object);
            // UI / Items / Crops
            customFont = content.Load<SpriteFont>("Seattle");
            LoadAtlas("Items", AtlasType.Universal);
            LoadAtlas("CropA", AtlasType.Universal);
            LoadAtlas("CropB", AtlasType.Universal);
            LoadAtlas("CropC", AtlasType.Universal);
            LoadAtlas("Machines", AtlasType.Universal);

            LoadAtlas("CropA_n", AtlasType.Normal);
            LoadAtlas("CropB_n", AtlasType.Normal);
            LoadAtlas("CropC_n", AtlasType.Normal);
            LoadAtlas("Machines_n", AtlasType.Normal);
        }

        public Texture2D GetAtlas(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string cleanName = Path.GetFileNameWithoutExtension(name);
            return _library.TryGetValue(cleanName, out var meta) ? meta.Texture : null;
        }

        public Texture2D GetNormalAtlas(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string cleanName = Path.GetFileNameWithoutExtension(name);
            string normalAtlas = string.Concat(cleanName, "_n");
            return _library.TryGetValue(normalAtlas, out var meta) ? meta.Texture : null;
        }

        public List<string> GetNamesByType(AtlasType type)
        {
            return _library.Values
                .Where(m => m.Type == type || m.Type == AtlasType.Universal)
                .Select(m => m.Name).ToList();
        }
    }

    public class PrefabManager
        {
        public Dictionary<string, ObjectPrefab> Prefabs { get; private set; } = new Dictionary<string, ObjectPrefab>();

        // --- FIX: Add the settings so JSON knows how to read MonoGame Vector2s! ---
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Converters = { new Vector2Converter(), new ColorConverter() },
            Formatting = Formatting.Indented
        };
        public void Save(string path)
        {
            try
            {
                // Ensure the directory exists
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Pass the settings here
                string json = JsonConvert.SerializeObject(Prefabs.Values, _jsonSettings);
                File.WriteAllText(path, json);
                System.Diagnostics.Debug.WriteLine($"Prefabs saved successfully to {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save prefabs: {ex.Message}");
            }
        }

        public void Load(string path)
        {
            Prefabs = new Dictionary<string, ObjectPrefab>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                var list = JsonConvert.DeserializeObject<List<ObjectPrefab>>(json, _jsonSettings);

                if (list != null)
                {
                    foreach (var prefab in list)
                    {
                        if (!Prefabs.ContainsKey(prefab.ID))
                        {
                            // FIX: Strip any phantom / dummy default tile layers targeting the Base tile atlas
                            var phantomKeys = prefab.AlternateStates
                                .Where(kvp => (string.Equals(kvp.Value.AtlasName, "Base", StringComparison.OrdinalIgnoreCase) ||
                                               string.Equals(kvp.Value.AtlasName, "BaseWorld", StringComparison.OrdinalIgnoreCase)) &&
                                              kvp.Value.SourceRect == new Rectangle(0, 0, 16, 16) &&
                                              kvp.Value.Offset == Point.Zero)
                                .Select(kvp => kvp.Key)
                                .ToList();

                            foreach (var key in phantomKeys)
                            {
                                prefab.AlternateStates.Remove(key);
                            }

                            Prefabs.Add(prefab.ID, prefab);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to parse objects.json. Error: {ex.Message}");
            }
        }

        public ObjectPrefab GetPrefab(string id) => Prefabs.TryGetValue(id, out var p) ? p : null;
    }
}

