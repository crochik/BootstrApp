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
using PI.Shared.Diff;
using PI.Shared.Models;
using PI.Shared.Models.Designer;
using PI.Shared.Services;

namespace PI.OpenAPI.Services.Jobs;

public enum EntityType
{
    ObjectType,
    EventType,
    Flow,
    ObjectStatus,
}

public class ImportObjectTypesJob(ILogger<ImportObjectTypesJob> logger, MongoConnection connection, ObjectTypeService objectTypeService) : IRunJob
{
    private string BasePath => "/Users/felipe/DEVELOPMENT/github/OTGSchema/"; // PISchema

    // private string[] Namespaces => [ "scheduler" ];
    private string[] Namespaces => ["m2", "docuseal", "fcb2b", "otg", "ai", "openapi", "u2", "fci", "idp", "scheduler"]; // 

    private EntityType[] EntityTypes => [EntityType.ObjectStatus, EntityType.Flow, EntityType.ObjectType, EntityType.EventType];
    // private EntityType[] EntityTypes => [ EntityType.ObjectType ];

    private bool CreateMissing => true;
    private bool DryRun => false;

    public string Name => "ImportObjectTypes";

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        if (!Directory.Exists(BasePath))
        {
            return new JobResult
            {
                Message = $"Failed to import, {BasePath} does not exist.",
            };
        }

        var account = await connection.Filter<Entity, Account>()
            .Eq(x => x.Id, context.AccountId.Value)
            .FirstOrDefaultAsync();

        var user = account?.Settings?.OwnerId == null
            ? null
            : await connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Id, account.Settings.OwnerId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();

        if (user == null)
        {
            return new JobResult
            {
                Message = $"Failed to import, No Owner Id for account.",
            };
        }

        var documents = 0;
        foreach (var ns in Namespaces)
        {
            foreach (var entityType in EntityTypes)
            {
                var basePath = $"{BasePath}{entityType}/";
                documents += await ProcessPath(user.Context.WithActorFrom(context), entityType, $"{basePath}{ns}/", basePath: basePath);
            }
        }

