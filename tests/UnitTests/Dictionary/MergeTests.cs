using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Services;
using Xunit;
using Xunit.Abstractions;

public class MergeTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MergeTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    IDictionary<string, object> Into => new Dictionary<string, object>
    {
        { "A", "a" },
        { "B", "b" },
        {
            "C", new Dictionary<string, object>
            {
                { "C1", "c1" },
                { "C2", "c2" },
            }
        }
    };

    IDictionary<string, object> From => new Dictionary<string, object>
    {
        { "B", "modified b" },
        { "D", "added d" },
        {
            "C", new Dictionary<string, object>
            {
                { "C2", "modified c2" },
                { "C5", "added c5" }
            }
        },
        {
            "Array", new object[]
            {
                "element1",
                "element2"
            }
        }
    };

    [Fact]
    public void MergeDicts()
    {
        var result = ObjectTypeService.Merge(Into, From);
        Assert(result);
    }

    [Fact]
    public void MergeExpando()
    {
        var into = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(Into));
        var from = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(From));
        var result = ObjectTypeService.Merge(into, from);
        Assert(result);
    }

    [Fact]
    public void Mismatch1()
    {
        var into = Into;
        var from = From;
        into["C"] = "not object";

        var into2 = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(into));
        var from2 = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(from));

        var result = ObjectTypeService.Merge(into,from);
        result.IsError.Should().BeTrue();
        _testOutputHelper.WriteLine(result.Status);
        
        result = ObjectTypeService.Merge(into2,from2);
        result.IsError.Should().BeTrue();
        _testOutputHelper.WriteLine(result.Status);
    }

    [Fact]
    public void Mismatch2()
    {
        var into = Into;
        var from = From;
        from["C"] = "not object";

        var into2 = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(into));
        var from2 = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(from));

        var result = ObjectTypeService.Merge(into,from);
        result.IsError.Should().BeTrue();
        _testOutputHelper.WriteLine(result.Status);
        
        result = ObjectTypeService.Merge(into2,from2);
        result.IsError.Should().BeTrue();
        _testOutputHelper.WriteLine(result.Status);
    }

    [Fact]
    public void NotSupportedArray()
    {
        var into = Into;
        var from = From;
        into["Array"] = new object[]
        {
            "x1",
            "x2"
        };

        var into2 = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(into));
        var from2 = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(from));

        var result = ObjectTypeService.Merge(into,from);
        result.IsError.Should().BeTrue();
        _testOutputHelper.WriteLine(result.Status);
        
        result = ObjectTypeService.Merge(into2,from2);
        result.IsError.Should().BeTrue();
        _testOutputHelper.WriteLine(result.Status);
    }

    private void Assert(Result<IDictionary<string, object>> result)
    {
        result.IsSuccess.Should().BeTrue();
        result.Value["A"].Should().Be("a");
        result.Value["B"].Should().Be("modified b");
        (result.Value["C"] is IDictionary<string, object>).Should().BeTrue();

        if (result.Value["C"] is IDictionary<string, object> cDict)
        {
            cDict["C1"].Should().Be("c1");
            cDict["C2"].Should().Be("modified c2");
            cDict["C5"].Should().Be("added c5");
        }

        result.Value["D"].Should().Be("added d");

        (result.Value["Array"] is IEnumerable<object>).Should().BeTrue();
        if (result.Value["Array"] is IEnumerable<object> enumerable)
        {
            enumerable.First().Should().Be("element1");
        }
    }
}