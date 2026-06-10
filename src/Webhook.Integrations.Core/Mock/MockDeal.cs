using Webhook.Integrations.Core.Catalog;

namespace Webhook.Integrations.Core.Mock;

/// <summary>
/// A mock sales deal. It declares explicit events, showing that an object can expose
/// a bespoke event set (here: created, stage changed, won, lost) instead of the
/// default lifecycle — again with no changes to the controllers or integrations.
/// </summary>
[TriggerObject(Key = "deal", Label = "Deal", Noun = "Deal", Description = "An opportunity in the sales pipeline.")]
[TriggerEvent("created", Label = "Deal Created", Description = "Fires when a new deal is created.")]
[TriggerEvent("stage_changed", Label = "Deal Stage Changed", Description = "Fires when a deal moves to a new pipeline stage.")]
[TriggerEvent("won", Label = "Deal Won", Description = "Fires when a deal is marked as won.")]
[TriggerEvent("lost", Label = "Deal Lost", Description = "Fires when a deal is marked as lost.")]
public sealed class MockDeal
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string Stage { get; set; } = "";
    public string OwnerEmail { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
