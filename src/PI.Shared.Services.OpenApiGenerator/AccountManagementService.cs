using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PI.Shared.Constants;
using PI.Shared.Diff;
using PI.Shared.Models;
using PI.Shared.Models.Designer;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services.OpenApiGenerator;

public class AccountManagementService(ILogger<AccountManagementService> logger, MongoConnection connection, ObjectTypeService objectTypeService)
{
    public async Task<Result<ImportResult>> ImportAllAsync(Options options, Actor actor, CancellationToken stoppingToken)
    {
        if (!Directory.Exists(options.BasePath)) return Result.Error<ImportResult>($"Failed to import, {options.BasePath} does not exist.");

        var account = await connection.Filter<Entity, Account>()
            .Eq(x => x.Id, options.TargetAccountId)
            .FirstOrDefaultAsync();

        var user = account?.Settings?.OwnerId == null
            ? null
            : await connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, options.TargetAccountId)
                .Eq(x => x.Id, account.Settings.OwnerId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

        if (user == null) return Result.Error<ImportResult>("Failed to import, No Owner Id for account.");

        var importedObjects = await connection.Filter<ImportedObject>()
            .Eq(x => x.AccountId, account.Id)
            .FindAsync();

        foreach (var importedObject in importedObjects)
        {
            options.AddToMap(importedObject);
        }
        
        var ownerContext = user.Context.With(actor);

        var documents = 0;
        var errors = new List<string>();
        foreach (var entityType in options.EntityTypes)
        {
            if (stoppingToken.IsCancellationRequested) break;

            if (options.Namespaces == null)
            {
                // All namespaces
                var basePath = $"{options.BasePath}{entityType}/";
                var pathResult = await ProcessPathAsync(ownerContext, options, entityType, basePath);
                if (pathResult.IsError)
                {
                    errors.Add(pathResult.Status);
                    continue;
                }

                if (pathResult.IsSuccess)
                {
                    documents += pathResult.Value.Count;
                }

                if (pathResult.Value?.Errors?.Length > 0)
                {
                    errors.AddRange(pathResult.Value.Errors);
                }

                continue;
            }
            
            // selected namespaces
            foreach (var ns in options.Namespaces)
            {
                if (stoppingToken.IsCancellationRequested) break;

                var basePath = $"{options.BasePath}{entityType}/";
                var pathResult = await ProcessPathAsync(ownerContext, options, entityType, $"{basePath}{ns}/", basePath: basePath);
                if (pathResult.IsError)
                {
                    errors.Add(pathResult.Status);
                    continue;
                }

                if (pathResult.IsSuccess)
                {
                    documents += pathResult.Value.Count;
                }

                if (pathResult.Value?.Errors?.Length > 0)
                {
                    errors.AddRange(pathResult.Value.Errors);
                }
            }
        }

        return Result.Success(new ImportResult
        {
            Count = documents,
            Errors = errors.ToArray(),
        });
    }

