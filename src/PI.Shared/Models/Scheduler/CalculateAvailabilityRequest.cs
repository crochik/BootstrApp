using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models;

public class TimeBlockRule : EntityOwnedModel
{
    public static readonly DayOfWeek[] WeekDays = new[]
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    };

    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DayOfWeek[] Days { get; set; }

    public int? WeeklyGoal { get; set; }
}

public class CalculateAvailabilityRequest
{
    private readonly ILogger _logger;

    public EntityOpenSlots Result { get; }
    public User User { get; }
    public SchedulingPolicy? OverrideSchedulingPolicy { get; }
    public DateTime? MinDate { get; set; }
    public int? MinDuration { get; set; }

    public CalculateAvailabilityRequest(ILogger logger,
        User user,
        Guid appointmentTypeId,
        DateTime start,
        DateTime end,
        SchedulingPolicy? overrideSchedulingPolicy = null)
    {
        _logger = logger;
        User = user;
        OverrideSchedulingPolicy = overrideSchedulingPolicy;

        Result = new EntityOpenSlots
        {
            AccountId = user.AccountId,
            EntityId = user.Id,
            TimeZoneInfo = user.GetTimeZoneInfo(),
            AppointmentTypeId = appointmentTypeId,
            Start = start,
            End = end,
        };
    }

    public IEnumerable<TimeSlot> BuildAvailableSlots(AppointmentTypeAvailability avail)
    {
        var minDate = MinDate ?? Result.AppointmentType.Settings.CalculateMinDate(Result.TimeZoneInfo, OverrideSchedulingPolicy);
        var minDuration = MinDuration ?? Result.AppointmentType.Settings.Duration;

        return avail.BuildAvailableSlots(minDate, minDuration, Result.Start, Result.End, Result.TimeZoneInfo);
    }

    public IEnumerable<TimeSlot> Apply(IEnumerable<TimeBlockRule> rules)
    {
        var slots = Result.Slots;
        foreach (var rule in rules)
        {
            slots = SplitSlotsByStart(
                slots,
                rule.StartMinutes,
                rule.EndMinutes,
                Result.AppointmentType.Settings.Duration,
                rule.Name,
                rule.Days?.ToHashSet());
        }

        return slots;
    }

    public IEnumerable<TimeSlot> SplitSlotsByStart(
        IEnumerable<TimeSlot> slots,
        int blockStartMin,
        int blockEndMin,
        int apptDuration,
        string tag,
        HashSet<DayOfWeek> days = null)
    {
        foreach (var slot in slots)
        {
            var tSlot = slot as XTimeSlot;
            if (tSlot?.Tag != null)
            {
                // no multi-tag
                yield return tSlot;
                continue;
            }

            if ((slot.End - slot.Start).TotalMinutes < apptDuration) continue;

            var localStart = TimeZoneInfo.ConvertTime(slot.Start, TimeZoneInfo.Utc, Result.TimeZoneInfo);
            var localEnd = TimeZoneInfo.ConvertTime(slot.End, TimeZoneInfo.Utc, Result.TimeZoneInfo);

            if (days != null && !days.Contains(localStart.DayOfWeek))
            {
                // does not match day
                yield return slot;
                continue;
            }

            var slotStartMin = localStart.Hour * 60 + localStart.Minute;
            var slotEndMin = localEnd.Hour * 60 + localEnd.Minute;
            if (slotStartMin >= blockEndMin || slotEndMin <= blockStartMin)
            {
                yield return slot;
                continue;
            }

            // before the start of the range
            if (blockStartMin - slotStartMin >= apptDuration)
            {
                yield return new TimeSlot
                {
                    Start = slot.Start,
                    End = slot.Start.AddMinutes(blockStartMin - slotStartMin),
                };
            }

            // in the range
            var start = Math.Max(slotStartMin, blockStartMin);
            for (; start < blockEndMin && start + apptDuration <= slotEndMin; start += apptDuration)
            {
                yield return new XTimeSlot
                {
                    Start = slot.Start.AddMinutes(start - slotStartMin),
                    End = slot.Start.AddMinutes(start - slotStartMin + apptDuration),
                    Tag = tag,
                };
            }

            // left over
            if (slotEndMin - start >= apptDuration)
            {
                // var count = (endMin - start) / duration;
                yield return new TimeSlot
                {
                    Start = slot.Start.AddMinutes(start - slotStartMin),
                    End = slot.End,
                };
            }
        }
    }

