namespace PI.Shared.Integrations.Catalog;

/// <summary>
/// A business object an integration can subscribe to (e.g. <c>Lead</c>) together with
/// the events it emits. Produced by <see cref="IObjectCatalog"/> from the account's
/// real <c>ObjectType</c> definitions, so adding an object type in the platform is all
/// it takes to surface a new trigger — nothing here, in the controllers or in the
/// integration apps enumerates objects by hand.
/// </summary>
public sealed record TriggerObjectDescriptor(
    string Key,
    string Label,
    string Description);

/// <summary>
/// A single event an object can emit. Keyed by the platform's lifecycle vocabulary
/// (<c>Create</c>/<c>Update</c>/<c>Delete</c>, matching <c>FlowObjectEventRoute</c>) so
/// the key stored on a subscription lines up with the routing key the event listener
/// receives.
/// </summary>
public sealed record TriggerEventDescriptor(string Key, string Label, string Description);
