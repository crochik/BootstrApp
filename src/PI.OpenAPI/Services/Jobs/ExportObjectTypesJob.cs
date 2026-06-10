using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using PI.OpenAPI.Controllers;
using PI.Shared.Diff;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.OpenAPI.Services.Jobs;

public class ExportObjectTypesJob(ILogger<ExportObjectTypesJob> logger, MongoConnection connection) : IRunJob
{
    private string BasePath => "/Users/felipe/DEVELOPMENT/github/OTGSchema/"; // PISchema
    // private string[] Tags = ["OTG", "System"];
    // private const string OTGProfileId = "33d883d8-c153-44b2-9094-d4fd158e767d";

    public string Name => "ExportObjectTypes";

    private IEntityContext Context;
    private CancellationToken StoppingToken;

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        Context = context;
        StoppingToken = stoppingToken;

        var objectTypes = await ExportObjectTypes();

        var graph = ObjectGrapher.Graph(objectTypes);
        graph.Graph();

        await ExportProfiles(graph.ProfileIds);

        var flows = await ExportFlows(graph.FlowIds);
        graph.Graph(flows);

        await ExportEventTypes(graph.EventTypeIds);

        await ExportObjectStatus(graph.ObjectStatusIds);

        await ExportPagesAsync(graph);
        await ExportFlowActionsAsync(graph);

