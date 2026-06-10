using System;

namespace Controllers.Models
{
    public class LeadIntegration
    {
        public Guid IntegrationId { get; set; }
        public string ExternalId { get; set; }
        public string Integration { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }
        public string Tag { get; set; }
    }
}