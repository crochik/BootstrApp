using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfFloorPlan : SfObject
{
    [BsonElement("Name")] public string Name { get; set; }

    [BsonElement("ParentProject__c")] public string WorkOrderId { get; set; }
    [BsonElement("Notes__c")] public string Notes { get; set; }
    [BsonElement("FloorPlanImageURL__c")] public string ImageUrl { get; set; }
}