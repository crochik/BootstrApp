using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;
using PI.Shared.Models.Interfaces;

namespace PI.ProductCatalog.Models;

public class ShapePoint
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal? Z { get; set; }
}

public class RoomSurface
{
    public string Name { get; set; }
    public decimal Area { get; set; }
    public decimal Perimeter { get; set; }
    public ShapePoint[] Shape { get; set; }
}

public enum EstimateInput
{
    Arbitrary,

    // rooms
    RoomArea,
    Perimeter,
    LengthOfDoorsToTrim,
    LengthOfDoorsToAddTransitionTo,
    NumberOfDoorsToTrim,
    NumberOfDoorsToAddTransitionTo,
    NumberOfHeavyItems,
    NumberOfAppliances,
    NumberOfBathFixtures,
    NumberOfDoors,

    // calculated  (sq.ft.)
    MainProductArea,
    HorizontalArea,
    VerticalArea,
    SurfaceArea, // based on the product type it will include vertical and horizontal areas

    // install option
    Underlayment,
    ExistingSubfloor,
    FurnitureToMove,
    TrimWork,
    InstallationType,
    PatternType,
    RemoveExistingFloor,
    SubfloorPrep,
    StairsRiserFinish,

    // even needed????
    ProductType,

    RoomType,

    // stairs
    CurrentStairs, // (option)
    StairsTreadType,
    StairsType,
    TreadWidth,
    MinTreadWidth,
    MaxTreadWidth,
    TreadDepth,
    MinTreadDepth,
    MaxTreadDepth,
    RiserHeight,
    NumberOfSteps,
    TreadLength,
    StarterTread,
    NumberOfRisers,
}

[BsonDiscriminator]
[DiscriminatorWithFallback]
[BsonCollection("fcb2b.Room")]
[BsonKnownTypes(typeof(RegularRoom), typeof(StairsRoom))]
public class AbstractRoom : FlowObjectModel, IWithParent, IWithRelatedObjects
{
    public override string ObjectType => "fcb2b.Room";

    public Measurement TotalArea { get; set; }
    public Measurement AdjustedArea { get; set; }
    public decimal? TakeOutArea { get; set; }
    public RoomSurface[] Surfaces { get; set; }
    public string[] Tags { get; set; }
    public ReferencedObject Parent { get; set; }
    public Dictionary<string, object> RelatedObjects { get; set; }

    /// <summary>
    /// floor level
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Matrix/Position
    /// </summary>
    public decimal[] Transformation { get; set; }

    public virtual IEnumerable<Guid> GetEstimateOptionIds()
    {
        yield break;
    }

    public virtual IEnumerable<KeyValuePair<EstimateInput, object>> GetEstimateInputs()
    {
        yield break;
    }

    public virtual Measurement GetAreaForProductType(ProductType? roomSelectionProductType)
    {
        var area = AdjustedArea ?? TotalArea;
        return area;
    }
}

[BsonDiscriminator("Room")]
public class RegularRoom : AbstractRoom
{
    public Measurement Perimeter { get; set; }

    public Guid? UsdzRemoteFileId { get; set; }
    public Guid? ModelRemoteFileId { get; set; }
    public Guid? ThumbnailRemoteFileId { get; set; }

    // this could all move to a room only calls
    public Door[] Doors { get; set; }
    public Guid? RemoveExistingFloorId { get; set; }
    public Guid? ExistingSubfloorId { get; set; }
    public Guid? FurnitureToMoveId { get; set; }
    public int AppliancesToMove { get; set; }
    public int BathFixturesToMove { get; set; }
    public int HeavyItemsToMove { get; set; }

    public override IEnumerable<Guid> GetEstimateOptionIds()
    {
        if (ExistingSubfloorId.HasValue) yield return ExistingSubfloorId.Value;
        if (RemoveExistingFloorId.HasValue) yield return RemoveExistingFloorId.Value;
        if (FurnitureToMoveId.HasValue) yield return FurnitureToMoveId.Value;
    }

    public override IEnumerable<KeyValuePair<EstimateInput, object>> GetEstimateInputs()
    {
        decimal lengthOfDoorsToTrim = 0;
        decimal lengthOfDoorsToAddTransitionTo = 0;
        decimal numberOfDoorsToTrim = 0;
        decimal numberOfDoorsToAddTransitionTo = 0;

        if (Doors != null)
        {
            foreach (var door in Doors)
            {
                if (!door.Width.ConvertTo(UnitOfMeasurement.Feet, out var doorWidth))
                {
                    // "Can't convert door width {door.Width} to Feet";
                    continue;
                }

                if (door.AddTransition)
                {
                    lengthOfDoorsToAddTransitionTo += doorWidth.Units;
                    numberOfDoorsToAddTransitionTo += 1;
                }

                if (door.TrimDoor)
                {
                    lengthOfDoorsToTrim += doorWidth.Units;
                    numberOfDoorsToTrim += 1;
                }
            }
        }

        // TODO: later use surfaces to calculate
        var floorArea = (AdjustedArea ?? TotalArea)?.Convert(UnitOfMeasurement.SqFt)?.Units ?? 0;

        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.RoomType, "Room");
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.RoomArea, floorArea);

        // TODO: later use surfaces to calculate
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.HorizontalArea, floorArea);
        // TODO: later use surfaces to calculate 
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.VerticalArea, (decimal)0);

        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.Perimeter, Perimeter?.Convert(UnitOfMeasurement.Feet)?.Units ?? 0);

        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfAppliances, (decimal)AppliancesToMove);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfBathFixtures, (decimal)BathFixturesToMove);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfHeavyItems, (decimal)HeavyItemsToMove);

        if (ExistingSubfloorId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.ExistingSubfloor, ExistingSubfloorId.Value);
        if (RemoveExistingFloorId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.RemoveExistingFloor, RemoveExistingFloorId.Value);
        if (FurnitureToMoveId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.FurnitureToMove, FurnitureToMoveId.Value);

        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfDoors, Doors?.Length ?? 0);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.LengthOfDoorsToAddTransitionTo, lengthOfDoorsToAddTransitionTo);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.LengthOfDoorsToTrim, lengthOfDoorsToTrim);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfDoorsToAddTransitionTo, numberOfDoorsToAddTransitionTo);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfDoorsToTrim, numberOfDoorsToTrim);
    }
}

