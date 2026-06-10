namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// A single event an object can emit (e.g. <c>created</c>). Built from a
/// <see cref="TriggerEventAttribute"/> or the default lifecycle — never hardcoded
/// in the API layer.
/// </summary>
public sealed record TriggerEventDescriptor(string Key, string Label, string Description);
