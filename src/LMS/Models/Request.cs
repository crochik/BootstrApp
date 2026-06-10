using System;
using System.Collections.Generic;
using PI.Shared.Services;

namespace LMS.Models;

public class Request
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid LeadTypeId { get; set; }
    public Dictionary<string, object> Query { get; set; }
    public Headers Headers { get; set; } = new Headers();
    public string RemoteIp { get; set; }
    
    public long? ContentLength { get; set; }
    public string ContentType { get; set; }
    public string Host { get; set; }
    public string Method { get; set; }
    public string Path { get; set; }
    public string TraceIdentifier { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    
    public IDictionary<string, object> Payload { get; set; }
}