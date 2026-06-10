using System;

namespace Messages.Flow;

public class CreateInvoiceActionOptions : ActionOptions
{
    public Guid? NextEventId { get; set; }
    public Guid? SkipEventId { get; set; }
    
    public override ActionOutput[] Output { get; set; }
    
    /// <summary>
    /// Name (can be a {{xxxx}})
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Entity, if organization will also set OrganizationId
    /// (can be a {{xxxx}})
    /// </summary>
    public string EntityId { get; set; }
    
    /// <summary>
    /// Suffix added to the object id to generate an unique ExternalId (can be a {{xxxx}})
    /// </summary>
    public string ExternalIdSuffix { get; set; }
    
    /// <summary>
    /// Description (can be a {{xxxx}})
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Path to billable item object
    /// </summary>
    public string Item { get; set; }
    
    /// <summary>
    /// Additional Items 
    /// </summary>
    public Guid[] AdditionalItems { get; set; }
}