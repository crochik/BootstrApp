using System;
using System.Collections.Generic;

namespace Messages.Flow;

public class ExtractDataToFileActionOptions : ActionOptions
{
    public Guid RemoteFileBucketId { get; set; }
    
    /// <summary>
    /// Path in the bucket (can be template)
    /// </summary>
    public string RemotePath { get; set; }
    
    /// <summary>
    /// File name (can be template)
    /// </summary>
    public string FileName { get; set; }
    
    public Guid? RemoteFileFlowId { get; set; }
    public Guid? RemoteFileObjectStatusId { get; set; }
    
    public bool AllowAnonymousDownload { get; set; }
    
    /// <summary>
    /// Data Source
    /// currently supported: Postgres
    /// </summary>
    public string Source { get; set; }
    
    /// <summary>
    /// Query (can be a template)
    /// </summary>
    public string Query { get; set; }
    
    /// <summary>
    /// Parameters (value can be path template or value)
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; }
    
    public Guid? NextEventId { get; set; }
    public Guid? ErrorEventId { get; set; }
}