using System;

namespace PI.Shared.Models;

/// <summary>
/// subset of AppFormLayout
/// </summary>
public class SaveFormLayoutsRequest 
{
    public string Name { get; set; }
    public string Description { get; set; }
    
    /// <summary>
    /// Allow user to override object type (as a derived type of the original)
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// (optional) Use this version for these profiles
    /// </summary>
    public Guid[] ProfileIds { get; set; }

    /// <summary>
    /// optional Role 
    /// </summary>
    public EntityRoleId? Role { get; set; }
    
    public BreakpointLayouts Layouts { get; set; }
}