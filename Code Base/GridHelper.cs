using Microsoft.Xna.Framework;
using System;

namespace Pixel_Simulations
{
    public static class GridHelper
    {
        public const int TILE_SIZE = 16;        // Map Tiles (Albedo Layer)
        public const int SUB_CELL_SIZE = 8;     // Props/Items (Dynamic Layer)

        /// <summary> Gets the 16x16 chunk/tile coordinate </summary>
        public static Point GetTileCell(Vector2 worldPos) =>
            new Point((int)Math.Floor(worldPos.X / TILE_SIZE), (int)Math.Floor(worldPos.Y / TILE_SIZE));
        public static Vector2 GetPlacementPosition(Vector2 lookPos, bool usePrecision)
        {
            int gridSize = usePrecision ? SUB_CELL_SIZE : TILE_SIZE;

            float snappedX = (float)Math.Floor(lookPos.X / gridSize) * gridSize;
            float snappedY = (float)Math.Floor(lookPos.Y / gridSize) * gridSize;

            // Return the exact Bottom-Center of the snapped cell
            return new Vector2(snappedX + (gridSize / 2f), snappedY + gridSize);
        }
        /// <summary> Snaps a world position to the nearest 8x8 Sub-Cell, accounting for the object's Pivot/Origin. </summary>
        public static Vector2 SnapToSubCell(Vector2 worldPos, Vector2 pivot)
        {
            float snappedX = (float)Math.Floor(worldPos.X / SUB_CELL_SIZE) * SUB_CELL_SIZE;
            float snappedY = (float)Math.Floor(worldPos.Y / SUB_CELL_SIZE) * SUB_CELL_SIZE;

            // Add the pivot so the SpriteBatch draws the object perfectly inside the cell bounds
            return new Vector2(snappedX, snappedY) + pivot;
        }

        /// <summary> Snaps a basic world coordinate to the 8x8 grid (Used for Triggers/Collision) </summary>
        public static Vector2 SnapCoordinate(Vector2 pos)
        {
            return new Vector2(
                (float)Math.Floor(pos.X / SUB_CELL_SIZE) * SUB_CELL_SIZE,
                (float)Math.Floor(pos.Y / SUB_CELL_SIZE) * SUB_CELL_SIZE
            );
        }
    }
}