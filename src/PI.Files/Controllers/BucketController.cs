using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace Controller.Files;

[Authorize("admin")]
[Route("/files/v1/[controller]")]
public class BucketController : APIController
{
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _service;

    public BucketController(
        MongoConnection connection,
        RemoteFileService service
    )
    {
        _connection = connection;
        _service = service;
    }

    [HttpPost("/files/v1/[controller]({id})/Folder")]
    public async Task<RemoteFolder> CreateFoldersAsync([FromRoute] Guid id, string remotePath)
    {
        var bucket = await _connection.Filter<RemoteFileBucket>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (bucket==null) throw NotFoundException.New<RemoteFileBucket>(id);

        return await _service.CreateFolderRecursivelyAsync(Context, bucket, remotePath);
    }
}
