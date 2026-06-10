namespace PI.ProductCatalog.Models
{
    public interface IPrice
    {
        PriceCriteria Criteria { get; set; }
        decimal UnitCost { get; set; }
        // Measurement Unit { get; set; }
        UnitOfMeasurement UOM { get; set; }
        decimal? MinimumQuantity { get; set; }
        PackagePriceCondition? PackageCondition { get; set; }
        string LocationId { get; set; }
    }
}