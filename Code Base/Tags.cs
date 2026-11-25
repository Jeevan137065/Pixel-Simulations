using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pixel_Simulations
{
    public static class SystemTags
    {
        // String constants matching the CodeNames authored in tags.html
        public const string SOLID = "SOLID";
        public const string NAV = "NAV";
        public const string WARP_MAP = "WARP_MAP";      // Load new map binary file
        public const string WARP_INTERIOR = "WARP_INTERIOR"; // Seamless building cutaway
        public const string WATER = "WATER";
        public const string SPAWN = "SPAWN";
        public const string LIGHT_SOURCE = "LIGHT_SOURCE";
        public const string LIGHT_FALLOFF = "LIGHT_FALLOFF";
        public const string REFLECTION = "REFLECTION";
        public const string GRASS = "GRASS";
        public const string FARMABLE = "FARMABLE";
        public const string VEGETATION = "VEGETATION";
        public const string BIOME_FARM_ZONE = "BIOME_FARM_ZONE";
        public const int BIOME_FARM_ZONE_VALUE = 90;

        // Fallbacks in case tags.json is temporarily absent or incomplete
        public static readonly Dictionary<string, int> Fallbacks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { SOLID, 2 },
            { NAV, 5 },
            { WARP_MAP, 6 },
            { WARP_INTERIOR, 7 },
            { LIGHT_SOURCE, 10 },
            { LIGHT_FALLOFF, 11 },
            { REFLECTION, 12 },
            { SPAWN, 20 },
            { WATER, 70 },
            { GRASS, 80 },
            { FARMABLE, 90 },
            { VEGETATION, 100 }
        };
    }

    // ==========================================================
    // DATA CONTRACTS
    // ==========================================================
    [JsonObject(MemberSerialization.OptIn)]
    public class TagDefinition
    {
        [JsonProperty] public int ID { get; set; }
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string CodeName { get; set; } // e.g. "SOLID", "WATER"
        [JsonProperty] public string Description { get; set; }
        [JsonProperty] public Color TagColor { get; set; } = Color.LightGray;
        [JsonProperty] public string Category { get; set; } = "General";
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PropertyTemplate
    {
        [JsonProperty] public string ID { get; set; }
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Category { get; set; } = "General";
        [JsonProperty] public string Description { get; set; }
        [JsonProperty] public Dictionary<string, MapProperty> Properties { get; set; } = new Dictionary<string, MapProperty>();
    }

    // ==========================================================
    // ROBUST COLOR CONVERTER (Supports "R,G,B,A" and Hex)
    // ==========================================================
    public class RobustColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.R},{value.G},{value.B},{value.A}");
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                string raw = (string)reader.Value;
                if (string.IsNullOrWhiteSpace(raw)) return Color.White;

                // 1. "R,G,B,A" or "R,G,B"
                if (raw.Contains(","))
                {
                    var parts = raw.Split(',');
                    if (parts.Length >= 3 &&
                        byte.TryParse(parts[0], out byte r) &&
                        byte.TryParse(parts[1], out byte g) &&
                        byte.TryParse(parts[2], out byte b))
                    {
                        byte a = parts.Length > 3 && byte.TryParse(parts[3], out byte parsedA) ? parsedA : (byte)255;
                        return new Color(r, g, b, a);
                    }
                }
                // 2. Hex String "#RRGGBB" or "#RRGGBBAA"
                else if (raw.StartsWith("#"))
                {
                    string hex = raw.Substring(1);
                    if (hex.Length == 6)
                    {
                        return new Color(
                            (byte)Convert.ToByte(hex.Substring(0, 2), 16),
                            (byte)Convert.ToByte(hex.Substring(2, 2), 16),
                            (byte)Convert.ToByte(hex.Substring(4, 2), 16),
                            (byte)255
                        );
                    }
                    if (hex.Length == 8)
                    {
                        return new Color(
                            Convert.ToByte(hex.Substring(0, 2), 16),
                            Convert.ToByte(hex.Substring(2, 2), 16),
                            Convert.ToByte(hex.Substring(4, 2), 16),
                            Convert.ToByte(hex.Substring(6, 2), 16)
                        );
                    }
                }
            }
            return Color.Magenta;
        }
    }

    // ==========================================================
    // TAG & PROPERTY TEMPLATE MANAGER
    // ==========================================================
    public class TagManager
    {
        public static TagManager Instance { get; private set; }

        public Dictionary<int, TagDefinition> Tags { get; private set; } = new Dictionary<int, TagDefinition>();
        public Dictionary<string, int> ByCodeName { get; private set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> ByName { get; private set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Property Templates loaded from property_templates.json
        public Dictionary<string, PropertyTemplate> PropertyTemplates { get; private set; } = new Dictionary<string, PropertyTemplate>(StringComparer.OrdinalIgnoreCase);

        private readonly JsonSerializerSettings _jsonSettings;

        public TagManager()
        {
            Instance = this;
            _jsonSettings = new JsonSerializerSettings
            {
                Converters = { new RobustColorConverter() },
                Formatting = Formatting.Indented
            };
        }

        public void Load(string tagsPath, string templatesPath = null)
        {
            Instance = this;
            Tags.Clear();
            ByCodeName.Clear();
            ByName.Clear();

            // 1. LOAD TAGS.JSON
            if (File.Exists(tagsPath))
            {
                try
                {
                    string json = File.ReadAllText(tagsPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = JsonConvert.DeserializeObject<List<TagDefinition>>(json, _jsonSettings);
                        if (list != null)
                        {
                            foreach (var tag in list) RegisterTag(tag);
                            System.Diagnostics.Debug.WriteLine($"[TagManager] Loaded {Tags.Count} tags from {tagsPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TagManager] Error parsing tags.json: {ex.Message}");
                }
            }

            EnsureSystemFallbacks();

            // 2. LOAD PROPERTY_TEMPLATES.JSON
            string autoTplPath = templatesPath ?? Path.Combine(Path.GetDirectoryName(tagsPath) ?? "", "property_templates.json");
            LoadPropertyTemplates(autoTplPath);
        }

        public void LoadPropertyTemplates(string path)
        {
            PropertyTemplates.Clear();
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var list = JsonConvert.DeserializeObject<List<PropertyTemplate>>(json, _jsonSettings);
                    if (list != null)
                    {
                        foreach (var tpl in list)
                        {
                            PropertyTemplates[tpl.ID] = tpl;
                        }
                        System.Diagnostics.Debug.WriteLine($"[TagManager] Loaded {PropertyTemplates.Count} property templates from {path}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TagManager] Error parsing property_templates.json: {ex.Message}");
            }
        }

        private void RegisterTag(TagDefinition tag)
        {
            Tags[tag.ID] = tag;
            if (!string.IsNullOrWhiteSpace(tag.CodeName))
                ByCodeName[tag.CodeName] = tag.ID;
            if (!string.IsNullOrWhiteSpace(tag.Name))
                ByName[tag.Name] = tag.ID;
        }

        private void EnsureSystemFallbacks()
        {
            foreach (var kvp in SystemTags.Fallbacks)
            {
                if (!ByCodeName.ContainsKey(kvp.Key))
                {
                    var fallbackTag = new TagDefinition
                    {
                        ID = kvp.Value,
                        Name = kvp.Key,
                        CodeName = kvp.Key,
                        Description = "Engine Fallback System Tag",
                        TagColor = Color.Magenta
                    };
                    RegisterTag(fallbackTag);
                }
            }
        }

        public TagDefinition GetTag(int id) => Tags.TryGetValue(id, out var t) ? t : null;

        public int GetId(string codeName)
        {
            if (string.IsNullOrEmpty(codeName)) return -1;
            if (ByCodeName.TryGetValue(codeName, out int id)) return id;
            if (ByName.TryGetValue(codeName, out id)) return id;
            if (SystemTags.Fallbacks.TryGetValue(codeName, out id)) return id;
            return -1;
        }

        public PropertyTemplate GetTemplate(string id) => PropertyTemplates.TryGetValue(id, out var tpl) ? tpl : null;

        /// <summary>
        /// Applies a loaded property template bundle to a map object without overwriting existing custom keys.
        /// </summary>
        public void ApplyTemplate(MapObject obj, string templateId)
        {
            var tpl = GetTemplate(templateId);
            if (tpl == null || obj == null) return;

            foreach (var kvp in tpl.Properties)
            {
                obj.Properties[kvp.Key] = new MapProperty(kvp.Value.Type, kvp.Value.Value);
            }

            if (tpl.ID.IndexOf("doorway", StringComparison.OrdinalIgnoreCase) >= 0 ||
        tpl.Properties.ContainsKey("TriggerType") && tpl.Properties["TriggerType"].Value == "Doorway")
            {
                obj.AddTag(SystemTags.WARP_INTERIOR);
            }
            else if (tpl.ID.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                obj.AddTag(SystemTags.LIGHT_SOURCE);
            }
            else if (tpl.ID.IndexOf("reflection", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                obj.AddTag(SystemTags.REFLECTION);
            }
        }
    }

    // ==========================================================
    // EXTENSION METHODS FOR TYPE-SAFE TAG HANDLING
    // ==========================================================
    public static class TagExtensions
    {
        public static bool HasTag(this MapObject obj, string codeName)
        {
            if (obj?.Tags == null) return false;
            int id = TagManager.Instance?.GetId(codeName) ?? -1;
            return id != -1 && obj.Tags.Contains(id);
        }

        public static void AddTag(this MapObject obj, string codeName)
        {
            if (obj == null) return;
            obj.Tags ??= new HashSet<int>();
            int id = TagManager.Instance?.GetId(codeName) ?? -1;
            if (id != -1) obj.Tags.Add(id);
        }

        public static void RemoveTag(this MapObject obj, string codeName)
        {
            if (obj?.Tags == null) return;
            int id = TagManager.Instance?.GetId(codeName) ?? -1;
            if (id != -1) obj.Tags.Remove(id);
        }

        public static bool HasTag(this GameEntity entity, string codeName)
        {
            if (entity?.ActiveTags == null) return false;
            int id = TagManager.Instance?.GetId(codeName) ?? -1;
            return id != -1 && entity.ActiveTags.Contains(id);
        }
    }
}