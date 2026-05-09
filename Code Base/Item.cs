using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pixel_Simulations
{
    // Defines the core properties of an item type

    [JsonObject(MemberSerialization.OptIn)]
    public class ItemDefinition
    {
        [JsonProperty] public int ID { get; set; }
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public string Description { get; set; }
        [JsonProperty] public string Category { get; set; }
        [JsonProperty] public List<string> ItemTags { get; set; } = new List<string>();

        [JsonProperty] public float MinWeight { get; set; }
        [JsonProperty] public float MaxWeight { get; set; }
        [JsonProperty("SellValue")] public int SellValue { get; set; } = -1;
        [JsonProperty] public string IconSource { get; set; } // Atlas Name
        [JsonProperty] public Point Coord { get; set; } // Multiplied by 32 in renderer
    }
    public enum PlacementType { Prop, Crop, Pile, Machine }

    [JsonObject(MemberSerialization.OptIn)]
    public class PhysicalStage
    {
        [JsonProperty("Name")] public string Name { get; set; }
        [JsonProperty("SpriteX")] public int SpriteX { get; set; }
        [JsonProperty("SpriteY")] public int SpriteY { get; set; }
        [JsonProperty("ThresholdInt1")] public int ThresholdInt1 { get; set; }
        [JsonProperty("ThresholdInt2")] public int ThresholdInt2 { get; set; }
        [JsonProperty("ThresholdBool")] public bool ThresholdBool { get; set; }
    }
    public class PhysicalItem
    {
        public int ItemID { get; set; }
        public string Type { get; set; } // "Pile", "Crop", "Prop", "Machines"
        public List<PhysicalStage> Stages { get; set; }

        public void ProcessInteraction(int currentStageIndex)
        {
            var stage = Stages[currentStageIndex];

            switch (Type)
            {
                case "Crop":
                    if (stage.ThresholdBool)
                        Console.WriteLine($"Harvesting item {stage.ThresholdInt2}");
                    else
                        Console.WriteLine($"Needs {stage.ThresholdInt1} more days.");
                    break;

                case "Pile":
                    Console.WriteLine($"Minimum quantity required: {stage.ThresholdInt1}");
                    break;

                case "Prop":
                    if (stage.ThresholdBool)
                        Console.WriteLine("Object is currently occupied/in use.");
                    break;
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PhysicalItemDefinition
    {
        // Explicitly map the exact string keys from the JSON
        [JsonProperty("ItemID")] public int ItemID { get; set; }
        [JsonProperty("AtlasName")] public string AtlasName { get; set; }
        [JsonProperty("CanBeDropped")] public bool CanBeDropped { get; set; }
        [JsonProperty("IsPlaceable")] public bool IsPlaceable { get; set; }
        [JsonProperty("HasCollision")] public bool HasCollision { get; set; }

        // This converter tells C# to read "Crop", "Prop", etc., as strings and convert them to the Enum
        [JsonProperty("IsMultiHarvest")] public bool IsMultiHarvest { get; set; } = false;
        [JsonProperty("HarvestRevertStage")] public int HarvestRevertStage { get; set; } = 1;
        [JsonProperty("Type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public PlacementType Type { get; set; }
        [JsonProperty("PrecisionPlacement")] public bool PrecisionPlacement { get; set; } = false;

        [JsonProperty("CellWidth")] public int CellWidth { get; set; }
        [JsonProperty("CellHeight")] public int CellHeight { get; set; }
        [JsonProperty("Bounds")] public Rectangle Bounds { get; set; }

        [JsonProperty("Stages")] public List<PhysicalStage> Stages { get; set; } = new List<PhysicalStage>();
    }
    // Represents a stack of items in an inventory slot
    public class ItemStack
    {
        public int ItemID { get; set; }
        public int Count { get; set; }
        public float TotalWeight { get; set; }
    }


}
