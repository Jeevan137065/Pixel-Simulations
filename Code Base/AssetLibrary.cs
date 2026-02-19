using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations
    {
    public enum AtlasType { Tile, Object, Universal }
    public class AtlasMetadata
    {
        public string Name { get; set; }
        public Texture2D Texture { get; set; }
        public AtlasType Type { get; set; }
    }
    public class AssetLibrary
        {
        public readonly Dictionary<string, AtlasMetadata> _library = new Dictionary<string, AtlasMetadata>();
        private readonly ContentManager _content;

            public AssetLibrary(ContentManager content) => _content = content;

        public void LoadAtlas(string assetName, AtlasType type)
        {
            if (!_library.ContainsKey(assetName))
            {
                _library[assetName] = new AtlasMetadata
                {
                    Name = assetName,
                    Texture = _content.Load<Texture2D>(assetName),
                    Type = type
                };
            }
        }

        public Texture2D GetAtlas(string name) => _library.TryGetValue(name, out var meta) ? meta.Texture : null;

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

                    string json = JsonConvert.SerializeObject(Prefabs.Values, Formatting.Indented);
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
                // 1. Initialize to empty dictionary immediately so we never have a null 'Prefabs' property
                Prefabs = new Dictionary<string, ObjectPrefab>();

                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Prefab file not found at {path}. Starting fresh.");
                    return;
                }

                try
                {
                    string json = File.ReadAllText(path);

                    // 2. Check if the file is empty or just whitespace
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        System.Diagnostics.Debug.WriteLine("Prefab file is empty. Starting fresh.");
                        return;
                    }

                    var list = JsonConvert.DeserializeObject<List<ObjectPrefab>>(json);

                    // 3. Check if the deserializer returned null (happens with "null" string or empty arrays)
                    if (list != null)
                    {
                        // Use a loop instead of .ToDictionary to safely handle potential duplicate IDs
                        foreach (var prefab in list)
                        {
                            if (!Prefabs.ContainsKey(prefab.ID))
                            {
                                Prefabs.Add(prefab.ID, prefab);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Warning: Duplicate Prefab ID '{prefab.ID}' found in JSON. Skipping duplicate.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 4. If the JSON is malformed (missing brackets, typos), catch the error and keep the empty dict
                    System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to parse objects.json. Error: {ex.Message}");
                }
            }

        public ObjectPrefab GetPrefab(string id) => Prefabs.TryGetValue(id, out var p) ? p : null;
        }
}

