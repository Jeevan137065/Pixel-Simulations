using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Pixel_Simulations.Data
{
    // ==========================================================
    // TOP-LEVEL BLUEPRINT FILE (Exported by BuildingArchitectStudio)
    // ==========================================================
    public class BuildingBlueprintFile
    {
        public string Version { get; set; }
        public string Timestamp { get; set; }
        public List<BuildingData> Buildings { get; set; } = new List<BuildingData>();
        public RoofCatalogData RoofCatalog { get; set; }
        public ModularCatalogData ModularCatalog { get; set; }
    }

    public class BuildingData
    {
        public string id { get; set; }
        public string name { get; set; }
        public int gridDimension { get; set; } = 32;
        public int activeFloorIndex { get; set; } = 0;
        public List<FloorData> floors { get; set; } = new List<FloorData>();
    }

    public class FloorData
    {
        public string id { get; set; }
        public string name { get; set; }
        public int level { get; set; } = 0;
        public List<List<RoomCellData>> grid { get; set; } // grid[x][y]
        public List<WallComponentData> edgeStrips { get; set; } = new List<WallComponentData>();
        public List<AuxComponentData> auxiliaries { get; set; } = new List<AuxComponentData>();
        public List<FloorPlateData> floorPlates { get; set; } = new List<FloorPlateData>();
        public List<RoofComponentData> roofs { get; set; } = new List<RoofComponentData>();
    }

    public class RoomCellData
    {
        public string roomType { get; set; } = "Void";
        public float stepElevation { get; set; } = 0f;
        public string shape { get; set; } = "full";
        public string floorPieceId { get; set; }
    }

    public class WallComponentData
    {
        public string id { get; set; }
        public string defId { get; set; }
        public string familyId { get; set; }
        public string variantId { get; set; } = "default";
        public string type { get; set; } = "wall"; // "wall", "slope", "door", "window"
        public string name { get; set; }
        public string atlas { get; set; } = "walls";
        public RectangleData rect { get; set; } // Atlas source quad
        public float x { get; set; }
        public float y { get; set; }
        public float x2 { get; set; }
        public float y2 { get; set; }
        public float widthUnits { get; set; } = 2f;
        public float heightUnits { get; set; } = 6f; // Standard story = 6u = 96px
        public bool isSlope { get; set; }
        public float slopeRatio { get; set; } = 1.0f;
        public string slopeDir { get; set; } = "up_right"; // "up_right" or "up_left"
        public bool isMirrored { get; set; }
        public int depthRank { get; set; } = 0; // 0 = Exterior Shell, 1+ = Interior Partition
        public float grimeIntensity { get; set; } = 0f;
        public Dictionary<string, string> PaletteMap { get; set; }
    }

    public class AuxComponentData
    {
        public string id { get; set; }
        public string defId { get; set; }
        public string familyId { get; set; }
        public string variantId { get; set; } = "default";
        public string type { get; set; } // "skirting", "wainscot", "pillar", "corner"
        public string name { get; set; }
        public string atlas { get; set; } = "walls";
        public RectangleData rect { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float x2 { get; set; }
        public float y2 { get; set; }
        public float widthUnits { get; set; } = 2f;
        public float heightUnits { get; set; } = 1f;
        public int depthRank { get; set; } = 1;
        public Dictionary<string, string> PaletteMap { get; set; }
    }

    public class FloorPlateData
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool isFloorPlate { get; set; } = true;
        public float x { get; set; }
        public float y { get; set; }
        public float w { get; set; } = 2f;
        public float h { get; set; } = 2f;
        public string familyId { get; set; }
        public string variantId { get; set; }
        public string pieceId { get; set; }
        public int depthRank { get; set; } = 1;
        public Dictionary<string, string> PaletteMap { get; set; }
    }

    public class RoofComponentData
    {
        public string id { get; set; }
        public string name { get; set; }
        public string roofType { get; set; } // "A" (Rect Pitch) or "B" (Polygon / South Slope)
        public string family { get; set; }
        public int variant { get; set; } = 0;
        public int roofY { get; set; } = 0; // Elevation in 16px units above story base
        public string tiltEdge { get; set; } // "W", "E", "S", "flat"
        public string slopeRatio { get; set; } = "1:1"; // "1:1", "1:2", "2:1", "flat", etc.

        // Roof A Fields
        public float x { get; set; }
        public float y { get; set; }
        public float w { get; set; }
        public float h { get; set; }
        public string fillPattern { get; set; } = "staggered"; // "uniform", "aligned", "staggered"

        // Roof B Fields
        public string flatPieceId { get; set; } = "flat_surface_1";
        public List<Vector2Data> points { get; set; } = new List<Vector2Data>();

        public Dictionary<string, string> PaletteMap { get; set; }
    }

    public class RoofCatalogData
    {
        public List<RoofFamilyData> families { get; set; } = new List<RoofFamilyData>();
    }

    public class RoofFamilyData
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<VariantData> variants { get; set; } = new List<VariantData>();
        public List<FlatPieceData> flatPieces { get; set; } = new List<FlatPieceData>();
        public Dictionary<string, AngleConfigData> angleConfig { get; set; }
        public Dictionary<string, RectangleData> pieces { get; set; } = new Dictionary<string, RectangleData>();
    }

    public class FlatPieceData
    {
        public string id { get; set; }
        public string name { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; } = 16;
        public int h { get; set; } = 16;
        public bool isBound { get; set; }
    }

    public class AngleConfigData
    {
        public bool hasB { get; set; } = false;
    }

    public class VariantData
    {
        public string id { get; set; }
        public string name { get; set; }
        public Dictionary<string, string> PaletteMap { get; set; }
        public Dictionary<string, string> map { get; set; }
    }

    public class ModularCatalogData
    {
        public Dictionary<string, List<ModularFamilyData>> families { get; set; }
    }

    public class ModularFamilyData
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<VariantData> variants { get; set; }
        public List<CatalogItemData> items { get; set; }
    }

    public class CatalogItemData
    {
        public string id { get; set; }
        public string name { get; set; }
        public float widthUnits { get; set; }
        public float heightUnits { get; set; }
        public bool isSlope { get; set; }
        public float slopeRatio { get; set; }
        public string slopeDir { get; set; }
        public string atlas { get; set; }
        public RectangleData rect { get; set; }
        public bool isBound { get; set; }
    }

    public class RectangleData
    {
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }

        public Rectangle ToRectangle() => new Rectangle(x, y, w, h);
    }

    public class Vector2Data
    {
        public float x { get; set; }
        public float y { get; set; }

        public Vector2 ToVector2() => new Vector2(x, y);
    }

    // ==========================================================
    // RUNTIME BUILDING INSTANCE (Placed in a Map Scene)
    // ==========================================================
    [JsonObject(MemberSerialization.OptIn)]
    public class ModularBuildingInstance : MapObject
    {
        [JsonProperty] public override ObjectType Type => ObjectType.ModularBuilding;

        [JsonProperty] public string BuildingId { get; set; }
        [JsonProperty] public int ActiveStory { get; set; } = 0;
        [JsonProperty] public bool ShowRoofInEditor { get; set; } = true;

        // Blueprint reference loaded from BuildingManager
        [JsonProperty] public BuildingData BlueprintRef => BuildingManager.Instance?.GetBuilding(BuildingId);

        // Runtime/Editor Cache
        [JsonProperty] public RenderTarget2D CachedProxyTexture { get; set; }
        [JsonProperty] public float RoofAlpha { get; set; } = 1.0f;
        [JsonProperty] public bool IsPlayerInside { get; set; } = false;

        public ModularBuildingInstance()
        {
            Name = "Modular_Building";
        }
    }
}