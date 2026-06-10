using System;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.ProductCatalog.Services;

public class MonitorService : AbstractMessageQueueService, ILifetimeService
{
    private readonly CatalogService _catalogService;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;

    public MonitorService(
        ILogger<MonitorService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        CatalogService catalogService,
        ObjectTypeService objectTypeService,
        MongoConnection connection
    ) : base(logger, configuration, messageBroker)
    {
        _catalogService = catalogService;
        _objectTypeService = objectTypeService;
        _connection = connection;
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.SpreadsheetToCatalog));
        mapper.Register<SpreadsheetToCatalogAction.Message>();

        MessageBroker.Bind(queue, FlowObjectEventRoute.Any.GetRoute(nameof(ProductCatalog), null));

        // MessageBroker.Bind(queue, FlowObjectEventRoute.Any.GetRoute(nameof(CatalogFeed), null));
        // MessageBroker.Bind(queue, FlowObjectEventRoute.Any.GetRoute(nameof(MALCatalogFeed), null));
        // MessageBroker.Bind(queue, FlowObjectEventRoute.Any.GetRoute(nameof(CloneCatalogFeed), null));
        // MessageBroker.Bind(queue, FlowObjectEventRoute.Any.GetRoute(nameof(XLSCatalogFeed), null));
        // MessageBroker.Bind(queue, FlowObjectEventRoute.Any.GetRoute(nameof(B2BCatalogFeed), null));
        mapper.Register<GenericFlowEvent>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var task = evt.Body switch
            {
                SpreadsheetToCatalogAction.Message post when post.Options.IsToProduction => MergeAsync(post),
                SpreadsheetToCatalogAction.Message post => PostProcessAsync(post),
                GenericFlowEvent generic when evt.RoutingKey.StartsWith("object.") => GenericObjectEventAsync(evt, generic),
                // BootstrapProductCatalogAction.Message bootstrap => ProcessAsync(bootstrap),
                _ => null,
            };

            if (task != null) await task;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    /// <summary>
    /// Process generic events for ProductCatalog 
    /// </summary>
    private async Task GenericObjectEventAsync(IMessage evt, GenericFlowEvent generic)
    {
        var route = evt.RoutingKey.Split(".");
        if (route.Length != 4) return;

        var task = generic.ObjectType switch
        {
            nameof(ProductCatalog) => HandleProductCatalogEventAsync(generic, route[3]),
            // nameof(CatalogFeed) => HandleCatalogFeedEventAsync(generic, route[3]),
            // nameof(XLSCatalogFeed) => HandleCatalogFeedEventAsync(generic, route[3]),
            // nameof(B2BCatalogFeed) => HandleCatalogFeedEventAsync(generic, route[3]),
            // nameof(CloneCatalogFeed) => HandleCatalogFeedEventAsync(generic, route[3]),
            // nameof(MALCatalogFeed) => HandleCatalogFeedEventAsync(generic, route[3]),
            _ => Task.CompletedTask
        };

        await task;
    }

    private async Task HandleCatalogFeedEventAsync(GenericFlowEvent generic, string action)
    {
        using var scope = Logger.AddScope(new
        {
            generic.TargetId,
            generic.ObjectType,
            Action = action,
        });

        var catalogFeed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, generic.AccountId)
            .Eq(x => x.Id, generic.TargetId)
            .FirstOrDefaultAsync();

        if (catalogFeed == null)
        {
            Logger.LogError("CatalogFeed not found");
            return;
        }

        Logger.LogInformation("Handle Event for {name}", catalogFeed.Name);

        // if (action == "update")
        // {
        //     var context = new AccountContext(catalogFeed.AccountId);

        //     var start = DateTime.UtcNow;
        //     Logger.LogInformation("Start Update crumbs");
        //     await _catalogService.UpdateBreadcrumbsAsync(context, catalogFeed);
        //     Logger.LogInformation("Updated breadcrumbs in {elapsedSeconds}s", (DateTime.UtcNow - start).TotalSeconds);
        // }
    }

    private async Task HandleProductCatalogEventAsync(GenericFlowEvent generic, string action)
    {
        using var scope = Logger.AddScope(new
        {
            generic.TargetId,
            generic.ObjectType,
            Action = action,
        });

        var catalog = await _connection.Filter<ProductCatalog.Models.ProductCatalog>()
            .Eq(x => x.AccountId, generic.AccountId)
            .Eq(x => x.Id, generic.TargetId)
            .FirstOrDefaultAsync();

        if (catalog == null)
        {
            Logger.LogError("ProductCatalog not found");
            return;
        }

        Logger.LogInformation("Handle Event for {name}", catalog.Name);

        switch (action)
        {
            case "create":
                await OnProductCatalogCreateAsync(generic, catalog);
                break;

            case "update":
                await OnProductCatalogUpdateAsync(generic, catalog);
                break;

            default:
                Logger.LogError("Unexpected action");
                break;
        }
    }

    private async Task OnProductCatalogUpdateAsync(GenericFlowEvent evt, Models.ProductCatalog catalog)
    {
        using var scope = Logger.AddScope(new
        {
            ProductCatalogId = evt.TargetId,
        });

        Logger.LogInformation("ProductCatalog was updated");

        if (evt.MetaValues.TryGetValue(nameof(IFlowObject.IsActive), out var isActiveObj) && isActiveObj is bool isActive && isActive)
        {
            var org = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, catalog.AccountId)
                .Eq(x => x.Id, catalog.EntityId)
                .FirstOrDefaultAsync();

            Logger.LogInformation("ProductCatalog was activated, refresh breadcrumbs");
            _ = Task.Run(() => _catalogService.ResetBreadcrumbsAsync(new AccountContext(org.AccountId), org.Id));
        }
    }

    private async Task OnProductCatalogCreateAsync(GenericFlowEvent evt, Models.ProductCatalog catalog)
    {
        using var scope = Logger.AddScope(new
        {
            ProductCatalogId = catalog.Id,
        });

        Logger.LogInformation("ProductCatalog created");

        try
        {
            if (catalog.EntityId == catalog.AccountId)
            {
                Logger.LogInformation("ProductCatalog for account, nothing to do");
                return;
            }

            var context = new OrganizationContext(catalog.EntityId, catalog.AccountId);

            var entity = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, catalog.AccountId)
                .Eq(x => x.Id, catalog.EntityId)
                .FirstOrDefaultAsync();

            if (entity == null)
            {
                Logger.LogError("Organization not found");
                return;
            }

            // stored procedure that could handle most of it 
            var result = await _connection.DipperAsync(
                "BootstrapCatalog",
                context.AccountId.Value.ToString("N"),
                new
                {
                    AccountId = context.AccountId.Value.AsSerializedId(),
                    EntityId = catalog.EntityId.AsSerializedId(),
                    OrganizationId = catalog.EntityId.AsSerializedId(),
                    ProductCatalogId = catalog.Id.AsSerializedId(),
                });

            // Clone CORP Catalog feeds
            var clones = await _connection.Filter<CatalogFeed, CloneCatalogFeed>()
                .Eq(x => x.AccountId, catalog.AccountId)
                .Eq(x => x.EntityId, catalog.AccountId)
                .Eq(x => x.IsActive, true)
                .FindAsync();

            foreach (var template in clones)
            {
                var clone = await _objectTypeService.CreateObjectAsync<CloneCatalogFeed>(context);
                clone.CatalogFeedId = template.CatalogFeedId;
                clone.Name = template.Name;
                clone.Description = template.Description;
                clone.IsActive = true;

                await _connection.InsertAsync(clone);
                await _objectTypeService.FireCreateEventAsync(context, clone, x =>
                {
                    x.Description ??= $"{x.ObjectType} Created";
                    x.Action ??= "ObjectCreated";
                    x.AddRefValue(catalog);
                });
            }

            // Add MAL
            await _catalogService.GetOrCreateMALCatalogAsync(context);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to bootstrap product catalog");
        }
    }

    /// <summary>
    /// Merge items from item.staging into item
    /// </summary>
    private async Task MergeAsync(SpreadsheetToCatalogAction.Message action)
    {
        var catalogFeed = await GetCatalogFeedAsync(action.Event.TargetId);
        if (!catalogFeed)
        {
            await PostFailedMessageAsync(action, "Catalog Feed not found");
            return;
        }

        var result = await ExecuteStoredProcedureAsync(action.Event.TargetId, action.Options.StoredProcedure, catalogFeed.Value.Id);
        if (!result)
        {
            await PostFailedMessageAsync(action, "Failed to execute stored procedure");
            return;
        }

        await _catalogService.SetLastUpdatedOnAsync(new AccountContext(catalogFeed.Value.AccountId), catalogFeed.Value);

        await MessageBroker.DispatchAsync(
            new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.SpreadsheetToCatalog),
                Description = action.GetEventDescription(action.Options.SuccessEventId, "Spreadsheet merged"),
                EventTypeId =action.Options.SuccessEventId, 
            }
        );
    }

    /// <summary>
    /// Post process imported spreadsheet 
    /// </summary>
    private async Task PostProcessAsync(SpreadsheetToCatalogAction.Message action)
    {
        var catalogFeed = await GetCatalogFeedAsync(action.Event.TargetId);
        if (!catalogFeed)
        {
            await PostFailedMessageAsync(action, "Catalog Feed not found");
            return;
        }

        var result = await ExecuteStoredProcedureAsync(action.Event.TargetId, action.Options.StoredProcedure, catalogFeed.Value.Id);
        if (!result)
        {
            await PostFailedMessageAsync(action, "Failed to execute stored procedure");
            return;
        }

        var spreadsheet = await _connection.Filter<Spreadsheet>()
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (spreadsheet.ErrorsCount > 0)
        {
            // finished with errors
            if (spreadsheet.ErrorsCount >= spreadsheet.RowsCount && action.Options.FailedEventId.HasValue)
            {
                // all failed
                await MessageBroker.DispatchAsync(new GenericFlowEvent(action.Event)
                    {
                        Action = nameof(ActionIds.SpreadsheetToCatalog),
                        Description = action.GetEventDescription(action.Options.FailedEventId, $"Conversion completed with {spreadsheet.ErrorsCount} errors"),
                        EventTypeId = action.Options.FailedEventId,
                    }
                );
                return;
            }

            if (action.Options.WithErrorsEventId.HasValue)
            {
                // partial 
                var evt = new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.SpreadsheetToCatalog),
                    Description = action.GetEventDescription(action.Options.WithErrorsEventId, $"Conversion completed with {spreadsheet.ErrorsCount} errors"),
                    EventTypeId = action.Options.WithErrorsEventId,
                };

                evt.SetMetaValue(nameof(Spreadsheet.ErrorsCount), spreadsheet.ErrorsCount);

                await MessageBroker.DispatchAsync(evt);
                return;
            }

            // only one event, flag as error
            await MessageBroker.DispatchAsync(
                new GenericFlowEvent(action.Event)
                {
                    Action = nameof(ActionIds.SpreadsheetToCatalog),
                    Description = $"Conversion completed with {spreadsheet.ErrorsCount} errors",
                    EventTypeId = action.Options.SuccessEventId,
                },
                true
            );
            return;
        }

        // complete success
        await MessageBroker.DispatchAsync(
            new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.SpreadsheetToCatalog),
                Description = action.GetEventDescription(action.Options.SuccessEventId, "Conversion completed"),
                EventTypeId = action.Options.SuccessEventId,
            }
        );
    }

    private async Task PostFailedMessageAsync(SpreadsheetToCatalogAction.Message action, string error)
    {
        await MessageBroker.DispatchAsync(new GenericFlowEvent(action.Event)
            {
                Action = nameof(ActionIds.SpreadsheetToCatalog),
                Description = error,
                EventTypeId = action.Options.SuccessEventId,
            },
            true
        );
    }

    private async Task<Result<XLSCatalogFeed>> GetCatalogFeedAsync(Guid spreadsheetId)
    {
        // "Spreadsheet -> EmailReceived -> XLSCatalogFeed"
        var spreadsheet = await _connection.Filter<Spreadsheet>()
            .Eq(x => x.Id, spreadsheetId)
            .FirstOrDefaultAsync();

        if (spreadsheet == null) return Result<XLSCatalogFeed>.Error("Spreadsheet not found");

        var emailReceived = await _connection.Filter<EmailReceived>()
            .Eq(x => x.Id, spreadsheet.ParentId)
            .FirstOrDefaultAsync();

        if (emailReceived == null) return Result<XLSCatalogFeed>.Error("Email not found");

        var catalogFeed = await _connection.Filter<XLSCatalogFeed>()
            .Eq(x => x.EmailInboxId, emailReceived.ParentId)
            .FirstOrDefaultAsync();

        if (catalogFeed == null) return Result<XLSCatalogFeed>.Error("Catalog Feed not found");

        return Result.Success(catalogFeed);
    }

    private async Task<bool> ExecuteStoredProcedureAsync(Guid spreadsheetId, string storedProcedure, Guid catalogFeedId)
    {
        try
        {
            var result = await _connection.DipperAsync(
                $"convert.{storedProcedure}",
                "productCatalog",
                new
                {
                    ParentId = spreadsheetId.AsSerializedId(),
                    CatalogFeedId = catalogFeedId.AsSerializedId()
                }.BuildDictionaryFromObjectProperties()
            );

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to execute stored procedure");
            return false;
        }
    }
}