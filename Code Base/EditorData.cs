using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Newtonsoft.Json;
using Pixel_Simulations.Editor;
using Pixel_Simulations.UI;
using System;
using System.Collections.Generic;

namespace Pixel_Simulations.Data
{

    public enum ObjectType { Prop, Rectangle, Shape, Point }
    public enum SliceMode { RowFirst, ColumnFirst }
    public enum ShapeOperation { None, Union, Intersection, Difference }
    public enum HandleType { None, Body, TopLeft, TopRight, BottomLeft, BottomRight, Top, Bottom, Left, Right, Center }
    [JsonObject(MemberSerialization.OptIn)] // Ensure this is present
    public abstract class MapObject
    {
        [JsonProperty] public string ID { get; set; } = System.Guid.NewGuid().ToString().Substring(0, 8);
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public Vector2 Position { get; set; }
        [JsonProperty] public abstract ObjectType Type { get; }
        [JsonProperty] public HashSet<string> Tags { get; set; } = new HashSet<string>();
        [JsonProperty] public List<string> LinkedObjects { get; set; } = new List<string>();
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ObjectPrefab
    {
        [JsonProperty] public string ID { get; set; }
        [JsonProperty] public string AtlasName { get; set; }
        [JsonProperty] public Rectangle SourceRect { get; set; }
        [JsonProperty] public Vector2 Pivot { get; set; }
        [JsonProperty] public List<string> Tags { get; set; } = new List<string>();
        // Helper: Get size in 16px cells
        [JsonProperty] public Point SizeInCells => new Point(SourceRect.Width / 16, SourceRect.Height / 16);
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ShapeObject : MapObject
    {
        public override ObjectType Type => ObjectType.Shape; // Add "Shape" to the enum
        [JsonProperty] public Polygon Shape { get; set; }
        [JsonProperty] public string Tag { get; set; }
        [JsonProperty] public Vector2 Size { get; set; }
        [JsonProperty] public Color DebugColor { get; set; }

        public ShapeObject()
        {
            Shape = new Polygon();
        }

        public void UpdateBoundsFromVertices()
        {
            if (Shape.Vertices.Count == 0) return;
            var rect = Shape.GetBounds();
            this.Position = new Vector2(rect.X, rect.Y);
            this.Size = new Vector2(rect.Width, rect.Height);
        }

    }
    [JsonObject(MemberSerialization.OptIn)]
    public class PropObject : MapObject
    {
        [JsonProperty] public override ObjectType Type => ObjectType.Prop;
        [JsonProperty] public string PrefabID { get; set; }
        [JsonProperty] public Vector2 Scale { get; set; } = Vector2.One;
        [JsonProperty] public float Rotation { get; set; } = 0f;
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class RectangleObject : MapObject
    {
        [JsonProperty] public override ObjectType Type => ObjectType.Rectangle;
        [JsonProperty] public Vector2 Size { get; set; }
        [JsonProperty] public string TriggerType { get; set; } // e.g., "Collision", "Trigger"
        [JsonProperty] public Color DebugColor { get; set; }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class PointObject : MapObject
    {
        [JsonProperty] public override ObjectType Type => ObjectType.Point;
        [JsonProperty] public float Radius { get; set; }
        [JsonProperty] public Vector2 Center { get; set; }
        [JsonProperty] public string Label { get; set; } // For identifying the trigger
        [JsonProperty] public Color DebugColor { get; set; }
    }
    //[JsonObject(MemberSerialization.OptOut)]

}
