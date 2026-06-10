using System.Net;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.Delivery;
using Webhook.Publisher.Storage;
using Xunit;

namespace Webhook.Publisher.Tests;

public class HttpWebhookDeliveryClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) => _responder = responder;

        public static StubHandler Returns(HttpStatusCode code) =>
            new((_, _) => Task.FromResult(new HttpResponseMessage(code)));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return await _responder(request, cancellationToken);
        }
    }

    private static (HttpWebhookDeliveryClient client, StubHandler handler) Build(
        StubHandler handler, TimeSpan? timeout = null)
    {
        var options = Options.Create(new WebhookPublisherOptions
        {
            Delivery = { HttpTimeout = timeout ?? TimeSpan.FromSeconds(5) },
        });
        var client = new HttpWebhookDeliveryClient(new HttpClient(handler), new HmacWebhookSigner(), options);
        return (client, handler);
    }

    private static WebhookDelivery Delivery() => new()
    {
        Id = "d1",
        EventId = "e1",
        Url = "https://sub.example/hook",
        Secret = "shh",
        SignatureHeader = "Webhook-Signature",
    };

    private static WebhookEvent Event() => new()
    {
        Id = "e1",
        TenantId = "t1",
        EventName = "order.created",
        OccurredAt = DateTime.UtcNow,
        Payload = BsonDocument.Parse("{\"orderId\":1}"),
    };

    [Fact]
    public async Task Success_returns_delivered_with_signed_headers_and_envelope_body()
    {
        var (client, handler) = Build(StubHandler.Returns(HttpStatusCode.OK));

        var result = await client.DeliverAsync(Delivery(), Event());

        Assert.Equal(DeliveryOutcome.Delivered, result.Outcome);
        Assert.Equal(200, result.StatusCode);

        Assert.True(handler.LastRequest!.Headers.TryGetValues("Webhook-Signature", out var sig));
        Assert.StartsWith("t=", Assert.Single(sig));
        Assert.Equal("e1", Assert.Single(handler.LastRequest.Headers.GetValues("Webhook-Id")));
        Assert.Equal("order.created", Assert.Single(handler.LastRequest.Headers.GetValues("Webhook-Event")));
        Assert.Contains("\"eventId\":\"e1\"", handler.LastBody);
        Assert.Contains("\"orderId\":1", handler.LastBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task Retryable_status_codes_map_to_retryable(HttpStatusCode code)
    {
        var (client, _) = Build(StubHandler.Returns(code));
        var result = await client.DeliverAsync(Delivery(), Event());
        Assert.Equal(DeliveryOutcome.RetryableFailure, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Client_error_codes_map_to_permanent(HttpStatusCode code)
    {
        var (client, _) = Build(StubHandler.Returns(code));
        var result = await client.DeliverAsync(Delivery(), Event());
        Assert.Equal(DeliveryOutcome.PermanentFailure, result.Outcome);
    }

    [Fact]
    public async Task Network_exception_is_retryable()
    {
        var handler = new StubHandler((_, _) => throw new HttpRequestException("connection refused"));
        var (client, _) = Build(handler);

        var result = await client.DeliverAsync(Delivery(), Event());

        Assert.Equal(DeliveryOutcome.RetryableFailure, result.Outcome);
    }

    [Fact]
    public async Task Timeout_is_retryable()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var (client, _) = Build(handler, timeout: TimeSpan.FromMilliseconds(50));

        var result = await client.DeliverAsync(Delivery(), Event());

        Assert.Equal(DeliveryOutcome.RetryableFailure, result.Outcome);
    }
}
