using System;
using Messages.Integration;

namespace Messages.Lead
{
    /// <summary>
    /// Received from the integration with update
    /// Some integrations will use the messagequeue and some will use the API to trigger the message
    /// </summary>
    public class AppointmentExported : IntegrationUpdate
    {
        public enum State
        {
            Added,
            Deleted
        }
        
        public State CurrentState { get; set; }
        public bool IsCalendar { get; set; }

        public static string IntegrationRemovedRoute(Guid appointmentTypeId)
        {
            return $"appointment.{appointmentTypeId}.exported";
        }

        public static string IntegrationAddedRoute(Guid appointmentTypeId)
        {
            return $"appointment.{appointmentTypeId}.exported";
        }
    }
}