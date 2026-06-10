using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using PI.Shared.Services;
using Xunit;

namespace UnitTests.Expressions;

public class AggregateSubDocumentsTest
{
    [Fact]
    public void Test()
    {
        var input = new Dictionary<string, object>()
        {
            { "A", "a" },
            { "Properties.A", "pa" },
            { "Properties.B", "pb" },
            { "B.C.D", "bcd" },
            { "B.C.E", "bce" },
        };

        var output = ObjectTypeService.AggregateSubDocumentsForCreation(input);
        output.IsSuccess.Should().BeTrue();
        output.Value.Count.Should().Be(3);
        output.Value["A"].Should().Be("a");
        (output.Value["Properties"] is IDictionary<string, object>).Should().BeTrue();
        ((IDictionary<string, object>)output.Value["Properties"])["A"].Should().Be("pa");
        ((IDictionary<string, object>)output.Value["Properties"])["B"].Should().Be("pb");
        (output.Value["B"] is IDictionary<string, object>).Should().BeTrue();
        (((IDictionary<string, object>)output.Value["B"])["C"] is IDictionary<string, object>).Should().BeTrue();
        ((IDictionary<string, object>)((IDictionary<string, object>)output.Value["B"])["C"])["D"].Should().Be("bcd");
        ((IDictionary<string, object>)((IDictionary<string, object>)output.Value["B"])["C"])["E"].Should().Be("bce");
    }
    
    [Fact]
    public void Test2()
    {
        var input = new Dictionary<string, object>()
        {
            { "A", "a" },
            { "A.1", "pa" },
        };

        var output = ObjectTypeService.AggregateSubDocumentsForCreation(input);
        output.IsError.Should().BeTrue();
    }
    
}