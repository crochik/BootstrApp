using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controller.Files;

[Authorize("admin")]
[Route("/files/v1/[controller]")]
public class FileController : APIController
{
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _service;

    public FileController(
        MongoConnection connection,
        RemoteFileService service
    )
    {
        _connection = connection;
        _service = service;
    }

    [Authorize("default")]
    [HttpPost("/files/v1/[controller]({id})/DataForm")]
    public async Task<DataFormActionResponse> OnActionAsync([FromRoute] Guid id, [FromBody] DataFormActionRequest request)
    {
        var remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (remoteFile == null) throw NotFoundException.New<RemoteFile>(id);
        if (!remoteFile.RBAC.Can(Context, RemoteFilePermission.Read)) throw new ForbiddenException("Read");
        
        switch (request.Action)
        {
            case "Redirect":
                break;
            
            case "Download":
                throw new BadRequestException("Invalid Action");
        }

        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, remoteFile.BucketId)
            .FirstOrDefaultAsync();

        if (bucket == null) throw NotFoundException.New<RemoteFileBucket>(remoteFile.BucketId);

        if (!_service.SupportsSignedUrl(bucket))
        {
            // TODO: handle internally?
            // ...
            throw new BadRequestException("Provider doesn't support redirection");
        }

        var url = await _service.GetSignedUrlAsync(Context, bucket, remoteFile);
        if (url == null) return new DataFormActionResponse(request, "Failed to generated signed url");

        return new DataFormActionResponse(request)
        {
            Success = true,
            NextUrl = url,
        };
    }

    /// <summary>
    /// get file contents
    /// </summary>
    [Authorize("default")]
    [HttpGet("/files/v1/[controller]({id})")]
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
    
    /// <summary>
    /// Download file (prefer using Redirect if the provider supports it)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/files/v1/[controller]({id})/Download")]
    public async Task<IActionResult> GetFileAsync([FromRoute] Guid id)
    {
        var (bucket, file) = await GetAnonymousFileAsync(id);
        var stream = await _service.GetStreamAsync(new AccountContext(file.AccountId), file, bucket);
        return File(stream, file.ContentType, file.Name); // "application/octet-stream"
    }

    /// <summary>
    /// Redirects to a url in the provider (to avoid transferring the contents via this api) 
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/files/v1/[controller]({id})/Redirect")]
    public async Task<IActionResult> RedirectToFileAsync([FromRoute] Guid id)
    {
        var (bucket, file) = await GetAnonymousFileAsync(id);
        if (!_service.SupportsSignedUrl(bucket)) throw new BadRequestException("Provider does not support redirection");
        var url = await _service.GetSignedUrlAsync(new AccountContext(file.AccountId), bucket, file);
        return Redirect(url);
    }

    private async Task<(RemoteFileBucket Bucket, RemoteFile File)> GetAnonymousFileAsync(Guid id)
    {
        // TODO: enforce that the file can be accessed anonymously
        // ...
        var file = await _connection.Filter<RemoteFile>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (file == null) throw NotFoundException.New<RemoteFile>(id);

        if (!file.AllowAnonymousDownload) throw new NotAuthorizedException();

        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, file.AccountId)
            .Eq(x => x.Id, file.BucketId)
            .FirstOrDefaultAsync();

        if (bucket == null) throw NotFoundException.New<RemoteFileBucket>(id);

        return (bucket, file);
    }
}