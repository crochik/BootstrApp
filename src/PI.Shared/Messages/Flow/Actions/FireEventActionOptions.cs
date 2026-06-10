using System;
using System.Dynamic;

namespace Messages.Flow;

public class FireEventActionOptions : ActionOptions
{
    public string ObjectType { get; set; }
    
    public Guid? EventTypeId { get; set; }
    
    /// <summary>
    /// object id to fire event for (expression)
    /// </summary>
    public string ObjectId { get; set; }
    
    /// <summary>
    /// Action Verb for the user action  
    /// </summary>
    public string Action { get; set; }
    
    /// <summary>
    /// user to be used to trigger action (expression)
    /// </summary>
    public string UserId { get; set; }
    
    /// <summary>
    /// Event description (template)
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Parameters to be used to execute the user action
    /// </summary>
    public ExpandoObject Parameters { get; set; }
}