        return new JobResult
        {
            Message = $"Imported {documents} documents",
        };
    }

    private async Task<int> ProcessPath(IEntityContext context, EntityType entityType, string path, string basePath = null)
    {
        basePath ??= path;

        logger.LogInformation("Processing {Path}", path);

        var count = 0;

        if (!Directory.Exists(path))
        {
            logger.LogWarning("Directory {Path} does not exist", path);
            return 0;
        }

        var files = Directory.GetFiles(path, "*.json");
        files.Sort();

        foreach (var file in files)
        {
            var subpath = file[basePath.Length..^5].Split("/");
            var fullName = string.Join('.', subpath);
            logger.LogInformation(">> Processing {FullName}", fullName);

            var json = File.ReadAllText(file);
            try
            {
                var result = entityType switch
                {
                    EntityType.ObjectType => await ProcessObjectTypeAsync(context, fullName, json),
                    EntityType.ObjectStatus => await ProcessObjectStatusAsync(context, fullName, json),
                    EntityType.Flow => await ProcessFlowAsync(context, fullName, json),
                    EntityType.EventType => await ProcessEventTypeAsync(context, fullName, json),
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

            count += await ProcessPath(context, entityType, dir, basePath: basePath);
        }

        return count;
    }

    private async Task<bool> ProcessObjectStatusAsync(IEntityContext context, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<ObjectStatus>(json);

        var doc = await connection.Filter<ObjectStatus>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, dst.Id)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .FirstOrDefaultAsync<BsonDocument>();

        if (doc == null)
        {
            logger.LogInformation("New Object Status {Id} {Status}", dst.Id, dst.Name);

            if (CreateMissing && !DryRun)
            {
                if (GetConfirmation($"New Object Status: \"{dst.Name}\"", "Add ObjectStatus?"))
                {
                    dst.CreatedOn = DateTime.UtcNow;
                    dst.AccountId = context.AccountId.Value;
                    dst.EntityId = context.AccountId.Value;
                    dst.LastActor = context.Actor();
                    dst.LastModifiedOn = DateTime.UtcNow;

                    await connection.InsertAsync(dst);
                }
            }

            return true;
        }

        var src = BsonSerializer.Deserialize<ObjectStatus>(doc);
        var diff = SimpleDiffer.Diff(src, dst, new SimpleDiffOptions
        {
            SkipBsonIgnore = true,
            ExcludeProperty = (type, info) =>
            {
                if (type == typeof(ObjectStatus))
                {
                    return info.Name switch
                    {
                        nameof(ObjectStatus.LastModifiedOn) => true,
                        nameof(ObjectStatus.LastActor) => true,
                        nameof(ObjectStatus.CreatedOn) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        var differences = diff?.ToChangeList();
        if (differences == null)
        {
            logger.LogInformation("No changes detected for {ObjectStatus}", fullName);
            return false;
        }

        if (DryRun)
        {
            logger.LogInformation("Changes for {ObjectStatus}: {Differences}", fullName, differences);
            return true;
        }

        logger.LogInformation("Changes for {ObjectStatus}:\n{Differences}", fullName, differences);

        var query = GetUpdateQuery(context, src, diff);
        var filter = query.GetFilterAsBsonDocument().ToString();
        var update = query.GetUpdateAsBsonDocument().ToString();
        if (GetConfirmation($"Object Status \"{fullName}\" was modified", differences, $"FILTER: {filter}", $"UPDATE: {update}", "Update Event Type?"))
        {
            var after = await query.UpdateAndGetOneAsync();
        }

        return true;
    }

    private bool GetConfirmation(params IEnumerable<string> messages)
    {
        Console.WriteLine("---------------------------------------------------------------------------");
        foreach (var message in messages)
        {
            Console.WriteLine(message);
        }

        Console.WriteLine("(Y)es, (N)o");
        var response = Console.ReadKey(true).Key;
        return response switch
        {
            ConsoleKey.Y => true,
            _ => false,
        };
    }

    private async Task<bool> ProcessFlowAsync(IEntityContext context, string fullName, string json)
    {
        await Task.CompletedTask;
        return false;
    }

    private async Task<bool> ProcessEventTypeAsync(IEntityContext context, string fullName, string json)
    {
        var dst = BsonSerializer.Deserialize<EventType>(json);

        var doc = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, dst.Id)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .FirstOrDefaultAsync<BsonDocument>();

        if (doc == null)
        {
            logger.LogInformation("New Event Type {Id} {Name}", dst.Id, dst.Name);

            if (CreateMissing && !DryRun)
            {
                if (GetConfirmation($"New Event Type: {dst.Name}", "Add Event Type?"))
                {
                    dst.CreatedOn = DateTime.UtcNow;
                    dst.AccountId = context.AccountId.Value;
                    dst.EntityId = context.AccountId.Value;
                    dst.LastActor = context.Actor();
                    dst.LastModifiedOn = DateTime.UtcNow;

                    await connection.InsertAsync(dst);
                }
            }

            return true;
        }

        var src = BsonSerializer.Deserialize<EventType>(doc);
        var diff = SimpleDiffer.Diff(src, dst, new SimpleDiffOptions
        {
            SkipBsonIgnore = true,
            ExcludeProperty = (type, info) =>
            {
                if (type == typeof(EventType))
                {
                    return info.Name switch
                    {
                        nameof(EventType.LastModifiedOn) => true,
                        nameof(EventType.LastActor) => true,
                        nameof(EventType.CreatedOn) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        var differences = diff?.ToChangeList();
        if (differences == null)
        {
            logger.LogInformation("No changes detected for {ObjectType}", fullName);
            return false;
        }

        if (DryRun)
        {
            logger.LogInformation("Changes for {ObjectType}: {Differences}", fullName, differences);
            return true;
        }

        logger.LogInformation("Changes for {EventType}:\n{Differences}", fullName, differences);

        var query = GetUpdateQuery(context, src, diff);
        var filter = query.GetFilterAsBsonDocument().ToString();
        var update = query.GetUpdateAsBsonDocument().ToString();
        if (GetConfirmation($"Event Type \"{fullName}\" was modified", differences, $"FILTER: {filter}", $"UPDATE: {update}", "Update Event Type?"))
        {
            var after = await query.UpdateAndGetOneAsync();
        }

        return true;
    }

    private UpdateQuery<T> GetUpdateQuery<T>(IEntityContext context, T src, DiffResult diff) where T : EntityOwnedModel
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

        query.Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow);

        return query;
    }

    private async Task<bool> ProcessObjectTypeAsync(IEntityContext context, string fullName, string json)
    {
        var doc = await connection.Filter<ObjectType>("ObjectType.1")
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.FullName, fullName)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.EntityId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .FirstOrDefaultAsync<BsonDocument>();

        var dst = BsonSerializer.Deserialize<ObjectType>(json);

        if (doc == null)
        {
            logger.LogInformation("New Object {ObjectType}", fullName);

            if (CreateMissing && !DryRun)
            {
                if (GetConfirmation($"New Object Type \"{fullName}\"", "Add ObjectType?"))
                {
                    dst.CreatedOn = DateTime.UtcNow;
                    dst.AccountId = context.AccountId.Value;
                    dst.EntityId = context.AccountId.Value;
                    dst.LastActor = context.Actor();
                    dst.LastModifiedOn = DateTime.UtcNow;

                    await connection.InsertAsync(dst);
                }
            }

            return true;
        }

        var src = BsonSerializer.Deserialize<ObjectType>(doc);
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
                        _ => false,
                    };
                }

                return false;
            },
        });

        var differences = diff?.ToChangeList();
        if (differences == null)
        {
            logger.LogInformation("No changes detected for {ObjectType}", fullName);
            return false;
        }

        if (DryRun)
        {
            logger.LogInformation("Changes for {ObjectType}: {Differences}", fullName, differences);
            return true;
        }

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
            Description = differences,
            ObjectType = dst,
            FlowId = draftObjectType?.InitialFlowId,
            ObjectStatusId = draftObjectType?.InitialObjectStatusId,
            Tags = ["Import"],
            // BaseObjectType = baseObjectType,
        };

        draft.UpdateRelatedObjectTypes();

        logger.LogInformation("Changes for {ObjectType}: {Differences}", fullName, differences);

        if (GetConfirmation($"Object Type \"{fullName}\" was modified", differences, "Create ObjectTypeDraft?"))
        {
            draft = await connection.InsertAsync(draft);
        }

        return true;
    }
}