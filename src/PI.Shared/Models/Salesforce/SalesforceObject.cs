using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace PI.Shared.Salesforce.Models;

public class SalesforceObject<T> : DynamicFlowObjectModel
    where T : SfObject
{
    public string ExternalId { get; set; }
    public T Properties { get; set; }
}

public class SfObject
{
    // TODO: for some reason if the property name is Id it doesn't get deserialized
    // investigate....
    [BsonElement("Id")] public string ExternalId { get; set; }
    [BsonElement("IsDeleted")] public bool IsDeleted { get; set; }
}