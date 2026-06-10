using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;

namespace PI.Shared.Services;

public class RemoteFileService
{
    private readonly ILogger<RemoteFileService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly Dictionary<string, IRemoteFileServiceProvider> _providers;

    public RemoteFileService(ILogger<RemoteFileService> logger, MongoConnection connection, IEnumerable<IRemoteFileServiceProvider> providers, ObjectTypeService objectTypeService)
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _providers = providers.ToDictionary(x => x.Name);
    }

    private IRemoteFileServiceProvider GetProvider(RemoteFileBucket bucket)
    {
        if (_providers.TryGetValue(bucket.Provider, out var provider)) return provider;
        throw NotFoundException.New($"Unregistered Provider: {bucket.Provider}");
    }

    private async Task<RemoteFolder> CreateFolderRecursivelyAsync(IEntityContext context, RemoteFolder folder, string relativePath)
    {
        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, folder.AccountId)
            .Eq(x => x.Id, folder.BucketId)
            .FirstOrDefaultAsync();

        if (bucket == null) throw NotFoundException.New<RemoteFileBucket>(folder.BucketId);

        return await CreateFolderRecursivelyAsync(context, bucket, folder.GetRelativePath(relativePath));
    }

    public async Task<RemoteFolder> CreateFolderRecursivelyAsync(IEntityContext context, RemoteFileBucket bucket, string remotePath)
    {
        var provider = GetProvider(bucket);

        var parts = remotePath.Split('/');

        var existing = await _connection.Filter<RemoteFolder>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.BucketId, bucket.Id)
            .In(x => x.RelativePath, allFolders())
            .FindAsync();

        var folder = existing.MaxBy(x => x.RelativePath.Length);
        var startIndex = folder?.RelativePath.Split('/').Length ?? 0;
        for (var c = startIndex; c < parts.Length; c++)
        {
            var path = string.Join("/", parts, 0, c + 1);
            var remoteFolder = await provider.CreateFolderAsync(context, bucket, parts[c], path, folder?.Id);

            if (remoteFolder == null) return null;
            folder = remoteFolder;

            await _connection.InsertAsync(remoteFolder);
            await _objectTypeService.FireCreateEventAsync(context, remoteFolder);
        }

        return folder;

        IEnumerable<string> allFolders()
        {
            for (var c = 1; c <= parts.Length; c++)
            {
                yield return string.Join("/", parts, 0, c);
            }
        }
    }

    public async Task<Result<RemoteFile>> CheckFileUploadedAsync(IEntityContext context, Guid remoteFileId)
    {
        var remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, remoteFileId)
            .FirstOrDefaultAsync();

        if (remoteFile?.RBAC == null || !remoteFile.RBAC.Can(context, RemoteFilePermission.Upload))
        {
            _logger.LogError("{RemoteFileId} Not found", remoteFileId);
            return Result.Error<RemoteFile>("File not found");
        }

        var contentType = remoteFile.ContentType;

        var result = await UpdateMetadataAsync(context, remoteFile);
        if (!result.IsSuccess)
        {
            // TODO: should it do anything with the file?
            // ...
            return result;
        }

        // TODO: should it remove upload permissions?
        // ...

        var query = _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, remoteFileId)
            .Update
            .Set(x => x.Size, remoteFile.Size)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        if (string.IsNullOrWhiteSpace(contentType))
        {
            query.Set(x => x.ContentType, remoteFile.ContentType);
        }

        // update file meta data
        remoteFile = await query.UpdateAndGetOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(context, remoteFile, new Dictionary<string, object>
        {
            { nameof(RemoteFile.ContentType), contentType ?? remoteFile.ContentType },
            { nameof(RemoteFile.Size), remoteFile.Size },
        });

        return Result.Success(remoteFile);
    }

    public async Task<Result<GetPresignedUploadUrlResult>> GetPresignedUploadUrlAsync(IEntityContext context, Guid remoteFileId)
    {
        var query = _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, remoteFileId);

        var remoteFile = await query.FirstOrDefaultAsync();

        if (remoteFile?.RBAC == null || !remoteFile.RBAC.Can(context, RemoteFilePermission.Upload))
        {
            _logger.LogError("{RemoteFileId} Not found", remoteFileId);
            return Result.Error<GetPresignedUploadUrlResult>("Object not found");
        }

        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, remoteFile.AccountId)
            .Eq(x => x.Id, remoteFile.BucketId)
            .FirstOrDefaultAsync();

        if (bucket == null)
        {
            _logger.LogError("{RemoteFileBucketId} Not found", remoteFile.BucketId);
            return Result.Error<GetPresignedUploadUrlResult>("Bucket not found");
        }

        if (!SupportsSignedUrl(bucket))
        {
            _logger.LogError("{RemoteFileBucketId} does not support presigned urls", remoteFile.BucketId);
            return Result.Error<GetPresignedUploadUrlResult>("Bucket does not support presigned urls");
        }

        var url = await GetSignedUrlAsync(context, bucket, remoteFile, forUpload: true);
        if (url == null) Result.Error<string>("Failed to generate url");

        return Result.Success(new GetPresignedUploadUrlResult
        {
            RemoteFile = remoteFile,
            Bucket = bucket,
            Url = url,
        });
    }

    public Task<RemoteFile> UploadAsync(IEntityContext context, RemoteFolder folder, IFormFile file, UploadFileOptions uploadFileOptions, IDictionary<string, object> objectContext = null)
        => UploadAsync(context, file.OpenReadStream(), file.ContentType, file.FileName, uploadFileOptions, objectContext, folder);

    public async Task<RemoteFile> UploadAsync(IEntityContext context, Stream stream, string contentType, string filename, UploadFileOptions uploadFileOptions, IDictionary<string, object> objectContext = null, RemoteFolder folder = null) 
    {
        objectContext ??= new Dictionary<string, object>();

        if (!objectContext.TryGetValue("Objects", out var objects))
        {
            objects = new Dictionary<string, object>();
            objectContext.Add("Objects", objects);
        }

        if (objects is not IDictionary<string, object> objectsDictionary)
        {
            throw new BadRequestException("Invalid context");
        }

        var id = Guid.NewGuid();
        var index = filename.LastIndexOf('.');
        objectsDictionary["UploadedFile"] = new Dictionary<string, object>
        {
            { "ContentType", contentType },
            { "FileName", filename },
            { "_id", id },
            { "FileExtension", index > 0 ? filename[index..] : string.Empty },
            { "Timestamp", DateTime.UtcNow.ToString("MMddyyyyHHmmss") } // TODO: add as a function (e.g. {{toString "MMddyyyyHHmmss" new Date}} or ...)
        };

        // objectContext.TryAdd("UploadedFile|ContentType", contentType);
        // objectContext.TryAdd("new Timestamp", DateTime.UtcNow.ToString("MMddyyyyHHmmss"));
        // objectContext.TryAdd("new UUID", id.ToString());
        // if (context.UserId.HasValue) objectContext.TryAdd("Context|UserId", context.UserId.Value.ToString());
        // if (context.OrganizationId.HasValue) objectContext.TryAdd("Context|OrganizationId", context.OrganizationId.Value.ToString());
        // objectContext.TryAdd("Context|AccountId", context.AccountId.ToString());
        // // hack for some calculated fields 
        // objectContext.TryAdd("UploadedFile|FileName", filename);
        // if (index > 0)
        // {
        //     objectContext.TryAdd("UploadedFile|FileExtension", filename[index..]);
        // }

        if (!ExpressionEvaluatorService.TryResolve(context, objectContext, uploadFileOptions.RemotePath, out var remotePathObj) || remotePathObj is not string remotePath || string.IsNullOrEmpty(remotePath))
        {
            throw new BadRequestException("RemotePath");
        }

        if (!ExpressionEvaluatorService.TryResolve(context, objectContext, uploadFileOptions.Name, out var nameObj) || nameObj is not string name || string.IsNullOrEmpty(name))
        {
            throw new BadRequestException("Name");
        }

        if (folder == null && uploadFileOptions?.RemoteFolderId != null)
        {
            folder = await _connection.Filter<RemoteFolder>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, uploadFileOptions.RemoteFolderId.Value)
                .FirstOrDefaultAsync();
        }

        if (folder == null) throw new BadRequestException("Folder invalid or not found");

        var fileIndex = remotePath.LastIndexOf('/');
        if (fileIndex > 0)
        {
            var subPath = remotePath[..fileIndex];
            remotePath = remotePath[(fileIndex + 1)..];

            folder = await CreateFolderRecursivelyAsync(context, folder, subPath);
            if (folder == null) throw NotFoundException.New("Folder");
        }

        // TODO: CHECK if there is a file with the same parent and name  (what about same remote path)
        // if so, check if the user has permission to replace it
        // ... 

        var remoteFile = await UploadAsync(context, folder, stream, remotePath, contentType);

        if (uploadFileOptions.Owner.HasValue)
        {
            throw new BadRequestException("specifying owner not supported yet");
        }

        remoteFile.Id = id;
        remoteFile.Name = name;
        remoteFile.FlowId = uploadFileOptions.RemoteFileFlowId;
        remoteFile.ObjectStatusId = uploadFileOptions.RemoteFileObjectStatusId;
        remoteFile.RBAC = new RBAC<RemoteFilePermission>
        {
            [context.UserId.Value] = RemoteFilePermission.Read
        };

        remoteFile.Refs ??= new List<KeyValuePair<string, object>>();

        return remoteFile;
    }

    /// <summary>
    /// Upload file to bucket
    /// IT WILL NOT SAVE THE FILE - the caller has to insert and fire event
    /// </summary>
    public async Task<RemoteFile> UploadAsync(IEntityContext context, RemoteFolder folder, Stream stream, string filename, string contentType)
    {
        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, folder.BucketId)
            .FirstOrDefaultAsync();

        if (bucket == null) throw NotFoundException.New<RemoteFileBucket>(folder.BucketId);
        var provider = GetProvider(bucket);

        return await provider.UploadAsync(context, bucket, folder, stream, filename, contentType);
    }

    public async Task<Result<RemoteFile>> UpdateMetadataAsync(IEntityContext context, RemoteFile file, RemoteFileBucket bucket = null)
    {
        bucket ??= await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, file.BucketId)
            .FirstOrDefaultAsync();

        if (file.BucketId != bucket?.Id) throw new BadRequestException("Bucket mismatch");

        var provider = GetProvider(bucket);
        return await provider.UpdateMetadataAsync(context, bucket, file);
    }

    public async Task<Stream> GetStreamAsync(IEntityContext context, RemoteFile file, RemoteFileBucket bucket = null)
    {
        bucket ??= await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, file.BucketId)
            .FirstOrDefaultAsync();

        if (file.BucketId != bucket?.Id) throw new BadRequestException("Bucket mismatch");

        var provider = GetProvider(bucket);
        return await provider.GetStreamAsync(context, bucket, file);
    }

    public bool SupportsSignedUrl(RemoteFileBucket bucket)
    {
        var provider = GetProvider(bucket);
        return provider.SupportsSignedUrl;
    }

    public Task<string> GetSignedUrlAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file, bool forUpload = false)
    {
        var provider = GetProvider(bucket);
        return provider.GetSignedUrlAsync(context, bucket, file, forUpload);
    }

    public async Task<RemoteFile> CopyFileAsync(AccountContext context, RemoteFile sourceFile, RemoteFolder destinationFolder, string fileName)
    {
        if (destinationFolder == null) throw NotFoundException.New<RemoteFileBucket>(destinationFolder.BucketId);

        var stream = await GetStreamAsync(context, sourceFile);

        var remoteFile = await UploadAsync(context, destinationFolder, stream, fileName, sourceFile.ContentType);

        remoteFile.Size = sourceFile.Size;

        return remoteFile;
    }

    public async Task<RemoteListPage> ListAsync(IEntityContext context, RemoteFileBucket bucket, string remotePath = null, string continuationToken = null)
    {
        var provider = GetProvider(bucket);
        return await provider.ListRemoteAsync(context, bucket, remotePath, continuationToken);
    }
}

public class RemoteListResult
{
    public string Name { get; set; }
    public bool IsContainer { get; set; }
    public long Size { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    public object Native { get; set; }
}

public class RemoteListPage
{
    public string ContinuationToken { get; set; }
    public IEnumerable<RemoteListResult> Results { get; set; }
    public bool HasMoreResults { get; set; }
}

public class GetPresignedUploadUrlResult
{
    public RemoteFile RemoteFile { get; set; }
    public RemoteFileBucket Bucket { get; set; }
    public string Url { get; set; }
}