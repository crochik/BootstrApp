using System;

namespace Controllers.Models
{
    public class LeadEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid LeadId { get; set; }
        public Guid? AppointmentId { get; set; }
        public Guid? EntityId { get; set; }
        public string Integration { get; set; }
        public string Entity { get; set; }
        // public string Event { get; set; }
        public string Message { get; set; }
        public Guid? IntegrationId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime AppointmentStart { get; set; }
        public DateTime AppointmentEnd { get; set; }
    }
}
