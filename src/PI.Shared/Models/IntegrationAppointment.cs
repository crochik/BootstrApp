using System;

namespace PI.Shared.Models
{
    public interface IIntegrationAppointment
    {
        Guid IntegrationId { get; }
        string ExternalId { get; }
        Guid AppointmentId { get; }
        string Status { get; }
        string Url { get; }
        object Data { get; }
    }

    // public class IntegrationAppointment : IIntegrationAppointment
    // {
    //     public Guid IntegrationId { get; set; }
    //     public string ExternalId { get; set; }
    //     public Guid AppointmentId { get; set; }
    //     public string Status { get; set; }
    //     public string Url { get; set; }
    //     public object Data { get; set; }
    // }
}