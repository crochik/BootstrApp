namespace PI.ProductCatalog.Models
{
    public class SLNCTP : IPrice
    {
        #region IPrice        
        public PriceCriteria Criteria { get; set; }
        public UnitOfMeasurement UOM { get; set; }
        public decimal? MinimumQuantity { get; set; }

        public PackagePriceCondition? PackageCondition { get; set; }
        public decimal UnitCost { get; set; }
        public string LocationId { get; set; }
        #endregion
    }
}