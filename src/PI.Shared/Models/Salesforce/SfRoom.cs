using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace PI.Shared.Salesforce.Models;


public class SfRoom : SfObject
{
    [BsonElement("Name__c")] private string name1;
    [BsonElement("Name")] private string name2;
    public string Name => name1 ?? name2;
    
    [BsonElement("FloorPlan__c")] public string FloorPlanId { get; set; }

    [BsonElement("NumberOfAppliances__c")] private decimal? appliancesToMove;
    [BsonElement("Heavy_Items__c")] private decimal? heavyItems;
    [BsonElement("OtherItemsToMove__c")] private decimal? other;

    [BsonElement("FurnitureToMove__c")] public string FurnitureToMoveId { get; set; }
     public int? NumberOfAppliances => GetPositiveIntOrNull(appliancesToMove);
    public int? NumberOfHeavyItems => GetPositiveIntOrNull(heavyItems);
    public int? NumberOfOtherItemsToMove => GetPositiveIntOrNull(other);

    private int? GetPositiveIntOrNull(decimal? value)
    {
        return value.HasValue && value > 0 ? (int)value : null;
    }

    // FloorNumber__c
    // TrimWorkToDo__c
    // PerimeterTakeout__c
    // Removing__c ???
    // Takeouts__c
    // AreaTakeout__c
    // Openings__c

    [BsonElement("StairsCount__c")] public decimal? StairsCount { get; set; }
    [BsonElement("StairsWidth__c")] public decimal? StairsWidth { get; set; }
    [BsonElement("StairsRiser__c")] public decimal? StairsRiser { get; set; }
    [BsonElement("StairsType__c")] public string StairsType { get; set; }
    [BsonElement("StairsThread__c")] public decimal? StairsThread { get; set; }

    [BsonElement("Perimeter__c")] public decimal? Perimeter { get; set; }
    [BsonElement("PerimeterTakeout__c")] public decimal? PerimeterTakeout { get; set; }
    [BsonElement("Area__c")] public decimal? Area { get; set; }
    [BsonElement("Takeouts__c")] public decimal? Takeouts { get; set; }
    [BsonElement("AreaTakeout__c")] public decimal? AreaTakeout { get; set; }
    [BsonElement("Openings__c")] public decimal? Openings { get; set; }

    [BsonElement("RoomType__c")] public string RoomTypeId { get; set; }

    [BsonElement("Sub_Floor_Type__c")] public string SubFloorType { get; set; }
    [BsonElement("Room_Draw__c")] public string RoomDraw { get; set; }

    [BsonElement("Notes__c")] public string Notes { get; set; }

    // [BsonElement("RoomImage__c")] public string RoomImage { get; set; }
    [BsonElement("RoomImageURL__c")] public string RoomImageUrl { get; set; }
}

public class SfRoomDrawInfo
{
    // new 
    [JsonProperty("mRoomCanvasWalls")] public CanvasWall[] RoomCanvasWalls { get; set; }

    [JsonProperty("carpetForStairs")] public decimal? carpetForStairs;
    [JsonProperty("numberOfStairs")] public int? numberOfStairs;
    [JsonProperty("stairsRiserFt")] public int? stairsRiserFt;
    [JsonProperty("stairsRiserInch")] public int? stairsRiserInch;
    [JsonProperty("stairsTreadFt")] public int? stairsThreadFt;
    [JsonProperty("stairsTreadInch")] public int? stairsThreadInch;
    [JsonProperty("stairsType")] public string stairsType;
    [JsonProperty("stairsTypeId")] public string stairsTypeId;
    [JsonProperty("stairsWidthFt")] public int? stairsWidthFt;
    [JsonProperty("stairsWidthInch")] public int? stairsWidthInch;

    // old
    [JsonProperty("mCarpetForStairs")] public decimal? carpetForStairsOld;
    [JsonProperty("mNumberOfStairs")] public int? numberOfStairsOld;
    [JsonProperty("mStairsRiserFt")] public int? stairsRiserFtOld;
    [JsonProperty("mStairsRiserInch")] public int? stairsRiserInchOld;
    [JsonProperty("mStairsTreadFt")] public int? stairsThreadFtOld;
    [JsonProperty("mStairsTreadInch")] public int? stairsThreadInchOld;
    [JsonProperty("mStairsType")] public string stairsTypeOld;
    [JsonProperty("mStairsTypeId")] public string stairsTypeIdOld;
    [JsonProperty("mStairsWidthFt")] public int? stairsWidthFtOld;
    [JsonProperty("mStairsWidthInch")] public int? stairsWidthInchOld;

    public decimal? CarpetForStairs => carpetForStairs ?? carpetForStairsOld;
    public int? NumberOfStairs => numberOfStairs ?? numberOfStairsOld;
    public int? StairsRiserFt => stairsRiserFt ?? stairsRiserFtOld;
    public int? StairsRiserInch => stairsRiserInch ?? stairsRiserInchOld;
    public int? StairsThreadFt => stairsThreadFt ?? stairsThreadFtOld;
    public int? StairsThreadInch => stairsThreadInch ?? stairsThreadInchOld;
    public string StairsType => stairsType ?? stairsTypeOld;
    public string StairsTypeId => stairsTypeId ?? stairsTypeIdOld;
    public int? StairsWidthFt => stairsWidthFt ?? stairsWidthFtOld;
    public int? StairsWidthInch => stairsWidthInch ?? stairsWidthInchOld;

