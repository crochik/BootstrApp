using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Models;

public class AppointmentMetaData
{
    public DateTime Date { get; }
    public string TimeZoneId { get; }
    public string LocalDateStr { get; }
    public string LocalTimeStr { get; }
    public string[] Tags { get; }

    private AppointmentMetaData(DateTime date, string timeZoneId, DateTime? creationDate = null)
    {
        Date = date;
        TimeZoneId = timeZoneId;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            LocalDateStr = "[missing-tz]";
            LocalTimeStr = "[missing-tz]";
            return;
        }

        try
        {
            var timeZoneInfo = System.TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localStart = TimeZoneInfo.ConvertTime(date, TimeZoneInfo.Utc, timeZoneInfo);
            LocalDateStr = localStart.ToString("MM/dd/yyyy");
            LocalTimeStr = localStart.ToString("hh:mm tt");
            Tags = getTags().ToArray();

            IEnumerable<string> getTags()
            {
                if (localStart.Hour < 9) yield return "Early morning";
                if (localStart.Hour < 12) yield return "Morning";
                if (localStart.Hour >= 12 && localStart.Hour < 19) yield return "Afternoon";
                if (localStart.Hour >= 19) yield return "Evening";
                yield return localStart.DayOfWeek.ToString();

                if (!creationDate.HasValue) yield break;

                var localCreation = TimeZoneInfo.ConvertTime(creationDate.Value, TimeZoneInfo.Utc, timeZoneInfo);
                var off = (int)(localStart.Date - localCreation.Date).TotalDays;
                yield return off switch
                {
                    0 => "Same Day",
                    1 => "Next Day",
                    < 7 => $"{off} Days",
                    < 14 => "8-14 Days",
                    < 30 => "15-30 Days",
                    _ => "Over 30 Days"
                };
            }
        }
        catch (Exception)
        {
            LocalDateStr = "[error]";
            LocalTimeStr = "[error]";
        }
    }

    public static AppointmentMetaData Get(DateTime date, string timeZoneId, DateTime? creationDate = null) => new AppointmentMetaData(date, timeZoneId, creationDate);
    public static AppointmentMetaData Get(Appointment appt) => new AppointmentMetaData(appt.Start, appt.TimeZoneId, appt.CreatedOn);
}

public class Appointment : IFlowObject, ITaggable, IAppointment
{
    [BsonId] public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>
    /// Should match the lead.EntityId
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// ideally User
    /// </summary>
    public Guid EntityId { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User that scheduled appointment
    /// </summary>
    public Guid? CreatedBy { get; set; }

    public DateTime? LastModifiedOn { get; set; }
    public Actor LastActor { get; set; }

    // public ObjectStatusMilestones ObjectStatusMilestones { get; set; }

    /// <summary>
    /// Related objects 
    /// </summary>
    public Dictionary<string, object> Refs { get; set; }

    // TODO: make it optional and move to Refs
    /// <summary>
    /// LeadId ... 
    /// </summary>
    public Guid LeadId { get; set; }

    public Guid AppointmentTypeId { get; set; }
    public bool IsAllDay { get; set; }

    [Obsolete("replace it with Name?")] public string Subject { get; set; }

    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string WebLink { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public DateTime? CancelledOn { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTime? AddedToCalendarOn { get; set; }
    public string LocalDate { get; set; }
    public string LocalTime { get; set; }
    public string TimeZoneId { get; set; }
    public string Notes { get; set; }
    public string Tool { get; set; }
    public AppointmentState State => this.GetState();
    public IDictionary<string, object> Data { get; set; }

    /// list of entities with access to this appt
    [Obsolete("may not be needed/used anymore")]
    public Guid[] EntityIds { get; set; }

    public AppointmentIntegration[] Integrations { get; set; }

    public string ObjectType => nameof(Appointment);

    public Guid? ObjectStatusId { get; set; }

    public Guid? FlowId { get; set; }

    /// <summary>
    /// When the appointment is "re-scheduled" this will indicate what the new appt is
    /// </summary>
    public Guid? ReplacedById { get; set; }

    [BsonElement("IsActive")] public bool IsActive => !CancelledOn.HasValue;

    public string Name
    {
        get => Subject;
        set => Subject = value;
    }

    public string Description { get; set; }

    public string[] Tags { get; set; }

    public void CalculateMetaData(string timeZoneId)
    {
        TimeZoneId = timeZoneId;

        var local = AppointmentMetaData.Get(this);
        LocalDate = local.LocalDateStr;
        LocalTime = local.LocalTimeStr;
        
        if (local.Tags?.Length>0)
        {
            Tags = (Tags ?? Enumerable.Empty<string>())
                .Concat(local.Tags)
                .ToArray();
        }
    }

    public Appointment()
    {
    }

    // new IAppointment fields
    public ReferencedObject Parent { get; set; }
    public Guid? CreatorId { get; set; }
    // not in use yet... 
    public string ContentType { get; set; }
    public string Content { get; set; }
    public AddressComponents Address { get; set; }
    public Dictionary<string, object> RelatedObjects { get; set; }
}

public static class AppointmentExtensions
{
    public static AppointmentState GetState(this Appointment appt)
    {
        if (appt.CancelledOn.HasValue) return AppointmentState.Cancelled;
        if (appt.AddedToCalendarOn.HasValue) return AppointmentState.Exported;
        if (appt.ExpiresOn.HasValue) return AppointmentState.Expired;
        return AppointmentState.Created;
    }
}