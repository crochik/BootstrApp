using Crochik.Mongo;
using PI.Shared.Models.Layout;

namespace PI.Shared.Models;

[BsonCollection("ObjectType.UserSettings")]
public class ObjectTypeUserSettings : EntityOwnedModel, ITaggable
{
    /// <summary>
    /// arbitrary hash to identify different views for the same object
    /// </summary>
    public string Hash { get; set; }

    public string[] Fields { get; set; }

    public string ObjectType { get; set; }

    public string OrderBy { get; set; }

    public string[] Tags { get; set; }

    public ScreenBreakpoint? Breakpoint { get; set; }
}