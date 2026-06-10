using System;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class RegexTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public RegexTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Basic()
    {
        var matches = Regex.Matches("https://{{Host}}/{NotMe}/{{Object.Test}}/{{I am a Formula}}", @"({{[\sa-zA-Z\.\|0-9]+}})");
        foreach (var m in matches)
        {
            _testOutputHelper.WriteLine(m.ToString());
        }
    }

    [Theory]
    [InlineData("1000 · Corporate Checking", true, "1000")]
    [InlineData("1000· Corporate Checking", false)]
    [InlineData("Corporate 1000 Checking", false)]
    [InlineData("1234 Checking", true, "1234")]
    [InlineData("1234 1212 Checking", true, "1234")]
    [InlineData("1 1212 Checking", true, "1")]
    public void QvinciAccount(string str, bool result, string code = null)
    {
        Regex expr = new Regex("^([0-9]+)\\s");
        var m = expr.Match(str);
        m.Success.Should().Be(result);
        if (result)
        {
            m.Groups.Count.Should().Be(2);
            m.Groups[1].Value.Should().Be(code);
        }
    }
}