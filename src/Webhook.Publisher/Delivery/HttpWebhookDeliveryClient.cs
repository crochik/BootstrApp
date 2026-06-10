using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Webhook.Publisher.Configuration;
using Webhook.Publisher.Storage;

namespace Webhook.Publisher.Delivery;

/// <summary>
/// Typed-client delivery over HTTP. Uses <see cref="HttpClient"/> from
/// <c>IHttpClientFactory</c> (pooled handlers, no socket exhaustion), signs the body
/// and applies a per-attempt timeout.
/// </summary>
public sealed class HttpWebhookDeliveryClient : IWebhookDeliveryClient
{
    private readonly HttpClient _httpClient;
    private readonly IWebhookSigner _signer;
    private readonly DeliveryOptions _options;

    public HttpWebhookDeliveryClient(HttpClient httpClient, IWebhookSigner signer, IOptions<WebhookPublisherOptions> options)
    {
        _httpClient = httpClient;
        _signer = signer;
        _options = options.Value.Delivery;
    }

    public async Task<DeliveryResult> DeliverAsync(WebhookDelivery delivery, WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        var body = WebhookPayload.Build(webhookEvent);
        var timestamp = DateTimeOffset.UtcNow;
        var signature = _signer.Sign(body, delivery.Secret, timestamp);

        using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Url)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation(delivery.SignatureHeader, signature);
        request.Headers.TryAddWithoutValidation("Webhook-Id", webhookEvent.Id);
        request.Headers.TryAddWithoutValidation("Webhook-Event", webhookEvent.EventName);
        request.Headers.TryAddWithoutValidation("Webhook-Timestamp", timestamp.ToUnixTimeSeconds().ToString());

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.HttpTimeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            stopwatch.Stop();

            var status = (int)response.StatusCode;
            var outcome = MapStatus(response.StatusCode);
            var error = outcome == DeliveryOutcome.Delivered ? null : $"HTTP {status} {response.ReasonPhrase}";
            return new DeliveryResult(outcome, status, error, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown / caller cancellation — not a delivery failure.
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new DeliveryResult(DeliveryOutcome.RetryableFailure, null, "Request timed out", stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new DeliveryResult(DeliveryOutcome.RetryableFailure, null, ex.Message, stopwatch.ElapsedMilliseconds);
        }
    }

    private static DeliveryOutcome MapStatus(HttpStatusCode code)
    {
        var status = (int)code;
        if (status is >= 200 and < 300)
        {
            return DeliveryOutcome.Delivered;
        }

        if (status == 408 || status == 429 || status >= 500)
        {
            return DeliveryOutcome.RetryableFailure;
        }

        return DeliveryOutcome.PermanentFailure;
    }
}
