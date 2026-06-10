using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Data.Mongo
{
    // EntityOwnedModel - Name ?!?
    // public class Appointment : IAppointment
    // {
    //     [BsonId]
    //     public Guid Id { get; set; }
    //     public Guid AccountId { get; set; }
    //     public Guid EntityId { get; set; }
    //     public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    //     public DateTime? LastModifiedOn { get; set; }
    //     public Actor LastActor { get; set; }

    //     public Guid LeadId { get; set; }
    //     public Guid AppointmentTypeId { get; set; }
    //     public bool IsAllDay { get; set; }
    //     public string Subject { get; set; }
    //     public DateTime Start { get; set; }
    //     public DateTime End { get; set; }
    //     public string WebLink { get; set; }
    //     public DateTime? ExpiresOn { get; set; }
    //     public DateTime? CancelledOn { get; set; }
    //     public DateTime? AddedToCalendarOn { get; set; }
    //     public string LocalDate { get; set; }
    //     public string LocalTime { get; set; }
    //     public string TimeZoneId { get; set; }
    //     public string Notes { get; set; }
    //     public string Tool { get; set; }
    //     public AppointmentState State => this.GetState();
    //     public IDictionary<string, object> Data { get; set; }

    //     public Guid? CreatedBy { get; set; }

    //     /// list of entities with access to this appt
    //     public Guid[] EntityIds { get; set; }

    //     public AppointmentIntegration[] Integrations { get; set; }

    //     public void UpdateLocalStrings(string timeZoneId)
    //     {
    //         var timeZoneInfo = System.TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    //         var localStart = TimeZoneInfo.ConvertTime(Start, TimeZoneInfo.Utc, timeZoneInfo);
    //         LocalDate = localStart.ToString("MM/dd/yyyy");
    //         LocalTime = localStart.ToString("hh:mm tt");
    //         TimeZoneId = timeZoneId;
    //     }
    // }

    [BsonCollection("Appointment.Snapshot")]
    [BsonIgnoreExtraElements]
    public class AppointmentSnapshot
    {
        [BsonId]
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid OrganizationId { get; set; }
        public string Organization { get; set; }
        public Guid EntityId { get; set; }
        public string Entity { get; set; }
        // public Guid[] EntityIds { get; set; }
        public Guid CreatedById { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string LocalDate { get; set; }
        public string LocalTime { get; set; }
        public string Tool { get; set; }
        public DateTime CreatedOn { get; set; }
        public Guid LeadId { get; set; }
        public string Lead { get; set; }
        public DateTime LeadCreatedOn { get; set; }
    }
}