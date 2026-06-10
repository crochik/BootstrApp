using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace PI.OpenAPI.Controllers;

[Obsolete("moving to AccountManagentService")]
[Authorize("admin")] // TODO: should it be root?
[Route("/openapi/v1/[controller]")]
public class AccountController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public AccountController(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }


    [HttpPost("CopySystemObjects")]
    public async Task<IActionResult> CopySystemObjectsAsync([FromQuery] string name)
    {
        var targetAccountId = Guid.NewGuid(); //  Guid.Parse("c5500000-0000-0000-0000-000000000000");
        var account = new Account
        {
            Id = targetAccountId,
            AccountId = targetAccountId,
            EntityId = targetAccountId,
            CreatedOn = DateTime.UtcNow,
            Name = name ?? $"Test Account: {DateTime.Now.ToShortDateString()}",
        };
        
        await _connection.InsertAsync(account);
        
        var sourceAccountId = Context.AccountId.Value;

        // await _connection.Filter<ObjectType>()
        //     .Eq(x => x.AccountId, targetAccountId)
        //     .DeleteAsync();

        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, sourceAccountId)
            .Eq(x => x.Namespace, null)
            .FindAsync();

        var flowIds = new Dictionary<Guid, Guid>();
        var objectStatusIds = new Dictionary<Guid, Guid>();

        foreach (var objectType in objectTypes)
        {
            objectType.Id = Guid.NewGuid();
            objectType.AccountId = targetAccountId;
            objectType.EntityId = targetAccountId;

            if (objectType.FlowId.HasValue)
            {
                // flow placeholder
                if (!flowIds.TryGetValue(objectType.FlowId.Value, out var flowId))
                {
                    flowId = Guid.NewGuid();
                    flowIds.Add(objectType.FlowId.Value, flowId);
                }

                objectType.FlowId = flowId;
            }

            if (objectType.ObjectStatusId.HasValue)
            {
                // initial status placeholder
                if (!objectStatusIds.TryGetValue(objectType.ObjectStatusId.Value, out var objectStatusId))
                {
                    objectStatusId = Guid.NewGuid();
                    objectStatusIds.Add(objectType.ObjectStatusId.Value, objectStatusId);
                }

                objectType.ObjectStatusId = objectStatusId;
            }

            if (objectType.InitialFlowId.HasValue)
            {
                // flow placeholder
                if (!flowIds.TryGetValue(objectType.InitialFlowId.Value, out var flowId))
                {
                    flowId = Guid.NewGuid();
                    flowIds.Add(objectType.InitialFlowId.Value, flowId);
                }

                objectType.InitialFlowId = flowId;
            }

            if (objectType.InitialObjectStatusId.HasValue)
            {
                // initial status placeholder
                if (!objectStatusIds.TryGetValue(objectType.InitialObjectStatusId.Value, out var objectStatusId))
                {
                    objectStatusId = Guid.NewGuid();
                    objectStatusIds.Add(objectType.InitialObjectStatusId.Value, objectStatusId);
                }

                objectType.InitialObjectStatusId = objectStatusId;
            }

            objectType.RBAC = new ObjectTypeRBAC
            {
                // remove profiles?
                Permissions = new Dictionary<string, ObjectTypePermission>((objectType.RBAC?.Permissions ?? [])
                    .Where(x => !Guid.TryParse(x.Key, out _))
                ),
            };

            objectType.LastActor = null;
            objectType.LastModifiedOn = null;
            objectType.CreatedOn = DateTime.UtcNow;
            objectType.DatabaseName = null;
            objectType.NativeType = null;
            objectType.Label ??= objectType.Name;
            objectType.LabelPlural ??= objectType.Label;

            // TODO: recalculate other relations
            // ???
            var relatedObjectTypes = (objectType.RelatedObjectTypes ?? []);
            foreach (var relatedObjectType in relatedObjectTypes)
            {
                relatedObjectType.RBAC = new RelatedObjectTypeRBAC
                {
                    // remove profiles?
                    Permissions = new Dictionary<string, RelatedObjectTypePermission>((relatedObjectType.RBAC?.Permissions ?? [])
                        .Where(x => !Guid.TryParse(x.Key, out _) && x.Value == RelatedObjectTypePermission.Read)
                    ),
                };
            }

            relatedObjectTypes = relatedObjectTypes.Where(x => x.RBAC.Permissions.Count > 0)
                .ToArray();

            objectType.RelatedObjectTypes = relatedObjectTypes;

            objectType.Fields ??= new Dictionary<string, FieldTemplate>();
            foreach (var field in objectType.Fields)
            {
                field.Value.IsFinal = true;
                field.Value.RBAC = new FieldRBAC
                {
                    Permissions = new Dictionary<string, FieldPermission>((field.Value.RBAC?.Permissions ?? [])
                        .Where(x => !Guid.TryParse(x.Key, out _))
                    ),
                };
            }

            if (objectType.IsEmbedded)
            {
                // ...
            }
            else
            {
                // top level
                objectType.Constraints ??= new Dictionary<string, Criteria>();
                if (!objectType.Constraints.TryGetValue("Account", out var accountConstraints))
                {
                    accountConstraints = new Criteria();
                    objectType.Constraints.Add("Account", accountConstraints);
                }

                // remove account id constraints
                var constraints = (accountConstraints.Conditions ?? [])
                    .Where(x => x.FieldName != "AccountId");

                if (objectType.BaseObjectType == null)
                {
                    // top level, add account constraint
                    constraints = constraints.Prepend(Condition.Eq("AccountId", "{{context \"AccountId\"}}"));
                }

                accountConstraints.Conditions = constraints.ToArray();
            }

            objectType.Tags = (objectType.Tags ?? []).Where(x => x != "OTG").ToArray();
            try
            {
                await _connection.InsertAsync(objectType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {objectType.FullName}");
            }
        }

        // await _connection.InsertManyAsync(objectTypes);

        // TODO: create/copy flows for ObjectType and objects of this type
        // TODO: create/copy object status for ObjectType and objects of this type
        // TODO: create/copy events for ObjectType and objects of this type
        // TODO: recalculate relations for ObjectType and objects of this type

        // TODO: views
        // ...
        // TODO: form layouts
        // ... 

        return Ok(objectTypes.Count);
    }

    [HttpPost("CopyCustomPages")]
    public async Task<IActionResult> CopyCustomPagesAsync()
    {
        var sourceAccountId = Context.AccountId.Value;
        var targetAccountId = Guid.Parse("c5500000-0000-0000-0000-000000000000");

        // await _connection.Filter<AppPage>()
        //     .Eq(x => x.AccountId, targetAccountId)
        //     .DeleteAsync();

        var pages = await _connection.Filter<AppPage>()
            .Eq(x => x.AccountId, sourceAccountId)
            .Eq(x => x.Role, null)
            .Eq(x => x.ProfileIds, null)
            .Ne(x => x.IsActive, false)
            .Eq($"{nameof(AppPage.Page)}._t", "CustomPage")
            .FindAsync();

        foreach (var page in pages)
        {
            page.Id = Guid.NewGuid();
            page.AccountId = targetAccountId;
            page.CreatedOn = DateTime.MaxValue;
            page.LastActor = null;
            page.LastModifiedOn = null;

            await _connection.InsertAsync(page);
        }

        return Ok(pages.Count);
    }    
    
    [HttpPost("CopyActions")]
    public async Task<IActionResult> CopyActionsAsync()
    {
        var sourceAccountId = Context.AccountId.Value;
        var targetAccountId = Guid.Parse("c5500000-0000-0000-0000-000000000000");

        // await _connection.Filter<AppPage>()
        //     .Eq(x => x.AccountId, targetAccountId)
        //     .DeleteAsync();

        var actions = await _connection.Filter<GenericAction>()
            .Eq(x => x.AccountId, sourceAccountId)
            // .Eq(x => x.Role, null)
            .Eq(x => x.ProfileIds, null)
            .Ne(x => x.IsActive, false)
            .FindAsync();

        foreach (var action in actions)
        {
            action.Id = Guid.NewGuid();
            action.AccountId = targetAccountId;
            action.CreatedOn = DateTime.MaxValue;
            action.LastActor = null;
            action.LastModifiedOn = null;

            await _connection.InsertAsync(action);
        }

        return Ok(actions.Count);
    }
}