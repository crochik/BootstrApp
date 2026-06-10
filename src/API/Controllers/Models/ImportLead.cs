using System;
using PI.Shared.Models;

namespace Controllers.Models
{
    public class ImportLead
    {
        public Guid LeadTypeId { get; set; }
        public ExternalProvider ProviderId { get; set; }
        public string ExternalEntityId { get; set; }
        public string ExternalLeadId { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }
        public dynamic LeadData { get; set; }
        public dynamic Data { get; set; }
        public string Name { get; set; }
    }
}