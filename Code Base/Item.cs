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

    // Represents a stack of items in an inventory slot
    public class ItemStack
    {
        public int ItemID { get; set; }
        public int Count { get; set; }
        public float TotalWeight { get; set; }
    }


}
