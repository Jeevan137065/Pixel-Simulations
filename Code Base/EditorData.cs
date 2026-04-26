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
    public enum PropertyType { String, Integer, Float, Boolean }
    public enum ObjectType { Prop, Rectangle, Shape, Point }
    public enum SliceMode { RowFirst, ColumnFirst }
    public enum ShapeOperation { None, Union, Intersection, Difference }
    public enum HandleType { None, Body, TopLeft, TopRight, BottomLeft, BottomRight, Top, Bottom, Left, Right, Center }
    [JsonObject(MemberSerialization.OptIn)]
    public class MapProperty
    {
        [JsonProperty] public PropertyType Type { get; set; }
        [JsonProperty] public string Value { get; set; }

        public MapProperty() { }
        public MapProperty(PropertyType type, string value)
        {
            Type = type;
            Value = value;
        }
    }
    [JsonObject(MemberSerialization.OptIn)] // Ensure this is present
    public abstract class MapObject
    {
        [JsonProperty] public string ID { get; set; } = System.Guid.NewGuid().ToString().Substring(0, 8);
        [JsonProperty] public string Name { get; set; }
        [JsonProperty] public Vector2 Position { get; set; }
        [JsonProperty] public abstract ObjectType Type { get; }
        [JsonProperty] public HashSet<int> Tags { get; set; } = new HashSet<int>();
        [JsonProperty] public List<string> LinkedObjects { get; set; } = new List<string>();
        [JsonProperty] public Dictionary<string, MapProperty> Properties { get; set; } = new Dictionary<string, MapProperty>();
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ObjectPrefab
    {
        [JsonProperty] public string ID { get; set; }
        [JsonProperty] public string AtlasName { get; set; }
        [JsonProperty] public Rectangle SourceRect { get; set; }
        [JsonProperty] public Vector2 Pivot { get; set; }
        [JsonProperty] public List<int> Tags { get; set; } = new List<int>();
        [JsonProperty] public Dictionary<string, MapProperty> Properties { get; set; } = new Dictionary<string, MapProperty>();
        [JsonProperty] public Dictionary<string, Rectangle> AlternateStates { get; set; } = new Dictionary<string, Rectangle>();
        [JsonProperty] public Point SizeInCells => new Point(SourceRect.Width / 16, SourceRect.Height / 16);
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class ShapeObject : MapObject
    {
        public override ObjectType Type => ObjectType.Shape; // Add "Shape" to the enum
        [JsonProperty] public Polygon Shape { get; set; }
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
    [JsonObject(MemberSerialization.OptIn)]
    public class LightBeamObject : MapObject
    {
        public override ObjectType Type => ObjectType.Shape; // We treat it as a shape for broad compatibility

        [JsonProperty] public Polygon SourceShape { get; set; } // The "Window" or "Bulb"
        [JsonProperty] public Polygon TargetShape { get; set; } // The ground area it illuminates

        [JsonProperty] public Color DebugColor { get; set; }

        // Extrusion mapping (Index of Source -> Index of Target)
        // E.g., [0] = 0 means Source Vertex 0 connects to Target Vertex 0
        [JsonProperty] public List<int> VertexMap { get; set; } = new List<int>();

        public LightBeamObject()
        {
            SourceShape = new Polygon();
            TargetShape = new Polygon();
        }

        public void UpdateBounds()
        {
            if (SourceShape.Vertices.Count == 0 || TargetShape.Vertices.Count == 0) return;

            // The bounding box must encapsulate BOTH shapes!
            var b1 = SourceShape.GetBounds();
            var b2 = TargetShape.GetBounds();

            float minX = System.Math.Min(b1.Left, b2.Left);
            float minY = System.Math.Min(b1.Top, b2.Top);
            float maxX = System.Math.Max(b1.Right, b2.Right);
            float maxY = System.Math.Max(b1.Bottom, b2.Bottom);

            Position = new Vector2(minX, minY);
            Size = new Vector2(maxX - minX, maxY - minY);
        }

        [JsonProperty] public Vector2 Size { get; set; }
    }
    [JsonObject(MemberSerialization.OptIn)]
    public class PlacedItemObject : MapObject
    {
        [JsonProperty] public override ObjectType Type => ObjectType.Prop; // Pretend it's a prop for the base engine

        [JsonProperty] public int ItemID { get; set; }
        [JsonProperty] public int Amount { get; set; } = 1; // Used for Piles
        [JsonProperty] public int DaysAlive { get; set; } = 0; // Used for Crops
        [JsonProperty] public int CurrentStageIndex { get; set; } = 0;
    }
}
