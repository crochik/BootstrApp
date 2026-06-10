namespace PI.ProductCatalog.Models
{
    public enum PriceCriteria
    {
        List, // LPR = List Price
        Promotional, // PRP = Promotional Price
        ThroughQuantity, // ICL = Price Through Quantity
        BreakQuantity, // PAQ = Price Break Quantity
        BeginningQuantity, // PBQ = Price Beginning Quantity            
    }
}