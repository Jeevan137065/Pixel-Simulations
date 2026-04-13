using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
namespace Pixel_Simulations
{
    public class ItemManager
    {
        public Dictionary<int, ItemDefinition> Items { get; private set; } = new Dictionary<int, ItemDefinition>();
        private Random _random = new Random();

        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonConvert.SerializeObject(Items.Values, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to save items: {ex.Message}"); }
        }

        public void Load(string path)
        {
            Items.Clear();
            if (!File.Exists(path))
            {
            //    AddItemDef(1, "Wood Log", "Sturdy timber for crafting.", "Material", 0.5f, 0.8f, "Basic", new Point(0, 0), "burnable", "crafting");
            //    AddItemDef(2, "Stone Block", "Heavy rock.", "Material", 1.5f, 2.5f, "Basic", new Point(1, 0), "heavy", "crafting");
            //    AddItemDef(3, "Iron Ore", "Unrefined metal.", "Material", 2.0f, 3.0f, "Basic", new Point(2, 0), "ore", "smeltable");

            //    AddItemDef(10, "Rusty Hoe", "Tills the soil.", "Tool", 1.0f, 1.2f, "Basic", new Point(0, 1), "tool", "farming");
            //    AddItemDef(11, "Watering Can", "Holds water for crops.", "Tool", 0.5f, 2.0f, "Basic", new Point(1, 1), "tool", "farming");

            //    AddItemDef(100, "Turnip Seeds", "Plant in Spring.", "Seed", 0.01f, 0.02f, "Basic", new Point(0, 2), "crop_family_root", "spring_crop");
            //    AddItemDef(101, "Turnip", "A crisp, spicy root.", "Crop", 0.3f, 0.6f, "Basic", new Point(1, 2), "crop_family_root", "edible");
            //    AddItemDef(102, "Carrot Seeds", "Plant in Spring.", "Seed", 0.01f, 0.02f, "Basic", new Point(2, 2), "crop_family_root", "spring_crop");
            //    AddItemDef(103, "Carrot", "Good for eyesight.", "Crop", 0.2f, 0.4f, "Basic", new Point(3, 2), "crop_family_root", "edible"); Save(path);
            //    System.Diagnostics.Debug.WriteLine($"items.json is FUCKING EMPTY: {path}");
                return;
            }
            try
            {
                // 1. Read the raw string from the file
                string json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine("items.json is empty.");
                    return;
                }

                var loadedItems = JsonConvert.DeserializeObject<List<ItemDefinition>>(json);

            // 3. Loop over the list and register each item into the Dictionary
                if (loadedItems != null)
                {
                    foreach (var itemDef in loadedItems)
                    {
                        // Safety check: Prevent duplicate IDs from crashing the dictionary
                        if (!Items.ContainsKey(itemDef.ID))
                        {
                            Items.Add(itemDef.ID, itemDef);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Duplicate Item ID found in items.json: {itemDef.ID}. Skipping.");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Successfully loaded {Items.Count} items into ItemManager.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to parse items.json. Error: {ex.Message}");
            }
        }
        private void AddItemDef(int id, string name, string desc, string cat, float minW, float maxW, string atlas, Point coord, params string[] tags)
        {
            Items[id] = new ItemDefinition { ID = id, Name = name, Description = desc, Category = cat, MinWeight = minW, MaxWeight = maxW, IconSource = atlas, Coord = coord, ItemTags = new List<string>(tags) };
        }
        public ItemDefinition GetItem(int id) => Items.TryGetValue(id, out var item) ? item : null;

        public float GetRandomWeight(ItemDefinition def)
        {
            return def.MinWeight + (float)_random.NextDouble() * (def.MaxWeight - def.MinWeight);
        }
    }
}
