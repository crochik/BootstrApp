using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfExternalLink : SfObject
{
    [BsonElement("Parent_Section__c")] public string ParentSectionId { get; set; }
    [BsonElement("ParentRoom__c")] public string ParentRoomId { get; set; }
    [BsonElement("RelatedObjectType__c")] public string RelatedObjectType { get; set; }
    [BsonElement("ParentPlan__c")] public string ParentFloorPlanId { get; set; }
    [BsonElement("Parent_Project__c")] public string ParentProjectId { get; set; }
    
    [BsonElement("Option_Name__c")] public string OptionId { get; set; }

    [BsonElement("URL__c")] public string Url { get; set; }

    [BsonElement("Name__c")] public string Name { get; set; }
    [BsonElement("Type__c")] public string Type { get; set; }
}