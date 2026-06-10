using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfSectionLineItem : AbstractSfLineItem
{
    [BsonElement("Section__c")] public string SectionId { get; set; }
}