using System.Collections.Generic;
using FluentAssertions;
using PI.Shared.Services;
using Xunit;

namespace UnitTests.Expressions;

public class NextUrlTests
{
    [Fact]
    public void Test()
    {
        var url = "https://www.google.com/maps/dir/?api=1&destination={{Address}},{{City}},{{State}},{{PostalCode}}";
        var context = new Dictionary<string, object>
        {
            { "Address", "107 Sedgemoor Dr" },
            { "City", "Cary" },
            { "State", "NC" },
            { "PostalCode", "27513" }
        };

        var result = ObjectTypeService.ProcessNextUrl(null, context, url);
        result.Should().NotBeSameAs(url);
    }
}