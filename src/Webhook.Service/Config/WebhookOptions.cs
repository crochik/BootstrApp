using Webhook.Service.Configuration;

namespace Webhook.Service.Config;

/// <summary>
/// Strongly typed options bound from the <c>Webhooks</c> configuration section.
/// Backing the store with <c>IOptionsMonitor</c> gives hot-reload for free when
/// the underlying JSON file is changed (reloadOnChange is enabled in Program.cs).
/// </summary>
public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    public List<WebhookDefinition> Definitions { get; set; } = new();
}
