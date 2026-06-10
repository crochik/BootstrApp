using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models.OpenAPI;

[BsonCollection("openapi.Operation")]
public class Operation : Model
{
    public string Namespace { get; set; }
    public string Summary { get; set; }
    public string Description { get; set; }
    public string OperationId { get; set; }
    public Dictionary<string, object> Raw { get; set; }

    public Request Request { get; set; }
    public Dictionary<string, Response> Responses { get; set; }
    
    public string[] Tags { get; set; }

    public Operation()
    {
    }
}