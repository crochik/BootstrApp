namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// A business object an integration can subscribe to (e.g. <c>contact</c>) together
/// with the events it emits. Produced by <see cref="IEventCatalog"/> at runtime so
/// adding a new decorated type is all it takes to surface a new object.
/// </summary>
public sealed record TriggerObjectDescriptor(
    string Key,
    string Label,
    string Noun,
    string Description,
    IReadOnlyList<TriggerEventDescriptor> Events,
    Type ClrType);
