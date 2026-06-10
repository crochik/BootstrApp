using System;
using PI.ProductCatalog.Models;

namespace PI.Shared.Salesforce.Models;

public class SalesforceWorkOrderObject : SalesforceObject<SfWorkOrder>, ITaxable
{
    public Guid? LeadId { get; set; }
    public bool IsNonTaxable { get; set; }
}