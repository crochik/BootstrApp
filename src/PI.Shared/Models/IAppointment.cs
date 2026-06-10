using System;
using System.Collections.Generic;
using Crochik.Mongo;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models;

public class AppointmentTool
{
    public static readonly Guid LuminEntityId = Guid.Parse("911BE1FF-54C2-4B28-960A-F0397604C936");
    public static readonly Guid CallcenterEntityId = Guid.Parse("6AB5FF81-F0F1-44C3-9B93-C7A271D88EC6");
    public static readonly string Callcenter = "Callcenter";
    public static readonly string Lumin = "Lumin.ai";
    public static readonly string Scheduler = "www";
}

[JsonConverter(typeof(StringEnumConverter))]
public enum AppointmentState
{
    Created,
    Exported,
    Cancelled,
    Expired
}

// public interface IAppointment : IRow<Guid>, IFlowObject
// {
//     Guid EntityId { get; set; }
//     Guid LeadId { get; set; }
//     Guid AppointmentTypeId { get; set; }
//     bool IsAllDay { get; set; }
//     string Subject { get; set; }
//     DateTime Start { get; set; }
//     DateTime End { get; set; }
//     string WebLink { get; set; }
//
//     DateTime? ExpiresOn { get; set; }
//     DateTime? CancelledOn { get; set; }
//     DateTime? AddedToCalendarOn { get; set; }
//
//     string LocalDate { get; set; }
//     string LocalTime { get; set; }
//     string TimeZoneId { get; set; }
//
//     // ???????
//     IDictionary<string, object> Data { get; set; }
//
//     Guid? CreatedBy { get; set; }
//     string Notes { get; set; }
//     string Tool { get; set; }
//
//     // calculated
//     AppointmentState State { get; }
// }