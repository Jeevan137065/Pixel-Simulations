using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pixel_Simulations.Data
{
    public class BuildingManager
    {
        public static BuildingManager Instance { get; private set; }

        public Dictionary<string, BuildingData> Buildings { get; private set; }
            = new Dictionary<string, BuildingData>(StringComparer.OrdinalIgnoreCase);

        public RoofCatalogData MasterRoofCatalog { get; private set; }
        public ModularCatalogData MasterModularCatalog { get; private set; }

        public BuildingManager()
        {
            Instance = this;
        }

        /// <summary>
        /// Loads roof and modular catalogs from Assets/Data/, and all individual building files from Assets/Buildings/.
        /// </summary>
        public void Load(string assetsRoot)
        {
            Instance = this;
            Buildings.Clear();

            string dataDir = Path.Combine(assetsRoot, "Data");
            string buildingsDir = Path.Combine(assetsRoot, "Buildings");

            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(buildingsDir);

            // 1. LOAD ROOF CATALOG from Assets/Data/roof_catalog.json
            string roofPath = Path.Combine(dataDir, "roof_catalog.json");
            LoadRoofCatalog(roofPath);

            // 2. LOAD MODULAR CATALOG from Assets/Data/modular_catalog.json
            string modularPath = Path.Combine(dataDir, "catalogA.json");
            LoadModularCatalog(modularPath);

            // 3. LOAD ALL INDIVIDUAL BUILDINGS from Assets/Buildings/*.json
            LoadBuildingFiles(buildingsDir);
        }

        private void LoadRoofCatalog(string path)
        {
            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingManager] Notice: roof_catalog.json not found at {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                // Handle both { "Families": [...] } and direct { "families": [...] }
                var fileData = JsonConvert.DeserializeObject<dynamic>(json);
                if (fileData != null)
                {
                    string rawJson = json;
                    if (fileData.Families != null)
                        rawJson = fileData.ToString();

                    MasterRoofCatalog = JsonConvert.DeserializeObject<RoofCatalogData>(rawJson);
                    System.Diagnostics.Debug.WriteLine($"[BuildingManager] Loaded Roof Catalog ({MasterRoofCatalog?.families?.Count ?? 0} families).");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingManager] Error loading roof_catalog.json: {ex.Message}");
            }
        }

        private void LoadModularCatalog(string path)
        {
            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingManager] Notice: modular_catalog.json not found at {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var fileData = JsonConvert.DeserializeObject<dynamic>(json);
                if (fileData != null)
                {
                    string rawJson = json;
                    if (fileData.Catalog != null)
                        rawJson = fileData.Catalog.ToString();

                    MasterModularCatalog = JsonConvert.DeserializeObject<ModularCatalogData>(rawJson);
                    System.Diagnostics.Debug.WriteLine($"[BuildingManager] Loaded Modular Catalog.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingManager] Error loading modular_catalog.json: {ex.Message}");
            }
        }

        private void LoadBuildingFiles(string directory)
        {
            if (!Directory.Exists(directory)) return;

            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    // 1. Try deserializing as an individual BuildingData
                    var bldg = JsonConvert.DeserializeObject<BuildingData>(json);

                    // 2. Fallback: if exported wrapped in an array or container
                    if (bldg == null || string.IsNullOrEmpty(bldg.id) || bldg.floors == null)
                    {
                        var container = JsonConvert.DeserializeObject<BuildingBlueprintFile>(json);
                        if (container?.Buildings != null && container.Buildings.Count > 0)
                        {
                            bldg = container.Buildings[0];
                        }
                    }

                    if (bldg != null && !string.IsNullOrEmpty(bldg.id))
                    {
                        Buildings[bldg.id] = bldg;
                        System.Diagnostics.Debug.WriteLine($"[BuildingManager] Loaded Building '{bldg.name}' ({bldg.id}) from {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BuildingManager] Error parsing building '{file}': {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[BuildingManager] Total Buildings Loaded: {Buildings.Count}");
        }

        public BuildingData GetBuilding(string buildingId) =>
            Buildings.TryGetValue(buildingId, out var b) ? b : null;

        public RoofFamilyData GetRoofFamily(string familyId) =>
            MasterRoofCatalog?.families?.Find(f => f.id.Equals(familyId, StringComparison.OrdinalIgnoreCase));
    }
}