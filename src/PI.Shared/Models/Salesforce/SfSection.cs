using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;


public class SfSection : SfObject
{
    [BsonElement("Name__c")] public string Name { get; set; }
    [BsonElement("StairsInstallation__c")] public string StairsInstallation { get; set; }
    [BsonElement("InstallType__c")] public string InstallTypeId { get; set; }

    [BsonElement("ProductType__c")] public string ProductTypeId { get; set; }

    [BsonElement("TrimWork__c")] public string TrimWorkId { get; set; }

    [BsonElement("PatternType__c")] public string PatternTypeId { get; set; }

    [BsonElement("StairsType__c")] public string StairsTypeId { get; set; }

    [BsonElement("SubfloorType__c")] public string SubfloorTypeId { get; set; }

    [BsonElement("Underlayment__c")] public string UnderlaymentId { get; set; }

    [BsonElement("LeadingProduct__c")] public string LeadingProduct { get; set; }

    [BsonElement("Area__c")] public decimal? Area { get; set; }

    [BsonElement("Perimeter__c")] public decimal? Perimeter { get; set; }

    [BsonElement("ParentOption__c")] public string OptionId { get; set; }
}