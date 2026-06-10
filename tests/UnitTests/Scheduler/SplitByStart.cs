using System;
using System.Collections.Generic;
using PI.Shared.Models;
using TimeZoneConverter;
using Xunit;

namespace UnitTests
{
    public class SplitByStartTest
    {
        [Fact]
        public void Test()
        {
            var tz = TZConvert.GetTimeZoneInfo("America/New_York");
            var start = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2021, 1, 1), tz);
            var end = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2021, 1, 31), tz);

            var request = new CalculateAvailabilityRequest(null, new User { TimeZoneId = "America/New_York" }, Guid.Empty, start, end);
            var slots = new List<TimeSlot>
            {
                new TimeSlot
                {
                    Start = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2021,01,01,9,30,0), tz),
                    End = TimeZoneInfo.ConvertTimeToUtc(new DateTime(2021,01,01,17,0,0), tz),
                }
            };

            var result = request.SplitSlotsByStart(slots, 9 * 60, 11 * 60, 120, "HOT");
            // ...
        }
    }
}