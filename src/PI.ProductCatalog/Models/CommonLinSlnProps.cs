using System.Collections.Generic;

namespace PI.ProductCatalog.Models
{
    public abstract class CommonLinSlnProps
    {
        public List<string> AssociatedStyleNumbers { get; set; }
        public string StyleNumber { get; set; }
        public string StyleName { get; set; }
        public string ManufacturerStyleNumber { get; set; }
        public string ManufacturerStyleName { get; set; }
        public string Backing { get; set; }
        public string SizeCode { get; set; }

        public string AbbreviatedProductName { get; set; }

        public Measurement PatternRepeat { get; set; }
        public Measurement PatternDrop { get; set; }
        public Measurement PatternLength { get; set; }
        public Measurement PatternWidth { get; set; }

        public Measurement NominalLength { get; set; }
        public Measurement ActualLength { get; set; }
        public Measurement NominalWidth { get; set; }
        public Measurement ActualWidth { get; set; }

        public Measurement Height { get; set; }

        public UOMRate ShippingWeight { get; set; }
        public Measurement FaceWeight { get; set; }
        public Measurement SellingUnit { get; set; }
    }
}