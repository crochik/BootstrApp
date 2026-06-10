using System;

namespace PI.ProductCatalog.Models;

public partial class CatalogItem
{
    public class SalesforceSync
    {
        /// <summary>
        /// Last time item was synced to Salesforce
        /// </summary>
        public DateTime? LastSyncedOn { get; set; }

        /// <summary>
        /// Salesforce product2 id
        /// </summary>
        public string Product2 { get; set; }

        /// <summary>
        /// Salesforce pricebook2 id
        /// </summary>
        public string Pricebook2 { get; set; }

        /// <summary>
        /// Url to pricebook entry in Sf
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Explicit value to be used as the ExternalId when exporting to Salesforce
        /// When omitted will be calculated based on the Item.Id
        /// </summary>
        public string ExternalId { get; set; }
    }
}