namespace Webhook.Integrations.Core.Catalog;

/// <summary>
/// Produces example data for an object. Integrations show this as "sample data"
/// while a user builds a trigger and use it to map fields, so the shape must match
/// what is actually delivered.
/// </summary>
public interface ISampleFactory
{
    /// <summary>
    /// A representative object body — the <c>data</c> that is published and ends up
    /// inside the delivered envelope.
    /// </summary>
    IDictionary<string, object?> CreateData(TriggerObjectDescriptor descriptor);

    /// <summary>
    /// The full envelope a subscriber receives, mirroring the
    /// <c>Webhook.Publisher</c> delivery payload
    /// (<c>eventId</c>/<c>tenantId</c>/<c>eventName</c>/<c>occurredAt</c>/<c>schemaVersion</c>/<c>data</c>).
    /// </summary>
    IDictionary<string, object?> CreateDeliveredSample(TriggerObjectDescriptor descriptor, string eventKey, string tenant);
}
