using System;

namespace PI.Shared.Models.Interfaces;

public interface IAppointment : INote, IWithAddress
{
    public bool IsAllDay { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    // public TimeSpan Duration => End - Start;
    // public string LocalDate { get; set; }
    // public string LocalTime { get; set; }

    public string TimeZoneId { get; set; }
}