using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models.Billing;

[BsonCollection("bill.Item")]
public class BillableItem : Model
{
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Value { get; set; }
    
    public string Description { get; set; }
    
    /// <summary>
    /// Factor applied to "templated" formula value
    /// </summary>
    public decimal? Factor { get; set; }
    
    /// <summary>
    /// allow to make the value conditional
    /// e.g. {{Objects.Lead.Properties|leadFee}}, {{Invoice.Total}}
    /// </summary>
    public string Formula { get; set; }

    /// <summary>
    /// Selection Criteria (Rule) 
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string LookupValue { get; set; }
    
    public bool IsActive { get; set; }
}