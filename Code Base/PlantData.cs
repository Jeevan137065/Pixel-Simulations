using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixel_Simulations.Data
{
    public enum BotanicalClass { Flora, Fungal, Aquatic, DomesticCrop }
    public enum HarvestDynamic { DestroyOnHarvest, RegrowToStage, PersistentForage }

    [JsonObject(MemberSerialization.OptIn)]
    public class PlantDrop
    {
        [JsonProperty("ItemID")] public int ItemID { get; set; }
        [JsonProperty("MinCount")] public int MinCount { get; set; } = 1;
        [JsonProperty("MaxCount")] public int MaxCount { get; set; } = 1;
        [JsonProperty("Chance")] public float Chance { get; set; } = 1.0f;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PlantStage
    {
        [JsonProperty("StageIndex")] public int StageIndex { get; set; }
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("SpriteX")] public int SpriteX { get; set; }
        [JsonProperty("SpriteY")] public int SpriteY { get; set; }
        [JsonProperty("DaysToGrow")] public int DaysToGrow { get; set; } = 2;
        [JsonProperty("NextStageIndex")] public int NextStageIndex { get; set; } = -1;
        [JsonProperty("IsHarvestable")] public bool IsHarvestable { get; set; } = false;
        [JsonProperty("IsMature")] public bool IsMature { get; set; } = false;
        [JsonProperty("HarvestRevertIndex")] public int HarvestRevertIndex { get; set; } = -1;
        [JsonProperty("RottenStageIndex")] public int RottenStageIndex { get; set; } = -1;
        [JsonProperty("Drops")] public List<PlantDrop> Drops { get; set; } = new List<PlantDrop>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PlantDefinition
    {
        [JsonProperty("ID")] public string ID { get; set; }
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("Category")] public BotanicalClass Category { get; set; }
        [JsonProperty("MaskSpawnValue")] public int MaskSpawnValue { get; set; } = 0; // Green channel value
        [JsonProperty("AllowedSeasons")] public List<string> AllowedSeasons { get; set; } = new List<string>();
        [JsonProperty("SeasonOverlap")] public bool SeasonOverlap { get; set; } = false;
        [JsonProperty("AtlasName")] public string AtlasName { get; set; } = "CropA";
        [JsonProperty("SeedItemID")] public int SeedItemID { get; set; } = 0; // Direct link to Item ID in items.json
        [JsonProperty("CellWidth")] public int CellWidth { get; set; } = 1;
        [JsonProperty("CellHeight")] public int CellHeight { get; set; } = 1;
        [JsonProperty("HarvestBehavior")] public HarvestDynamic HarvestBehavior { get; set; } = HarvestDynamic.DestroyOnHarvest;
        [JsonProperty("Stages")] public List<PlantStage> Stages { get; set; } = new List<PlantStage>();
    }

    // ==============================================================
    // RUNTIME INSTANCE (Saved in MapSaveData)
    // ==============================================================
    [JsonObject(MemberSerialization.OptIn)]
    public class PlantInstance
    {
        [JsonProperty] public string InstanceID { get; set; } = System.Guid.NewGuid().ToString().Substring(0, 8);
        [JsonProperty] public string PlantID { get; set; }
        [JsonProperty] public Vector2 Position { get; set; }
        [JsonProperty] public int CurrentStageIndex { get; set; } = 0;
        [JsonProperty] public int DaysInStage { get; set; } = 0;
        [JsonProperty] public bool IsWildForage { get; set; } = false;
        [JsonProperty] public bool IsWateredToday { get; set; } = false;
        [JsonProperty] public Point SoilCell { get; set; }
    }
}
