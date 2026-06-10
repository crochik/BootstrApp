using System;
using System.Collections.Generic;
using Crochik.Messaging;
using PI.Shared.Models;

namespace Messages.Lead;

/// <summary>
/// Appointment Events
/// </summary>
public class AppointmentEvent : IMessageBody
{
    public Appointment Appointment { get; set; }
    public IEnumerable<TrunkIdentity> ExternalIdentities { get; set; }
    public IEnumerable<IntegrationMapping> IntegrationMapping { get; set; }
    public Guid? ScheduledBy { get; set; }
    public string SchedulerUrl { get; set; }

    public static string AddRoute(Guid appointmentTypeId)
    {
        return $"appointment.{appointmentTypeId}.add";
    }

    public static string CancelRoute(Guid appointmentTypeId)
    {
        return $"appointment.{appointmentTypeId}.cancel";
    }
}