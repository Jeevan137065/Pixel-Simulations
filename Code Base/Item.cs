using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

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

        [JsonProperty] public string IconSource { get; set; } // Atlas Name
        [JsonProperty] public Point Coord { get; set; } // Multiplied by 32 in renderer
    }
    public enum PlacementType { Prop, Crop, Pile, Machine }

    [JsonObject(MemberSerialization.OptIn)]
    public class PhysicalStage
    {
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public int SpriteX { get; set; }
        [JsonProperty] public int SpriteY { get; set; }

        // Thresholds
        public int ThresholdInt1 { get; set; } // Pile: Min Count | Crop: Growth Days
        public int ThresholdInt2 { get; set; } // Crop: Harvest Item ID | Standard: Item ID
        public bool ThresholdBool { get; set; } // Crop: Is Ripe | Standard: Is Occupied
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
        [JsonProperty] public int ItemID { get; set; }
        [JsonProperty] public string AtlasName { get; set; }
        [JsonProperty] public bool CanBeDropped { get; set; }
        [JsonProperty] public bool IsPlaceable { get; set; }
        [JsonProperty] public bool HasCollision { get; set; }

        [JsonProperty] public PlacementType Type { get; set; }

        [JsonProperty] public int CellWidth { get; set; }
        [JsonProperty] public int CellHeight { get; set; }
        [JsonProperty] public Rectangle Bounds { get; set; }

        [JsonProperty] public List<PhysicalStage> Stages { get; set; } = new List<PhysicalStage>();
    }
    // Represents a stack of items in an inventory slot
    public class ItemStack
    {
        public int ItemID { get; set; }
        public int Count { get; set; }
        public float TotalWeight { get; set; }
    }


}
