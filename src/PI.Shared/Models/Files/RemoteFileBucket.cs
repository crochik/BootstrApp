using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models.Interfaces;

namespace PI.Shared.Models.Files;

[BsonCollection("file.Bucket")]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(S3RemoteFileBucket),
    typeof(SftpRemoteFileBucket)
)]
public abstract class RemoteFileBucket : EntityOwnedModel
{
    // TODO: add object level permissions (RBAC)
    // ...

    public abstract string Provider { get; }

    public abstract string GetAbsoluteUri(string path);

    protected string RemoveSeparators(string path)
    {
        if (path == null) return null;

        if (path.Length > 0 && path[0] == '/') path = path[1..];
        if (path.Length > 0 && path[^1] == '/') path = path[..^1];
        return path;
    }
}

[BsonDiscriminator("s3")]
public class S3RemoteFileBucket : RemoteFileBucket
{
    public override string Provider => "s3";

    /// <summary>
    /// Name of the bucket
    /// </summary>
    public string RemoteName { get; set; }

    /// <summary>
    /// (Secret) AccessKey
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    /// (Secret) Secret Key
    /// </summary>
    public string SecretKey { get; set; }

    public override string GetAbsoluteUri(string path) => $"s3://{RemoteName}/{path}";
}

// public class GcpRemoteFileBucket : RemoteFileBucket
// {
// }
//
// public class FtpRemoteFileBucket : RemoteFileBucket
// {
// }

[BsonDiscriminator("sftp")]
public class SftpRemoteFileBucket : RemoteFileBucket
{
    public override string Provider => "sftp";

    public string Host { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string SecretKey { get; set; }
    public string Passphrase { get; set; }

    public override string GetAbsoluteUri(string path) => $"sftp://{Host}/{RemoveSeparators(path)}";
}

[BsonCollection("file.Item")]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(RemoteFolder),
    typeof(RemoteFile)
)]
public class RemoteItem : FlowObjectModel, ITaggable
{
    public Guid BucketId { get; set; }
    public Guid? ParentId { get; set; }
    public string AbsoluteUri { get; set; }
    public string RelativePath { get; set; }

    public Guid? CreatedBy { get; set; }

    public List<KeyValuePair<string, object>> Refs { get; set; }
    public string[] Tags { get; set; }
}

[Flags]
public enum RemoteFolderPermission
{
    None = 0,
    UploadFile = 4, // create file 
}

[BsonDiscriminator("folder")]
public class RemoteFolder : RemoteItem
{
    public string GetRelativePath(string filename) => filename.StartsWith('/') ? $"{RelativePath}{filename}" : $"{RelativePath}/{filename}";
    
    public RBAC<RemoteFolderPermission> RBAC { get; set; }
}

[Flags]
public enum RemoteFilePermission
{
    None = 0,
    Read = 1,
    Update = 2, // save a new file with the same name (but different remotePath)
    Overwrite = 4, // save a new file with the same name and remote path 
    Delete = 8, // delete remote file (and object)
    Upload = 16, // upload initial file
}

[BsonDiscriminator("file")]
public class RemoteFile : RemoteItem, IWithParent
{
    public string ContentType { get; set; }
    public long Size { get; set; }

    /// <summary>
    /// (optional) public url to access file contents directly
    /// </summary>
    public string PublicUrl { get; set; }

    public bool AllowAnonymousDownload { get; set; }

    public RBAC<RemoteFilePermission> RBAC { get; set; }
    
    /// <summary>
    /// Object that owns this file (optional)
    /// </summary>
    public ReferencedObject Parent { get; set; }
}

public class UploadFileOptions
{
    /// <summary>
    /// Relative Path including file name in the bucket (can be template)
    /// </summary>
    public string RemotePath { get; set; }

    /// <summary>
    /// Name for the remote file (can be template)
    /// </summary>
    public string Name { get; set; }

    public Guid? RemoteFileFlowId { get; set; }
    public Guid? RemoteFileObjectStatusId { get; set; }

    /// <summary>
    /// Owner of the new file
    /// If missing, will default to the same owner of the parent object (auto)
    /// </summary>
    public EntityRoleId? Owner { get; set; }

    /// <summary>
    /// Permissions for new file
    /// key (can be template): role (e.g. Admin, Manager, ...), profile or entity id ...first match will win
    /// value: number representing permissions
    /// </summary>
    public Dictionary<string, RemoteFilePermission> Permissions { get; set; }
    
    /// <summary>
    /// Base folder (optional)
    /// </summary>
    public Guid? RemoteFolderId { get; set; }
}