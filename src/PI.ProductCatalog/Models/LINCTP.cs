using System;
using System.Collections.Generic;

namespace PI.ProductCatalog.Models
{
    /// <summary>
    /// CTP Loop inside LIN
    /// </summary>
    public class LINCTP : IPrice
    {
        #region IDates
        public DateTime? EffectiveDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? DroppedDate { get; set; }
        public DateTime? PromotionalStart { get; set; }
        public DateTime? PromotionalEnd { get; set; }
        #endregion

        #region IPrice
        public PriceCriteria Criteria { get; set; }
        public UnitOfMeasurement UOM { get; set; }
        public decimal? MinimumQuantity { get; set; }

        public PackagePriceCondition? PackageCondition { get; set; }
        public decimal UnitCost { get; set; }
        public string LocationId { get; set; }
        #endregion

        public string Promotion { get; internal set; }
        public List<Allowance> Allowances { get; set; }
    }
}