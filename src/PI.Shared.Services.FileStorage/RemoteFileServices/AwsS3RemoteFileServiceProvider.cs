using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services.DataProtection;

namespace PI.Shared.Services;

public class AwsS3RemoteFileServiceProvider : IRemoteFileServiceProvider
{
    public string Name => "s3";
    public bool SupportsSignedUrl => true;

    private readonly ILogger<AwsS3RemoteFileServiceProvider> _logger;
    private readonly DataProtectionService _dataProtectionService;

    public AwsS3RemoteFileServiceProvider(ILogger<AwsS3RemoteFileServiceProvider> logger, DataProtectionService dataProtectionService)
    {
        _logger = logger;
        _dataProtectionService = dataProtectionService;
    }

    private async Task<IAmazonS3> GetClientAsync(IEntityContext context, S3RemoteFileBucket bucket)
    {
        // TODO: in theory would need to get the RemoteFileBucket object for the account 
        // to figure out the options for the field 
        // .... 

        // for now just hardcode 
        var config = new MicrosoftDataProtectionConfig
        {
            Purpose = "objectType://S3RemoteFileBucket.SecretKey",
        };

        var secret = await _dataProtectionService.UnprotectAsync(context, config, bucket.SecretKey);
        var credentials = new BasicAWSCredentials(bucket.AccessKey, secret);

        return new AmazonS3Client(credentials);
    }

    public Task<RemoteFolder> CreateFolderAsync(IEntityContext context, RemoteFileBucket bucket, string name, string path, Guid? folderId = null)
    {
        var tmp = new RemoteFolder
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
            // FlowId =
            // ObjectStatusId 
        };

