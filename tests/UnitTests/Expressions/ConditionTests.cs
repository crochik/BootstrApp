using System;
using System.Collections.Generic;
using FluentAssertions;
using Messages.Flow;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Xunit;

namespace UnitTests.Expressions;

public class ConditionTests
{
    private IDictionary<string, object> Input = new Dictionary<string, object>
    {
        { "A", true },
        { "B", "test" },
        {
            "C", new Dictionary<string, object>
            {
                { "D", "d" },
                {
                    "FlowEvent", new FlowEvent
                    {
                        Description = "event description"
                    }
                }
            }
        },
        { "D", "test" },
        { "Array", new[] { 1, 2, 3 } },
        { "Tags", new[] { "A", "B", "D" } },
        { "NumberAsString", "10" },
        { "Number", 12.1 },
    };

    [Fact]
    public void Basics()
    {
        Condition.IsTrue("A").Evaluate(Input).Should().BeTrue();
        Condition.IsTrue("MissingA").Evaluate(Input).Should().BeFalse();
        Condition.IsTrue("B").Evaluate(Input).Should().BeFalse();

        Condition.IsFalse("A").Evaluate(Input).Should().BeFalse();
        Condition.IsFalse("MissingA").Evaluate(Input).Should().BeFalse();
        Condition.IsFalse("B").Evaluate(Input).Should().BeFalse();

        Condition.Eq("B", "test").Evaluate(Input).Should().BeTrue();
        Condition.Eq("C|D", "d").Evaluate(Input).Should().BeTrue();
        Condition.Eq("{{C.D}}", "d").Evaluate(Input).Should().BeTrue();
        Condition.Eq("C|FlowEvent|Description", "event description").Evaluate(Input).Should().BeTrue();
        Condition.Eq("{{C.FlowEvent.Description}}", "event description").Evaluate(Input).Should().BeTrue();

        Condition.Ne("C|D", "e").Evaluate(Input).Should().BeTrue();
        Condition.Ne("C|E", "e").Evaluate(Input).Should().BeTrue();
        Condition.Ne("C|D", null).Evaluate(Input).Should().BeTrue();
        Condition.Ne("{{C.D}}", null).Evaluate(Input).Should().BeTrue();
        Condition.Ne("C|D", "d").Evaluate(Input).Should().BeFalse();
    }

    [Fact]
    public void Missing()
    {
        Condition.Eq("B|C|D", null).Evaluate(Input).Should().BeTrue();
        Condition.Eq("{{B.C.D}}", null).Evaluate(Input).Should().BeTrue();
        Condition.Ne("B|C|D", null).Evaluate(Input).Should().BeFalse();
        Condition.Ne("{{B.C.D}}", null).Evaluate(Input).Should().BeFalse();

        // optional (expression) is not supported in field names, will evaluate to null
        Condition.Eq("{{B.C.D?}}", null).Evaluate(Input).Should().BeTrue();
        Condition.Ne("{{B.C.D?}}", null).Evaluate(Input).Should().BeFalse();

        // optional is supported when using AllTrue (since resolves the field name as expression) 
        new[]
        {
            Condition.Eq("{{B.C.D?}}", null),
        }.AllTrueUsingExpressions(null, Input).Should().BeTrue();

        new[]
        {
            Condition.Eq("{{B.C.D?}}", null),
        }.AnyFalseUsingExpressions(null, Input).Should().BeFalse();

        new[]
        {
            Condition.Ne("{{B.C.D?}}", null),
        }.AllTrueUsingExpressions(null, Input).Should().BeFalse();

        new[]
        {
            Condition.Ne("{{B.C.D?}}", null),
        }.AnyFalseUsingExpressions(null, Input).Should().BeTrue();
    }

    [Fact]
    public void In()
    {
        Condition.New("B", Operator.In, "test").Evaluate(Input).Should().BeFalse();
        Condition.In("B", "a", "b").Evaluate(Input).Should().BeFalse();
        Condition.In("B", "a", "b", "test").Evaluate(Input).Should().BeTrue();
    }

    [Fact]
    public void NIn()
    {
        Condition.New("B", Operator.Nin, "test").Evaluate(Input).Should().BeTrue();
        Condition.Nin("B", "a", "b").Evaluate(Input).Should().BeTrue();
        Condition.Nin("B", "a", "b", "test").Evaluate(Input).Should().BeFalse();
    }

