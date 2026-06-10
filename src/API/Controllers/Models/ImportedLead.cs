using System;

namespace Controllers.Models
{
    public class ImportedLead
    {
        public Guid LeadId { get; set; }
        public Guid? AppointmentTypeId { get; set; }
        public Guid? EntityId { get; set; }
        public string ReferenceId { get; set; }
    }
}