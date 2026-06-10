using System;
using System.Collections.Generic;
using Controllers.Scheduler;
using FluentAssertions;
using PI.Shared.Models;
using PI.Shared.Services;
using Xunit;

namespace UnitTests;

public class NormalizePostalCodeTest
{
    [Theory]
    [InlineData("12345", "12345")]
    [InlineData("12345-222", "12345")]
    [InlineData("123", null)]
    [InlineData("1234", "01234")]
    [InlineData("01234", "01234")]
    [InlineData("2a234", null)]
    [InlineData("31b34", null)]
    [InlineData("4213a", null)]
    [InlineData("a1b 2a3", "A1B")]
    [InlineData("a1b", "A1B")]
    [InlineData("a1b-3cs", "A1B")]
    [InlineData("a1b-blah-blah", "A1B")]
    [InlineData("A1B-blah-blah", "A1B")]
    public void TesT(string input, string expected)
    {
        var output = PI.Shared.Models.Lead.GetPostalCodeForLookup(input);
        output.Should().Be(expected);
    }

    // [Fact]
    // this can't work because there is no database 
    // just debug and add a breakpoint to make sure it is doing what it should :)
    // ...
    private void ValueMapperService()
    {
        var service = new ValueMapperService(new FakeLogger<ValueMapperService>(), null, null, null, null);
        var config = new FieldMapperConfig
        {
            Source  = "postalCode"
        };
        
        var mapper = service.CreateMaper(config, "", new[] { "CustomObject", "EntityId", "ObjectType=='ZeeTerritory'", "ExternalId==postalCode" });
        
        var result = mapper(null, null, new Lead
        {
            Properties = new Dictionary<string, object>
            {
                { "postalCode", "A1B-blah-bla" }
            }
        });

        result.Should().BeNull();
    }
}