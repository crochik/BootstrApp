using System.Collections.Generic;
using FluentAssertions;
using Messages.Flow;
using PI.Shared.Models.Expressions;
using Xunit;

namespace UnitTests.Expressions;

public class DictionaryTests
{
    IDictionary<string, object> Context => new Dictionary<string, object>
    {
        { "A", "A" },
        {
            "B", new Dictionary<string, object>
            {
                { "NULL", null },
                { "C", "C" },
                {
                    "FlowEvent", new FlowEvent
                    {
                        Action = "Test",
                    }
                }
            }
        },
    };

    [Theory]
    [InlineData("A", "A")]
    [InlineData("A|B")]
    [InlineData("B|C", "C")]
    [InlineData("B|NULL")]
    [InlineData("B|FlowEvent|Action", "Test")]
    [InlineData("B|FlowEvent|Actor|ElementType", null)]
    private void EvaluateOptional(string path, object expectedValue = null)
    {
        var value = Context.GetFieldValue(path);
        expectedValue.Should().Be(value);
    }

    [Theory]
    [InlineData("A|B", false)]
    [InlineData("B|FlowEvent|Actor|ElementType", false, null)]
    [InlineData("B|NULL", true)]
    private void EvaluateRequired(string path, bool expected, object expectedValue = null)
    {
        if (Context.TryGetFieldValue(path, out var value))
        {
            expected.Should().BeTrue();
            expectedValue.Should().Be(value);
            return;
        }

        expected.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("A|B")]
    [InlineData("B|FlowEvent|Actor|ElementType")]
    [InlineData("B|NULL")]
    private void Default(string path)
    {
        var value = Context.GetFieldValue(path, "DEFAULT");
        value.Should().Be("DEFAULT");
    }    
}