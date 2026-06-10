using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

/// <summary>
/// Related Object Types
/// </summary>
public class RelatedObjectType
{
    public string Name { get; set; }
    public string ObjectType { get; set; }
    public string Label { get; set; }
    public Criteria Criteria { get; set; }
    public RelatedObjectTypeRBAC RBAC { get; set; }
    public RelationType RelationType { get; set; }
    public RelatedObjectTypeOptions Options { get; set; }
    
    public string ApiName { get; set; }
    
    /// <summary>
    /// Extra Conditions to decide whether to include on the page or not
    /// </summary>
    public Condition[] Conditions { get; set; }
}