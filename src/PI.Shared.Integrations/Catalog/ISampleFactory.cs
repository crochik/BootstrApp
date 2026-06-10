using PI.Shared.Models;

namespace PI.Shared.Integrations.Catalog;

/// <summary>
/// Produces example data for an object. Integrations show this as "sample data" while
/// a user builds a trigger and use it to map fields, so the shape mirrors what is
/// actually delivered (the signed envelope from the delivery pipeline).
/// </summary>
public interface ISampleFactory
{
    /// <summary>
    /// A representative object body — the <c>data</c> that is published and ends up
    /// inside the delivered envelope. <c>null</c> when the object key is unknown.
    /// </summary>
    Task<IDictionary<string, object?>?> CreateDataAsync(IEntityContext context, string objectKey);

    /// <summary>
    /// The full envelope a subscriber receives
    /// (<c>eventId</c>/<c>tenantId</c>/<c>eventName</c>/<c>occurredAt</c>/<c>schemaVersion</c>/<c>data</c>),
    /// mirroring <see cref="Delivery.WebhookPayload"/>. <c>null</c> when the object key is unknown.
    /// </summary>
    Task<IDictionary<string, object?>?> CreateDeliveredSampleAsync(IEntityContext context, string objectKey, string eventKey, string tenant);
}
