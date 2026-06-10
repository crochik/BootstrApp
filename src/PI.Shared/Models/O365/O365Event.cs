using System;
using Crochik.Mongo;

namespace PI.Shared.Models;

public class O365Recurrence
{
    public string Id { get; set; }
    public RecurrencePatternType? PatternType { get; set; }
    public int? PatternInterval { get; set; }
    public int? PatternMonth { get; set; }
    public int? PatternDayOfMonth { get; set; }
    public bool PatternSunday { get; set; }
    public bool PatternMonday { get; set; }
    public bool PatternTuesday { get; set; }
    public bool PatternWednesday { get; set; }
    public bool PatternThursday { get; set; }
    public bool PatternFriday { get; set; }
    public bool PatternSaturday { get; set; }
    public DayOfWeek? PatternFirstDayOfWeek { get; set; }
    public WeekIndex? PatternIndex { get; set; }
    public RecurrenceRangeType? RangeType { get; set; }
    public DateTime RangeStartDate { get; set; }
    public DateTime? RangeEndDate { get; set; }
    public string RangeRecurrenceTimeZone { get; set; }
    public int? RangeNumberOfOccurrences { get; set; }
}

public class SeriesInstances
{
    /// <summary>
    /// Last time instances were loaded
    /// </summary>
    public DateTime? LastLoadedOn { get; set; }

    /// <summary>
    /// Start of range of loaded instances
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End of range of loaded instances
    /// </summary>
    public DateTime? EndDate { get; set; }
}

public class O365ResponseStatus
{
    //
    // Summary:
    //     Gets or sets response. The response type. Possible values are: none, organizer,
    //     tentativelyAccepted, accepted, declined, notResponded.To differentiate between
    //     none and notResponded: as an example, if attendee Alex hasn't responded to a
    //     meeting request, getting Alex' response status for that event in Alex' calendar
    //     returns notResponded. Getting Alex' response from the calendar of any other attendee
    //     or the organizer's returns none. Getting the organizer's response for the event
    //     in anybody's calendar also returns none.
    public ResponseType? Response { get; set; }

    //
    // Summary:
    //     Gets or sets time. The date and time that the response was returned. It uses
    //     ISO 8601 format and is always in UTC time. For example, midnight UTC on Jan 1,
    //     2014 is 2014-01-01T00:00:00Z
    public DateTime? Time { get; set; }
}

[BsonCollection("o365.Event")]
public class O365Event : EntityOwnedModel
{
    public const string ObjectTypeName = "o365.Event";
    
    public string ExternalId { get; set; }
    public FreeBusyStatus? ShowAs { get; set; }
    public CalendarEventType? Type { get; set; }

    //
    // Summary:
    //     Gets or sets response status. Indicates the type of response sent in response
    //     to an event message.
    public O365ResponseStatus ResponseStatus { get; set; }

    //
    // Summary:
    //     Gets or sets sensitivity. The possible values are: normal, personal, private,
    //     confidential.
    public Sensitivity? Sensitivity { get; set; }        

    public bool? IsCancelled { get; set; }
    public bool? IsAllDay { get; set; }
    public string ICalUId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string[] Categories { get; set; }
    public string WebLink { get; set; }

    /// <summary>
    /// Id of event that represent the series
    /// </summary>
    public Guid? SeriesMasterId { get; set; }

    /// <summary>
    /// o365 Id for the master in the series
    /// </summary>
    public string MasterExternalId { get; set; }

    /// <summary>
    /// When this event is associated with a PI Appt
    /// </summary>
    public Guid? AppointmentId { get; set; }

    // ignored since there is no use for it for now 
    // public O365Recurrence Recurrence { get; set; }

    /// <summary>
    /// For master events track resolution of the instances
    /// </summary>
    public SeriesInstances Instances { get; set; }
}