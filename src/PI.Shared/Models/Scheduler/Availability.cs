using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models;

public class Availability
{
    [BsonId]

    public Guid Id { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DayOfWeek DayId { get; set; }

    public int StartMinutes { get; set; }

    public int DurationMinutes { get; set; }

    [BsonElement(nameof(EndMinutes))]
    public int EndMinutes => StartMinutes + DurationMinutes;

    public Guid[] AppointmentTypeIds { get; set; }
}