    private async Task<Result<ImportResult>> ProcessPathAsync(IEntityContext context, Options options, EntityType entityType, string path, string basePath = null)
    {
        basePath ??= path;

        logger.LogInformation("Processing {Path}", path);

        var count = 0;
        var errors = new List<string>();

        if (!Directory.Exists(path)) return Result.Unknown<ImportResult>($"Directory {path} does not exist");

        var files = Directory.GetFiles(path, "*.json");
        files.Sort();

        foreach (var file in files)
        {
            var subpath = file[basePath.Length..^5].Split("/");
            var fullName = string.Join('.', subpath);
            logger.LogInformation(">> Processing {FullName}", fullName);

            var json = await File.ReadAllTextAsync(file);
            try
            {
                var result = entityType switch
                {
                    // EntityType.Profile 
                    EntityType.ObjectStatus => await ProcessObjectStatusAsync(context, options, fullName, json),
                    EntityType.EventType => await ProcessEventTypeAsync(context, options, fullName, json),
                    EntityType.Flow => await ProcessFlowAsync(context, options, fullName, json),
                    EntityType.ObjectType => await ProcessObjectTypeAsync(context, options, fullName, json),
                    EntityType.AppPage => await ProcessPageAsync(context, options, fullName, json),
                    EntityType.FlowAction => await ProcessFlowActionsAsync(context, options, fullName, json),
                    _ => false,
                };

                if (result)
                {
                    count++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to import {FullName}", fullName);
                errors.Add($"Failed to import {fullName}");
            }
        }

        // process sub folders
        var dirs = Directory.GetDirectories(path);
        foreach (var dir in dirs)
        {
            if (dir.Contains("/."))
            {
                // hidden folder
                continue;
            }

            var pathResult = await ProcessPathAsync(context, options, entityType, dir, basePath: basePath);
            if (pathResult.IsError)
            {
                errors.Add($"{dir}: {pathResult.Status}");
                continue;
            }

            if (pathResult.IsSuccess) count += pathResult.Value.Count;
            if (pathResult.Value?.Errors?.Length > 0)
            {
                errors.AddRange(pathResult.Value.Errors);
            }
        }

        return Result.Success(new ImportResult
        {
            Count = count,
            Errors = errors.IsEmpty() ? null : errors.ToArray(),
        });
    }

    private async Task<bool> ProcessFlowActionsAsync(IEntityContext context, Options options, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<GenericAction>(json);

        // profile 
        // dst.ProfileIds
        
        return await ProcessProfileElementAsync(context, options, fullName, dst, options.UpdateFlowAction);
    }

    private async Task<bool> ProcessPageAsync(IEntityContext context, Options options, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<AppPage>(json);

        // map profile ids?
        // dst.ProfileIds
        // ...
        
        return await ProcessProfileElementAsync(context, options, fullName, dst, options.UpdatePage);
    }

    private async Task<bool> ProcessObjectStatusAsync(IEntityContext context, Options options, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<ObjectStatus>(json);

        // does not depend on any other

        return await ProcessObjectAsync(context, options, fullName, dst, options.UpdateObjectStatus);
    }

    private async Task<bool> ProcessEventTypeAsync(IEntityContext context, Options options, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<EventType>(json);

        if (options.PreserveIds) return await ProcessObjectAsync(context, options, fullName, dst, options.UpdateEventType);

        // object status
        if (dst.Trigger is { } trigger)
        {
            trigger.ObjectStatusId = options.MapObjectStatus(trigger.ObjectStatusId);
        }

        if (dst.Trigger is UserTrigger userTrigger)
        {
            // profile
            if (userTrigger.ProfileIds?.Length > 0)
            {
                // ... 
            }
        }

        return await ProcessObjectAsync(context, options, fullName, dst, options.UpdateEventType);
    }
    
    private async Task<bool> ProcessFlowAsync(IEntityContext context, Options options, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<Flow>(json);

        if (options.PreserveIds) return await ProcessObjectAsync(context, options, fullName, dst, options.UpdateFlow);

        foreach (var evt in dst.Steps)
        {
            // for events that are not defined in the flow (user, scheduled, ... )
            if (options.IsMapped(nameof(EventType), evt.EventIdTrigger, out var eventTypeId))
            {
                evt.EventIdTrigger = eventTypeId;
            }
            
            // trigger object status id
            evt.CurrentStatusId = options.MapObjectStatus(evt.CurrentStatusId);

            if (evt.ActionId == ActionIds.SetObjectStatus)
            {
                // TODO: map object status
                // ...
            }

            if (evt.ActionId == ActionIds.FireEvent)
            {
                // TODO: map event
                // ...
            }

            // TODO: other actions? update, create, ...  
            // ...
        }

        return await ProcessObjectAsync(context, options, fullName, dst, options.UpdateFlow);
    }

    private async Task<bool> ProcessObjectTypeAsync(IEntityContext context, Options options, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<ObjectType>(json);

        if (options.PreserveIds) return await ProcessObjectTypeAsync(context, options, fullName, dst);

        dst.FlowId = options.MapFlow(dst.FlowId);
        dst.ObjectStatusId = options.MapObjectStatus(dst.ObjectStatusId);

        dst.InitialFlowId = options.MapFlow(dst.InitialFlowId);
        dst.InitialObjectStatusId = options.MapObjectStatus(dst.InitialObjectStatusId);

        if (dst.Fields.TryGetValue(nameof(IFlowObject.FlowId), out var flowField))
        {
            if (flowField.Field.DefaultValue.TryToParseObjectId(out var value))
            {
                flowField.Field.DefaultValue = options.MapFlow(value)?.ToString();
            }
            if (flowField.InitialValue.TryToParseObjectId(out  value))
            {
                flowField.InitialValue = options.MapFlow(value)?.ToString();
            }
        }
        
        if (dst.Fields.TryGetValue(nameof(IFlowObject.ObjectStatusId), out var objectStatusField))
        {
            if (objectStatusField.Field.DefaultValue.TryToParseObjectId(out var value))
            {
                objectStatusField.Field.DefaultValue = options.MapObjectStatus(value)?.ToString();
            }
            if (objectStatusField.InitialValue.TryToParseObjectId(out  value))
            {
                objectStatusField.InitialValue = options.MapObjectStatus(value)?.ToString();
            }
        }

        if (!dst.IsEmbedded && dst.Fields.ContainsKey(nameof(Model.AccountId)))
        {
            // there is accountId field so it should have constraint
            if (dst.Constraints!=null && dst.Constraints.TryGetValue(nameof(EntityRoleId.Account), out var accountConstraint) )
            {
                accountConstraint = new Criteria
                {
                    Conditions = (accountConstraint.Conditions ?? Enumerable.Empty<Condition>())
                        .Where(x => x.FieldName != nameof(Model.AccountId))
                        .Append(Condition.Eq(nameof(Model.AccountId), options.TargetAccountId.ToString() )) // "{{context \"AccountId\"}}"
                        .ToArray(),
                };
            }
            else
            {
                accountConstraint = new Criteria
                {
                    Conditions = [Condition.Eq(nameof(Model.AccountId), options.TargetAccountId.ToString())] // "{{context \"AccountId\"}}"
                };
            }

            // constraints 
            dst.Constraints ??= new Dictionary<string, Criteria>();
            dst.Constraints[nameof(EntityRoleId.Account)] = accountConstraint;
        }
        
        // TODO: object type rbac (profile)
        // ...

        // TODO: fields rbac (profile)
        // ...

        return await ProcessObjectTypeAsync(context, options, fullName, dst);
    }

    private async Task<bool> ProcessObjectAsync<T>(IEntityContext context, Options options, string fullName, T dst, Func<T, DiffResult, UpdateQuery<T>, bool> prepare) where T : EntityOwnedModel
    {
        var doc = await FindExistingAsync(context, dst, options);

        if (doc == null)
        {
            logger.LogInformation("New {Object} {Id}: {Name}", typeof(T).Name, dst.Id, dst.Name);

            if (prepare(dst, null, null))
            {
                var added = await AddObjectAsync(context, dst, options);
                return added != null;
            }

            return true;
        }

        var src = BsonSerializer.Deserialize<T>(doc);
        var diff = SimpleDiffer.Diff(src, dst, new SimpleDiffOptions
        {
            SkipBsonIgnore = true,
            ExcludeProperty = (type, info) =>
            {
                if (type == typeof(T))
                {
                    return info.Name switch
                    {
                        // not really important
                        nameof(EntityOwnedModel.LastModifiedOn) => true,
                        nameof(EntityOwnedModel.LastActor) => true,
                        nameof(EntityOwnedModel.CreatedOn) => true,
                        // account specific (never change)
                        nameof(EntityOwnedModel.AccountId) => true,
                        nameof(EntityOwnedModel.EntityId) => true,
                        nameof(EntityOwnedModel.Id) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        if (diff == null)
        {
            logger.LogInformation("No changes detected for {Path}", fullName);
            return false;
        }

        var query = GetUpdateQuery(context, src, diff);
        
        query.Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        logger.LogInformation("Changes for {Path}", fullName);

        if (prepare(dst, diff, query))
        {
            var after = await query.UpdateAndGetOneAsync();
        }

        return true;
    }
    
    private async Task<bool> ProcessProfileElementAsync<T>(IEntityContext context, Options options, string fullName, T dst, Func<T, DiffResult, UpdateQuery<T>, bool> prepare) where T : AppProfileElement
    {
        var doc = await FindProfileElementAsync(context, dst, options);

        if (doc == null)
        {
            logger.LogInformation("New {Object} {Id}: {Name}", typeof(T).Name, dst.Id, dst.Name);

            if (prepare(dst, null, null))
            {
                var added = await AddProfileElementAsync(context, dst, options);
                return added != null;
            }

            return true;
        }

        var src = BsonSerializer.Deserialize<T>(doc);
        var diff = SimpleDiffer.Diff(src, dst, new SimpleDiffOptions
        {
            SkipBsonIgnore = true,
            ExcludeProperty = (type, info) =>
            {
                if (type == typeof(T))
                {
                    return info.Name switch
                    {
                        // not really important
                        nameof(AppProfileElement.LastModifiedOn) => true,
                        nameof(AppProfileElement.LastActor) => true,
                        nameof(AppProfileElement.CreatedOn) => true,
                        // account specific (never change)
                        nameof(AppProfileElement.AccountId) => true,
                        nameof(AppProfileElement.Id) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        if (diff == null)
        {
            logger.LogInformation("No changes detected for {Path}", fullName);
            return false;
        }

        var query = GetUpdateQuery(context, src, diff);
        
        query.Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        logger.LogInformation("Changes for {Path}", fullName);

        if (prepare(dst, diff, query))
        {
            var after = await query.UpdateAndGetOneAsync();
        }

        return true;
    }

    private UpdateQuery<T> GetUpdateQuery<T>(IEntityContext context, T src, DiffResult diff) where T : IModel
    {
        var query = connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, src.Id)
            .Update;

        diff.Traverse((path, name, t, o, n) =>
        {
            var full = string.Join(".", path.Append(name));

            if (t == DiffType.Unset)
            {
                query.Unset(full);
                return;
            }

            // special serializers 
            if (n is IDictionary dict)
            {
                var newDict = new Dictionary<string, object>();
                foreach (var key in dict.Keys)
                {
                    if (key == null) continue;
                    newDict[$"{key}"] = dict[key];
                }

                query.Set(full, newDict);
                return;
            }

            query.Set(full, n);
        }, []);
        
        return query;
    }

    private async Task<bool> ProcessObjectTypeAsync(IEntityContext context, Options options, string fullName, ObjectType dst)
    {
        var doc = await connection.Filter<ObjectType>("ObjectType.1")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.FullName, fullName) // ALWAYS use name instead of id
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.EntityId)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .FirstOrDefaultAsync<BsonDocument>();

        if (doc == null)
        {
            logger.LogInformation("New {Object} {Id}: {Name}", nameof(ObjectType), dst.Id, dst.Name);

            if (options.UpdateObjectType(dst, null, null))
            {
                var added = await AddObjectAsync(context, dst, options);
                return added != null;
            }

            return true;
        }
        
        var src = BsonSerializer.Deserialize<ObjectType>(doc);
        
        // id doesn't matter - make it match 
        dst.Id = src.Id;
        
        var diff = SimpleDiffer.Diff(src, dst, new SimpleDiffOptions
        {
            SkipBsonIgnore = true,
            ExcludeProperty = (type, info) =>
            {
                if (type == typeof(ObjectType))
                {
                    return info.Name switch
                    {
                        nameof(ObjectType.LoadedBaseObjectType) => true,
                        nameof(ObjectType.LastModifiedOn) => true,
                        nameof(ObjectType.LastActor) => true,
                        nameof(ObjectType.CreatedOn) => true,
                        nameof(ObjectType.OverriddenFields) => true,
                        nameof(ObjectType.RelatedObjectTypes) => true,
                        nameof(ObjectType.Id) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        if (diff == null)
        {
            logger.LogInformation("No changes detected for {Path}", fullName);
            return false;
        }

        if (options.CreateObjectTypeDrafts)
        {
            if (!options.UpdateObjectType(dst, diff, null)) return true;
            
            // deactivate any with pending changes
            await connection.Filter<ObjectTypeDraft>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.UserId.Value)
                .Eq(x => x.Name, fullName)
                .Ne(x => x.IsActive, false)
                .Update
                .Set(x => x.IsActive, false)
                .Set(x => x.Description, "Removed as part of import")
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateManyAsync();

            // fill blanks
            dst.AccountId = context.AccountId.Value;
            dst.EntityId = context.AccountId.Value;

            var draftObjectType = await objectTypeService.GetAsync(context, ObjectTypeDraft.ObjectTypeFullName);

            var draft = new ObjectTypeDraft
            {
                Id = Guid.NewGuid(),
                AccountId = context.AccountId.Value,
                EntityId = context.UserId.Value,
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                LastActor = context.Actor(),
                Name = dst.FullName,
                Description = diff?.ToChangeList(), //  TODO: 
                ObjectType = dst,
                FlowId = draftObjectType?.InitialFlowId,
                ObjectStatusId = draftObjectType?.InitialObjectStatusId,
                Tags = ["Import"],
                // BaseObjectType = baseObjectType,
            };

            draft.UpdateRelatedObjectTypes();
            draft = await connection.InsertAsync(draft);
            return true;
        }

        // update directly
        var query = GetUpdateQuery(context, src, diff);
        
        query.Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);
        
        if (options.UpdateObjectType(dst, diff, query))
        {
            var after = await query.UpdateAndGetOneAsync();
        }

        return true;
    }

    private async Task<BsonDocument> FindExistingAsync<T>(IEntityContext context, T src, Options options) where T : EntityOwnedModel
    {
        var id = src.Id;
        if (!options.PreserveIds && !options.IsMapped(src, out id))
        {
            // first time 
            return null;
        }

        return await connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.EntityId)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .FirstOrDefaultAsync<BsonDocument>();
    }

    private async Task<BsonDocument> FindProfileElementAsync<T>(IEntityContext context, T src, Options options) where T : AppProfileElement
    {
        var id = src.Id;
        if (!options.PreserveIds && !options.IsMapped(src, out id))
        {
            // first time 
            return null;
        }

        return await connection.Filter<T>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, id)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .FirstOrDefaultAsync<BsonDocument>();
    }
    
    private async Task<T> AddObjectAsync<T>(IEntityContext context, T dst, Options options) where T : EntityOwnedModel
    {
        var importedObject = new ImportedObject
        {
            Id = Guid.CreateVersion7(),
            AccountId = context.AccountId.Value,
            ObjectId = options.PreserveIds ? dst.Id : Guid.NewGuid(),
            Name = dst.Name,
            ObjectType = dst.GetType().Name,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            // source
            SourceAccountId = dst.AccountId,
            SourceObjectId = dst.Id,
        };

        // account specific ids 
        dst.Id = importedObject.ObjectId;
        dst.AccountId = context.AccountId.Value;
        dst.EntityId = context.AccountId.Value;
        // 
        dst.CreatedOn = DateTime.UtcNow;
        dst.LastActor = context.Actor();
        dst.LastModifiedOn = DateTime.UtcNow;

        dst = await connection.InsertAsync(dst);
        await connection.InsertAsync(importedObject);
        options.AddToMap(importedObject);

        return dst;
    }

    private async Task<T> AddProfileElementAsync<T>(IEntityContext context, T dst, Options options) where T : AppProfileElement
    {
        var importedObject = new ImportedObject
        {
            Id = Guid.CreateVersion7(),
            AccountId = context.AccountId.Value,
            ObjectId = options.PreserveIds ? dst.Id : Guid.NewGuid(),
            Name = dst.Name,
            ObjectType = dst.GetType().Name,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            // source
            SourceAccountId = dst.AccountId,
            SourceObjectId = dst.Id,
        };

        // account specific ids 
        dst.Id = importedObject.ObjectId;
        dst.AccountId = context.AccountId.Value;
        dst.CreatedOn = DateTime.UtcNow;
        dst.LastActor = context.Actor();
        dst.LastModifiedOn = DateTime.UtcNow;

        dst = await connection.InsertAsync(dst);
        await connection.InsertAsync(importedObject);
        options.AddToMap(importedObject);

        return dst;
    }

    public class ImportResult
    {
        public string[] Errors { get; set; }
        public int Count { get; set; }
    }

    public enum EntityType
    {
        ObjectType,
        EventType,
        Flow,
        ObjectStatus,
        AppPage,
        FlowAction,
        Form,
        FormLayout,
    }

    public class Options
    {
        public string BasePath { get; init; }
        public string[] Namespaces { get; init; }

        public EntityType[] EntityTypes { get; init; } =
        [
            // profiles ?
            // ...
            EntityType.ObjectStatus,
            EntityType.EventType,
            EntityType.FlowAction,
            EntityType.Flow,
            EntityType.ObjectType,
            EntityType.AppPage,
        ];

        public Func<ObjectStatus, DiffResult, UpdateQuery<ObjectStatus>, bool> UpdateObjectStatus { get; init; } = (dst, diff, query) => false;
        public Func<GenericAction, DiffResult, UpdateQuery<GenericAction>, bool> UpdateFlowAction { get; init; } = (dst, diff, query) => false;
        public Func<AppPage, DiffResult, UpdateQuery<AppPage>, bool> UpdatePage { get; init; } = (dst, diff, query) => false;
        public Func<Flow, DiffResult, UpdateQuery<Flow>, bool> UpdateFlow { get; init; } = (dst, diff, query) => false;
        public Func<EventType, DiffResult, UpdateQuery<EventType>, bool> UpdateEventType { get; init; } = (dst, diff, query) => false;
        public Func<ObjectType, DiffResult, UpdateQuery<ObjectType>, bool> UpdateObjectType { get; init; } = (dst, diff, query) => false;

        public bool PreserveIds { get; init; } = false;
        public Guid TargetAccountId { get; init; }

        private Dictionary<string, Guid> Mapping { get; init; } = new();
        public bool CreateObjectTypeDrafts { get; init; } = true;

        public bool IsMapped<T>(T model, out Guid targetId) where T : IModel
            => IsMapped(typeof(T).Name, model.Id, out targetId);

        public bool IsMapped(string objectType, Guid sourceId, out Guid targetId)
        {
            var key = $"{objectType}:{sourceId}";
            return Mapping.TryGetValue(key, out targetId);
        }

        public void AddToMap(ImportedObject importedObject)
        {
            var key = $"{importedObject.ObjectType}:{importedObject.SourceObjectId}";
            Mapping.Add(key, importedObject.ObjectId);
        }
        
        public Guid? MapObjectStatus(Guid? dstObjectStatusId)
        {
            if (!dstObjectStatusId.HasValue) return null;
        
            if (IsMapped(nameof(ObjectStatus), dstObjectStatusId.Value, out var objectStatusId))
            {
                return objectStatusId;
            }
        
            return Guid.Empty;
        }

        public Guid? MapFlow(Guid? dstFlowId)
        {
            if (!dstFlowId.HasValue) return null;

            if (IsMapped(nameof(Flow), dstFlowId.Value, out var flowId))
            {
                return flowId;
            }
            
            return Guid.Empty;
        }
    }
}

[BsonCollection("management.ImportedObjects")]
public class ImportedObject : Model
{
    public Guid ObjectId { get; set; }
    public string ObjectType { get; set; }

    public Guid SourceAccountId { get; set; }
    public Guid SourceObjectId { get; set; }
}