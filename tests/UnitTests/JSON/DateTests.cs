using System;
using System.Collections.Generic;
using System.Dynamic;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.JSON;

public class DateTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public DateTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void DeserializeDate()
    {
        var json = @"
{
    ""CreatedDate"": ""2017-05-30T21:10:25.000z"",
    ""CreatedById"": ""00541000002smVGAAY"",
    ""LastModifiedDate"": ""2017-05-30T21:10:25.000+0000"",
    ""LastModifiedById"": ""00541000002smVGAAY"",
    ""SystemModstamp"": ""2017-05-31T10:39:47.000+0000"",
    ""A"": { ""B"": ""2017-05-31T10:39:47.000+0000"" } 
}
        ";

        // var expando = JsonConvert.DeserializeObject<ExpandoObject>(json);
        // _testOutputHelper.WriteLine($"{expando.LastModifiedDate}");
        // (expando.LastModifiedDate is DateTime).Should().BeTrue();

        var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };
        var dict = JsonConvert.DeserializeObject<Dictionary<string,object>>(json, settings);
        var date = dict["LastModifiedDate"] as DateTime?;
        date.HasValue.Should().BeTrue();
        _testOutputHelper.WriteLine($"{date} {date.Value.Kind}");

        date = dict["CreatedDate"] as DateTime?;
        date.HasValue.Should().BeTrue();
        _testOutputHelper.WriteLine($"{date} {date.Value.Kind}");

    }
}