[BsonDiscriminator("Stairs")]
public class StairsRoom : AbstractRoom
{
    /// <summary>
    /// Current stairs
    /// </summary>
    public Guid? CurrentStairsOptionId { get; set; }

    public Guid? StairsTreadTypeOptionId { get; set; }
    public Guid? StairsTypeOptionId { get; set; }
    
    public Guid? StarterTreadOptionId { get; set; }
    
    public int? NumberOfSteps { get; set; }
    
    public int? NumberOfRisers { get; set; }

    public decimal? TreadWidth { get; set; }
    public decimal? TreadWidth2 { get; set; }

    /// <summary>
    /// Riser Height
    /// </summary>
    public decimal? RiserHeight { get; set; }

    /// <summary>
    /// Thread depth 
    /// </summary>
    public decimal? TreadDepth { get; set; }

    public decimal? TreadDepth2 { get; set; }

    public decimal? CalculateHorizontalArea()
    {
        if (!NumberOfSteps.HasValue || !TreadWidth.HasValue || !TreadDepth.HasValue) return null;

        var width = TreadWidth.Value;
        var width2 = TreadWidth2 ?? width;
        var avgWidth = (width + width2) / 2;
   
        var depth = TreadDepth.Value;
        var depth2 = TreadDepth2 ?? depth;

        return NumberOfSteps.Value * avgWidth * (depth > depth2 ? depth: depth2) / 144;  
    } 

    public decimal? CalculateVerticalArea()
    {
        if (!NumberOfRisers.HasValue || !TreadWidth.HasValue || !RiserHeight.HasValue) return null;

        var width = TreadWidth.Value;
        var width2 = TreadWidth2 ?? width;
        var avgWidth = (width + width2) / 2;

        return NumberOfRisers.Value * avgWidth * RiserHeight.Value / 144;  
    } 

    public override IEnumerable<Guid> GetEstimateOptionIds()
    {
        if (CurrentStairsOptionId.HasValue) yield return CurrentStairsOptionId.Value;
        if (StairsTreadTypeOptionId.HasValue) yield return StairsTreadTypeOptionId.Value;
        if (StairsTypeOptionId.HasValue) yield return StairsTypeOptionId.Value;
        if (StarterTreadOptionId.HasValue) yield return StarterTreadOptionId.Value;
    }

    public override IEnumerable<KeyValuePair<EstimateInput, object>> GetEstimateInputs()
    {
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.RoomArea, 0); // should it contribute to the room area? probably no
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.RoomType, "Stairs");

        if (CurrentStairsOptionId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.CurrentStairs, CurrentStairsOptionId);
        if (StairsTreadTypeOptionId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.StairsTreadType, StairsTreadTypeOptionId);
        if (StairsTypeOptionId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.StairsType, StairsTypeOptionId);
        if (StarterTreadOptionId.HasValue) yield return new KeyValuePair<EstimateInput, object>(EstimateInput.StarterTread, StarterTreadOptionId);

        if (!TreadWidth.HasValue || !RiserHeight.HasValue || !TreadDepth.HasValue || !NumberOfSteps.HasValue || !NumberOfRisers.HasValue) yield break;

        var width = TreadWidth.Value;
        var width2 = TreadWidth2 ?? width;
        var avgWidth = (width + width2) / 2;
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.TreadWidth, avgWidth);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.MinTreadWidth, (width < width2) ? width : width2);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.MaxTreadWidth, (width > width2) ? width : width2);

        var depth = TreadDepth ?? 0;
        var depth2 = TreadDepth2 ?? depth;
        var avgDepth = (depth + depth2) / 2;
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.TreadDepth, avgDepth);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.MinTreadDepth, (depth < depth2) ? depth : depth2);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.MaxTreadDepth, (depth > depth2) ? depth : depth2);

        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.RiserHeight, RiserHeight.Value);

        // calculate
        var horzArea = CalculateHorizontalArea();
        var vertArea = CalculateVerticalArea();
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.HorizontalArea, horzArea);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.VerticalArea, vertArea);

        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.TreadLength, NumberOfSteps.Value * avgWidth);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfSteps, NumberOfSteps);
        yield return new KeyValuePair<EstimateInput, object>(EstimateInput.NumberOfRisers, NumberOfRisers);
    }
    
    public override Measurement GetAreaForProductType(ProductType? productType)
    {
        var horzArea = CalculateHorizontalArea();
        var vertArea = CalculateVerticalArea();

        return productType switch
        {
            ProductType.Carpet or ProductType.SheetVinyl => horzArea.HasValue && vertArea.HasValue ? new Measurement
            {
                Units = horzArea.Value + vertArea.Value,
                UOM = UnitOfMeasurement.SqFt,
            } : null,
            _ => horzArea.HasValue ? new Measurement
            {
                Units = horzArea.Value,
                UOM = UnitOfMeasurement.SqFt,
            } : null,
        };
    }
}