    [Fact]
    public void JArray()
    {
        var condition = JsonConvert.DeserializeObject<Condition>("{ \"fieldName\": \"C|D\", \"operator\": \"In\", \"value\": [\"a\",\"b\",\"c\", \"d\"]}");
        condition.Evaluate(Input).Should().BeTrue();
    }

    [Fact]
    public void Tags()
    {
        // [] Eq => All in
        Condition.Eq("Tags", new object[] { "A" }).Evaluate(Input).Should().BeTrue();
        Condition.Eq("Tags", new object[] { "A", "B", "D" }).Evaluate(Input).Should().BeTrue();
        Condition.Eq("Tags", new object[] { "A", "B", "C" }).Evaluate(Input).Should().BeFalse();
        Condition.Eq("Tags", new object[] { "C" }).Evaluate(Input).Should().BeFalse();

        // little questionable but...
        Condition.Eq("Tags", "A").Evaluate(Input).Should().BeTrue();

        Condition.In("Tags", new object[] { "C", "A" }).Evaluate(Input).Should().BeTrue();
        Condition.In("Tags", new object[] { "C" }).Evaluate(Input).Should().BeFalse();
        new Condition
        {
            FieldName = "Tags",
            Operator = Operator.In,
            Value = "A"
        }.Evaluate(Input).Should().BeFalse();

        // [] In [] => any in 
        Condition.In("Tags", new object[] { "A", "B", "C" }).Evaluate(Input).Should().BeTrue();
    }

    [Fact]
    public void NotNull()
    {
        Condition.Ne("B", null).Evaluate(Input).Should().BeTrue();
        Condition.Eq("B", null).Evaluate(Input).Should().BeFalse();
        Condition.Ne("NOT_THERE", null).Evaluate(Input).Should().BeFalse();
        Condition.Eq("NOT_THERE", null).Evaluate(Input).Should().BeTrue();

        var fieldValue = Input.ResolvePathValue("B");
        Condition.Ne("B", null).EvaluateValue(fieldValue).Should().BeTrue();
        Condition.Eq("B", null).EvaluateValue(fieldValue).Should().BeFalse();
    }

    [Fact]
    public void AllTrueWithFieldNames()
    {
        var conditions = new[]
        {
            Condition.Ne("B", null),
            Condition.Eq("B", "test"),
            Condition.Eq("A", true),
        };

        conditions.AllTrueUsingExpressions(null, Input).Should().BeTrue();
    }

    [Fact]
    public void AllTrueWithPath()
    {
        var conditions = new[]
        {
            Condition.Ne("{{B}}", null),
            Condition.Eq("{{B}}", "test"),
            Condition.Eq("{{A}}", true),
            Condition.Eq("{{A}}", "{{A}}"),
            Condition.Eq("A", "{{A}}"),
        };

        conditions.AllTrueUsingExpressions(null, Input).Should().BeTrue();
    }

    [Fact]
    public void NumberAsString()
    {
        var conditions = new[]
        {
            Condition.Ne("{{NumberAsString}}", null),
            Condition.Eq("{{NumberAsString}}", "10"),
            Condition.Ne("{{NumberAsString}}", "11"),
            // Condition.New("{{NumberAsString}}", Operator.Gt, 9),
            // Condition.New("{{NumberAsString}}", Operator.Lt,11),
            Condition.Eq("NumberAsString", 10),
        };

        conditions.AllTrueUsingExpressions(null, Input).Should().BeTrue();
    }

    [Fact]
    public void UseExpressionsInFieldNames()
    {
        var userId = Guid.NewGuid();
        var entityContext = UserContext.Admin(userId, "test", AccountIds.CSS, "CLIENT_ID", claims: new Dictionary<string, string[]>
        {
            { "x-quickbooks-id", null },
            { "x-companycam-id", new[] { "not_null" } },
        });

        var conditions = new[]
        {
            Condition.Eq("{{C.D.E?}}", null),
            Condition.Eq("{{context \"Claims.x-quickbooks-id\"}}", null),
            Condition.Ne("{{context Claims.x-companycam-id}}", null),
            Condition.Eq("{{context \"UserId\"}}", userId),
            Condition.Eq("{{context \"AccountId\"}}", AccountIds.CSS),
            Condition.Eq("{{context \"ClientId\"}}", "CLIENT_ID"),
        };

        conditions.AllTrueUsingExpressions(entityContext, Input).Should().BeTrue();

        // expressions that can't be evaluated will throw
        Assert.Throws<FailedToResolveExpressionException>(() =>
            new[]
            {
                Condition.Eq("{{C.D.E}}", null),
            }.AllTrueUsingExpressions(entityContext, Input)
        );

        Assert.Throws<FailedToResolveExpressionException>(() => new[]
            {
                Condition.Ne("{{C.D.E}}", null),
            }.AllTrueUsingExpressions(entityContext, Input)
        );

        Assert.Throws<FailedToResolveExpressionException>(() => new[]
            {
                Condition.Eq("{{C.D.E}}", null),
            }.AnyFalseUsingExpressions(entityContext, Input)
        );

        Assert.Throws<FailedToResolveExpressionException>(() => new[]
            {
                Condition.Ne("{{C.D.E}}", null),
            }.AnyFalseUsingExpressions(entityContext, Input)
        );

        // conditions do not support expressions in field names, so always evaluate to null
        Condition.Eq("{{C.D.E?}}", null).Evaluate(Input).Should().BeTrue();
        Condition.Eq("{{anything odd here}}", null).Evaluate(Input).Should().BeTrue();
        Condition.Eq("{{context \"UserId\"}}", null).Evaluate(Input).Should().BeTrue();
    }

