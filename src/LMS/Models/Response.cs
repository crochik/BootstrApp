using System;
using PI.Shared.Models;

namespace LMS.Models;

public class Response
{
    public bool Success { get; set; }

    public string Reason { get; set; }
    
    public Lead Lead { get; set; }
    
    public string Message { get; set; }
    
    public DateTime? FinishedOn { get; set; }
    
    /// <summary>
    /// v2: Content Type
    /// </summary>
    public string ContentType { get; set; }
    
    /// <summary>
    /// v2: Body
    /// </summary>
    public string Body { get; set; }
}