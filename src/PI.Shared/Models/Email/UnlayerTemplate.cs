using System;

namespace PI.Shared.Models.Email;

public class UnlayerTemplate : FlowObjectModel, ITaggable
{
    public Guid? PreviousVersionId { get; set; }
    public string Design { get; set; }
    public string Html { get; set; }
    public string Plain { get; set; }
    public string[] Tags { get; set; }
    
    /// <summary>
    /// To group templates based on "merge tags"
    /// </summary>
    public string TemplateType { get; set; }
 
    /// <summary>
    /// When the template is only for a specific type
    /// </summary>
    public Guid? TemplatedObjectId { get; set; }
}