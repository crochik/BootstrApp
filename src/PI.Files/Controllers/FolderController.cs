using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Services;

namespace Controller.Files;

[Route("/files/v1/[controller]")]
public class FolderController : APIController
{
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _service;
    private readonly ObjectTypeService _objectTypeService;

    public FolderController(
        MongoConnection connection,
        RemoteFileService service,
        ObjectTypeService objectTypeService
    )
    {
        _connection = connection;
        _service = service;
        _objectTypeService = objectTypeService;
    }

    [RequestSizeLimit(50_000_000)]
    [Authorize("default")]
    [HttpPost("/files/v1/[controller]({id})/Upload")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    public async Task<RemoteFile> UploadFileAsync([FromRoute] Guid id, IFormFile file)
    {
        // TODO: have to figure out a good way to determine the name of the new item (potentially using "templates")
        // ...

        var folder = await _connection.Filter<RemoteFolder>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (folder == null) throw NotFoundException.New<RemoteFolder>(id);

        if (!folder.RBAC.Can(Context, RemoteFolderPermission.UploadFile)) throw new ForbiddenException("Can't upload file");

        var index = file.FileName.LastIndexOf('.');
        var fileExtension = index > 0 ? file.FileName[index..] : string.Empty;
        var remoteFile = await _service.UploadAsync(Context, folder, file, new UploadFileOptions
            {
                RemotePath = $"{Guid.NewGuid():N}{fileExtension}",
                Name = file.FileName,
            }
        );

        remoteFile.EntityId = Context.UserId ?? folder.EntityId;
        
        await _connection.InsertAsync(remoteFile);

        // TODO: fire event
        // ...

        return remoteFile;
    }

    /// <summary>
    /// Handle uploads for RemoteFileField
    /// </summary>
    [RequestSizeLimit(50_000_000)]
    [Authorize("default")]
    [HttpPost("/files/v1/[controller]({id})/{objectTypeName}({objectId})/{fieldName}/Upload")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    public async Task<RemoteFile> UploadFileForFieldAsync(
        [FromRoute] Guid id,
        [FromRoute] string objectTypeName,
        [FromRoute] Guid objectId,
        [FromRoute] string fieldName,
        IFormFile file
    )
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw NotFoundException.New(objectTypeName);

        if (!objectType.Fields.TryGetValue(fieldName, out var field) || field.Field.Options is not RemoteFileFieldOptions options)
        {
            throw new BadRequestException("Invalid field");
        }

        if (options.UploadFileOptions == null) throw new BadRequestException("Bad config");

        // can update?
        if (!field.RBAC.CanUpdate(Context)) throw new ForbiddenException();

        var obj = await _objectTypeService.GetFlatObjectAsync(Context, objectType, objectId);
        if (obj == null) throw NotFoundException.New(objectTypeName, objectId);

        // can reset if set?
        if (obj.TryGetGuidParam(fieldName, out _) && !field.RBAC.CanReset(Context)) throw new ForbiddenException();

        var objectContext = new Dictionary<string, object>
        {
            { "Object", obj },
            {
                "Objects", new Dictionary<string, object>
                {
                    { FlowRun.GetObjectAlias(objectType.FullName), obj },
                }
            }
        };

        var folder = await _connection.Filter<RemoteFolder>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (folder == null) throw NotFoundException.New<RemoteFolder>(id);
        if (!folder.RBAC.Can(Context, RemoteFolderPermission.UploadFile)) throw new ForbiddenException("Can't upload file");

        var remoteFile = await _service.UploadAsync(Context, folder, file, options.UploadFileOptions, objectContext);

        if (!obj.TryGetGuidParam(nameof(IEntityOwnedModel.EntityId), out var entityId))
        {
            if (objectTypeName == nameof(Organization) || objectTypeName == nameof(User))
            {
                entityId = objectId;
            }
            else
            {
                throw new BadRequestException("Can't determine object owner");
            }
        }

        remoteFile.EntityId = entityId;
        remoteFile.Refs.Add(new KeyValuePair<string, object>(objectTypeName, objectId));

        await _connection.InsertAsync(remoteFile);

        await _objectTypeService.FireCreateEventAsync(Context, remoteFile, e =>
        {
            e.Description ??= $"{file.FileName} Uploaded to {objectType.Description ?? objectType.Name}";
            e.Action ??= "ObjectCreated";
            e.AddRefValue(objectTypeName, objectId);
        });

        return remoteFile;
    }
}