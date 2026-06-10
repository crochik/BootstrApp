using System;

namespace Controllers.Models
{
    public class AddAppointment
    {
        public Guid AppointmentTypeId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Subject { get; set; }
        public Guid UserId { get; set; }
        public string Notes { get; set; }
    }
}