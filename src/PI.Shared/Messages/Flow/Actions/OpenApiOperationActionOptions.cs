using System;
using System.Dynamic;

namespace Messages.Flow;

public class OpenApiOperationActionOptions : ActionOptions
{
    /// <summary>
    /// Expression to resolve to the entity used to get the integration credentials
    /// </summary>
    public string EntityId { get; set; }
    
    public string Namespace { get; set; }
    public Guid OperationId { get; set; }
    public ExpandoObject Parameters { get; set; }
    public ExpandoObject Request { get; set; }
    public string Alias { get; set; }
}