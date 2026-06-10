using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Attributes;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace Controllers;

/// <summary>
/// User Actions for an Object
/// subset of the API one but this has access to files runner so it can run synchronously 
/// </summary>
[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[Route("/files/api/File")]
public class ApiFileController : APIController
{
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _service;

    public ApiFileController(MongoConnection connection, RemoteFileService service)
    {
        _connection = connection;
        _service = service;
    }

    /// <summary>
    /// Get Presigned url to upload file
    /// </summary>
    [HttpGet("/files/api/File({id})/Upload")]
    [UseApiNames]
    public async Task<PrepareUploadResponse> PrepareUploadAsync([FromRoute] Guid id)
    {
        var result = await _service.GetPresignedUploadUrlAsync(Context, id);
        return new PrepareUploadResponse
        {
            Success = result.IsSuccess, 
            Message = result.Status, 
            Url = result.Value?.Url
        };
    }

    /// <summary>
    /// Finish upload, check file with provider 
    /// </summary>
    [HttpPut("/files/api/File({id})/Upload")]
    [UseApiNames]
    public async Task<ConfirmUploadResponse> ConfirmUploadAsync([FromRoute] Guid id)
    {
        var result = await _service.CheckFileUploadedAsync(Context, id);
        return new ConfirmUploadResponse
        {
            Success = result.IsSuccess, 
            Message = result.Status, 
        };
    }
    
    /// <summary>
    /// get file contents
    /// will redirect if the bucket supports presigned urls
    /// </summary>
    [HttpGet("/files/api/File({id})/Download")]
    public async Task<IActionResult> GetFileContentAsync([FromRoute] Guid id)
    {
        var remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (remoteFile == null) throw NotFoundException.New<RemoteFile>(id);
        if (!remoteFile.RBAC.Can(Context, RemoteFilePermission.Read) && !remoteFile.AllowAnonymousDownload && remoteFile.EntityId != Context.UserId)
        {
            throw new ForbiddenException("Read");
        }
        
        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, remoteFile.BucketId)
            .FirstOrDefaultAsync();

        if (bucket == null) throw NotFoundException.New<RemoteFileBucket>(remoteFile.BucketId);

        if (_service.SupportsSignedUrl(bucket))
        {
            var url = await _service.GetSignedUrlAsync(Context, bucket, remoteFile);
            return Redirect(url);
        }

        var stream = await _service.GetStreamAsync(Context, remoteFile, bucket);
        return File(stream, remoteFile.ContentType, remoteFile.Name); // "application/octet-stream"
    }
}

public class PrepareUploadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Url { get; set; }
}

public class ConfirmUploadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
}