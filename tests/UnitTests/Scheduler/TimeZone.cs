using System;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class TimeZone
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TimeZone(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void InvalidDateInTimeZoneDueToDaylightSavings()
    {
        var date = new DateTime(2025, 3, 9, 2, 0, 0);
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        // var result = TimeZoneInfo.ConvertTime(date, timeZoneInfo, TimeZoneInfo.Utc);
        Assert.Throws<ArgumentException>( ()=>TimeZoneInfo.ConvertTimeToUtc(date, timeZoneInfo));
    }
}