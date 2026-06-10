using System;
using System.Collections.Generic;
using System.Linq;

namespace PI.ProductCatalog.Models
{
    public class SLN : CommonLinSlnProps, ISLNOnlyProps
    {
        public string SKU { get; set; }
        public string UniqueColorCode { get; set; }

        public string UPCCode { get; set; }
        public string ManufacturerSKU { get; set; }
        public string ColorName { get; set; }
        public string ColorNumber { get; set; }

        public string[] ImageUrls { get; set; }
        public string ProductSpecification { get; set; }

        #region IDates 
        public DateTime? EffectiveDate { get; set; }
        public DateTime? PendingDate { get; set; }
        public DateTime? DroppedDate { get; set; }
        public DateTime? PromotionalStart { get; set; }
        public DateTime? PromotionalEnd { get; set; }
        #endregion

        internal void AddImageUrl(string url)
        {
            ImageUrls = ImageUrls != null ? ImageUrls.Append(url).ToArray() : new string[] { url };
        }

        public List<SLNCTP> CTPs { get; set; }
        public string[] AssociatedSKUs => PID08?.ToArray();
        public List<string> PID08 { get; set; }

        public SLN() { }
    }
}