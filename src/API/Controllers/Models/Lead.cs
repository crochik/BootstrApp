using System;
using System.Collections.Generic;

namespace Controllers.Models
{
    public class Lead
    {
        public Guid AccountId { get; set; }
        public Guid EntityId { get; set; }
        public Guid LeadTypeId { get; set; }
        public Guid? AssignedEntityId { get; set; }
        public Guid Id { get; set; }
        public Guid? ObjectStatusId { get; set; }
        public Guid? FlowId { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, string> CommunicationPreferences { get; set; }
        public DateTime? ConvertedOn { get; set; }
        public Guid? ReplacedById { get; set; }
        public string TimeZoneId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PostalCode { get; set; }
        public string State { get; set; }
        public DateTime CreatedOn { get; set; }
        public string Notes { get; set; }
        // public Dictionary<string, object> Values { get; set; }
    }
}