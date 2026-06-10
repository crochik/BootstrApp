using Webhook.Integrations.Core.Catalog;

namespace Webhook.Integrations.Core.Mock;

/// <summary>
/// A mock task/to-do. Mixes the lifecycle with a domain-specific "completed" event.
/// </summary>
[TriggerObject(Key = "task", Label = "Task", Description = "A to-do item assigned to a user.")]
[TriggerEvent("created")]
[TriggerEvent("completed", Label = "Task Completed", Description = "Fires when a task is marked complete.")]
public sealed class MockTask
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string AssigneeEmail { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
}
