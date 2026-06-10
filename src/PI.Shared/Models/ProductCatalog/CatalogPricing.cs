namespace PI.ProductCatalog.Models
{
    public enum CatalogPricing
    {
        Undefined = -1, 
        NoColorPricing, // 0 = No Color Level pricing
        LINPricing,     // 1 = Color Level pricing (multiple LIN)
        SLNPricing,     // 2 = Color Level pricing (CTP/DTM in the SLN)            
    }
}