namespace PI.ProductCatalog.Models
{
    public class BaseUnit
    {
        public UnitOfMeasurement? UOM { get; set; }
        public Measurement Weight { get; set; }
        public Measurement Height { get; set; }
        public Measurement Width { get; set; }
        public Measurement Length { get; set; }
        public Measurement Volume { get; set; }
        public Measurement Coverage { get; set; }
    }
}