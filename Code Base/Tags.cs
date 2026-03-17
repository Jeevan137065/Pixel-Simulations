using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Pixel_Simulations.UI;
using Pixel_Simulations.Data;
using Pixel_Simulations.Editor;

namespace Pixel_Simulations
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TagDefinition
    {
        [JsonProperty] public string HashID { get; set; } // e.g., "#solid"
        [JsonProperty] public string Name { get; set; }   // e.g., "Solid Object"
        [JsonProperty] public string Description { get; set; } // e.g., "Blocks player movement"
        [JsonProperty] public Color TagColor { get; set; } = Color.LightGray;

        [JsonIgnore] public int UsageCount { get; set; } = 0; // Calculated at runtime
    }

    public class TagManager
    {
        public Dictionary<string, TagDefinition> Tags { get; private set; } = new Dictionary<string, TagDefinition>();
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
            Tags = new Dictionary<string, TagDefinition>();
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                var list = JsonConvert.DeserializeObject<List<TagDefinition>>(json);
                if (list != null)
                {
                    foreach (var tag in list)
                        if (!Tags.ContainsKey(tag.HashID)) Tags.Add(tag.HashID, tag);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to parse tags.json: {ex.Message}"); }
        }

        public TagDefinition GetTag(string hashId) => Tags.TryGetValue(hashId, out var t) ? t : null;
    }
}
