using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services.DataProtection;
using Renci.SshNet;

namespace PI.Shared.Services;

public class SftpRemoteFileServiceProvider : IRemoteFileServiceProvider
{
    private readonly DataProtectionService _dataProtectionService;
    public string Name => "sftp";
    public bool SupportsSignedUrl => false;

    public SftpRemoteFileServiceProvider(DataProtectionService dataProtectionService)
    {
        _dataProtectionService = dataProtectionService;
    }

    private async Task<SftpClient> GetClientAsync(IEntityContext context, SftpRemoteFileBucket sftpBucket)
    {
        var password = await UnprotectAsync(context, "objectType://SftpRemoteFileBucket.Password", sftpBucket.Password);
        var passphrase = await UnprotectAsync(context, "objectType://SftpRemoteFileBucket.Passphrase", sftpBucket.Passphrase);
        var secretKey = await UnprotectAsync(context, "objectType://SftpRemoteFileBucket.SecretKey", sftpBucket.SecretKey);

        var connectionInfo = new ConnectionInfo(sftpBucket.Host, sftpBucket.UserName, getAuthMethods().ToArray());

        return new SftpClient(connectionInfo);

        IEnumerable<AuthenticationMethod> getAuthMethods()
        {
            if (password != null)
            {
                yield return new PasswordAuthenticationMethod(sftpBucket.UserName, password);
            }

            if (secretKey != null)
            {
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(secretKey));
                var privateKeyFile = new PrivateKeyFile(stream, passphrase);

                yield return new PrivateKeyAuthenticationMethod(sftpBucket.UserName, privateKeyFile);
            }
        }
    }

    private async Task<string> UnprotectAsync(IEntityContext context, string purpose, string encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;

        // TODO: in theory would need to get the RemoteFileBucket object for the account 
        // to figure out the options for the field 
        // .... 

        // for now just hardcode
        var config = new MicrosoftDataProtectionConfig
        {
            Purpose = purpose,
        };

        var secret = await _dataProtectionService.UnprotectAsync(context, config, encrypted);
        return secret;
    }

    public async Task<RemoteFolder> CreateFolderAsync(IEntityContext context, RemoteFileBucket bucket, string name, string path, Guid? folderId = null)
    {
        if (!path.StartsWith("/")) path = "/" + path;

        using var client = await GetClientAsync(context, bucket as SftpRemoteFileBucket);
        client.Connect();
        try
        {
            if (!client.Exists(path))
            {
                client.CreateDirectory(path);
            }
        }
        finally
        {
            client.Disconnect();
        }

        return new RemoteFolder
        {
            AccountId = bucket.AccountId,
            EntityId = bucket.EntityId,
            CreatedBy = context.UserId,
            Id = Guid.NewGuid(),
            Name = name,
            RelativePath = path,
            BucketId = bucket.Id,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            ParentId = folderId,
            AbsoluteUri = bucket.GetAbsoluteUri(path),
        };
    }

    public async Task<RemoteFile> UploadAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFolder folder, Stream stream, string filename, string contentType)
    {
        var path = folder.GetRelativePath(filename);

        using var client = await GetClientAsync(context, bucket as SftpRemoteFileBucket);
        client.Connect();
        try
        {
            client.UploadFile(stream, path);
        }
        finally
        {
            client.Disconnect();
        }

        return new RemoteFile
        {
            AccountId = bucket.AccountId,
            EntityId = bucket.EntityId,
            CreatedBy = context.UserId,
            Id = Guid.NewGuid(),
            Name = filename,
            RelativePath = path,
            BucketId = bucket.Id,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            ParentId = folder.Id,
            AbsoluteUri = bucket.GetAbsoluteUri(path),
            ContentType = contentType,
            // FlowId =
            // ObjectStatusId
        };
    }

    public async Task<Stream> GetStreamAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file)
    {
        // TODO: seems a little naive/dangerous
        // probably should create an overload that takes a stream and/or save to a temporary file.
        // ... 
        var stream = new MemoryStream();

        using var client = await GetClientAsync(context, bucket as SftpRemoteFileBucket);
        client.Connect();
        try
        {
            client.DownloadFile(file.RelativePath, stream);
        }
        finally
        {
            client.Disconnect();
        }

        return stream;
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
