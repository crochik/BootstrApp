using System;
using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("app.FormLayout")]
public class AppFormLayout : AppProfileElement
{
    /// <summary>
    /// Object Type that this layout applies to 
    /// For now it is required as we only support saving layouts for object forms
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Name of the form that this layout applies  to 
    /// Form name, for now will be Edit, Add, View
    /// </summary>
    public string FormName { get; set; }
    
    public BreakpointLayouts Layouts { get; set; }
    
    public Guid? ReplacedById { get; set; }
}