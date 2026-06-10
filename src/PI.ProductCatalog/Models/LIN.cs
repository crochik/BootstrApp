using System;
using System.Collections.Generic;

namespace PI.ProductCatalog.Models
{
    /// <summary>
    /// LIN Loop
    /// </summary>
    public class LIN : CommonLinSlnProps
    {
        public string UniqueStyleCode { get; set; }
        public string PricingGroup { get; set; }
        public string Manufacturer { get; set; }

        public string BuyingGroup { get; set; }
        public string Contract { get; set; }
        public string SellingCompany { get; set; }
        public string Gauge { get; set; }

        public DateTime? EffectiveDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? DroppedDate { get; set; }

        public string MaterialClassification { get; set; }
        public string ProductType { get; set; }
        public string Composition { get; set; }
        public string CollectionName { get; set; }
        public string PrimaryComponent { get; set; }

        public Warranty Warranty { get; set; }
        public BuilderProgram BuilderProgram { get; set; }
        public List<UOMRate> Packaging { get; set; }
        public BaseUnit BaseUnit { get; set; }

        public List<LINCTP> CTPs { get; set; }
        public List<SLN> Items { get; set; }
    }
}