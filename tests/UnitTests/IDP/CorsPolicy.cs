using Services;
using Xunit;
using System.Linq;
using FluentAssertions;

namespace UnitTests.IDP;

public class CorsPolicy
{
    [Fact]
    public void Test()
    {
        var list = CorsPolicyService.DeriveHosts("http://API.fci.cloud").ToArray();
        list.Contains("http://api.fci.cloud").Should().BeTrue();
        list.Contains("https://api.fci.cloud").Should().BeTrue();
        list.Contains("api.fci.cloud").Should().BeTrue();
        list.Contains("*.fci.cloud").Should().BeTrue();
    }
}