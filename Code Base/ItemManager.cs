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
        public Dictionary<int, PhysicalItemDefinition> PhysicalItems { get; private set; } = new Dictionary<int, PhysicalItemDefinition>();
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

        public void Load(string itemsPath, string physicalPath)
        {
            Items.Clear();
            if (!File.Exists(itemsPath))
            {
                System.Diagnostics.Debug.WriteLine($"items.json is FUCKING EMPTY: {itemsPath}");
                return;
            }
            try
            {
                // 1. Read the raw string from the file
                string json = File.ReadAllText(itemsPath);

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
            PhysicalItems.Clear();
            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    string physJson = System.IO.File.ReadAllText(physicalPath);

                    // BUG FIX: Deserialize as a LIST, not a Dictionary!
                    var physList = JsonConvert.DeserializeObject<List<PhysicalItemDefinition>>(physJson);

                    if (physList != null)
                    {
                        foreach (var itemDef in physList)
                        {
                            // Register into the dictionary using the ItemID as the key
                            PhysicalItems[itemDef.ItemID] = itemDef;
                        }
                        System.Diagnostics.Debug.WriteLine($"Successfully loaded {PhysicalItems.Count} physical items.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse physical_items.json: {ex.Message}");
                }
            }
        }
        private void AddItemDef(int id, string name, string desc, string cat, float minW, float maxW, string atlas, Point coord, params string[] tags)
        {
            Items[id] = new ItemDefinition { ID = id, Name = name, Description = desc, Category = cat, MinWeight = minW, MaxWeight = maxW, IconSource = atlas, Coord = coord, ItemTags = new List<string>(tags) };
        }
        public ItemDefinition GetItem(int id) => Items.TryGetValue(id, out var item) ? item : null;
        public PhysicalItemDefinition GetPhysicalDef(int itemId) => PhysicalItems.TryGetValue(itemId, out var def) ? def : null;
        public float GetRandomWeight(ItemDefinition def) => def.MinWeight + (float)new Random().NextDouble() * (def.MaxWeight - def.MinWeight);
    }
}
