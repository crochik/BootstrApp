using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public class AppointmentTypeAvailability
{
    // is it used?
    public Guid AppointmentTypeId { get; set; }

    [BsonElement] public DayAvailability[] Days { get; } = new DayAvailability[7];

    public DayAvailability this[System.DayOfWeek day]
    {
        get => Days[(int)day];
        set => Days[(int)day] = value;
    }

    public void Add(DayAvailability day)
    {
        this[day.DayOfWeek] = day;
    }

    public IEnumerable<TimeSlot> BuildAvailableSlots(DateTime minDate, int minDuration, DateTime start, DateTime end, TimeZoneInfo timeZoneInfo)
    {
        // round to minutes precision
        minDate = new DateTime(minDate.Year, minDate.Month, minDate.Day, minDate.Hour, minDate.Minute, 0, minDate.Kind);
        
        // convert to local so it will have the right day of the week
        var localStart = TimeZoneInfo.ConvertTime(start, TimeZoneInfo.Utc, timeZoneInfo).Date;
        var localEnd = TimeZoneInfo.ConvertTime(end, TimeZoneInfo.Utc, timeZoneInfo);
        for (var date = localStart; date < localEnd; date = date.AddDays(1))
        {
            var daySlots = this[date.DayOfWeek];
            if (daySlots == null)
            {
                continue;
            }

            foreach (var slot in daySlots.Slots)
            {
                if (slot.Duration < minDuration) continue;

                var min = slot.Start % 60;
                var hours = (slot.Start - min) / 60;

                var utcStart = TimeZoneInfo.ConvertTime(new DateTime(date.Year, date.Month, date.Day, hours, min, 0), timeZoneInfo, TimeZoneInfo.Utc);
                if (utcStart < minDate)
                {
                    // add partial slot from minDate => slot.End
                    var off = (minDate - utcStart).TotalMinutes;
                    if (slot.Duration > off && slot.Duration - off > minDuration)
                    {
                        yield return new TimeSlot
                        {
                            Start = minDate,
                            End = minDate.AddMinutes(slot.Duration - off)
                        };
                    }

                    continue;
                }

                var open = new TimeSlot
                {
                    Start = utcStart,
                    End = utcStart.AddMinutes(slot.Duration)
                };

                yield return open;
            }
        }
    }
}