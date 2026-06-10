using System;
using System.Dynamic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Designer;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Route("/api/v1/[controller]")]
public partial class ObjectTypeDesignerController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public ObjectTypeDesignerController(
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    // copy/cut fields (shared clipboard)
    // ... 

    // paste fields (shared clipboard)
    // ... 

    // reorder fields?
    // ...

    [HttpPost("Duplicate/DataForm")]
    // [HttpPost("Duplicate/DataViewAction")]
    public async Task<DataFormActionResponse> DuplicateObjectTypeActionAsync([FromBody] DataFormActionRequest<DuplicateObjectTypeParameters> request, [FromRoute] string objectTypeName)
    {
        await CheckPermission(ObjectType.ObjectTypeFullName, ObjectTypePermission.Create);
        
        if (string.IsNullOrWhiteSpace(request.Parameters.Name))
        {
            return DataFormActionResponse.Error(request, "Name is required");
        }

        var fullName = ObjectType.GetFullName(request.Parameters.Name, request.Parameters.Namespace);
        var existing = await _objectTypeService.GetAsync(Context, fullName);
        if (existing != null)
        {
            return DataFormActionResponse.Error(request, $"There is already an object type with the same name: {fullName}");
        }

        existing = await _objectTypeService.GetAsync(Context, request.Parameters.ObjectType, new GetObjectOptions
        {
            LoadBaseObject = !request.Parameters.KeepBaseObjectType
        });

        if (existing == null)
        {
            return DataFormActionResponse.Error(request, $"Can't load {request.Parameters.ObjectType}");
        }

        var parts = fullName.Split('.');
        var now = DateTime.UtcNow;
        existing.Id = Model.NewGuid();
        existing.CreatedOn = now;
        existing.LastActor = Context.Actor;
        existing.LastModifiedOn = now;
        existing.Namespace = parts.Length > 1 ? string.Join(".", parts[..^1]) : null;
        existing.Name = parts[^1];
        
        if (!request.Parameters.KeepBaseObjectType)
        {
            existing.BaseObjectType = null;
        }

        try
        {
            await _connection.InsertAsync(existing);
        }
        catch (Exception ex)
        {
            return DataFormActionResponse.Error(request, $"Failed to add: {ex.Message}");    
        }

        return new DataFormActionResponse(request, $"{fullName} created", true)
        {
            NextUrl = $"page:/ObjectTypeDesigner?id={fullName}", 
        };
    }

    /// <summary>
    /// Get/create draft
    /// </summary>
    /// <param name="objectTypeName"></param>
    /// <returns></returns>
    [HttpGet("{objectTypeName}")]
    public async Task<ObjectTypeDraft> EditExistingObjectAsync([FromRoute] string objectTypeName) => await GetOrCreateAsync(objectTypeName);

    private async Task<ObjectTypeDraft> GetOrCreateAsync(string objectTypeName = null, Guid? objectTypeDraftId = null)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        ObjectTypeDraft draft = null;
        
        if (!objectTypeDraftId.HasValue)
        {
            if (string.IsNullOrEmpty(objectTypeName)) throw new BadRequestException("Missing Name");

            draft = await _connection.Filter<ObjectTypeDraft>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.EntityId, Context.UserId)
                .Eq(x => x.IsActive, true)
                .Eq(x => x.Name, objectTypeName)
                .SortDesc(x => x.CreatedOn)
                .FirstOrDefaultAsync();

            if (draft != null)
            {
                // little hack to use existing code 
                // existing.ObjectType.OverriddenFields = existing.OverriddenFields;
                // existing.OverriddenFields = null;
                
                await loadBaseObject(draft);

                return draft;
            }
        }
        else if (string.IsNullOrEmpty(objectTypeName))
        {
            // get name from existing draft
            var existing = await _connection.Filter<ObjectTypeDraft>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, objectTypeDraftId.Value)
                .FirstOrDefaultAsync();

            if (existing == null) throw new BadRequestException("Invalid Draft");
            objectTypeName = existing.ObjectType.FullName;
        }

        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName, new GetObjectOptions
        {
            LoadBaseObject = false,
        });

        if (objectType == null) throw NotFoundException.New("Object Type not found in namespace");
        
        var draftObjectType = await _objectTypeService.GetAsync(Context, ObjectTypeDraft.ObjectTypeFullName);

        draft = new ObjectTypeDraft
        {
            Id = objectTypeDraftId ?? Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            IsActive = true,
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            Name = objectType.FullName,
            Description = objectType.Description,
            ObjectType = objectType,
            FlowId = draftObjectType?.InitialFlowId,
            ObjectStatusId = draftObjectType?.ObjectStatusId,
            // BaseObjectType = baseObjectType,
        };

        await loadBaseObject(draft);

        draft.UpdateRelatedObjectTypes();
        
        if (objectTypeDraftId.HasValue)
        {
            var result = await _connection.Filter<ObjectTypeDraft>()
                .Eq(x => x.Id, objectTypeDraftId.Value)
                .ReplaceOneAsync(draft);

            // fire event?
            // probably not necessary is every change will trigger an event
            // ...
            
            return result.ModifiedCount == 1 ? draft : null;
        }

        draft = await _connection.InsertAsync(draft);

        // await _objectTypeService.FireCreateEventAsync(Context, draft);
        
        return draft;

        async Task loadBaseObject(ObjectTypeDraft draftObject)
        {
            var objType = draftObject?.ObjectType;
            var baseObjectType = !string.IsNullOrEmpty(objType?.BaseObjectType) ? await _objectTypeService.GetAsync(Context, objType.BaseObjectType) : null;

            // complete fields 
            if (baseObjectType != null)
            {
                foreach (var field in objType.Fields)
                {
                    if (baseObjectType.Fields.TryGetValue(field.Key, out var baseField))
                    {
                        ObjectTypeService.MergeField(field.Value, baseField);
                    }
                }

                baseObjectType.LoadedBaseObjectType = null;
                baseObjectType.OverriddenFields = null;

                draft.BaseObjectType = baseObjectType;
            }
        }
    }

    private async Task<ObjectTypeDraft> GetDraftAsync(Guid objectTypeDraftId)
    {
        var draft = await _connection.Filter<ObjectTypeDraft>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, objectTypeDraftId)
            .Eq(x => x.IsActive, true)
            .FirstOrDefaultAsync();

        if (draft == null) throw NotFoundException.New<ObjectTypeDraft>(objectTypeDraftId);

        return draft;
    }

    private async Task<(IEntityContext Context, AppProfile Profile)> BuildContextAsync(string profileOrRole)
    {
        if (Guid.TryParse(profileOrRole, out var profileId))
        {
            var profile = await _connection.Filter<AppProfile>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, profileId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            if (profile == null) throw NotFoundException.New<AppProfile>(profileId);

            return (ProfileContext.Create(profile.Id, Context.AccountId.Value, Context.UserId.Value, Context.ClientId), profile);
        }

        if (Enum.TryParse<EntityRoleId>(profileOrRole, out var roleId))
        {
            IEntityContext context = roleId switch
            {
                EntityRoleId.Admin => Context,
                EntityRoleId.Manager or EntityRoleId.User => UserContext.OrgUser(Context.UserId.Value, "Manager", roleId, Guid.Empty, Context.AccountId, Context.ClientId),
                _ => throw new BadRequestException("Unexpected Role"),
            };

            return (context, null);
        }

        throw new BadRequestException("Invalid profile");
    }

    private UpdateQuery<ExpandoObject> UpdateQuery(Guid objectTypeDraftId)
        => _connection.Filter<ExpandoObject>("ObjectType.Draft")
            .Eq(nameof(ObjectTypeDraft.AccountId), Context.AccountId)
            .Eq(Model.IdFieldName, objectTypeDraftId)
            .Update
            .Set(nameof(ObjectTypeDraft.LastModifiedOn), DateTime.UtcNow);

    private async Task CheckPermission(string objectTypeName, ObjectTypePermission permission)
    {
        var hasPermission = await _objectTypeService.HasPermission(Context, objectTypeName, permission);
        if (!hasPermission) throw new ForbiddenException("Access Forbidden");
    }


    public class DuplicateObjectTypeParameters
    {
        public string ObjectType { get; set; }
        public string Namespace { get; set; }
        public string Name { get; set; }
        public bool KeepBaseObjectType { get; set; }
    }
}