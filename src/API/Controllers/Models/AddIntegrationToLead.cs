using System;

namespace Controllers.Models
{
    public class AddIntegrationToLead
    {
        public Guid LeadId { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }
        public object Data { get; set; }
    }
}