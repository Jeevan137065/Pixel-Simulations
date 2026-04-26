// --- START OF FILE Interactions.cs ---
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pixel_Simulations
{
    [JsonObject(MemberSerialization.OptIn)]
    public class InteractionRule
    {
        [JsonProperty] public string RuleID { get; set; }

        // --- CONDITIONS (Subject + Object) ---
        [JsonProperty] public List<int> RequiredTargetTags { get; set; } = new List<int>();
        [JsonProperty] public Dictionary<string, string> RequiredTargetProperties { get; set; } = new Dictionary<string, string>();
        [JsonProperty] public List<string> RequiredToolTags { get; set; } = new List<string>(); // Use "empty_hand" for no tool

        // --- RESULTS (Action) ---
        [JsonProperty] public string LootPropertyKey { get; set; } // If the entity holds its drop ID in a property (e.g. "FruitID")
        [JsonProperty] public int DefaultLootID { get; set; } = -1; // Hardcoded fallback drop
        [JsonProperty] public int LootAmount { get; set; } = 1;

        [JsonProperty] public bool DestroyTarget { get; set; } = false;

        // State Mutations (e.g. "HasFruit" -> "False")
        [JsonProperty] public Dictionary<string, string> SetProperties { get; set; } = new Dictionary<string, string>();

        // Visual Mutation (Shifts the source rect on the spritesheet to show a different state)
        [JsonProperty] public Point SpriteOffset { get; set; } = Point.Zero;
    }

    public class InteractionManager
    {
        public List<InteractionRule> Rules { get; private set; } = new List<InteractionRule>();

        public void Load(string path)
        {
            Rules.Clear();
            if (!File.Exists(path))
            {
                // Generate default test rules if missing

                // Rule 1: Harvest Berry Bush (Empty Hand)
                Rules.Add(new InteractionRule
                {
                    RuleID = "harvest_bush",
                    RequiredTargetTags = new List<int> { 10 },
                    RequiredTargetProperties = new Dictionary<string, string> { { "HasFruit", "True" } },
                    RequiredToolTags = new List<string> { "empty_hand" },
                    LootPropertyKey = "FruitID", // The bush tells us what fruit it holds
                    DefaultLootID = 200, // Salmon Berry ID fallback
                    LootAmount = 2,
                    SetProperties = new Dictionary<string, string> { { "HasFruit", "False" } },
                    SpriteOffset = new Point(32, 0) // Shift spritesheet 32px right to show the "empty" bush
                });

                // Rule 2: Chop Bush (Axe)
                Rules.Add(new InteractionRule
                {
                    RuleID = "chop_bush",
                    RequiredTargetTags = new List<int> { 101 },
                    RequiredToolTags = new List<string> { "axe" },
                    DefaultLootID = 1, // Wood Log
                    LootAmount = 1,
                    DestroyTarget = true
                });

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(Rules, Formatting.Indented));
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var list = JsonConvert.DeserializeObject<List<InteractionRule>>(json);
                if (list != null) Rules = list;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Interaction Load Error: {ex.Message}"); }
        }

        public InteractionRule Evaluate(GameEntity target, ItemDefinition heldItem)
        {
            if (target == null) return null;

            // Determine Tool Tags
            var toolTags = new HashSet<string>();
            if (heldItem == null) toolTags.Add("empty_hand");
            else foreach (var tag in heldItem.ItemTags) toolTags.Add(tag);

            // Find the first rule that matches all conditions
            foreach (var rule in Rules)
            {
                // 1. Check Tool Tags
                if (rule.RequiredToolTags.Count > 0 && !rule.RequiredToolTags.All(t => toolTags.Contains(t))) continue;

                // 2. Check Target Tags
                if (rule.RequiredTargetTags.Count > 0 && !rule.RequiredTargetTags.All(t => target.ActiveTags.Contains(t))) continue;

                // 3. Check Target Properties
                bool propsMatch = true;
                foreach (var kvp in rule.RequiredTargetProperties)
                {
                    string targetVal = target.GetProperty(kvp.Key, "");
                    if (targetVal != kvp.Value) { propsMatch = false; break; }
                }
                if (!propsMatch) continue;

                // If we get here, it's a perfect match!
                return rule;
            }
            return null;
        }
    }
}
// --- END OF FILE ---