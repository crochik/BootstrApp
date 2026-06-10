using FluentAssertions;
using Xunit;

namespace UnitTests.Expressions;

public class LoadRelatedObjectsTest
{
    [Theory]
    [InlineData("{{Test}}", "DEFAULT", "Test")]
    [InlineData("{{Father}}.{{Child}}", "Father", "Child")]
    [InlineData("{{Objects.Father|Other}}.{{ComplicatedChild}}", "Objects.Father|Other", "ComplicatedChild")]
    public void Test(string expression, string expectedParent, string expectedRelatedObject)
    {
        var parts = expression.Split("}}.{{");
        var parent = parts.Length == 1 ? "DEFAULT" : parts[0][2..];
        var relatedObject = parts.Length == 1 ? parts[0][2..^2] : parts[^1][..^2];
        parent.Should().Be(expectedParent);
        relatedObject.Should().Be(expectedRelatedObject);
    }
}