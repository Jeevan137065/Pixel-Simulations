using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Pixel_Simulations.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pixel_Simulations
{
    public class PlantManager
    {
        public Dictionary<string, PlantDefinition> Plants { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, List<PlantDefinition>> ForagesByMaskValue { get; private set; } = new();
        public Dictionary<int, PlantDefinition> PlantsBySeedItemId { get; private set; } = new();
        public void Load(string plantsJsonPath)
        {
            Plants.Clear();
            ForagesByMaskValue.Clear();
            PlantsBySeedItemId.Clear();

            if (!File.Exists(plantsJsonPath))
            {
                System.Diagnostics.Debug.WriteLine($"[PlantManager] Notice: {plantsJsonPath} not found.");
                return;
            }

            try
            {
                string json = File.ReadAllText(plantsJsonPath);
                var list = JsonConvert.DeserializeObject<List<PlantDefinition>>(json);

                if (list != null)
                {
                    foreach (var p in list)
                    {
                        Plants[p.ID] = p;
                        if (p.SeedItemID > 0)
                        {
                            PlantsBySeedItemId[p.SeedItemID] = p;
                        }
                        // Index wild forages by Green channel mask values
                        if (p.MaskSpawnValue > 0)
                        {
                            if (!ForagesByMaskValue.ContainsKey(p.MaskSpawnValue))
                                ForagesByMaskValue[p.MaskSpawnValue] = new List<PlantDefinition>();

                            ForagesByMaskValue[p.MaskSpawnValue].Add(p);
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[PlantManager] Successfully loaded {Plants.Count} plants and {ForagesByMaskValue.Count} forage mask bindings.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlantManager] Error loading plants.json: {ex.Message}");
            }
        }

        public PlantDefinition GetPlant(string id) =>
            Plants.TryGetValue(id, out var def) ? def : null;

        public PlantDefinition FindPlantBySeed(ItemDefinition seedItem)
        {
            if (seedItem == null) return null;

            // 1. Check explicit property or tag (e.g. tag "plant:carrot")
            var plantTag = seedItem.ItemTags.FirstOrDefault(t => t.StartsWith("plant:", StringComparison.OrdinalIgnoreCase));
            if (plantTag != null)
            {
                string pid = plantTag.Substring(6);
                if (Plants.TryGetValue(pid, out var p)) return p;
            }

            // 2. Fallback: match by clean name (e.g. "Carrot Seeds" -> "Carrot")
            string cleanName = seedItem.Name.Replace("Seeds", "").Replace("Seed", "").Trim();
            return Plants.Values.FirstOrDefault(p => p.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                                                    p.ID.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
        }
        public PlantDefinition GetPlantBySeedItem(int seedItemId)
        {
            if (PlantsBySeedItemId.TryGetValue(seedItemId, out var def))
                return def;
            return null;
        }
        /// <summary>
        /// Populates seasonal wild forages (Mushrooms, Berry Bushes, Seaweed) across the map based on the Green mask channel.
        /// </summary> 
        public int ForceSpawnForages(GameState state)
        {
            if (state.CurrentMap == null || state.TerrainMaskChunks == null || state.TerrainMaskChunks.Count == 0) return 0;

            int spawnedCount = 0;
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;

            foreach (var kvp in state.TerrainMaskChunks)
            {
                Point chunkCoord = kvp.Key;
                var maskTex = kvp.Value;
                Color[] maskPixels = new Color[chunkSize * chunkSize];
                maskTex.GetData(maskPixels);

                float chunkWorldX = chunkCoord.X * chunkSize;
                float chunkWorldY = chunkCoord.Y * chunkSize;

                for (int py = 0; py < chunkSize; py += 16)
                {
                    for (int px = 0; px < chunkSize; px += 16)
                    {
                        Color maskCol = maskPixels[py * chunkSize + px];
                        int greenByte = maskCol.G;

                        if (greenByte > 0 && ForagesByMaskValue.TryGetValue(greenByte, out var eligiblePlants))
                        {
                            Vector2 worldPos = new Vector2(chunkWorldX + px + 8f, chunkWorldY + py + 16f);
                            Point soilCell = GridHelper.GetTileCell(worldPos);

                            bool alreadyExists = state.ActiveSave.ActivePlants.Any(p => p.SoilCell == soilCell);
                            if (alreadyExists) continue;

                            var chosenPlant = eligiblePlants.FirstOrDefault();
                            if (chosenPlant == null) continue;

                            int ripeStage = chosenPlant.Stages.FindIndex(s => s.IsHarvestable);
                            if (ripeStage < 0) ripeStage = chosenPlant.Stages.Count - 1;

                            var instance = new PlantInstance
                            {
                                PlantID = chosenPlant.ID,
                                Position = worldPos,
                                CurrentStageIndex = ripeStage, // Spawns ripe ready to harvest
                                DaysInStage = 0,
                                IsWildForage = true,
                                SoilCell = soilCell
                            };

                            state.ActiveSave.ActivePlants.Add(instance);
                            spawnedCount++;
                        }
                    }
                }
            }

            state._entityManager.RebuildStaticSprites(state);
            System.Diagnostics.Debug.WriteLine($"[PlantManager] Force-spawned {spawnedCount} wild forages.");
            return spawnedCount;
        }
        public void SpawnSeasonalWildForages(GameState state)
        {
            if (state.CurrentMap == null || state.TerrainMaskChunks == null || state.TerrainMaskChunks.Count == 0) return;

            string curSeason = state.Calendar.CurrentSeason.ToString().ToLower();
            int chunkSize = MaskLayer.CHUNK_PIXEL_SIZE;
            Random rand = new Random(state.WorldSeed + state.Calendar.Year * 100 + state.Calendar.Day);

            foreach (var kvp in state.TerrainMaskChunks)
            {
                Point chunkCoord = kvp.Key;
                var maskTex = kvp.Value;
                Color[] maskPixels = new Color[chunkSize * chunkSize];
                maskTex.GetData(maskPixels);

                float chunkWorldX = chunkCoord.X * chunkSize;
                float chunkWorldY = chunkCoord.Y * chunkSize;

                // Scan in 16px tile increments
                for (int py = 0; py < chunkSize; py += 16)
                {
                    for (int px = 0; px < chunkSize; px += 16)
                    {
                        Color maskCol = maskPixels[py * chunkSize + px];
                        int greenByte = maskCol.G;

                        if (greenByte > 0 && ForagesByMaskValue.TryGetValue(greenByte, out var eligiblePlants))
                        {
                            Vector2 worldPos = new Vector2(chunkWorldX + px + 8f, chunkWorldY + py + 16f);

                            // Check if an instance already exists at this tile
                            bool alreadyExists = state.ActiveSave.ActivePlants.Any(p => Vector2.Distance(p.Position, worldPos) < 12f);
                            if (alreadyExists) continue;

                            // Filter by current season
                            var seasonMatches = eligiblePlants.Where(p => p.AllowedSeasons.Contains(curSeason)).ToList();
                            if (seasonMatches.Count == 0) continue;

                            // Spawn chance (e.g. 35% density per eligible tile)
                            if (rand.NextDouble() < 0.35)
                            {
                                var chosenPlant = seasonMatches[rand.Next(seasonMatches.Count)];
                                int startingStage = chosenPlant.Stages.FindIndex(s => s.IsHarvestable);
                                if (startingStage < 0) startingStage = chosenPlant.Stages.Count - 1;

                                var instance = new PlantInstance
                                {
                                    PlantID = chosenPlant.ID,
                                    Position = worldPos,
                                    CurrentStageIndex = startingStage, // Spawns ripe in season
                                    DaysInStage = 0,
                                    IsWildForage = true,
                                    SoilCell = GridHelper.GetTileCell(worldPos)
                                };

                                state.ActiveSave.ActivePlants.Add(instance);
                            }
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[PlantManager] Active world plants: {state.ActiveSave.ActivePlants.Count}");
        }
    }
}