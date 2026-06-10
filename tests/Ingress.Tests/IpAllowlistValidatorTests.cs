using System.Net;
using Ingress.Configuration;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class IpAllowlistValidatorTests
{
    [Theory]
    [InlineData("173.252.10.5", "173.252.0.0/16", true)]
    [InlineData("173.253.0.1", "173.252.0.0/16", false)]
    [InlineData("10.0.0.1", "10.0.0.1/32", true)]
    [InlineData("10.0.0.2", "10.0.0.1/32", false)]
    [InlineData("192.168.1.50", "192.168.1.0/24", true)]
    public void Cidr_matching(string ip, string range, bool expected)
    {
        Assert.Equal(expected, IpAllowlistValidator.IsInRange(IPAddress.Parse(ip), range));
    }

    [Fact]
    public void Validates_remote_ip_against_ranges()
    {
        var config = new AuthConfig { Type = "ipAllowlist", Ranges = { "192.0.2.0/24" } };
        var allowed = TestContextFactory.Create(new WebhookDefinition(), remoteIp: "192.0.2.44");
        var blocked = TestContextFactory.Create(new WebhookDefinition(), remoteIp: "198.51.100.1");

        var validator = new IpAllowlistValidator();
        Assert.True(validator.Validate(allowed, config).Succeeded);
        Assert.False(validator.Validate(blocked, config).Succeeded);
    }

    [Fact]
    public void Uses_forwarded_for_when_trusted()
    {
        var config = new AuthConfig
        {
            Type = "ipAllowlist", TrustForwardedFor = true, Ranges = { "203.0.113.0/24" }
        };
        var context = TestContextFactory.Create(
            new WebhookDefinition(),
            headers: new Dictionary<string, string> { ["X-Forwarded-For"] = "203.0.113.7, 10.0.0.1" },
            remoteIp: "10.0.0.1");

        Assert.True(new IpAllowlistValidator().Validate(context, config).Succeeded);
    }
}
