using System;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum UserPolicy
    {
        AnyInOrganization,
        SameOfLastAppt,
        SameOfNextAppt,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SchedulingPolicy
    {
        NextDay,
        FromNow
    }

    public class SchedulingSettings
    {
        public static SchedulingSettings Default => new();

        public int MinMinutesFromNow { get; set; } = 120;
        public int MaxMinutesFromNow { get; set; } = 0;
        public int Duration { get; set; } = 90;

        /// <summary>
        /// start slots every StartMinutesMod
        /// </summary>
        public int? StartMinutesMod { get; set; } = 30;

        /// <summary>
        /// Number of minutes to be added to every slot in the start and end so they don't overlap
        /// (e.g. travel time)
        /// </summary>
        public int EventBufferInMinutes { get; set; } = 15;

        public int Expiration { get; set; } = 0;
        public UserPolicy UserPolicy { get; set; } = UserPolicy.SameOfNextAppt;
        public SchedulingPolicy SchedulingPolicy { get; set; } = SchedulingPolicy.NextDay;

        [BsonElement("IncludeStatuses")] private FreeBusyStatus[] _includeStatuses;

        /// <summary>
        /// Status to be considered busy (and included when calculating busy events)
        /// </summary> 
        [BsonIgnore]
        public FreeBusyStatus[] IncludeStatuses
        {
            // Exclude all 
            get => _includeStatuses ?? new[]
            {
                FreeBusyStatus.Busy,
                FreeBusyStatus.Free,
                FreeBusyStatus.Oof,
                FreeBusyStatus.Tentative,
                FreeBusyStatus.Unknown,
                FreeBusyStatus.WorkingElsewhere
            };
            set => _includeStatuses = value;
        }

        [BsonElement("ExcludeResponseTypes")] private ResponseType[] _excludeResponseTypes;

        /// <summary>
        /// Do not include these ResponseTypes when calculating busy events
        /// </summary>
        [BsonIgnore]
        public ResponseType[] ExcludeResponseTypes
        {
            get => _excludeResponseTypes ?? new[]
            {
                ResponseType.Declined,
                ResponseType.NotResponded,
                ResponseType.None
            };
            set => _excludeResponseTypes = value;
        }

        [BsonElement("ExcludeBusyResponseTypes")]
        private Sensitivity[] _excludeSensitivities;

        /// <summary>
        /// Do not include these Sesnsitivities when calculating busy events
        /// </summary>
        [BsonElement("ExcludeSensitivities")]
        public Sensitivity[] ExcludeSensitivities
        {
            get => _excludeSensitivities ?? new[]
            {
                Sensitivity.Private,
            };
            set => _excludeSensitivities = value;
        }

        public DateTime CalculateMinDate(TimeZoneInfo timeZoneInfo, SchedulingPolicy? overrideSchedulingPolicy = null)
        {
            switch (overrideSchedulingPolicy ?? SchedulingPolicy)
            {
                case SchedulingPolicy.FromNow:
                    return DateTime.UtcNow.AddMinutes(MinMinutesFromNow);

                case SchedulingPolicy.NextDay:
                    var date = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, timeZoneInfo);
                    date = date.Date.AddDays(1);
                    date = TimeZoneInfo.ConvertTime(date, timeZoneInfo, TimeZoneInfo.Utc);
                    return date;

                default:
                    return DateTime.UtcNow;
            }
        }
    }

    public class AppointmentType : EntityOwnedModel
    {
        public Guid? LeadTypeId { get; set; }
        public SchedulingSettings Settings { get; set; } = SchedulingSettings.Default;
        public AppointmentTypeIntegration[] Integrations { get; set; }

        /// <summary>
        /// Initial flow id for appointments created by it
        /// </summary>
        public Guid? InitialFlowId { get; set; }

        /// <summary>
        /// Initial object status id for appointments created by it
        /// </summary>
        public Guid? InitialObjectStatusId { get; set; }

        /// <summary>
        /// Default iCal title (template)
        /// </summary>
        public string ICalDescription { get; set; }

        /// <summary>
        /// Default iCal summary (template)
        /// </summary>
        public string ICalSummary { get; set; }
    }

    public class SchedulingConstraints
    {
        /// <summary>
        /// interval to check (a day is the minimum, multiple of day) 
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Maximum number of occurrences in the interval allowed
        /// </summary>
        public int MaxOccurrences { get; set; }

        /// <summary>
        /// If applies to any appointment type or only the one specified in the settings 
        /// </summary>
        public bool AnyAppointmentTypeId { get; set; }
    }

    public class UserSchedulingSettings
    {
        public Guid UserId { get; set; }
        public Guid AppointmentTypeId { get; set; }
        public int Priority { get; set; }

        /// <summary>
        /// not implemented 
        /// </summary>
        public SchedulingConstraints[] Constraints { get; set; }
    }

    public enum AvailabilityPolicy
    {
        UserAvailability,
        DefaultForAccount,
    }

    public class UnavailableSlots
    {
        public SchedulingPolicy SchedulingPolicy { get; set; }

        /// <summary>
        /// Flow for on demand appointments 
        /// </summary>
        public Guid? FlowId { get; set; }

        /// <summary>
        /// initial object status for on demand appointments 
        /// </summary>
        public Guid? ObjectStatusId { get; set; }

        /// <summary>
        /// Who to assign the appointment/lead to when scheduling an unavailable slot 
        /// </summary>
        public Guid? AssignEntityId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public AvailabilityPolicy AvailabilityPolicy { get; set; }
    }

    [BsonCollection("scheduler.Settings")]
    public class SchedulerSettings : EntityOwnedModel, IExternalId
    {
        /// <summary>
        /// Alternative Id that can be used to initiate a session
        /// </summary>
        public string ExternalId { get; set; }

        /// <summary>
        /// Possible Users
        /// </summary>
        public UserSchedulingSettings[] Settings { get; set; }

        /// <summary>
        /// Lead Type that will be used to create leads 
        /// (instead of the AppointmentType.LeaadTypeId)
        /// </summary>
        public Guid LeadTypeId { get; set; }

        /// <summary>
        /// Client id requesting session
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Flag to indicate that is not active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// When set it will prevent the config from being used
        /// </summary>
        public string OutOfServiceMessage { get; set; }

        /// <summary>
        /// When set, allows users to request appointments even when the slot is not available
        /// </summary>
        public UnavailableSlots UnavailableSlots { get; set; }
        
        /// <summary>
        /// Override appointment type SchedulingPolicy
        /// </summary>
        public SchedulingPolicy? OverrideSchedulingPolicy { get; set; }

        public SchedulerSettings()
        {
        }
    }

    [BsonCollection("scheduler.Session")]
    public class SchedulerSession : EntityOwnedModel, IExternalId
    {
        /// <summary>
        /// JTI
        /// </summary>
        public string ExternalId { get; set; }

        /// <summary>
        /// Effective Settings to be used for the session
        /// </summary>
        public SchedulerSettings Settings { get; set; }

        /// <summary>
        /// Lead Id (created)
        /// </summary>
        public Guid? LeadId { get; set; }

        /// <summary>
        /// Appointment Id (created)
        /// </summary>
        public Guid? AppointmentId { get; set; }

        public string TimeZoneId { get; set; }

        /// <summary>
        /// Errors 
        /// </summary>
        public SchedulingError[] Errors { get; set; }

        /// <summary>
        /// Referer that initiated session
        /// </summary>
        public string Referer { get; set; }

        [BsonIgnore] public Lead Lead { get; set; }

        [BsonIgnore] public Appointment Appointment { get; set; }

        [BsonIgnore] public Entity Entity { get; set; }

        public SchedulerSession()
        {
        }
    }

    public class SchedulingError
    {
        public TimeSlot Slot { get; set; }
        public string Error { get; set; }
    }
}