using Webhook.Integrations.Core.Catalog;

namespace Webhook.Integrations.Core.Mock;

/// <summary>
/// A mock CRM contact. It declares no events, so the catalog exposes the default
/// created / updated / deleted lifecycle. Adding this class is the <em>only</em>
/// thing needed for "Contact" and its three events to appear in every integration.
/// </summary>
[TriggerObject(Key = "contact", Label = "Contact", Description = "A person in the CRM.")]
public sealed class MockContact
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Company { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
