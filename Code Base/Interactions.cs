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
    public enum EffectType
    {
        DropLoot,
        ConsumeHeldItem,
        SetProperty,
        DestroyTarget,
        SpawnEntity,
        ChangeTile
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class InteractionEffect
    {
        [JsonProperty] public EffectType Type { get; set; }
        [JsonProperty] public string StringValue { get; set; }  // e.g., Property Key, or Entity Prefab ID
        [JsonProperty] public int IntValue { get; set; }        // e.g., Item ID, Amount, or Tile ID
        [JsonProperty] public string SecondaryValue { get; set; } // e.g., Property Value
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class InteractionRule
    {
        [JsonProperty] public string RuleID { get; set; }

        // --- CONDITIONS ---
        [JsonProperty] public List<int> RequiredTargetTags { get; set; } = new List<int>();
        [JsonProperty] public Dictionary<string, string> RequiredTargetProperties { get; set; } = new Dictionary<string, string>();
        [JsonProperty] public List<string> RequiredToolTags { get; set; } = new List<string>(); // "empty_hand" for no tool

        // NEW: Allows interacting with the ground! (e.g. requires Dirt tile to use Hoe)
        [JsonProperty] public int RequiredTileID { get; set; } = -1;

        // --- EFFECTS ---
        [JsonProperty] public List<InteractionEffect> Effects { get; set; } = new List<InteractionEffect>();
    }

    public class InteractionManager
    {
        public List<InteractionRule> Rules { get; private set; } = new List<InteractionRule>();

        public void Load(string path)
        {
            Rules.Clear();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var list = JsonConvert.DeserializeObject<List<InteractionRule>>(json);
                    if (list != null) Rules = list;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Interaction Load Error: {ex.Message}"); }
            }
        }

        // We now pass in the Tile under the cursor/player, along with the entity
        public InteractionRule Evaluate(GameEntity targetEntity, TileInfo targetTile, ItemDefinition heldItem)
        {
            var toolTags = new HashSet<string>();
            if (heldItem == null) toolTags.Add("empty_hand");
            else foreach (var tag in heldItem.ItemTags) toolTags.Add(tag);

            foreach (var rule in Rules)
            {
                // 1. Check Tool
                if (rule.RequiredToolTags.Count > 0 && !rule.RequiredToolTags.All(t => toolTags.Contains(t))) continue;

                // 2. Check Tile Condition (If rule requires a specific ground tile)
                if (rule.RequiredTileID != -1)
                {
                    if (targetTile == null || targetTile.TileID != rule.RequiredTileID) continue;
                }

                // 3. Check Entity Conditions (If rule targets an object)
                if (rule.RequiredTargetTags.Count > 0 || rule.RequiredTargetProperties.Count > 0)
                {
                    if (targetEntity == null) continue; // Rule requires an entity, but we didn't click one

                    if (rule.RequiredTargetTags.Count > 0 && !rule.RequiredTargetTags.All(t => targetEntity.ActiveTags.Contains(t))) continue;

                    bool propsMatch = true;
                    foreach (var kvp in rule.RequiredTargetProperties)
                    {
                        if (targetEntity.GetProperty(kvp.Key, "") != kvp.Value) { propsMatch = false; break; }
                    }
                    if (!propsMatch) continue;
                }

                // Match found!
                return rule;
            }
            return null;
        }
    }
}
// --- END OF FILE ---