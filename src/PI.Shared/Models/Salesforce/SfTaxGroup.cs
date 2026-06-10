using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfTaxGroup : SfObject
{
    [BsonElement("Name")] public string Name { get; set; }
}