using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

/// <summary>
/// Dynamic links to internal pages for object type
/// </summary>
[BsonCollection("app.PageLink")]
public class AppPageLink : AppProfileElement, IObjectTypeProfileElement
{
    /// <summary>
    /// Object Type that this link applies to
    /// </summary>
    public string ObjectType { get; set; }
    
    /// <summary>
    /// Dynamic Url to be resolved using ExpressionEvaluationService + Object
    /// </summary>
    public string Url { get; set; }
    
    /// <summary>
    /// Whether it is hidden from users by default 
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Conditions for the page to be included (based on object properties and/or Context)
    /// - should be processed in the API. Will be excluded if any of the conditions returns false
    /// </summary>
    public Condition[] Conditions { get; set; }
    
    // related objects to load?
    // ...
}