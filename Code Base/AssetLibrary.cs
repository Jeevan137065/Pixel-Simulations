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
    public enum AtlasType { Tile, Object,Normal, Universal }
    public class AtlasMetadata
    {
        public string Name { get; set; }
        public Texture2D Texture { get; set; }
        public AtlasType Type { get; set; }
    }
    public class EditorLibrary
        {
        public readonly Dictionary<string, AtlasMetadata> _library = new Dictionary<string, AtlasMetadata>();
        private readonly ContentManager _content;
        
        public EditorLibrary(ContentManager content) => _content = content;

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
    public class AssetLibrary
    {
        // Store metadata, just like the EditorLibrary
        private readonly Dictionary<string, AtlasMetadata> _library = new Dictionary<string, AtlasMetadata>();
        private ContentManager _content;
        public SpriteFont customFont;
        // Pass ContentManager in constructor for cleaner loading later
        public AssetLibrary()
        {

        }

        /// <summary>
        /// Loads a specific atlas and categorizes it.
        /// </summary>
        public void LoadAtlas(string assetName, AtlasType type)
        {
            if (!_library.ContainsKey(assetName))
            {
                try
                {
                    _library[assetName] = new AtlasMetadata
                    {
                        Name = assetName,
                        Texture = _content.Load<Texture2D>(assetName),
                        Type = type
                    };
                }
                catch (ContentLoadException)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Could not load texture '{assetName}'.");
                }
            }
        }

        /// <summary>
        /// Loads the core set of necessary game atlases.
        /// </summary>
        public void LoadCoreContent(ContentManager content)
        {
            _content = content;
            // Load Tile Atlases
            LoadAtlas("Base", AtlasType.Tile);
            //LoadAtlas("BasiR", AtlasType.Tile);
            LoadAtlas("Wild", AtlasType.Tile);
            //LoadAtlas("Temp", AtlasType.Tile);

            // Load Object Atlases
            LoadAtlas("Trees", AtlasType.Object);
            LoadAtlas("Building", AtlasType.Object);
            LoadAtlas("Trees_n", AtlasType.Normal);
            LoadAtlas("Building_n", AtlasType.Normal);

            // Load Universal/UI Atlases
            customFont = content.Load<SpriteFont>("Seattle");
            LoadAtlas("Items", AtlasType.Universal);
        }

        /// <summary>
        /// Retrieves a pre-loaded texture by its name.
        /// </summary>
        public Texture2D GetAtlas(string name)
        {
            if (_library.TryGetValue(name, out var meta))
            {
                return meta.Texture;
            }

            //System.Diagnostics.Debug.WriteLine($"WARNING: Atlas '{name}' requested but not loaded.");
            return null;
        }
        
        public Texture2D GetNormalAtlas(string name)
        {
            string normal_atlas = String.Concat(name, "_n");
            if (_library.TryGetValue(normal_atlas, out var meta))
            {
                return meta.Texture;
            }

            //System.Diagnostics.Debug.WriteLine($"WARNING: Normal Atlas '{name}' requested but not loaded.");
            return null;
        }

        /// <summary>
        /// Gets all loaded atlas names of a specific type.
        /// </summary>
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
                Prefabs = new Dictionary<string, ObjectPrefab>(StringComparer.OrdinalIgnoreCase);
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

