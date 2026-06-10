using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfOptionLineItem : AbstractSfLineItem
{
    [BsonElement("Option__c")] public string OptionId { get; set; }
}