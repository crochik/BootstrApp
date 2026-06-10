using Crochik.Mongo;
using PI.Shared.Salesforce.Models;

namespace PI.ProductCatalog.Models;

/// <summary>
/// Salesforce workorder object enhanced with PI Information
/// </summary>
public class SfProject : SfWorkOrder
{
    
}

[BsonCollection("salesforce.WorkOrder")]
public class SfProjectObject : SalesforceObject<SfProject>
{
    /// <summary>
    /// Tax rates for the project based on the address
    /// </summary>
    public TaxRates TaxRates { get; set; }   
}