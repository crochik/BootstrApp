using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Bson;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.U2;
using PI.Shared.Services;

namespace Services;

public class RedirectionService
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public RedirectionService(MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    public async Task<Result<ShareLink>> CreateRedirectionAsync(IEntityContext context, string objectTypeName, Guid objectId, Guid shareTemplateId, string fieldName) // , [FromBody] DataFormActionRequest request
    {
        var objectType = await _objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null) return Result<ShareLink>.Error($"{objectTypeName}: Type Not found");

        if (string.IsNullOrWhiteSpace(fieldName)) return Result<ShareLink>.Error("Missing field");
        
        var obj = await _objectTypeService.GetFlatObjectAsync(context, objectType, objectId);
        if (obj == null) return Result<ShareLink>.Error($"{objectTypeName}: Object Not found");

        if (!objectType.Fields.TryGetValue(fieldName, out var field) || !field.RBAC.CanRead(context)) return Result<ShareLink>.Error($"{fieldName}: Invalid field");

        var redirection = default(ShareLink);
        if (!obj.TryGetFieldValue(fieldName, out var fieldValue) || fieldValue == null)
        {
            // check if can create one
            if (!field.RBAC.CanUpdate(context)) return Result<ShareLink>.Error($"{fieldName}: Access forbidden");
        }
        else
        {
            if (!fieldValue.TryToParseObjectId(out var existingShareId)) return Result<ShareLink>.Error("Bad request");

            redirection = await _connection.Filter<ShortLinkRedirection, ShareLink>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, existingShareId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

            // if (redirection == null) return DataFormActionResponse.Error(request, "Bad request");
        }

        if (redirection == null)
        {
            var template = await _connection.Filter<ShortLinkRedirection, ShareTemplate>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.Id, shareTemplateId)
                .Ne(x => x.IsActive, false)
                .Eq(x => x.MetaValues["ObjectType"], objectType.FullName)
                .FirstOrDefaultAsync();

            if (template == null) return Result<ShareLink>.Error("Invalid Template");

            redirection = new ShareLink
            {
                Id = Guid.NewGuid(),
                AccountId = context.AccountId.Value,
                EntityId = context.UserId.Value,
                CreatedOn = DateTime.UtcNow,
                IsActive = true,
                MetaValues = new Dictionary<string, object>
                {
                    // { "ObjectType", objectType.FullName },
                    // { "ObjectId", objectId },
                    // { "FieldName", fieldName },
                    // { "TemplateId", template.Id }
                },
                LastActor = context.Actor(),
            };

            var dataContext = new Dictionary<string, object>
            {
                { "Object", obj },
                { "ShareId", redirection.Id }
                // user, organization, account, ...
                // ...
            };

            object value = null;
            if (!ExpressionEvaluatorService.TryResolve(context, dataContext, template.Host, out value)) return Result<ShareLink>.Error("Couldn't resolve expression");
            redirection.Host = value?.ToString();

            if (!ExpressionEvaluatorService.TryResolve(context, dataContext, template.Name, out value)) return Result<ShareLink>.Error("Couldn't resolve expression");
            redirection.Name = value?.ToString();

            if (!ExpressionEvaluatorService.TryResolve(context, dataContext, template.Description, out value)) return Result<ShareLink>.Error("Couldn't resolve expression");
            redirection.Description = value?.ToString();

            if (!ExpressionEvaluatorService.TryResolve(context, dataContext, template.ShortCode, out value)) return Result<ShareLink>.Error("Couldn't resolve expression");
            redirection.ShortCode = value?.ToString();

            if (!ExpressionEvaluatorService.TryResolve(context, dataContext, template.Location, out value)) return Result<ShareLink>.Error("Couldn't resolve expression");
            redirection.Location = value?.ToString();

            foreach (var kvp in template.MetaValues)
            {
                if (!ExpressionEvaluatorService.TryResolve(context, dataContext, kvp.Value, out value)) continue;
                redirection.MetaValues[kvp.Key] = value;
            }

            // create redirection
            await _connection.InsertAsync(redirection);

            var modifiedFields = new Dictionary<string, object>();
            var expandoObject = await _objectTypeService.UpdateObjectAsync(
                context,
                objectType,
                objectId,
                q => q.Update.Set(FormField.GetPathInCollection(fieldName), redirection.Id),
                modifiedFields);

            if (expandoObject == null)
            {
                // error?
                //... 
            }
        }

        return Result.Success(redirection);
        
        // return new DataFormActionResponse(request, "Redirected", true)
        // {
        //     NextUrl = $"https://{redirection.Host}/{redirection.ShortCode}",
        // };
    }
}