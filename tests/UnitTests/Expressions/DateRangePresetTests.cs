using System;
using FluentAssertions;
using FluentAssertions.Extensions;
using PI.Shared.Form.Models;
using Xunit;

namespace UnitTests.Expressions;

public class DateRangePresetTests
{
    TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    
    [Fact]
    public void Null()
    {
        DateRangePreset.Calculate(null, timeZone).Should().BeNull();
    }

    [Fact]
    public void Date()
    {
        var now = DateTime.UtcNow;
        now = now.AddMilliseconds(-now.Millisecond);
        now = now.AddMicroseconds(-now.Microsecond());

        var result = DateRangePreset.Calculate(now.ToString(), timeZone);
        result.Value.Date.Should().Be(now.Date);
        result.Value.Should().Be(now);
    }

    [Fact]
    public void Now()
    {
        var now = DateTime.UtcNow;
        var result = DateRangePreset.Calculate("{{now}}", timeZone);
        var off = result.Value - now;
        off.TotalSeconds.Should().BeLessThan(2);
    }

    [Fact]
    public void Today()
    {
        var result = DateRangePreset.Calculate("{{today}}", timeZone);

        var local = TimeZoneInfo.ConvertTimeFromUtc(result.Value, timeZone);
        local.Hour.Should().Be(0);
        local.Minute.Should().Be(0);
        local.Second.Should().Be(0);
    }

    [Fact]
    public void Month()
    {
        var result = DateRangePreset.Calculate("{{month}}", timeZone);

        var local = TimeZoneInfo.ConvertTimeFromUtc(result.Value, timeZone);
        local.Day.Should().Be(1);
        local.Hour.Should().Be(0);
        local.Minute.Should().Be(0);
        local.Second.Should().Be(0);
    }

    [Fact]
    public void Year()
    {
        var result = DateRangePreset.Calculate("{{year}}", timeZone);

        var local = TimeZoneInfo.ConvertTimeFromUtc(result.Value, timeZone);
        local.Month.Should().Be(1);
        local.Day.Should().Be(1);
        local.Hour.Should().Be(0);
        local.Minute.Should().Be(0);
        local.Second.Should().Be(0);
    }

    // hard coded expectations, won't work
    // [Theory]
    [InlineData("{{today}}", 2023, 03, 29)]
    [InlineData("{{today -1d}}", 2023, 03, 28)]
    [InlineData("{{month}}", 2023, 03, 01)]
    [InlineData("{{month - 3M}}", 2022, 12, 01)]
    [InlineData("{{day - 28d}}", 2023, 03, 01)]
    [InlineData("{{month + 1M}}", 2023, 04, 01)]
    [InlineData("{{month + 13M}}", 2024, 04, 01)]
    [InlineData("{{month - 24M}}", 2021, 03, 01)]
    public void OneTime(string expression, int year, int month, int day = 1, int hour = 0, int min = 0, int sec = 0)
    {
        var result = DateRangePreset.Calculate(expression, timeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(result.Value, timeZone);
    
        local.Year.Should().Be(year);
        local.Month.Should().Be(month);
        local.Day.Should().Be(day);
        local.Hour.Should().Be(hour);
        local.Minute.Should().Be(min);
        local.Second.Should().Be(sec);
    }

    [Fact]
    public void SevenDays()
    {
        var utc = DateTime.UtcNow.Date;
        var localAnchor = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
        var result = DateRangePreset.CalculateExpressionWithoutAnchor("{{today}}", timeZone, localAnchor);
        result.Should().Be(utc);
        
        var thirty = DateRangePreset.CalculateExpressionWithoutAnchor("{{today -7d}}", timeZone, localAnchor);
        (result - thirty).TotalDays.Should().Be(7);
    }
}