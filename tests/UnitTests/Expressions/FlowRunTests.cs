using System.Collections.Generic;
using FluentAssertions;
using Messages.Flow;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Xunit;

namespace UnitTests.Expressions;

public class FlowRunTests
{
    private FlowRun Run => new FlowRun
    {
        ObjectType = "Test",
        Objects = new Dictionary<string, ObjectWithType>
        {
            {
                "Test", new ObjectWithType
                {
                    ObjectType = "Test",
                    Object = new Dictionary<string, object>()
                    {
                        { "Path|To|Field", "TestValue" },
                        { "Boolean|Value", true },
                    }
                }
            }
        }
    };

    private FlowEvent Event => new FlowEvent
    {
    };

    private IDictionary<string, object> Context => Run.BuildHandlebarsContext(Event);

    [Fact]
    public void ContextIsDictionary()
    {
        Context.Should().NotBeNull();
    }

    [Fact]
    public void Criteria()
    {
        var criteria = new Criteria
        {
            Conditions = new[]
            {
                Condition.Eq("{{Objects.Test.Path|To|Field}}", "TestValue"),
                Condition.Eq("{{Objects.Test.Boolean|Value}}", true),
            }
        };

        criteria.Conditions.AllTrueUsingExpressions(null, Context).Should().BeTrue();
    }
}