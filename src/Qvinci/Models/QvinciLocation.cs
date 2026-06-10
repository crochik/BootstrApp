using System;

namespace Qvinci.Models
{
    public class QvinciLocation
    {
        public Guid? EntityId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int CompanyId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; }
        public string FileType { get; set; } // "QuickBooks",
        public DateTime Founded { get; set; } // "3/1/2020",
        public int NumberEmployees { get; set; }
        public int NAICS { get; set; }
        public string Url { get; set; }
        public LocationAddress Address { get; set; }
    }
}
