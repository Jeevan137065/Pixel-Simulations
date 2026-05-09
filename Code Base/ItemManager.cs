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
            if (File.Exists(itemsPath))
            {
                try
                {
                    string json = File.ReadAllText(itemsPath);
                    var loadedItems = JsonConvert.DeserializeObject<List<ItemDefinition>>(json);
                    if (loadedItems != null)
                    {
                        foreach (var itemDef in loadedItems)
                        {
                            if (!Items.ContainsKey(itemDef.ID)) Items.Add(itemDef.ID, itemDef);
                            else System.Diagnostics.Debug.WriteLine($"Warning: Duplicate Item ID in items.json: {itemDef.ID}. Skipping.");
                        }
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to parse items.json: {ex.Message}"); }
            }

            PhysicalItems.Clear();
            if (File.Exists(physicalPath))
            {
                try
                {
                    string physJson = File.ReadAllText(physicalPath);
                    var physList = JsonConvert.DeserializeObject<List<PhysicalItemDefinition>>(physJson);

                    if (physList != null)
                    {
                        foreach (var itemDef in physList)
                        {
                            // Warn if we are overwriting a physical item due to duplicate IDs!
                            if (PhysicalItems.ContainsKey(itemDef.ItemID))
                                System.Diagnostics.Debug.WriteLine($"WARNING: Overwriting Physical Item ID {itemDef.ItemID}. Check your HTML Tool exports!");

                            PhysicalItems[itemDef.ItemID] = itemDef;
                        }
                        System.Diagnostics.Debug.WriteLine($"Successfully loaded {PhysicalItems.Count} physical items.");
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to parse physical_items.json: {ex.Message}"); }
            }
        }

        public ItemDefinition GetItem(int id) => Items.TryGetValue(id, out var item) ? item : null;
        public PhysicalItemDefinition GetPhysicalDef(int itemId) => PhysicalItems.TryGetValue(itemId, out var def) ? def : null;
        public float GetRandomWeight(ItemDefinition def) => def.MinWeight + (float)new Random().NextDouble() * (def.MaxWeight - def.MinWeight);
    }
}
