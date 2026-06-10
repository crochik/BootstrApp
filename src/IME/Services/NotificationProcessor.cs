using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Messaging;
using Crochik.Mongo;
using IME.API;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

[BsonCollection("ime.Organization")]
public class IMEOrganization : EntityOwnedModel, IExternalId
{
    public string ExternalId { get; set; }
    public Guid LeadTypeId { get; set; }
}

public class NotificationProcessor : AbstractMessageQueueService, ILifetimeService
{
    private readonly LeadBuilderService _leadBuilderService;
    private readonly MongoConnection _connection;
    private readonly Client _client;

    public NotificationProcessor(
        ILogger<NotificationProcessor> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        LeadBuilderService leadBuilderService,
        MongoConnection connection,
        Client client) :
        base(logger, configuration, messageBroker)
    {
        _leadBuilderService = leadBuilderService;
        _connection = connection;
        _client = client;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, "ime.#");
        mapper.Register<Notification>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        switch (evt.Body)
        {
            case Notification notification:
                await ProcessAsync(notification);
                break;
        }

        evt.Acknowledge();
    }

    public async Task<Lead> ProcessAsync(Notification notification)
    {
        using var scope = Logger.BeginScope(
            "Notification for {affiliateId}: {workOrderId} {type}",
            new
            {
                notification.Payload.AffiliateId,
                notification.Payload.WorkOrderId,
                notification.Payload.Type
            });

        var affiliate = await _connection.Filter<IMEOrganization>()
            .Eq(x => x.AccountId, notification.AccountId)
            .Eq(x => x.ExternalId, notification.Payload.AffiliateId.ToString())
            .FirstOrDefaultAsync();

        if (affiliate?.EntityId == null)
        {
            Logger.LogError("Couldn't find affiliate");
            return null;
        }

        await _client.LoginAsync();
        var workOrder = await _client.WorkOrdersAsync(notification.Payload.WorkOrderId);
        if (workOrder == null)
        {
            Logger.LogError("Couldn't get workOrder from IME");
            return null;
        }

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, affiliate.AccountId)
            .Eq(x => x.EntityId, affiliate.EntityId)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.IME)
                    .Eq(x => x.ExternalId, notification.Payload.WorkOrderId.ToString())
            )
            .FirstOrDefaultAsync();

        if (lead == null)
        {
            lead = await AddLeadAsync(affiliate, workOrder);
            Logger.LogInformation("Created {LeadId} for workOrder", lead.Id);
            return lead;
        }

        // for now just update the integration data
        lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, affiliate.AccountId)
            .Eq(x => x.Id, lead.Id)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationIds.IME)
                    .Eq(x => x.ExternalId, notification.Payload.WorkOrderId.ToString())
            )
            .Update
                .Set($"{nameof(Lead.Integrations)}.$", Map(workOrder))
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        // fire event(s)
        var evt = new GenericFlowEvent(lead)
        {
            RefValues = lead.GetRefs().ToList(),
            MetaValues = new Dictionary<string, object>(lead.GetMeta()),
            EventTypeId = EventIds.OnLeadUpdated, // TODO: should it be the generic update object event id instead?
            // Actor = ...
        };

        evt.SetMetaValue("Fields", nameof(Lead.Integrations));
        evt.Description = "IME Integration update to Lead";

        await MessageBroker.DispatchAsync(evt, FlowObjectEventRoute.Update.GetRoute(lead));

        if (lead.FlowId.HasValue)
        {
            await MessageBroker.DispatchAsync(evt);
        }

        // should it fire the EventIds.OnIntegrationForLeadUpdated in addition or instead?
        // ...

        return lead;
    }

    public async Task<Lead> AddLeadAsync(IMEOrganization affiliate, GetWorkOrder workOrder)
    {
        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, affiliate.AccountId)
            .Eq(x => x.Id, affiliate.EntityId)
            .FirstOrDefaultAsync();

        if (entity == null)
        {
            Logger.LogError("Didn't find {entityId}", affiliate.EntityId);
            throw new NotFoundException(nameof(Entity), affiliate.EntityId);
        }

        var leadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.Id, affiliate.LeadTypeId)
            .FirstOrDefaultAsync();

        var leadRequest = new LeadRequest
        {
            LeadTypeId = leadType.Id,
            Body = JsonConvert.SerializeObject(workOrder),
        };

        await _connection.InsertAsync(leadRequest);

        var body = JsonConvert.SerializeObject(workOrder);
        var builder = await _leadBuilderService.AddAsync(entity.Context, leadType, body);
        if (builder.Failed)
        {
            Logger.LogError("Failed to parse lead: {error}", builder.Error);
            throw new BadRequestException(builder.Error);
        }

        return await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, affiliate.AccountId)
            .Eq(x => x.Id, builder.LeadId)
            .Update
                .Push(x => x.Integrations, Map(workOrder))
            .UpdateAndGetOneAsync();
    }

    private static LeadIntegration Map(GetWorkOrder workOrder)
        => new()
        {
            IntegrationId = IntegrationIds.IME,
            ExternalId = workOrder.Id.ToString(),
            CreatedOn = DateTime.UtcNow,
            Data = workOrder,
            Status = workOrder.Status,
            // Tag = workOrder.
        };

}