    [Fact]
    void Range()
    {
        // numbers
        Condition.New("NumberAsString", Operator.Eq, 10).Evaluate(Input).Should().BeTrue();
        Condition.New("{{NumberAsString}}", Operator.Eq, 10).Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Eq, "10").Evaluate(Input).Should().BeTrue();
        Condition.New("{{NumberAsString}}", Operator.Eq, "10").Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Ne, 11).Evaluate(Input).Should().BeTrue();
        Condition.New("{{NumberAsString}}", Operator.Ne, 9).Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Ne, "11").Evaluate(Input).Should().BeTrue();
        Condition.New("{{NumberAsString}}", Operator.Ne, "9").Evaluate(Input).Should().BeTrue();
        
        Condition.New("NumberAsString", Operator.Gt, 9).Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lt, 11).Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lte, 9).Evaluate(Input).Should().BeFalse();
        Condition.New("NumberAsString", Operator.Gte, 11).Evaluate(Input).Should().BeFalse();
        
        Condition.New("NumberAsString", Operator.Gte, 10).Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lte, 10).Evaluate(Input).Should().BeTrue();
        
        Condition.New("NumberAsString", Operator.Gt, "9.0").Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lt, "11.0").Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lte, "9.0").Evaluate(Input).Should().BeFalse();
        Condition.New("NumberAsString", Operator.Gte, "11.0").Evaluate(Input).Should().BeFalse();

        // string vs string
        Condition.New("B", Operator.Gt, "test").Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Gte, "test").Evaluate(Input).Should().BeTrue();
        Condition.New("B", Operator.Lt, "test").Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Lte, "test").Evaluate(Input).Should().BeTrue();

        Condition.New("B", Operator.Gt, "rest").Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Gte, "rest").Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Lt, "rest").Evaluate(Input).Should().BeTrue();
        Condition.New("B", Operator.Lte, "rest").Evaluate(Input).Should().BeTrue();

        Condition.New("B", Operator.Gt, "test2").Evaluate(Input).Should().BeTrue();
        Condition.New("B", Operator.Gte, "test2").Evaluate(Input).Should().BeTrue();
        Condition.New("B", Operator.Lt, "test2").Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Lte, "test2").Evaluate(Input).Should().BeFalse();
        
        // mixed: should always be false if it can't compare 
        Condition.New("B", Operator.Gt, 2).Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Gte, 2).Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Lt, 2).Evaluate(Input).Should().BeFalse();
        Condition.New("B", Operator.Lte, 2).Evaluate(Input).Should().BeFalse();
        Condition.New("Number", Operator.Gt, "A").Evaluate(Input).Should().BeFalse();
        Condition.New("Number", Operator.Gte, "A").Evaluate(Input).Should().BeFalse();
        Condition.New("Number", Operator.Lt, "A").Evaluate(Input).Should().BeFalse();
        Condition.New("Number", Operator.Lte, "A").Evaluate(Input).Should().BeFalse();
        
        // if can't parse both string into numbers will compare as strings
        Condition.New("NumberAsString", Operator.Gte, "9a").Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lte, "0zzz").Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Gt, "2x").Evaluate(Input).Should().BeTrue();
        Condition.New("NumberAsString", Operator.Lt, "0.a").Evaluate(Input).Should().BeTrue();
    }
    
// TODO: compare arrays?
    // [Fact]
    // public void Array()
    // {
    //     Condition.Eq("Array", new[] { 1, 2, 3 }).AnyTrue(Input).Should().BeTrue();
    // }
}