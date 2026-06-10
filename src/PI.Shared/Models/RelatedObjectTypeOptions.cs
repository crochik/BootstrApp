using System;

namespace PI.Shared.Models;

public class RelatedObjectTypeOptions
{
    /// <summary>
    /// Whether to auto-expand (on a dataPage for the parent object)
    /// </summary>
    public bool AutoExpand { get; set; }

    /// <summary>
    /// Component/Style to be used to render
    /// TODO: NOT USED? 
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Template to build url using other fields to get to object
    /// OVERRIDES the detail click on the dataView ?
    /// TODO: NOT USED?
    /// </summary>
    public string LinkUrl { get; set; }

    /// <summary>
    /// List of fields to display by default
    /// TODO: NOT USED?
    /// </summary>
    public string[] Fields { get; set; }
    
    /// <summary>
    /// When set and "all true", trying to view/edit an object will automatic redirect to this
    /// TODO: NOT IMPLEMENTED YET!
    /// </summary>
    public Criteria AutoRedirect { get; set; }
}