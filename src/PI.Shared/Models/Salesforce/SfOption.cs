using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Salesforce.Models;

public class SfOption : SfObject
{
    public const string ObjectTypeName = "sf_INET_Option__c";
    
    [BsonElement("Notes__c")] public string Notes { get; set; }
    [BsonElement("Project_Name__c")] public string ProjectName { get; set; }
    [BsonElement("Customer_Name__c")] public string CustomerName2 { get; set; }
    [BsonElement("CustomerName__c")] public string CustomerName { get; set; }
    [BsonElement("Design_Associate__c")] public string DesignAssociate { get; set; }

    [BsonElement("ProposalInformationString__c")]
    public string ProposalInformation { get; set; }

    [BsonElement("Proposal_Note__c")] public string ProposalNote { get; set; }

    [BsonElement("Proposal_Number__c")] public string ProposalNumber { get; set; }

    // Leading_Products__c
    // Name__c
    [BsonElement("Name")] public string Name { get; set; }

    [BsonElement("ParentProject__c")] public string WorkOrderId { get; set; }
    [BsonElement("Option_Status__c")] public string Status { get; set; }

    [BsonElement("SystemModstamp")] public DateTime SystemModstamp { get; set; }
}

[BsonCollection("salesforce.INET_Option__c")]
public class SfOptionObject : SalesforceObject<SfOption>
{
    public const string CollectionName = "salesforce.INET_Option__c";
}