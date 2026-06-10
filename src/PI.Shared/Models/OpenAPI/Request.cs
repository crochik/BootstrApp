using System;
using System.Collections.Generic;
using PI.Shared.Form.Models;

namespace PI.Shared.Models.OpenAPI;

public class Request
{
    public string HeaderObjectType { get; set; }
    
    /// <summary>
    /// Request parameters as an object
    /// </summary>
    [Obsolete("use Parameters")]
    public string ParametersObjectType { get; set; }
    
    /// <summary>
    /// Instead of using a separate object type?
    /// </summary>
    public Dictionary<string, FormField> Parameters { get; set; }
    
    /// <summary>
    /// Where parameters (ParametersObjectType fields are in the request)
    /// Key: Parameter name
    /// Value: where 
    /// </summary>
    public Dictionary<string, string> ParametersPlacement { get; set; }
    
    public string Method { get; set; }
    public string Path { get; set; }

    public Dictionary<string, Payload> Payloads { get; set; }
}