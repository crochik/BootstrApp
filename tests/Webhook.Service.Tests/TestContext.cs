using System.Text;
using Webhook.Service.Configuration;
using Webhook.Service.Engine;

namespace Webhook.Service.Tests;

/// <summary>Helpers to construct <see cref="WebhookContext"/> instances in tests.</summary>
internal static class TestContextFactory
{
    public static WebhookContext Create(
        WebhookDefinition definition,
        string body = "",
        string method = "POST",
        IDictionary<string, string>? headers = null,
        IDictionary<string, string>? query = null,
        string? remoteIp = null,
        string? requestUrl = null)
    {
        return new WebhookContext
        {
            Definition = definition,
            Method = method,
            RawBody = Encoding.UTF8.GetBytes(body),
            Headers = new Dictionary<string, string>(
                headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
            Query = new Dictionary<string, string>(
                query ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
            RemoteIp = remoteIp,
            RequestUrl = requestUrl
        };
    }
}
