using System.Dynamic;
using Crochik.Mongo;
using McpServer.Tools;
using Messages.Flow;
using MongoDB.Bson;
using PI.Shared.Constants;
using PI.Shared.Diff;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using PI.Shared.Services.OpenApiGenerator;
using IResult = PI.Shared.Models.IResult;

namespace MCP.Services;

public class BootstrAppService(
    ILogger<BootstrAppService> logger,
    MongoConnection connection,
    ObjectTypeService objectTypeService,
    AccountManagementService accountManagementService,
    IServiceScopeFactory scopeFactory
)
{
    public async Task<BootstrApp?> GetAppAsync(IEntityContext context, string appName)
    {
        return await connection.Filter<BootstrApp>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Name, appName)
            .FirstOrDefaultAsync();
    }

    public Task<ObjectType?> GetObjectTypeAsync(IEntityContext context, string objectTypeName) => objectTypeService.GetAsync(context, objectTypeName);

    public async Task<string[]> GetObjectTypeNames(BootstrApp app)
    {
        var list = await connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, app.AccountId)
            .Regex(x => x.Namespace, $"/^app\\.{app.Name}/")
            .IncludeFields(x => x.Namespace, x => x.Name, x => x.FullName)
            .FindAsync();

        return list.Select(x => x.FullName).ToArray();
    }

    public async Task<Result<AccountManagementService.ImportResult>> ProvisionAccountAsync(IEntityContext context, CancellationToken cancellationToken = default)
    {
        bool log<T>(T d, DiffResult? diff, UpdateQuery<T>? query) where T : IModel
        {
            if (diff == null)
            {
                logger.LogInformation("Create New {Type}: {Name}", typeof(T).Name, d.Name);
            }
            else
            {
                logger.LogInformation("Update {Type}({Id}): {Name}", typeof(T).Name, d.Id, d.Name);
                var differences = diff.ToChangeList();
                logger.LogInformation("> {Diff}", differences);
                if (query != null)
                {
                    var filter = query.GetFilterAsBsonDocument().ToString();
                    var update = query.GetUpdateAsBsonDocument().ToString();
                    logger.LogInformation(">> {Filter}", filter);
                    logger.LogInformation(">> {Update}", update);
                }
            }

            return true;
        }

        var path = AppDomain.CurrentDomain.BaseDirectory; // AppContext.BaseDirectory

        return await accountManagementService.ImportAllAsync(
            new AccountManagementService.Options
            {
                TargetAccountId = context.AccountId.Value,
                PreserveIds = false,
                BasePath = $"{path}Content/Account/",
                Namespaces = null,
                CreateObjectTypeDrafts = false, // TODO: auto replace?
                // EntityTypes = [AccountManagementService.EntityType.ObjectStatus, AccountManagementService.EntityType.EventType, AccountManagementService.EntityType.Flow],
                UpdateObjectStatus = log,
                UpdateEventType = log,
                UpdateFlow = log,
                UpdateObjectType = log,
                UpdatePage = log,
                UpdateFlowAction = log,
            },
            new Actor(),
            cancellationToken
        );
    }

    public async Task<IResult> ValidateObjectTypesAsync(BootstrApp app, CancellationToken cancellationToken = default)
    {
        var accountContext = new AccountContext(app.AccountId);

        // top level object types
        var objectTypes = (
                await connection.Filter<ObjectType>()
                    .Eq(x => x.AccountId, app.AccountId)
                    .Eq(x => x.IsEmbedded, false)
                    .Eq(x => x.Namespace, app.ObjectsNamespace)
                    .IncludeFields(
                        x => x.Namespace,
                        x => x.Name,
                        x => x.FullName,
                        x => x.Label,
                        x => x.InitialFlowId
                    )
                    .FindAsync()
            )
            .ToDictionary(x => x.FullName);

        // flows for objects types (with actions)
        var flows = (
                await connection.Filter<Flow>()
                    .Eq(x => x.AccountId, app.AccountId)
                    .In(x => x.ObjectType,
                        objectTypes
                            .Where(x => !x.Value.InitialFlowId.HasValue)
                            .Select(x => x.Key)
                    )
                    .FindAsync()
            )
            .ToDictionary(x => x.ObjectType, x => x.Id);

        foreach (var kvp in objectTypes)
        {
            if (kvp.Value.InitialFlowId.HasValue)
            {
                flows[kvp.Key] = kvp.Value.InitialFlowId.Value;
                continue;
            }

            if (flows.TryGetValue(kvp.Key, out var flowId))
            {
                await setDefaultFlowId(kvp.Key, flowId);
                continue;
            }

            var flow = await connection.InsertAsync(new Flow
            {
                Id = Guid.NewGuid(),
                CreatedOn = DateTime.UtcNow,
                AccountId = app.AccountId,
                EntityId = app.AccountId,
                ObjectType = kvp.Key,
                Name = kvp.Key,
                Description = $"Default flow for {kvp.Value}",
                Steps = [],
            });

            flows.Add(kvp.Key, flow.Id);

            await setDefaultFlowId(kvp.Key, flow.Id);
        }

        var eventTypes = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, app.AccountId)
            .In(x => x.ObjectType, objectTypes.Keys)
            .FindAsync();

        foreach (var evt in eventTypes)
        {
            if (flows.TryGetValue(evt.ObjectType, out var flowId))
            {
                // TODO: should it limit to the flow
                // ...
            }

            // events (add forms)
            await CreateFormAsync(accountContext, evt);

            // flows, add script action to events
        }

        // app menu 

        // 
        return Result.Success("done");

        async Task setDefaultFlowId(string objectTypeName, Guid flowId)
        {
            await connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, app.AccountId)
                .Eq(x => x.FullName, objectTypeName)
                .Eq(x => x.InitialFlowId, null)
                .Update
                .Set(x => x.InitialFlowId, flowId)
                .Set(x => x.LastActor, accountContext.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateOneAsync();
        }
    }

    private async Task CreateFormAsync(IEntityContext context, EventType evt)
    {
        if (evt.Trigger is not UserTrigger trigger) //  { Form: null }
        {
            // not user or already has form
            return;
        }

        if (trigger.InputObjectType == null)
        {
            // nothing to do?
            // is it even a valid action?
            return;
        }

        var input = await objectTypeService.GetAsync(context, trigger.InputObjectType);
        if (input == null)
        {
            throw new Exception($"Failed to load {trigger.InputObjectType}");
        }

        var fields = input.Fields.Values
            // remove calculated and _id if is applied directly to the object  
            .Where(x => x.InitialValue == null && (trigger.AllowNone || trigger.AllowMultiple || (x.Field.Name != Model.IdFieldName && x.Field.Name != "id")))
            .Select(x => x.Field);

        var form = new Form
        {
            Name = evt.Name, // trigger.Name ???
            Title = evt.Name, // title?
            Fields = fields.Prepend(new LabelField
            {
                Name = "#message",
                Label = evt.Description,
            }).ToArray(),
            Actions =
            [
                new FormAction
                {
                    Name = "Run",
                    Label = "Run",
                },
                new FormAction
                {
                    Name = FormAction.Client_Cancel,
                    Action = FormAction.Client_Cancel,
                    Label = "Cancel",
                }
            ]
        };

        evt = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, evt.Id)
            .Eq($"{nameof(Trigger)}._t", "User")
            .Update
            .Set($"{nameof(Trigger)}.{nameof(UserTrigger.Form)}", form)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();
    }

    public async Task<IResult> ValidateScriptsAsync(BootstrApp app, CancellationToken cancellationToken)
    {
        var accountContext = new AccountContext(app.AccountId);

        // top level object types
        var objectTypes = (
                await connection.Filter<ObjectType>()
                    .Eq(x => x.AccountId, app.AccountId)
                    .Eq(x => x.IsEmbedded, false)
                    .Eq(x => x.Namespace, app.ObjectsNamespace)
                    .Ne(x => x.InitialFlowId, null)
                    .IncludeFields(
                        x => x.Namespace,
                        x => x.Name,
                        x => x.FullName,
                        x => x.Label,
                        x => x.InitialFlowId
                    )
                    .FindAsync()
            )
            .ToDictionary(x => x.FullName, x => x.InitialFlowId);

        var eventTypes = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, app.AccountId)
            .In(x => x.ObjectType, objectTypes.Keys)
            .Ne("Script", default(string?))
            .IncludeField("Script")
            .IncludeField(x => x.ObjectType)
            .IncludeField(x => x.Summary)
            .FindAsync<BsonDocument>();

        var count = 0;
        foreach (var evt in eventTypes)
        {
            if (!evt.TryGetStringValue("Script", out var script)
                || !evt.TryGetStringValue(nameof(EventType.ObjectType), out var objectType)
                || !evt.TryGetStringValue("_id", out var eventIdStr))
            {
                // TODO: error
                continue;
            }

            if (!Guid.TryParse(eventIdStr, out var eventTypeId))
            {
                // TODO: error
                continue;
            }

            if (!objectTypes.TryGetValue(objectType, out var flowId) || !flowId.HasValue)
            {
                // TODO: error
                continue;
            }

            if (!evt.TryGetStringValue(nameof(EventType.Summary), out var summmary) || summmary == null)
            {
                summmary = "Execute Script";
            }
            
            await AddScriptActionAsync(accountContext, flowId.Value, eventTypeId, script, summmary);
            count++;
        }

        return Result.Success(count);
    }

    private async Task<FlowStep?> AddScriptActionAsync(AccountContext context, Guid flowId, Guid eventTypeId, string script, string description)
    {
        var step = new FlowStep
        {
            Id = Guid.NewGuid(),
            EventIdTrigger = eventTypeId,
            CurrentStatusId = null,
            Description = description,
            // IconName = IconName,
            ActionId = ActionIds.RunScript,
            // can we use the RunScriptActionOptions directly?
            // ...
            Options = new GenericActionOptions
            {
                Raw = GenericActionOptions.Convert<RunScriptActionOptions, ExpandoObject>(new RunScriptActionOptions
                {
                    Script = script,
                }),
                Output = [],
            },
        };

        // try to update existing
        var flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, flowId)
            .ElemMatchBuilder(f => f.Steps, q => q
                .Eq(x => x.EventIdTrigger, eventTypeId)
                .Eq(x => x.ActionId, ActionIds.RunScript)
            )
            .Update
            .Set($"{nameof(Flow.Steps)}.$.Options.Raw.Script", script)
            .Set($"{nameof(Flow.Steps)}.$.Description", description)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        if (flow != null) return step;

        flow = await connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, flowId)
            .Update
            .Push(x => x.Steps, step)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, null) // ??? 
            .UpdateAndGetOneAsync();

        return step;
    }

    public class RunScriptActionOptions : ActionOptions
    {
        public const string ObjectTypeFullName = "openapi.RunScriptActionOptions";

        public string Script { get; set; }
    }
}