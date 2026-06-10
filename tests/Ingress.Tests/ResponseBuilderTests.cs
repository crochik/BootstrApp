using Ingress.Configuration;
using Ingress.Engine;
using Ingress.Responses;
using Xunit;

namespace Ingress.Tests;

public class ResponseBuilderTests
{
    [Fact]
    public void Substitutes_uuid_and_name_tokens()
    {
        var definition = new WebhookDefinition
        {
            Uuid = "abc", Name = "demo",
            Response = { Body = "id={{uuid}} name={{name}}" }
        };
        var context = TestContextFactory.Create(definition);

        var built = ResponseBuilder.Build(context, WebhookResult.Default);

        Assert.Equal("id=abc name=demo", built.Body);
    }

    [Fact]
    public void Substitutes_json_path_token()
    {
        var definition = new WebhookDefinition
        {
            Response = { Body = "user={{json:data.user.id}}" }
        };
        var context = TestContextFactory.Create(
            definition, body: "{\"data\":{\"user\":{\"id\":\"42\"}}}");

        var built = ResponseBuilder.Build(context, WebhookResult.Default);

        Assert.Equal("user=42", built.Body);
    }

    [Fact]
    public void Handler_override_wins_over_config()
    {
        var definition = new WebhookDefinition { Response = { Status = 200, Body = "OK" } };
        var context = TestContextFactory.Create(definition);

        var built = ResponseBuilder.Build(context, WebhookResult.Custom(202, "queued", "text/plain"));

        Assert.Equal(202, built.Status);
        Assert.Equal("queued", built.Body);
    }
}
