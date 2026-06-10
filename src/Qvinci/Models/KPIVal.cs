namespace Qvinci.Models
{
    public class KPIValue
    {
        public string Key { get; set; }
        public string Column { get; set; }
        public double Value { get; set; }

        public int? Month { get; set; }
        public int? Year { get; set; }
    }
}
