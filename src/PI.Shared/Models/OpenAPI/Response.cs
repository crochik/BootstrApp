using System.Collections.Generic;

namespace PI.Shared.Models.OpenAPI;

public class Response
{
    public string HeaderObjectType { get; set; }
    public Dictionary<string, Payload> Payloads { get; set; }
    public string Description { get; set; }

    public Response()
    {
        
    }
}