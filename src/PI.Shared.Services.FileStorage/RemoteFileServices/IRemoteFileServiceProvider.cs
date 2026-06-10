using System;
using System.IO;
using System.Threading.Tasks;
using PI.Shared.Models;
using PI.Shared.Models.Files;

namespace PI.Shared.Services;

public interface IRemoteFileServiceProvider
{
    public string Name { get; }
    bool SupportsSignedUrl { get; }
    Task<RemoteFolder> CreateFolderAsync(IEntityContext context, RemoteFileBucket remoteFileBucket, string name, string path, Guid? folderId = null);
    Task<RemoteFile> UploadAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFolder folder, Stream stream, string filename, string contentType);

    /// <summary>
    /// Get file stream (for anonymously access)
    /// </summary>
    Task<Stream> GetStreamAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file);

    Task<string> GetSignedUrlAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file, bool forUpload);
    Task<RemoteListPage> ListRemoteAsync(IEntityContext context, RemoteFileBucket bucket, string path = null, string remotePath = null);

    /// <summary>
    /// Update meta data with provider info
    /// </summary>
    Task<Result<RemoteFile>> UpdateMetadataAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file);
}