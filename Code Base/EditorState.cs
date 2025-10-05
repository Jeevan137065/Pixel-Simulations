using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class EditorState
    {
        public Map ActiveMap { get; private set; }
        public int ActiveLayerIndex { get; set; }

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
    }
}
