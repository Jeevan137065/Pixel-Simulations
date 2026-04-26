using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Pixel_Simulations
{
    //Quick way of accesing tags instead of hard coding tag ids
    //public  QuickTag
    //{
    //    Hard_Collision = 2,
    //    SoftCollision = 3,

    //    Building = 40,

    //    Light_Source = 50,
    //    Light_Falloff = 51,
    //    Reflections = 52,

    //    Grass = 100,
    //    Bush = 101,
    //}
    [JsonObject(MemberSerialization.OptIn)]

    public class TagDefinition
    {
        [JsonProperty] public int ID { get; set; } // Changed from string HashID
        [JsonProperty] public string Name { get; set; }   // e.g., "Solid Object"
        [JsonProperty] public string Description { get; set; } // e.g., "Blocks player movement"
        [JsonProperty] public Color TagColor { get; set; } = Color.LightGray;

        [JsonIgnore] public int UsageCount { get; set; } = 0; // Calculated at runtime
    }

    public class TagManager
    {
        public Dictionary<int, TagDefinition> Tags { get; private set; } = new Dictionary<int, TagDefinition>();
        public Dictionary<string, int> QuickTags { get; private set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonConvert.SerializeObject(Tags.Values, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to save tags: {ex.Message}"); }
        }

        public void Load(string path)
        {
            Tags.Clear();
            QuickTags.Clear();

            if (!File.Exists(path))
            {
                Save(path);
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new ColorConverter());

                var list = JsonConvert.DeserializeObject<List<TagDefinition>>(json, settings);

                if (list != null)
                {
                    foreach (var tag in list)
                    {
                        if (!Tags.ContainsKey(tag.ID))
                        {
                            Tags.Add(tag.ID, tag);
                            if (!QuickTags.ContainsKey(tag.Name)) QuickTags.Add(tag.Name, tag.ID);
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to parse tags.json: {ex.Message}"); }

            // --- ENFORCE CORE SYSTEM TAGS ---
            // Even if a JSON is loaded from the HTML tool, we force these to exist 
            // so the engine's hardcoded physics/lighting don't crash.
        }

        private void EnsureSystemTag(int id, string name, string desc, Color color)
        {
            if (!Tags.ContainsKey(id))
            {
                var newTag = new TagDefinition { ID = id, Name = name, Description = desc, TagColor = color };
                Tags.Add(id, newTag);

                // Add to quick lookup
                if (!QuickTags.ContainsKey(name))
                {
                    QuickTags.Add(name, id);
                }
            }
        }

        // Returns the Tag object if you need its color/description
        public TagDefinition GetTag(int id) => Tags.TryGetValue(id, out var t) ? t : null;

        // NEW: Safely get a Tag ID by its string name. Returns -1 if it doesn't exist.
        public int GetId(string name) => QuickTags.TryGetValue(name, out int id) ? id : -1;
    }
}
