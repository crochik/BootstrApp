namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// Declares an event a <see cref="TriggerObjectAttribute"/> type emits. Apply it
/// multiple times to one class to expose several events. When a decorated type
/// declares no events the catalog falls back to the conventional
/// created / updated / deleted lifecycle, so most objects need no annotation at all.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class TriggerEventAttribute : Attribute
{
    public TriggerEventAttribute(string key)
    {
        Key = key;
    }

    /// <summary>Stable machine key for the event (e.g. <c>won</c>).</summary>
    public string Key { get; }

    /// <summary>Human label (e.g. <c>Deal Won</c>). Defaults to the noun + humanized key.</summary>
    public string? Label { get; init; }

    /// <summary>Short description surfaced to the end user.</summary>
    public string? Description { get; init; }
}
