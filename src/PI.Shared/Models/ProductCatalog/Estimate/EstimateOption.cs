using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models;

[BsonCollection("fcb2b.EstimateOption")]
[BsonKnownTypes(
        typeof(ExistingSubfloorOption),
        typeof(FurnitureToMoveOption),
        typeof(InstallationTypeOption),
        typeof(ProductTypeOption),
        typeof(RemoveExistingFloorOption),
        typeof(TrimWorkOption),
        typeof(UnderlaymentOption),
        typeof(PatternTypeOption)
    )
]
public class EstimateOption
{
    [BsonId] public Guid Id { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public string ExternalId { get; set; }
    public string[] MOBs { get; set; }
}

[BsonDiscriminator("InstallationType")]
public class InstallationTypeOption : EstimateOption
{
    public ProductType ProductType { get; set; }
}

[BsonDiscriminator("PatternType")]
public class PatternTypeOption : EstimateOption
{
    public ProductType ProductType { get; set; }
}

[BsonDiscriminator("Underlayment")]
public class UnderlaymentOption : EstimateOption
{
    public ProductType ProductType { get; set; }
}

[BsonDiscriminator("SubfloorPrep")]
public class SubfloorPrepOption : EstimateOption
{
    public ProductType ProductType { get; set; }
}

[BsonDiscriminator("StairsRiserFinish")]
public class StairsRiserFinishOption : EstimateOption
{
    public ProductType ProductType { get; set; }
}

[BsonDiscriminator("ExistingSubfloor")]
public class ExistingSubfloorOption : EstimateOption
{
}

[BsonDiscriminator("FurnitureToMove")]
public class FurnitureToMoveOption : EstimateOption
{
}

[BsonDiscriminator("ProductType")]
public class ProductTypeOption : EstimateOption
{
    public ProductType Code { get; set; }
}

[BsonDiscriminator("RemoveExistingFloor")]
public class RemoveExistingFloorOption : EstimateOption
{
}

[BsonDiscriminator("TrimWork")]
public class TrimWorkOption : EstimateOption
{
}

[BsonCollection("fcb2b.EstimateOptionDefault")]
public class EstimateOptionDefault
{
    /// <summary>
    /// Estimate Option Object Type
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Default estimate option id (of type ObjectType)
    /// </summary>
    public Guid EstimateOptionId { get; set; }
    
    public ProductType ProductType { get; set; }   
    public Guid? ExistingSubfloorId { get; set; }
}