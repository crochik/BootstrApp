using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models;

public class DayAvailability
{
    [JsonConverter(typeof(StringEnumConverter))]
    public DayOfWeek DayOfWeek { get; set; }
    public Slot[] Slots { get; set; }

    public DayAvailability()
    {
    }

    public DayAvailability(System.DayOfWeek day, Slot[] slots)
    {
        DayOfWeek = day;
        Slots = slots;
    }
}