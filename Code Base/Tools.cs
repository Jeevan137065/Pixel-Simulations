using Microsoft.Xna.Framework;
using MonoGame.Extended.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Simulations
{
    public class BrushTool: ITool
    {
        public string Name => "Brush";

        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            if (activeLayer != null && activeBrush.HasValue)
            {
                activeLayer.PlaceTile(cell, activeBrush.Value);
            }
        }

        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            // Same action as mouse down for continuous painting
            OnMouseDown(cell, activeLayer, activeBrush);
        }

        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { }
    }

    public class EraserTool : ITool
    {
        public string Name => "Eraser";

        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            activeLayer?.RemoveTile(cell);
        }

        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            OnMouseDown(cell, activeLayer, activeBrush);
        }

        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { }
    }

    public class FillTool : ITool
    {
        public string Name => "Fill";

        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            if (activeLayer == null || !activeBrush.HasValue) return;

            // Basic flood fill algorithm (can be slow on large areas, but good for a start)
            if (!activeLayer.Grid.TryGetValue(cell, out var targetTile))
            {
                // If the clicked cell is empty, we can't determine the target to fill.
                // A more advanced version might fill any empty cell.
                return;
            }

            if (targetTile.Equals(activeBrush.Value)) return; // Already filled with the brush color

            var pixels = new Queue<Point>();
            pixels.Enqueue(cell);

            while (pixels.Count > 0)
            {
                Point current = pixels.Dequeue();
                if (current.X < 0 || current.X >= 200 || current.Y < 0 || current.Y >= 200) continue; // Bounds check

                if (activeLayer.Grid.TryGetValue(current, out var currentTile) && currentTile.Equals(targetTile))
                {
                    activeLayer.PlaceTile(current, activeBrush.Value);
                    pixels.Enqueue(new Point(current.X + 1, current.Y));
                    pixels.Enqueue(new Point(current.X - 1, current.Y));
                    pixels.Enqueue(new Point(current.X, current.Y + 1));
                    pixels.Enqueue(new Point(current.X, current.Y - 1));
                }
            }
        }

        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { }
        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { }
    }

    public class RectangleTool : ITool
    {
        public string Name => "Rectangle";
        private Point _startCell;
        private bool _isDrawing = false;

        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            _startCell = cell;
            _isDrawing = true;
        }

        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { }

        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush)
        {
            if (!_isDrawing || activeLayer == null || !activeBrush.HasValue)
            {
                _isDrawing = false;
                return;
            }

            int minX = System.Math.Min(_startCell.X, cell.X);
            int maxX = System.Math.Max(_startCell.X, cell.X);
            int minY = System.Math.Min(_startCell.Y, cell.Y);
            int maxY = System.Math.Max(_startCell.Y, cell.Y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    activeLayer.PlaceTile(new Point(x, y), activeBrush.Value);
                }
            }
            _isDrawing = false;
        }
    }

    public class LineTool : ITool
        {
        public string Name => "Line";
        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
    }
    public class SelectionTool : ITool
    {   public string Name => "Line";
        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
    }
    public class CollisionBrushTool : ITool
    {
        public string Name => "Line";
        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
    }
    public class ObjectPlacerTool : ITool
    {
        public string Name => "Line";
        public void OnMouseDown(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseMove(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
        public void OnMouseUp(Point cell, TileLayer activeLayer, TileInfo? activeBrush) { /* Placeholder */ }
    }

    public class ToolManager
    {
        public List<ITool> Tools { get; }
        public ITool ActiveTool { get; private set; }

        public ToolManager()
        {
            Tools = new List<ITool>{
                new BrushTool(),
                new EraserTool(),
                new FillTool(),
                new RectangleTool(),
                new LineTool(),
                new SelectionTool(),
                new CollisionBrushTool(),
                new ObjectPlacerTool()};

            ActiveTool = Tools.FirstOrDefault(); // Default to Brush
        }

        public void SetActiveTool(string toolName)
        {
            var newTool = Tools.FirstOrDefault(t => t.Name == toolName);
            if (newTool != null)
            {
                ActiveTool = newTool;
            }
        }
    }

}
