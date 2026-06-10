using Ingress.Configuration;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class BodyFieldValidatorTests
{
    private static AuthConfig ClientState(string expected) =>
        new() { Type = "bodyField", Path = "value[].clientState", Value = expected };

    [Fact]
    public void Accepts_when_all_array_items_match()
    {
        var body = "{\"value\":[{\"clientState\":\"secret\"},{\"clientState\":\"secret\"}]}";
        var context = TestContextFactory.Create(new WebhookDefinition(), body: body);

        Assert.True(new BodyFieldValidator().Validate(context, ClientState("secret")).Succeeded);
    }

    [Fact]
    public void Rejects_when_any_array_item_differs()
    {
        var body = "{\"value\":[{\"clientState\":\"secret\"},{\"clientState\":\"rogue\"}]}";
        var context = TestContextFactory.Create(new WebhookDefinition(), body: body);

        Assert.False(new BodyFieldValidator().Validate(context, ClientState("secret")).Succeeded);
    }

    [Fact]
    public void Validates_simple_dot_path()
    {
        var body = "{\"meta\":{\"token\":\"abc\"}}";
        var config = new AuthConfig { Type = "bodyField", Path = "meta.token", Value = "abc" };
        var context = TestContextFactory.Create(new WebhookDefinition(), body: body);

        Assert.True(new BodyFieldValidator().Validate(context, config).Succeeded);
    }

    [Fact]
    public void Rejects_empty_array()
    {
        var body = "{\"value\":[]}";
        var context = TestContextFactory.Create(new WebhookDefinition(), body: body);

        Assert.False(new BodyFieldValidator().Validate(context, ClientState("secret")).Succeeded);
    }
}
