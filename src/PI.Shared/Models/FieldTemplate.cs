using System.Collections.Generic;
using PI.Shared.Form.Models;

namespace PI.Shared.Models;

public class FieldTemplate
{
    public FormField Field { get; set; }
    public FieldRBAC RBAC { get; set; } = new();
    public bool Indexed { get; set; }
    public object InitialValue { get; set; }
    public object CalculatedValue { get; set; }
    
    /// <summary>
    /// Indices available for this field
    /// </summary>
    public Dictionary<IndexType, Index> Indices { get; set; }
    
    /// <summary>
    /// whether it can be modified or not (system objects)
    /// </summary>
    public bool IsFinal { get; set; }
}