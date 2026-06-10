using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models;

[BsonCollection("fcb2b.MAL")]
public class MOBItem
{
    [BsonId] public Guid Id { get; set; }

    public Guid AccountId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string ExternalId { get; set; }
    public bool IsActive { get; set; }
    public MOBProperties Properties { get; set; }

    public QuantityCriteria? Criteria { get; set; }

    public QuantityCriteria GetQuantityCriteria()
    {
        if (Criteria != null) return Criteria.Value;
        return Properties?.UOM switch
        {
            UnitOfMeasurement.SqFt or UnitOfMeasurement.SqYd => QuantityCriteria.MainProductArea,
            UnitOfMeasurement.Feet or UnitOfMeasurement.FeetAndInches => QuantityCriteria.Perimeter,
            _ => QuantityCriteria.Arbitrary,
        };
    }
}