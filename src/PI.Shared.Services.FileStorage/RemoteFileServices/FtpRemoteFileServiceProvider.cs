using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PI.Shared.Models;
using PI.Shared.Models.Files;

namespace PI.Shared.Services;

public class FtpRemoteFileServiceProvider : IRemoteFileServiceProvider
{
    public string Name => "ftp";
    public bool SupportsSignedUrl => false;
    
    public Task<RemoteFolder> CreateFolderAsync(IEntityContext context, RemoteFileBucket remoteFileBucket, string name, string path, Guid? folderId = null)
    {
        throw new NotImplementedException();
    }

    public Task<RemoteFile> UploadAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFolder folder, Stream stream, string filename, string contentType)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GetStreamAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetSignedUrlAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file, bool forUpload)
    {
        throw new NotSupportedException();
    }

    public Task<RemoteListPage> ListRemoteAsync(IEntityContext context, RemoteFileBucket bucket, string path = null, string remotePath = null)
    {
        throw new NotImplementedException();
    }

    public Task<Result<RemoteFile>> UpdateMetadataAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file)
    {
        throw new NotImplementedException();
    }
}