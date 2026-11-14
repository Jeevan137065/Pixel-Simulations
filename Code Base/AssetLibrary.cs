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
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Pixel_Simulations
    {
    public enum AtlasType { Tile, Object,Normal, Universal }
    public class AtlasMetadata
    {
        public string Name { get; set; }
        public Texture2D Texture { get; set; }
        public AtlasType Type { get; set; }
    }
    // ==========================================================
    // UNIFIED ATLAS PROVIDER INTERFACE
    // Implemented by both EditorLibrary and AssetLibrary
    // ==========================================================
    public interface IAtlasProvider
    {
        Texture2D GetAtlas(string name);
        Texture2D GetNormalAtlas(string name);
        List<string> GetNamesByType(AtlasType type);
    }
    // ==========================================================
    // 1. EDITOR ASSET REPOSITORY (Loads everything upfront for tools)
    // ==========================================================
    public class EditorLibrary : IAtlasProvider
    {
        public readonly Dictionary<string, AtlasMetadata> _library = new(StringComparer.OrdinalIgnoreCase);
        private readonly ContentManager _content;

        public EditorLibrary(ContentManager content) => _content = content;

        public SpriteFont EditorFont { get; private set; }

        public void LoadCoreContent()
        {
            EditorFont = _content.Load<SpriteFont>("Font");
            //AssetLibrary.LoadAtlas("Basic",AtlasType.Tile);
            LoadAtlas("Base", AtlasType.Tile);
            LoadAtlas("BaseWorld", AtlasType.Tile);
            LoadAtlas("Wild", AtlasType.Tile);
            LoadAtlas("Farm", AtlasType.Tile);
            LoadAtlas("Trees", AtlasType.Object);
            LoadAtlas("Building", AtlasType.Object);
            LoadAtlas("Prototype_Building", AtlasType.Object);
            LoadAtlas("Prototype_Building_i", AtlasType.Object);
            LoadAtlas("Prototype_Building_e", AtlasType.Object);
            LoadAtlas("Prototype_Building_r", AtlasType.Object);

            LoadAtlas("Roofs", AtlasType.Object);
            LoadAtlas("Roofs_n", AtlasType.Normal);
            LoadAtlas("Walls", AtlasType.Object);

            LoadAtlas("Trees_b", AtlasType.Object);
            LoadAtlas("Trees_l", AtlasType.Object);
            LoadAtlas("Trees_t", AtlasType.Object);
            LoadAtlas("Trees_s", AtlasType.Object);
        }

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
                    System.Diagnostics.Debug.WriteLine($"[EditorLibrary] Note: Atlas '{cleanName}' not loaded: {ex.Message}");
                }
            }
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
    // ==========================================================
    // 2. GAME ASSET REPOSITORY (Dynamically loads per active map)
    // ==========================================================
    public class AssetLibrary : IAtlasProvider
    {
        private readonly Dictionary<string, AtlasMetadata> _library = new(StringComparer.OrdinalIgnoreCase);
        private ContentManager _content;
        public SpriteFont customFont;

        public void LoadCoreContent(ContentManager content)
        {
            _content = content;
            try
            {
                customFont = content.Load<SpriteFont>("Seattle");
            }
            catch
            {
                try { customFont = content.Load<SpriteFont>("Font"); } catch { }
            }

            LoadAtlas("Items", AtlasType.Universal);

        }

        public void LoadMapAssets(Data.Map map, PrefabManager prefabManager, ContentManager content)
        {
            _content = content;
            if (map == null) return;

            var tilesets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var objectAtlases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Scan Tiles
            foreach (var tileLayer in map.Layers.OfType<Data.TileLayer>())
            {
                foreach (var chunk in tileLayer.Chunks.Values)
                {
                    for (int x = 0; x < Data.Chunk.CHUNK_SIZE; x++)
                    {
                        for (int y = 0; y < Data.Chunk.CHUNK_SIZE; y++)
                        {
                            var tile = chunk.Tiles[x, y];
                            if (tile != null && !string.IsNullOrWhiteSpace(tile.TilesetName))
                                tilesets.Add(tile.TilesetName);
                        }
                    }
                }
            }

            // Scan Objects & Modular Buildings
            foreach (var objLayer in map.Layers.OfType<Data.ObjectLayer>().Where(l => l.Type == Data.LayerType.Object))
            {
                foreach (var prop in objLayer.Objects.OfType<Data.PropObject>())
                {
                    var prefab = prefabManager.GetPrefab(prop.PrefabID);
                    if (prefab != null)
                    {
                        foreach (var state in prefab.AlternateStates.Values)
                        {
                            if (!string.IsNullOrWhiteSpace(state.AtlasName) &&
                                !state.AtlasName.Equals("Base", StringComparison.OrdinalIgnoreCase))
                            {
                                objectAtlases.Add(state.AtlasName);
                            }
                        }
                    }
                }

                // Scan Modular Buildings
                foreach (var bldg in objLayer.Objects.OfType<Data.ModularBuildingInstance>())
                {
                    objectAtlases.Add("Walls");
                    objectAtlases.Add("Roofs");

                    var bp = bldg.BlueprintRef;
                    if (bp != null)
                    {
                        foreach (var floor in bp.floors)
                        {
                            foreach (var w in floor.edgeStrips)
                                if (!string.IsNullOrWhiteSpace(w.atlas)) objectAtlases.Add(w.atlas);
                            foreach (var t in floor.auxiliaries)
                                if (!string.IsNullOrWhiteSpace(t.atlas)) objectAtlases.Add(t.atlas);
                        }
                    }
                }

            }

            foreach (var ts in tilesets) LoadAtlas(ts, AtlasType.Tile);
            foreach (var oa in objectAtlases)
            {
                LoadAtlas(oa, AtlasType.Object);
                LoadAtlas(string.Concat(Path.GetFileNameWithoutExtension(oa), "_n"), AtlasType.Normal);
            }
        }

        public void LoadAtlas(string assetName, AtlasType type)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return;
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
                catch (ContentLoadException) { }
            }
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

