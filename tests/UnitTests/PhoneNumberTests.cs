using FluentAssertions;
using PI.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class PhoneNumberTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public PhoneNumberTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("+1(678)-570-3872", true)]
    [InlineData("+16785703872", true)]
    [InlineData("+1 (678) 570-3872", true)]
    [InlineData("+1 678 570 3872", true)]
    [InlineData("1 678 570 3872", true)]
    [InlineData("678 570 3872", true)]
    // [InlineData("570 3872", false)]
    public void Test(string phoneNumber, bool result)
    {
        if (PhoneNumber.TryParse(phoneNumber, out var parsed))
        {
            _testOutputHelper.WriteLine(parsed.International);
            result.Should().BeTrue();
        }
        else
        {
            result.Should().BeFalse();
        }
    }
}