    public void FilterSlots()
    {
        using var eOpen = Result.Slots.OrderBy(x => x.Start).ToList().GetEnumerator();
        if (!eOpen.MoveNext())
        {
            _logger.LogInformation("No availability information available: {entityId}", Result.EntityId);
            return;
        }

        // create list of busy slots
        // var included = Result.AppointmentType.Settings.IncludeStatuses?.ToHashSet();
        var evtList = Result.Events
            .Select(
                busy =>
                {
                    var busyStart = busy.Start;
                    var busyEnd = busy.End;

                    if (busy.IsAllDay)
                    {
                        // calculate time using the user's timezone (the all day slots are not in any timezone)
                        busyStart = DateTime.SpecifyKind(busyStart, DateTimeKind.Unspecified);
                        busyEnd = DateTime.SpecifyKind(busyEnd, DateTimeKind.Unspecified);
                        busyStart = TimeZoneInfo.ConvertTimeToUtc(busyStart, Result.TimeZoneInfo);
                        busyEnd = TimeZoneInfo.ConvertTimeToUtc(busyEnd, Result.TimeZoneInfo);
                        _logger.LogTrace("> Adjusted Busy from {oldStart}-{oldEnd} to {start} - {end}", busy.Start, busy.End, busyStart, busyEnd);
                    }

                    return new TimeSlot
                    {
                        Start = busyStart,
                        End = busyEnd,
                    };
                }
            )
            .OrderBy(x => x.Start)
            .ToList();

        var filtered = new List<TimeSlot>();
        using var eBusy = evtList.GetEnumerator();
        if (!eBusy.MoveNext())
        {
            _logger.LogInformation("No events in the selected timeframe: {start}-{end}", Result.Start, Result.End);

            filtered.AddRange(Result.Slots);
            adjustSlots();
            return;
        }

        var open = eOpen.Current;
        while (open != null)
        {
            var busy = eBusy.Current;
            _logger.LogTrace("> Busy {start} - {end}", busy?.Start, busy?.End);
            _logger.LogTrace("> Open {start} - {end}", eOpen.Current.Start, eOpen.Current.End);

            if (busy == null)
            {
                _logger.LogTrace("No more events, add open slot: {start}-{end}", open.Start, open.End);

                filtered.Add(open);
                eOpen.MoveNext();
                open = eOpen.Current;
                continue;
            }

            var busyStart = busy.Start.AddMinutes(-Result.AppointmentType.Settings.EventBufferInMinutes);
            var busyEnd = busy.End.AddMinutes(Result.AppointmentType.Settings.EventBufferInMinutes);
            if (busyEnd < open.Start)
            {
                _logger.LogTrace("no intersection, skip event: {start}-{end} < {start2}", busyStart, busyEnd, open.Start);
                eBusy.MoveNext();
                continue;
            }

            if (open.End < busyStart)
            {
                // open slot ends before busy
                _logger.LogTrace("no intersection, add open slot: {start}-{end} < {start2}", open.Start, open.End, busyStart);

                filtered.Add(open);
                eOpen.MoveNext();
                open = eOpen.Current;
                continue;
            }

            if (busyStart <= open.Start)
            {
                if (busyEnd >= open.End)
                {
                    _logger.LogTrace("slot inside event, skip slot: {t1} ({t2}-{t3}) {t4}", busyStart, open.Start, open.End, busyEnd);

                    eOpen.MoveNext();
                    open = eOpen.Current;
                    continue;
                }

                _logger.LogTrace("intersection, trim slot from {t1}-{t2} to ({t3}-{t4})", open.Start, open.End, busyEnd, open.End);

                open.Start = busyEnd;
                eBusy.MoveNext();
                continue;
            }

            if (busyEnd >= open.End)
            {
                _logger.LogTrace("intersection, add slot: ({t1}-{t2}) {t3} {t4}", open.Start, busyStart, open.End, busyEnd);

                filtered.Add(new TimeSlot
                {
                    Start = open.Start,
                    End = busyStart
                });
                eOpen.MoveNext();
                open = eOpen.Current;
                continue;
            }

            _logger.LogTrace("event in slot, add slot: ({t1}-{t2}) and ({t3}-{t4})", open.Start, busyStart, busyEnd, open.End);

            filtered.Add(new TimeSlot
            {
                Start = open.Start,
                End = busyStart
            });
            open.Start = busyEnd;
            eBusy.MoveNext();
        }

        adjustSlots();

        void adjustSlots()
        {
            // adjust start of slots
            if (Result.AppointmentType.Settings.StartMinutesMod.HasValue)
            {
                int startMinutesMod = Result.AppointmentType.Settings.StartMinutesMod.Value;

                foreach (var slot in filtered)
                {
                    var start = TimeZoneInfo.ConvertTimeFromUtc(slot.Start, Result.TimeZoneInfo);
                    var startMinutes = start.Hour * 60 + start.Minute;
                    int off = startMinutes % startMinutesMod;
                    if (off == 0) continue;

                    // move up to next 
                    slot.Start = slot.Start.AddMinutes(startMinutesMod - off);
                }
            }

            // exclude short slots
            Result.Slots = filtered
                .Where(x => (x.End - x.Start).TotalMinutes >= Result.AppointmentType.Settings.Duration)
                .ToArray();
        }
    }
}

public class CalendarEventComparer : IComparer<PI.Shared.Models.CalendarEvent>
{
    public int Compare(PI.Shared.Models.CalendarEvent l, PI.Shared.Models.CalendarEvent r)
    {
        int result = l.Start.CompareTo(r.Start);
        return result == 0 ? l.End.CompareTo(r.End) : result;
    }
}

public class CalendarEventTimeComparer : IEqualityComparer<CalendarEvent>
{
    public bool Equals([AllowNull] CalendarEvent x, [AllowNull] CalendarEvent y) => x.Start == y.Start && x.End == y.End;
    public int GetHashCode([DisallowNull] CalendarEvent obj) => HashCode.Combine(obj.Start, obj.End);
}