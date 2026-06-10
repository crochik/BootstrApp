using System.Text;
using Ingress.Configuration;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class BasicAndTokenValidatorTests
{
    [Fact]
    public void Basic_accepts_correct_credentials()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:secret"));
        var config = new AuthConfig { Type = "basic", Username = "alice", Password = "secret" };
        var context = TestContextFactory.Create(
            new WebhookDefinition(),
            headers: new Dictionary<string, string> { ["Authorization"] = "Basic " + credentials });

        Assert.True(new BasicAuthValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Basic_rejects_wrong_password()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:wrong"));
        var config = new AuthConfig { Type = "basic", Username = "alice", Password = "secret" };
        var context = TestContextFactory.Create(
            new WebhookDefinition(),
            headers: new Dictionary<string, string> { ["Authorization"] = "Basic " + credentials });

        Assert.False(new BasicAuthValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Bearer_accepts_matching_token()
    {
        var config = new AuthConfig { Type = "bearer", Token = "abc123" };
        var context = TestContextFactory.Create(
            new WebhookDefinition(),
            headers: new Dictionary<string, string> { ["Authorization"] = "Bearer abc123" });

        Assert.True(new TokenHeaderValidator("bearer").Validate(context, config).Succeeded);
    }

    [Fact]
    public void ApiKey_accepts_matching_header()
    {
        var config = new AuthConfig { Type = "apikey", Header = "X-Api-Key", Token = "k-42" };
        var context = TestContextFactory.Create(
            new WebhookDefinition(),
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "k-42" });

        Assert.True(new TokenHeaderValidator("apikey").Validate(context, config).Succeeded);
    }

    [Fact]
    public void ApiKey_rejects_wrong_header_value()
    {
        var config = new AuthConfig { Type = "apikey", Header = "X-Api-Key", Token = "k-42" };
        var context = TestContextFactory.Create(
            new WebhookDefinition(),
            headers: new Dictionary<string, string> { ["X-Api-Key"] = "nope" });

        Assert.False(new TokenHeaderValidator("apikey").Validate(context, config).Succeeded);
    }
}