        return new JobResult
        {
            Message = $"Exported {objectTypes.Count} object types",
        };
    }

    private async Task<List<GenericAction>> ExportFlowActionsAsync(ObjectGrapher graph)
    {
        var cursor = await connection.Filter<GenericAction>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Ne(x => x.IsActive, false)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .ToCursor<BsonDocument>();

        var destinationPath = $"{BasePath}FlowAction";
        if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath!);

        var exported = new List<GenericAction>();
        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(Model.IdFieldName, out var idField))
                {
                    logger.LogError("Couldn't find _id for object");
                    continue;
                }

                var name = doc.TryGetValue(nameof(GenericAction.Name), out var nameField) ? nameField.AsString : "_NO_NAME_";

                var id = Guid.Parse(idField.AsString);

                var path = $"{destinationPath}/";

                if (!Directory.Exists(path)) Directory.CreateDirectory(path!);
                var action = Export<GenericAction>($"{path}{name}", doc);
                if (action != null) exported.Add(action);

                logger.LogInformation("Export {AppPage} {ActionId}", name, id);
            }
        }

        return exported;
    }

    private async Task<List<AppPage>> ExportPagesAsync(ObjectGrapher graph)
    {
        var cursor = await connection.Filter<AppPage>()
            .Eq(x => x.AccountId, Context.AccountId)
            // .In(x => x.ObjectType, graph.ObjectTypes.Keys)
            .OrBuilder(
                q => q.In(x => x.ObjectType, graph.ObjectTypes.Keys),
                q => q.Eq( "Page._t", nameof(CustomPage))
            )
            .Ne(x => x.IsActive, false)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .ToCursor<BsonDocument>();

        var destinationPath = $"{BasePath}AppPage";
        if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath!);

        var exported = new List<AppPage>();
        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(Model.IdFieldName, out var idField))
                {
                    logger.LogError("Couldn't find _id for object");
                    continue;
                }

                var name = doc.TryGetValue(nameof(AppPage.Name), out var nameField) ? nameField.AsString : "_NO_NAME_";
                var objectTypeName = doc.TryGetValue(nameof(AppPage.ObjectType), out var objectTypeField) ? objectTypeField.AsString : null;

                var id = Guid.Parse(idField.AsString);

                var path = $"{destinationPath}/";
                if (objectTypeName != null)
                {
                    path += string.Join("/", objectTypeName.Split('.')) + "/";
                }

                if (!Directory.Exists(path)) Directory.CreateDirectory(path!);
                var page = Export<AppPage>($"{path}{name}", doc);
                if (page != null) exported.Add(page);

                logger.LogInformation("Export {ObjectType} {AppPage} {AppPageId}", objectTypeName, name, id);
            }
        }

        return exported;
    }

    private async Task<List<EventType>> ExportEventTypes(IEnumerable<Guid> eventTypeIds)
    {
        var cursor = await connection.Filter<EventType>()
            // .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.Id, eventTypeIds)
            .Ne(x => x.Trigger, null)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            // .ExcludeField(x => x.EntityId) // ????
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .ToCursor<BsonDocument>();

        var destinationPath = $"{BasePath}EventType";
        if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath!);

        var exported = new List<EventType>();
        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(Model.IdFieldName, out var idField))
                {
                    logger.LogError("Couldn't find _id for object");
                    continue;
                }

                var name = doc.TryGetValue(nameof(EventType.Name), out var nameField) ? nameField.AsString : "_NO_NAME_";
                var objectTypeName = doc.TryGetValue(nameof(EventType.ObjectType), out var objectTypeField) ? objectTypeField.AsString : null;

                var id = Guid.Parse(idField.AsString);

                var path = $"{destinationPath}/";
                if (objectTypeName != null)
                {
                    path += string.Join("/", objectTypeName.Split('.')) + "/";
                }

                if (!Directory.Exists(path)) Directory.CreateDirectory(path!);
                var flow = Export<EventType>($"{path}{name}", doc);
                if (flow != null) exported.Add(flow);

                logger.LogInformation("Export {ObjectType} {EventType} {EventTypeId}", objectTypeName, name, id);
            }
        }

        return exported;
    }

    private async Task<List<ObjectStatus>> ExportObjectStatus(HashSet<Guid> objectStatusIds)
    {
        var cursor = await connection.Filter<ObjectStatus>()
            // .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.Id, objectStatusIds)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .ToCursor<BsonDocument>();

        var destinationPath = $"{BasePath}ObjectStatus/";
        if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath!);

        var exported = new List<ObjectStatus>();
        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(Model.IdFieldName, out var idField))
                {
                    logger.LogError("Couldn't find _id for object");
                    continue;
                }

                var name = doc.TryGetValue(nameof(ObjectStatus.Name), out var nameField) ? nameField.AsString : "_NO_NAME_";
                var objectTypeName = doc.TryGetValue(nameof(ObjectStatus.ObjectType), out var objectTypeField) ? objectTypeField.AsString : null;

                var id = Guid.Parse(idField.AsString);

                var path = destinationPath;
                if (objectTypeName != null)
                {
                    path += string.Join("/", objectTypeName.Split('.')) + "/";
                }

                if (!Directory.Exists(path)) Directory.CreateDirectory(path!);
                var flow = Export<ObjectStatus>($"{path}{name}", doc);
                if (flow != null) exported.Add(flow);

                logger.LogInformation("Export {ObjectType} {ObjectStatus} {ObjectStatusId}", objectTypeName, name, id);
            }
        }

        return exported;
    }

    private async Task<List<Flow>> ExportFlows(IEnumerable<Guid> flowIds)
    {
        var cursor = await connection.Filter<Flow>()
            // .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.Id, flowIds)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.EntityId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .ToCursor<BsonDocument>();

        var destinationPath = $"{BasePath}Flow/";
        if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath!);

        var exported = new List<Flow>();
        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(Model.IdFieldName, out var idField))
                {
                    logger.LogError("Couldn't find _id for object");
                    continue;
                }

                if (!doc.TryGetValue(nameof(Flow.Name), out var nameField))
                {
                    logger.LogError("Couldn't find Name for object");
                    continue;
                }

                if (!doc.TryGetValue(nameof(Flow.ObjectType), out var objectTypeField))
                {
                    logger.LogError("Couldn't find Name for object");
                    continue;
                }

                var id = Guid.Parse(idField.AsString);
                var name = nameField.AsString;
                var objectTypeName = objectTypeField.AsString;

                var path = $"{destinationPath}" + string.Join("/", objectTypeName.Split('.')) + "/";
                if (!Directory.Exists(path)) Directory.CreateDirectory(path!);
                var flow = Export<Flow>($"{path}{name}", doc);
                if (flow != null) exported.Add(flow);

                logger.LogInformation("Export {ObjectType} {Flow} {FlowId}", objectTypeName, name, id);
            }
        }

        return exported;
    }

    private async Task ExportProfiles(IEnumerable<Guid> profileIds)
    {
        var cursor = await connection.Filter<AppProfile>()
            // .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.Id, profileIds)
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .ToCursor<BsonDocument>();

        var destinationPath = $"{BasePath}AppProfile/";
        if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath!);

        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(Model.IdFieldName, out var idField))
                {
                    logger.LogError("Couldn't find _id for object");
                    continue;
                }

                if (!doc.TryGetValue(nameof(AppProfile.Name), out var nameField))
                {
                    logger.LogError("Couldn't find Name for object");
                    continue;
                }

                var id = Guid.Parse(idField.AsString);
                var name = nameField.AsString;

                Export<AppProfile>($"{destinationPath}{name}", doc);

                logger.LogInformation("Export {Profile} {ProfileId}", name, id);
            }
        }
    }

    private async Task<List<ObjectType>> ExportObjectTypes()
    {
        var destinationPath = $"{BasePath}/ObjectType/";
        var cursor = await connection.Filter<ObjectType>("ObjectType.1")
            .Eq(x => x.AccountId, Context.AccountId.Value)
            // .OrBuilder(
            //     q => q.AnyIn(x => x.Tags, Tags),
            //     q => q.In(x => x.Namespace, ["otg", "fcb2b"]),
            //     q => q.BitsAllSet(x=>x.RBAC.Permissions[OTGProfileId], 1)
            // )
            .ExcludeField(x => x.CreatedOn)
            .ExcludeField(x => x.AccountId)
            .ExcludeField(x => x.EntityId)
            .ExcludeField(x => x.LastActor)
            .ExcludeField(x => x.LastModifiedOn)
            .SortAsc(x => x.FullName)
            // .WithBatchSize(1)
            .ToCursor<BsonDocument>();

        var objectTypes = new List<ObjectType>();
        while (await cursor.MoveNextAsync(StoppingToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (!doc.TryGetValue(nameof(ObjectType.FullName), out var fullNameValue))
                {
                    logger.LogError("Couldn't find fullname for object");
                    continue;
                }

                var fullName = fullNameValue.AsString;

                var objectType = Export(destinationPath, fullName, doc);
                objectTypes.Add(objectType);
            }
        }

        return objectTypes;
    }

    private T Export<T>(string filePath, BsonDocument doc) where T : class
    {
        var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson, Indent = true };
        var json = doc.ToJson(settings);
        var rootNode = JsonNode.Parse(json);
        var sorted = JsonSorter.Sort(rootNode);

        var yaml = Yamler.ToString(sorted);
        File.WriteAllText($"{filePath}.yaml", yaml);
        File.WriteAllText($"{filePath}.json", json);

        var src = BsonSerializer.Deserialize<T>(doc);
        var dst = BsonSerializer.Deserialize<T>(json);
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

                if (type == typeof(Flow))
                {
                    return info.Name switch
                    {
                        nameof(Flow.LastModifiedOn) => true,
                        nameof(Flow.LastActor) => true,
                        nameof(Flow.CreatedOn) => true,
                        _ => false,
                    };
                }

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

                if (type == typeof(AppProfile))
                {
                    return info.Name switch
                    {
                        nameof(AppProfile.LastModifiedOn) => true,
                        nameof(AppProfile.LastActor) => true,
                        nameof(AppProfile.CreatedOn) => true,
                        _ => false,
                    };
                }

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

                if (type == typeof(AppPage))
                {
                    return info.Name switch
                    {
                        nameof(AppPage.LastModifiedOn) => true,
                        nameof(AppPage.LastActor) => true,
                        nameof(AppPage.CreatedOn) => true,
                        _ => false,
                    };
                }

                if (type == typeof(GenericAction))
                {
                    return info.Name switch
                    {
                        nameof(GenericAction.LastModifiedOn) => true,
                        nameof(GenericAction.LastActor) => true,
                        nameof(GenericAction.CreatedOn) => true,
                        _ => false,
                    };
                }

                return false;
            },
        });

        var differences = diff?.ToChangeList();
        if (differences != null)
        {
            logger.LogError("Failed to export {File}: {Differences}", filePath, differences);
        }

        return differences == null ? dst : null;
    }

    private ObjectType Export(string destinationPath, string fullName, BsonDocument doc)
    {
        logger.LogInformation("Export {ObjectType}", fullName);

        var parts = fullName.Split('.');

        var path = parts.Length == 1 ? destinationPath : destinationPath + string.Join("/", parts[..^1]) + "/";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path!);

        return Export<ObjectType>($"{path}{parts[^1]}", doc);
    }
}