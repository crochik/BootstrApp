using Crochik.Mongo;

namespace PI.Shared.Models.Dashboards;

[BsonCollection("bi.Dashboard")]
public class Dashboard : EntityOwnedModel, ITaggable
{
    public string Xml { get; set; }
    public string[] Tags { get; set; }
    public bool IsActive { get; set; } = true;
}