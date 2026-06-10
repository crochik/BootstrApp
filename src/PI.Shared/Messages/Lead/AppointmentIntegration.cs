using System;
using Messages.Integration;
using PI.Shared.Models;

namespace Messages.Lead
{
    /// <summary>
    /// Appointment Integration added to database
    /// </summary>
    public class AppointmentIntegration : IntegrationUpdate
    {
        public Appointment Appointment { get; set; }

        public string SchedulerUrl { get; set; }

        public static string IntegrationRoute(Guid appointmentTypeId)
        {
            return $"appointment.{appointmentTypeId}.integration";
        }
    }
}