        return CreateFolderAsync(context, bucket as S3RemoteFileBucket, tmp);
    }

    public Task<RemoteFile> UploadAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFolder folder, Stream stream, string filename, string contentType)
    {
        var path = folder.GetRelativePath(filename);

        var tmp = new RemoteFile
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
            // Size = 
            // FlowId =
            // ObjectStatusId
        };

        return UploadAsync(context, bucket as S3RemoteFileBucket, stream, tmp);
    }

    /// <summary>
    /// Get signed url (for download or upload)
    /// </summary>
    public async Task<string> GetSignedUrlAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file, bool forUpload)
    {
        var s3Bucket = bucket as S3RemoteFileBucket;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3Bucket.RemoteName,
            Key = file.RelativePath,
            Verb = forUpload ? HttpVerb.PUT : HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(10),
        };

        var client = await GetClientAsync(context, s3Bucket);
        return client.GetPreSignedURL(request);
    }

    public async Task<Result<RemoteFile>> UpdateMetadataAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file)
    {
        var response = await GetFileInformationAsync(context, bucket, file.RelativePath);
        if (!response.IsSuccess) return response.ConvertTo<RemoteFile>();

        if (file.Size == response.Value.ContentLength && file.ContentType == response.Value.Headers.ContentType)
        {
            // ...
            return Result.Unknown<RemoteFile>("Nothing changed");
        }
        
        // update meta info
        file.Size = response.Value.ContentLength;
        file.ContentType = response.Value.Headers.ContentType;
        // ...

        return Result.Success(file);
    }
    
    private async Task<Result<GetObjectMetadataResponse>> GetFileInformationAsync(IEntityContext context, RemoteFileBucket bucket, string key)
    {
        var s3Bucket = bucket as S3RemoteFileBucket;
        var client = await GetClientAsync(context, s3Bucket);
        
        var request = new GetObjectMetadataRequest
        {
            BucketName = s3Bucket.RemoteName,
            Key = key
        };

        try
        {
            var meta =  await client.GetObjectMetadataAsync(request);
            return Result.Success(meta);
        } 
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Error getting object metadata for {Key}: {StatusCode}", key, ex.StatusCode);
            return Result.Error<GetObjectMetadataResponse>($"{ex.StatusCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting object metadata for {Key}", key);
            return Result.Error<GetObjectMetadataResponse>(ex.Message);
        }
    }

    public async Task<RemoteListPage> ListRemoteAsync(IEntityContext context, RemoteFileBucket bucket, string remotePath, string continuationToken)
    {
        // TODO: try to find parent folder
        // ... ?
        
        var s3Bucket = bucket as S3RemoteFileBucket;
        var client = await GetClientAsync(context, s3Bucket);

        // exclude leading '/'
        var path = remotePath!=null && remotePath.StartsWith("/") ? remotePath[1..] : remotePath;
        
        // TODO: should always end with '/'? 
        // ...
        
        var request = new ListObjectsV2Request
        {
            BucketName = s3Bucket.RemoteName,
            Prefix = path,
            StartAfter = path, // so it will not include itself
            Delimiter = "/",
            // ContinuationToken =
        };

        var response = await client.ListObjectsV2Async(request);

        // use S3DirectoryInfo instead?
        // ...

        return new RemoteListPage
        {
            ContinuationToken = response.ContinuationToken,
            HasMoreResults = response.ContinuationToken != null && response.KeyCount == response.MaxKeys,
            Results = response.S3Objects
                .Select(x => new RemoteListResult
                {
                    Name = x.Key,
                    Native = x,
                    Size = x.Size ?? 0,
                    // IsContainer = 
                })
                .Concat(response.CommonPrefixes.Select(x =>
                    new RemoteListResult
                    {
                        Name = x,
                        Native = x,
                        IsContainer = true
                    }
                ))
        };
    }

    public Task<Stream> GetStreamAsync(IEntityContext context, RemoteFileBucket bucket, RemoteFile file) => DownloadAsync(context, bucket as S3RemoteFileBucket, file);

    private async Task DownloadAsync(IEntityContext context, S3RemoteFileBucket bucket, RemoteFile file, string localPath)
    {
        var request = new TransferUtilityDownloadRequest
        {
            BucketName = bucket.RemoteName,
            Key = file.RelativePath,
            FilePath = localPath
        };

        request.WriteObjectProgressEvent += (sender, args) => { };

        var client = await GetClientAsync(context, bucket);
        var transferUtility = new TransferUtility(client);
        await transferUtility.DownloadAsync(request);
    }

    private async Task<Stream> DownloadAsync(IEntityContext context, S3RemoteFileBucket bucket, RemoteFile file)
    {
        var client = await GetClientAsync(context, bucket);
        var transferUtility = new TransferUtility(client);
        return await transferUtility.OpenStreamAsync(bucket.RemoteName, file.RelativePath);
    }

    private async Task<RemoteFile> UploadAsync(IEntityContext context, S3RemoteFileBucket bucket, Stream stream, RemoteFile file)
    {
        var request = new TransferUtilityUploadRequest
        {
            InputStream = stream,
            BucketName = bucket.RemoteName,
            Key = file.RelativePath,
            Metadata =
            {
                [nameof(RemoteFile.Id)] = file.Id.ToString(),
                [nameof(RemoteFile.ParentId)] = file.ParentId.ToString(),
                [nameof(RemoteFile.AccountId)] = file.AccountId.ToString()
            }
        };

        request.UploadProgressEvent += (sender, args) => { file.Size = args.TransferredBytes; };

        // if (allowAnonymousRead.GetValueOrDefault())
        // {
        //     uploadRequest.CannedACL = S3CannedACL.PublicRead;
        // }

        var client = await GetClientAsync(context, bucket);
        var transferUtility = new TransferUtility(client);
        await transferUtility.UploadAsync(request);

        return file;
    }

    private async Task<RemoteFolder> CreateFolderAsync(IEntityContext context, S3RemoteFileBucket bucket, RemoteFolder remoteFolder)
    {
        var client = await GetClientAsync(context, bucket);
        var request = new PutObjectRequest
        {
            BucketName = bucket.RemoteName,
            Key = $"{remoteFolder.RelativePath}/",
            Metadata =
            {
                [nameof(RemoteFolder.Id)] = remoteFolder.Id.ToString(),
                [nameof(RemoteFolder.AccountId)] = remoteFolder.AccountId.ToString()
            }
        };

        if (remoteFolder.ParentId.HasValue) request.Metadata[nameof(RemoteFolder.ParentId)] = remoteFolder.ParentId.ToString();

        var result = await client.PutObjectAsync(request);

        // ...
        return remoteFolder;
    }
}