    [JsonProperty("stairCase")] public StairCase StairCase { get; set; }

    [JsonProperty("mSpaceInformationData")]
    public SpaceInformation SpaceInformation { get; set; }

    public CanvasDoor[] Doors => RoomCanvasWalls?
        .Where(x => x.Doors?.Length > 0)
        .SelectMany(x => x.Doors)
        .OrderBy(x => x.Name)
        .ToArray();
}

public class SpaceInformation
{
    [JsonProperty("mRemovingDescription")] public string RemovingDescription { get; set; }
    [JsonProperty("mSubFloorTypeDescription")] public string SubfloorDescription { get; set; }
}

public class CanvasWall
{
    [JsonProperty("mCanvasDoors")] public CanvasDoor[] Doors { get; set; }
}

public class Pivot
{
    [JsonProperty("isLeftCorner")] public bool IsLeftCorner { get; set; }
    [JsonProperty("mDistanceFt")] public int DistanceFt { get; set; }
    [JsonProperty("mDistanceIn")] public int DistanceIn { get; set; }

    public string Distance => DistanceFt > 0 ? (DistanceIn > 0 ? $"{DistanceFt}' {DistanceIn}\"" : $"{DistanceFt}'") : (DistanceIn > 0 ? $"{DistanceIn}\"" : $"0' 0\"");
    public string DistanceOrigin => IsLeftCorner ? "from Left" : "from Right";
}

public class CanvasDoor
{
    [JsonProperty("mHintName")] public string Name { get; set; }
    [JsonProperty("mDoorWidthFt")] public int WidthFt { get; set; }
    [JsonProperty("mDoorWidthInch")] public int WidthIn { get; set; }
    [JsonProperty("isOpenWall")] public bool IsOpenWall { get; set; }
    [JsonProperty("isTransition")] public bool IsTransition { get; set; }
    [JsonProperty("isTrimDoor")] public bool IsTrimDoor { get; set; }

    [JsonProperty("mPivot")] public Pivot Pivot { get; set; }

    public string Width => WidthFt > 0 ? (WidthIn > 0 ? $"{WidthFt}' {WidthIn}\"" : $"{WidthFt}'") : (WidthIn > 0 ? $"{WidthIn}\"" : null);

    public string Tags => IsTransition || IsOpenWall || IsTrimDoor ? $"({string.Join(", ", GetTags())})" : null;

    public string DistanceFromCorner => Pivot == null ? null : $"{Pivot.Distance} {Pivot.DistanceOrigin}";

    private IEnumerable<string> GetTags()
    {
        if (IsOpenWall) yield return "Open Wall";
        if (IsTransition) yield return "Transition";
        if (IsTrimDoor) yield return "Trim Door";
    }
}

public enum UOM
{
    In,
    Ft
}

public class Measurement
{
    public UOM Unit { get; set; }
    public decimal Value { get; set; }

    public string ToFtAndInches()
    {
        var inches = (int)(Unit == UOM.In ? Value : Value * 12);
        var ft = inches / 12;
        inches -= ft * 12;
        return ft > 0 ? (inches > 0 ? $"{ft}' {inches}\"" : $"{ft}'") : (inches > 0 ? $"{inches}\"" : string.Empty);
    }
}

public class StairFlight
{
    [JsonProperty("addStarterTread")] public bool AddStarterTread { get; set; }
    [JsonProperty("currentFlooring")] public string CurrentFlooringId { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("notes")] public string Notes { get; set; }
    [JsonProperty("stairType")] public string StairType { get; set; }
    [JsonProperty("stairSubType")] public string StairSubType { get; set; }
    [JsonProperty("numberOfStairs")] public int NumberOfStairs { get; set; }

    public string CurrentFlooring => CurrentFlooringId switch
    {
        "Solid" => "Solid Treads",
        "Vinyl_Laminate_Engineered" => "Vinyl / Laminate / Engineered",
        "Carpet" => "Carpet",
        "Bare_Floor" => "Bare Floor",
        "Tile" => "Tile",
        _ => CurrentFlooringId
    };

    public Measurement Riser { get; set; }
    public Measurement Tread { get; set; }
    public Measurement Width { get; set; }
    public Measurement BottomWidth { get; set; }

    public string WidthString => Width == null ? string.Empty :
        BottomWidth == null || Width.ToFtAndInches() == BottomWidth.ToFtAndInches() ? Width?.ToFtAndInches() : $"{Width.ToFtAndInches()} (Bottom: {BottomWidth.ToFtAndInches()})";
}

public class StairCase
{
    public bool ConvertLandingsToRooms { get; set; }

    [JsonProperty("flights")] private StairFlight[] flights;

    public StairFlight[] Flights => flights?.OrderBy(x => x.Name).ToArray();
}