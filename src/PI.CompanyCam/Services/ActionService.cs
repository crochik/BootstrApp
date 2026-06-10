using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace PI.CompanyCam.Services;

public class ActionService : AbstractMessageQueueService, ILifetimeService
{
    private const string OnSuccessOutputName = "OnSuccessEvent";
    private const string OnErrorOutputName = "OnErrorEvent";

    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly CompanyCamService _service;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient Client => _httpClientFactory.CreateClient(nameof(ActionService));

    public ActionService(
        ILogger<ActionService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        CompanyCamService service,
        IHttpClientFactory httpClientFactory
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
        _service = service;
        _httpClientFactory = httpClientFactory;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.CompanyCamCreateProject));
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();

        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.CompanyCamAddDocument));
    }

    protected override async System.Threading.Tasks.Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var route = evt.RoutingKey.Split('.');
            if (!Guid.TryParse(route[1], out var actionId))
            {
                Logger.LogError("Unexpected {RoutingKey}", evt.RoutingKey);
                return;
            }

            switch (evt.Body)
            {
                case SimpleActionMessage<GenericActionOptions> msg:
                    await ProcessAsync(actionId, msg);
                    break;
            }
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task<Result<CreateContext>> CreateContextAsync(SimpleActionMessage<GenericActionOptions> action, AbstractCompanyCamActionOptions options)
    {
        var accountContext = new AccountContext(action.Event.AccountId);
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, action.Event.AccountId)
            .Eq(x => x.Id, action.Event.RunId)
            .FirstOrDefaultAsync();

        var runContext = flowRun.BuildHandlebarsContext(action.Event);

        if (!ExpressionEvaluatorService.TryResolve(accountContext, runContext, options.EntityId, out var entityObj))
        {
            Logger.LogError("Couldn't resolve entity: {Input}", options.EntityId);
            return Result.Error<CreateContext>("Couldn't resolve entity");
        }

        var entityId = entityObj switch
        {
            Guid guid => guid,
            string str => Guid.TryParse(str, out var uuid) ? uuid : default(Guid?),
            _ => default(Guid?),
        };

        if (!entityId.HasValue)
        {
            Logger.LogError("Invalid {EntityId}", entityObj);
            return Result.Error<CreateContext>("Invalid entity");
        }

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, accountContext.AccountId)
            .Eq(x => x.Id, entityId.Value)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            Logger.LogError("{EntityId} Not found", entityId);
            return Result.Error<CreateContext>("Entity not found");
        }

        var context = entity.Context;
        var client = await _service.GetClientAsync(context);
        client.ReadResponseAsString = true;

        if (!ExpressionEvaluatorService.TryResolve(context, runContext, options.Alias, out var aliasObj))
        {
            Logger.LogError("Failed to resolve: {Alias}: {Value}", options.Alias, aliasObj);
            return Result.Error<CreateContext>("Failed to resolve alias");
        }

        if (aliasObj is not string alias)
        {
            alias = null;
        }

        return Result.Success(new CreateContext
        {
            EntityContext = context,
            FlowRun = flowRun,
            RunContext = runContext,
            Entity = entity,
            Client = client,
            Alias = alias,
        });
    }

    private async System.Threading.Tasks.Task ProcessAsync(Guid actionId, SimpleActionMessage<GenericActionOptions> action)
    {
        var result = default(Result<ObjectWithType>);
        var actionName = default(string);
        var options = default(AbstractCompanyCamActionOptions);
        try
        {
            if (actionId == ActionIds.CompanyCamCreateProject)
            {
                var projOptions = action.Options.ConvertTo<AbstractCompanyCamActionOptions>();
                projOptions.Output = action.Options.Output;
                options = projOptions;

                actionName = nameof(ActionIds.CompanyCamCreateProject);
            }
            else if (actionId == ActionIds.CompanyCamAddDocument)
            {
                var docOptions = action.Options.ConvertTo<CompanyCamAddDocumentActionOptions>();
                docOptions.Output = action.Options.Output;
                options = docOptions;

                actionName = nameof(ActionIds.CompanyCamAddDocument);
            }
            else
            {
                Logger.LogError("Unexpected {ActionId}", actionId);
                return;
            }

            var context = await CreateContextAsync(action, options);

            result = context.IsSuccess
                ? options switch
                {
                    CompanyCamAddDocumentActionOptions docOptions => await CreateDocumentAsync(context.Value, action, docOptions),
                    _ => await CreateProjectAsync(context.Value, action, options),
                }
                : context.ConvertTo<ObjectWithType>();

            if (result.IsSuccess)
            {
                Logger.LogInformation("{Action} Successful, created {ObjectType}", actionName, result.Value.ObjectType);

                var objectType = await _objectTypeService.GetAsync(context.Value.EntityContext, result.Value.ObjectType);
                if (objectType != null)
                {
                    await _objectTypeService.AddObjectToFlowRunAsync(context.Value.EntityContext, objectType, result.Value.Object, context.Value.FlowRun.Id, options.Alias);
                }
            }
            else
            {
                Logger.LogError("{Action} Failed: {Status}", actionName, result.Status);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create");
            result = Result.Error<ObjectWithType>(ex.Message);
        }

        var outputName = result.IsSuccess ? OnSuccessOutputName : OnErrorOutputName;
        var output = options?.Output.FirstOrDefault(x => x.Name == outputName);
        if (output?.EventId.HasValue ?? false)
        {
            var evt = new GenericFlowEvent(action.Event)
            {
                Action = actionName,
                Description = output.Description,
                EventTypeId = output.EventId,
            };

            await MessageBroker.DispatchAsync(evt);
        }
    }

    private async Task<Result<ObjectWithType>> CreateDocumentAsync(CreateContext c, SimpleActionMessage<GenericActionOptions> action, CompanyCamAddDocumentActionOptions options)
    {
        if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.FileName, out var fileNameObj) || fileNameObj is not string fileName)
        {
            Logger.LogError("Couldn't resolve file name: {Input}", options.FileName);
            return Result.Error<ObjectWithType>("Couldn't resolve filename");
        }

        if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.SourceUrl, out var sourceUrlObj) || sourceUrlObj is not string sourceUrl)
        {
            Logger.LogError("Couldn't resolve source url: {Input}", options.SourceUrl);
            return Result.Error<ObjectWithType>("Couldn't resolve source url");
        }

        if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.CompanyCamProjectId, out var projectId))
        {
            Logger.LogError("Couldn't resolve source url: {Input}", options.CompanyCamProjectId);
            return Result.Error<ObjectWithType>("Couldn't resolve source url");
        }

        if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.CreatedByEmail, out var createdBy))
        {
            Logger.LogInformation("Didn't resolve {CreatedByEmail}", options.CreatedByEmail);
        }

        sourceUrl = sourceUrl.Replace(" ", "%20");
        
        return options.Type switch
        {
            CompanyCamAddDocumentActionOptions.FileType.Photo => await uploadPhotoAsync(),
            _ => await uploadDocumentAsync(),
        };

        async Task<Result<ObjectWithType>> uploadPhotoAsync()
        {
            try
            {
                if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.CreatedDate, out var createdDateObj))
                {
                    Logger.LogInformation("Didn't resolve {CreatedDate}", options.CreatedDate);
                }

                if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.Tags, out var tagsObj))
                {
                    Logger.LogInformation("Didn't resolve {Tags}", options.Tags);
                }

                var tags = new List<string>
                {
                    "LeadsPiper.com"
                };

                if (tagsObj is string tag)
                {
                    Logger.LogInformation("Add {Tag}", tag);
                    tags.Add(tag);
                }
                else if (tagsObj is IEnumerable<object> strings)
                {
                    Logger.LogInformation("Add {Tags}", string.Join(", ", strings));
                    tags.AddRange(strings.Select(x=>x?.ToString()));
                }
                else
                {
                    Logger.LogInformation("Unknown {TagType}: {Tags}", tagsObj?.GetType().FullName, tagsObj);
                }

                var createdDate = createdDateObj switch
                {
                    DateTime dt => dt,
                    string str => DateTime.TryParse(str, out var dateTime) ? dateTime : DateTime.UtcNow,
                    _ => DateTime.UtcNow,
                };

                var photo = await c.Client.CreateProjectPhotoAsync(
                    createdBy?.ToString(),
                    projectId.ToString(),
                    new Body5
                    {
                        Photo = new Photo2
                        {
                            Uri = sourceUrl,
                            Captured_at = (int)createdDate.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                            // Coordinates =
                            Tags = tags,
                        }
                    });

                return Result.Success<ObjectWithType>(new ObjectWithType
                {
                    ObjectType = "api.companycam.Photo",
                    Object = JsonObjectConverter.Convert<ExpandoObject>(photo),
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add photo to {ProjectId}: {SourceUrl}", projectId, sourceUrl);
                return Result.Error<ObjectWithType>(ex.Message);
            }
        }

        async Task<Result<ObjectWithType>> uploadDocumentAsync()
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
                var response = await Client.SendAsync(requestMessage);
                var body = await response.Content.ReadAsByteArrayAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError("Failed to get document contents from {Url}", sourceUrl);
                    return Result.Error<ObjectWithType>("Failed to get document contents");
                }

                var base64String = Convert.ToBase64String(body);
                var document = await c.Client.CreateProjectDocumentAsync(
                    createdBy?.ToString(),
                    projectId.ToString(),
                    new Body8
                    {
                        Document = new Document2
                        {
                            Name = fileName,
                            Attachment = base64String,
                        }
                    });

                return Result.Success<ObjectWithType>(new ObjectWithType
                {
                    ObjectType = "api.companycam.Document",
                    Object = JsonObjectConverter.Convert<ExpandoObject>(document),
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add document to {ProjectId}: {SourceUrl}", projectId, sourceUrl);
                return Result.Error<ObjectWithType>(ex.Message);
            }
        }
    }

    private async Task<Result<ObjectWithType>> CreateProjectAsync(CreateContext c, SimpleActionMessage<GenericActionOptions> action, AbstractCompanyCamActionOptions options)
    {
        if (!ExpressionEvaluatorService.TryResolve(c.EntityContext, c.RunContext, options.CreatedByEmail, out var createdBy))
        {
            Logger.LogInformation("Didn't resolve {CreatedByEmail}", options.CreatedByEmail);
        }

        if (!action.Options.Raw.TryGetParam("Project", out var projectObj) || projectObj is not IDictionary<string, object> input)
        {
            Logger.LogError("Missing project object");
            return Result.Error<ObjectWithType>("Missing Project");
        }

        var resolved = ExpressionEvaluatorService.TryResolveRecursively(c.EntityContext, c.RunContext, input);
        if (resolved.IsError)
        {
            Logger.LogError("Failed to resolve object: {Error}", resolved.Status);
            return Result.Error<ObjectWithType>(resolved.Status);
        }

        var body = JsonObjectConverter.Convert<CompanyCam.Body3>(resolved.Value);

        var project = await c.Client.CreateProjectAsync(createdBy?.ToString(), body);
        return Result.Success(new ObjectWithType
        {
            ObjectType = "api.companycam.Project",
            Object = JsonObjectConverter.Convert<ExpandoObject>(project),
        });
    }

    private class CreateContext
    {
        public ExpandoObject RunContext { get; set; }
        public Entity Entity { get; set; }
        public CompanyCamClient Client { get; set; }
        public IEntityContext EntityContext { get; set; }
        public string Alias { get; set; }
        public FlowRun FlowRun { get; set; }
    }
}