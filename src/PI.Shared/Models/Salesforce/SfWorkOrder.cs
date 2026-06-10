using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfWorkOrder : SfObject
{
    [BsonElement("INET_Name__c")] public string Name { get; set; }
    [BsonElement("INET_Notes__c")] public string Notes { get; set; }
    
    [BsonElement("WorkOrderNumber")] public string ProjectNumber { get; set; }
    
    public string Status { get; set; }

    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Country { get; set; }
    public string PostalCode { get; set; }

    [BsonElement("INET_FloorPlan__c")] public string FloorPlanId { get; set; }
    
    /// <summary>
    /// SF Customer Id
    /// </summary>
    [BsonElement("AccountId")] public string CustomerId { get; set; }
    
    /// <summary>
    /// SF Lead Id
    /// </summary>
    [BsonElement("INET_Lead__c")] public string LeadId { get; set; }
}