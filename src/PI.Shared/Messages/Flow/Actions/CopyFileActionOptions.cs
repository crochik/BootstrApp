using System;

namespace Messages.Flow;

public class CopyFileActionOptions : ActionOptions
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

    public Guid? NextEventId { get; set; }
    public Guid? ErrorEventId { get; set; }
}