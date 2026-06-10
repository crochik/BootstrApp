using System;
using System.Collections.Generic;
using System.Dynamic;
using FluentAssertions;
using Messages.Flow;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class GenericActionTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public GenericActionTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Serialize()
    {
        var nextEventId = Guid.NewGuid();
        var errorEventId = Guid.NewGuid();

        var options = new UpdateObjectActionOptions
        {
            ObjectId = null, // current object
            Mapping = new Dictionary<string, object>
            {
                { "A", "{{A}}" },
                { "b", "B" },
            },
            Output = new[]
            {
                new ActionOutput
                {
                    EventId = nextEventId,
                    Name = "MapInput",
                    Description = "Request Properties Mapped"
                },
                new ActionOutput
                {
                    EventId = errorEventId,
                    Name = "MapInputError",
                    Description = "Error mapping request properties"
                }
            }
        };

        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        var json = JsonConvert.SerializeObject(options, Formatting.Indented, settings);
        _testOutputHelper.WriteLine(json);
        
        var dict = JsonConvert.DeserializeObject<ExpandoObject>(json, settings);
        json = JsonConvert.SerializeObject(dict, Formatting.Indented, settings);
        _testOutputHelper.WriteLine(json);
        
        var options2 = JsonConvert.DeserializeObject<UpdateObjectActionOptions>(json, settings);
        options2.ObjectId.Should().Be(options.ObjectId);
        options2.Output[0].EventId.Should().Be(options.Output[0].EventId);
        options2.Output[0].Name.Should().Be(options.Output[0].Name);
        options2.Output[0].Description.Should().Be(options.Output[0].Description);
        options2.Output[1].EventId.Should().Be(options.Output[1].EventId);
        options2.Output[1].Name.Should().Be(options.Output[1].Name);
        options2.Output[1].Description.Should().Be(options.Output[1].Description);

        foreach (var kvp in options.Mapping)
        {
            options2.Mapping.TryGetValue(kvp.Key, out var value).Should().BeTrue();
            kvp.Value.Should().Be(value);
        }
        
        var generic = new GenericActionOptions(options);
        options2 = generic.ConvertTo<UpdateObjectActionOptions>();
        
        options2.ObjectId.Should().Be(options.ObjectId);
        options2.Output[0].EventId.Should().Be(options.Output[0].EventId);
        options2.Output[0].Name.Should().Be(options.Output[0].Name);
        options2.Output[0].Description.Should().Be(options.Output[0].Description);
        options2.Output[1].EventId.Should().Be(options.Output[1].EventId);
        options2.Output[1].Name.Should().Be(options.Output[1].Name);
        options2.Output[1].Description.Should().Be(options.Output[1].Description);

        foreach (var kvp in options.Mapping)
        {
            options2.Mapping.TryGetValue(kvp.Key, out var value).Should().BeTrue();
            kvp.Value.Should().Be(value);
        }
        
    }
}