using System;

namespace PI.ProductCatalog.Models
{
    public interface IDates 
    {
        DateTime? EffectiveDate { get; set; }
        DateTime? PendingDate { get; set; }
        DateTime? DroppedDate { get; set; }
        DateTime? PromotionalStart { get; set; }
        DateTime? PromotionalEnd { get; set; }
    }
}