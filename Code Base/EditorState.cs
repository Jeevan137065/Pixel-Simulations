using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Pixel_Simulations
{
    public class EditorState
    {
        public Map ActiveMap { get;  set; }
        public int ActiveLayerIndex { get; set; }

        [JsonConverter(typeof(LayerConverter))]
        public List<Layer> Layers { get;  set; }

        public EditorState(int mapWidth, int mapHeight)
        {
            ActiveMap = new Map(mapWidth, mapHeight);
            ActiveLayerIndex = 0; // Default to the first layer
        }

        public TileLayer GetActiveTileLayer()
        {
            if (ActiveLayerIndex >= 0 && ActiveLayerIndex < ActiveMap.Layers.Count)
            {
                return ActiveMap.Layers[ActiveLayerIndex] as TileLayer;
            }
            return null;
        }

        // All map manipulation is now handled here, ensuring everyone is notified.
        public void AddNewLayerUp()
        {
            ActiveMap.AddLayerAbove(ActiveLayerIndex);
        }

        public void AddNewLayerDown()
        {
            ActiveMap.AddLayerBelow(ActiveLayerIndex);
        }

        public void DeleteActiveLayer()
        {
            ActiveMap.DeleteLayer(ActiveLayerIndex);
            // Clamp active index to a valid range after deletion
            ActiveLayerIndex = MathHelper.Clamp(ActiveLayerIndex, 0, ActiveMap.Layers.Count - 1);
        }

        public void MoveActiveLayerUp()
        {
            ActiveMap.MoveLayerUp(ActiveLayerIndex);
            if (ActiveLayerIndex > 0) ActiveLayerIndex--;
        }

        public void MoveActiveLayerDown()
        {
            ActiveMap.MoveLayerDown(ActiveLayerIndex);
            if (ActiveLayerIndex < ActiveMap.Layers.Count - 1) ActiveLayerIndex++;
        }

        public void SaveToFile(string filePath)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented, // Makes the JSON file readable
                                                  // This tells JSON.NET how to handle polymorphism (abstract types)
                TypeNameHandling = TypeNameHandling.Auto
            };

            // We might need to be more explicit with our converter for specific types if Auto doesn't work well.
            // settings.Converters.Add(new LayerConverter()); 

            string json = JsonConvert.SerializeObject(this, settings);
            System.IO.File.WriteAllText(filePath, json);
        }

        public void LoadFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Handle error or just start with a new map
                return;
            }

            string json = System.IO.File.ReadAllText(filePath);
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto, // Crucial for polymorphism
                                                          // If Auto doesn't work, we'll need our custom converter here too.
                                                          // settings.Converters.Add(new LayerConverter());
            };

            // Deserialize the JSON back into our editor state.
            // We need to be careful here, as this replaces the entire current state.
            var loadedState = JsonConvert.DeserializeObject<EditorState>(json, settings);

            if (loadedState != null)
            {
                this.ActiveMap = loadedState.ActiveMap;
                this.ActiveLayerIndex = loadedState.ActiveLayerIndex;
                // The TileSetManager will need to be re-populated/re-linked after loading.
            }
        }
    }
}
