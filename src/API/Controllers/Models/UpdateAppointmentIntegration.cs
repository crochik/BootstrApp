using System;

namespace Controllers.Models
{
    public class UpdateAppointmentIntegration
    {
        public Guid AppointmentId { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }        
        public object Data { get; set; }
    }
}