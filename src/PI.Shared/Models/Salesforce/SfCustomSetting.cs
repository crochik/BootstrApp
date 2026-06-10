using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfCustomSetting : SfObject
{
    [BsonElement("Name")] public string Name { get; set; }
    [BsonElement("SettingsType__c")] public string Type { get; set; }
    [BsonElement("IsStairs__c")] public bool IsStairs { get; set; }
}