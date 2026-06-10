using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LeadFlowService : AbstractMessageQueueService, ILifetimeService
{
    private readonly ILogger<LeadFlowService> _logger;
    private readonly ILeadEventService _leadEventService;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;
    private readonly IEntityIdentityAdapter _identityAdapter;
    private readonly IFlowAdapter _flowAdapter;
    private readonly IIntegrationLeadAdapter _integrationLeadAdapter;
    private readonly IIntegrationAppointmentAdapter _integrationAppointmentAdapter;

    public LeadFlowService(
        ILogger<LeadFlowService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        ILeadEventService leadEventService,
        ObjectTypeService objectTypeService,
        MongoConnection connection,
        IEntityIdentityAdapter identityAdapter,
        IFlowAdapter flowAdapter,
        IIntegrationLeadAdapter integrationLead,
        IIntegrationAppointmentAdapter integrationAppointment
    ) : base(logger, configuration, messageBroker)
    {
        _logger = logger;
        _leadEventService = leadEventService;
        _objectTypeService = objectTypeService;
        _connection = connection;
        _identityAdapter = identityAdapter;
        _flowAdapter = flowAdapter;
        _integrationLeadAdapter = integrationLead;
        _integrationAppointmentAdapter = integrationAppointment;
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.AssignLead));
        mapper.Register<AssignAction.Message>();

        // MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.AssignFlow));
        // mapper.Register<AssignFlowAction.Message>();

        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.UpdateIntegrationForLead));
        mapper.Register<UpdateIntegrationForLeadAction.Message>();

        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.UpdateIntegrationForAppointment));
        mapper.Register<UpdateIntegrationForAppointmentAction.Message>();

        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.DuplicatedLeadCheck));
        mapper.Register<LeadDupeCheckAction.Message>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            Logger.LogTrace("Event: {routingKey}", evt.RoutingKey);

            switch (evt.Body)
            {
                case AssignAction.Message assign:
                    if (!await AssignLeadAsync(assign))
                    {
                        // TODO: fire error event
                        // ...
                    }

                    break;

                // case AssignFlowAction.Message assignFlow:
                //     await AssignFlowAsync(assignFlow);
                //     break;

                case UpdateIntegrationForLeadAction.Message leadIntegration:
                    await UpdateIntegrationAsync(leadIntegration);
                    break;

                case UpdateIntegrationForAppointmentAction.Message appointmentIntegration:
                    await UpdateIntegrationAsync(appointmentIntegration);
                    break;

                case LeadDupeCheckAction.Message dedupeAction:
                    await LeadDupeCheckAsync(dedupeAction);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {Id}", evt.RoutingKey);
        }

        evt.Acknowledge();
    }

    private async Task LeadDupeCheckAsync(LeadDupeCheckAction.Message action)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId = action.Event.TargetId,
        });

        _logger.LogInformation("Look for duplicate leads");

        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.Id, action.Event.TargetId)
            .FirstOrDefaultAsync();

        if (lead == null) throw NotFoundException.New<Lead>(action.Event.TargetId);

        // TODO: make these into options (phone, email, address, name, # of days, limit to entity or not, ...)
        // ... 
        var originalLead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.EntityId, lead.EntityId)
            .Gt(x => x.CreatedOn, DateTime.UtcNow.AddDays(-14))
            .Ne(x => x.Id, lead.Id)
            .OrBuilder(
                q => q.Eq(x => x.NormalizedPhoneNumber, lead.NormalizedPhoneNumber),
                q => q.Eq(x => x.NormalizedEmail, lead.NormalizedEmail)
            )
            .SortAsc(x => x.CreatedOn)
            .FirstOrDefaultAsync();

        if (originalLead == null)
        {
            _logger.LogInformation("Didn't find any duplicates");
            if (action.Options.NextEventId.HasValue)
            {
                var evt = new GenericFlowEvent(action.Event)
                {
                    Action = nameof(LeadDupeCheckAction),
                    Description = "Original Lead",
                    EventTypeId = action.Options.NextEventId,
                };
                await MessageBroker.DispatchAsync(evt);
            }

            return;
        }

        lead = await _connection.Filter<Lead>()
            .Eq(x => x.Id, lead.Id)
            .Update
            .Set(x => x.ReplacedById, originalLead.ReplacedById ?? originalLead.Id)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            // .Set(x => x.LastActor,)
            .UpdateAndGetOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(
            new AccountContext(lead.AccountId),
            lead,
            new Dictionary<string, object>
            {
                { nameof(Lead.ReplacedById), lead.ReplacedById }
            },
            e =>
            {
                e.Description = "Set Link to original Lead";
                e.Action = "LeadDupeCheck";
            }
        );

        _logger.LogInformation("Lead is a duplicate of {OriginalLeadId}", originalLead.Id);
        if (action.Options.DuplicateLeadEventId.HasValue)
        {
            var evt = new GenericFlowEvent(action.Event)
            {
                Action = nameof(LeadDupeCheckAction),
                Description = "Lead is a duplicate",
                EventTypeId = action.Options.DuplicateLeadEventId,
            };
            evt.AddRefValue(originalLead);
            if (originalLead.ReplacedById.HasValue) evt.AddRefValue(nameof(Lead), originalLead.ReplacedById.Value);

            await MessageBroker.DispatchAsync(evt);
        }

        if (action.Options.OriginalLeadEventId.HasValue)
        {
            var evt = new GenericFlowEvent(originalLead)
            {
                RunId = action.Event.RunId,
                Action = nameof(LeadDupeCheckAction),
                Description = "Duplicate Lead added",
                EventTypeId = action.Options.OriginalLeadEventId,
            };
            evt.AddRefValue(lead);
            await MessageBroker.DispatchAsync(evt);
        }
    }

    [Obsolete("the integration exporting the data should update the object directly")]
    private async Task UpdateIntegrationAsync(UpdateIntegrationForAppointmentAction.Message action)
    {
        if (!action.Event.Context.IntegrationId.HasValue)
        {
            Logger.LogError(
                "Missing context for {LeadId} / {AppointmentId}",
                action.Event.Lead.Lead.Id,
                action.Event.Appointment.Appointment.Id
            );
            return;
        }

        var integrationId = action.Event.Context.IntegrationId.Value;
        var list = action.Event.Appointment.IntegrationMapping?.Where(x => x.IntegrationId == integrationId).ToArray();
        if (list == null || list.Length != 1)
        {
            Logger.LogError(
                "Couldn't determine integration mapping for {appointmentId}/{integrationId}",
                action.Event.Appointment.Appointment.Id, integrationId
            );
            return;
        }

        var mapping = list[0];

        await _integrationAppointmentAdapter.UpsertAsync(new IntegrationContext(integrationId), new AppointmentIntegration
        {
            ExternalId = mapping.ExternalId.Or(action.Event.Appointment.Appointment.Id.ToString()), // TODO: change schema to allow null external ids 
            AppointmentId = action.Event.Appointment.Appointment.Id,
            IntegrationId = integrationId,
            Status = action.Event.Description
        });

        if (action.Options.NextEventId.HasValue)
        {
            var description = action.GetEventDescription(action.Options.NextEventId, "Integration Data for appointment saved");
            await _leadEventService.FireAsync(
                action.Options.NextEventId.Value,
                action.Event.Lead.Lead,
                description,
                null,
                action.Event.Appointment.Appointment,
                action: nameof(ActionIds.UpdateIntegrationForAppointment),
                runId: action.Event.RunId
            );
        }
    }

    private async Task UpdateIntegrationAsync(UpdateIntegrationForLeadAction.Message action)
    {
        if (!action.Event.Context.IntegrationId.HasValue)
        {
            Logger.LogError("Missing context for {LeadId}", action.Event.Lead.Lead.Id);
            return;
        }

        var integrationId = action.Event.Context.IntegrationId.Value;
        var list = action.Event.Lead.IntegrationMapping?.Where(x => x.IntegrationId == integrationId).ToArray();
        if (list == null || list.Length != 1)
        {
            Logger.LogError("Couldn't determine integration mapping for {LeadId}/{IntegrationId}", action.Event.Lead.Lead.Id, integrationId);
            return;
        }

        var mapping = list[0];

        await _integrationLeadAdapter.UpsertAsync(new IntegrationContext(integrationId), new LeadIntegration
        {
            ExternalId = mapping.ExternalId.Or(action.Event.Lead.Lead.Id.ToString()), // TODO: change schema to allow null external ids 
            LeadId = action.Event.Lead.Lead.Id,
            IntegrationId = integrationId,
            Status = action.Event.Description
        });

        if (action.Options.NextEventId.HasValue)
        {
            var description = action.GetEventDescription(action.Options.NextEventId, "Integration Data for lead saved");
            await _leadEventService.FireAsync(
                action.Options.NextEventId.Value,
                action.Event.Lead.Lead,
                description,
                null,
                action.Event.Appointment?.Appointment,
                action: nameof(ActionIds.UpdateIntegrationForLead),
                runId: action.Event.RunId
            );
        }
    }

    // private async Task AssignFlowAsync(AssignFlowAction.Message action)
    // {
    //     var lead = action.Event.Lead.Lead;
    //     var transition = await _assignFlowAdapter.GetAsync(lead.EntityId, lead.FlowId.Value, action.Options.Tag);
    //     var targetFlowId = transition?.TargetFlowId ?? action.Options.FallbackFlowId;
    //
    //     if (!targetFlowId.HasValue)
    //     {
    //         _logger.LogWarning("Couldn't resolve flowId for {LeadId}", lead.Id);
    //         // TODO: fire error event
    //         // ...
    //         return;
    //     }
    //
    //     // TODO: get flow to make sure that exists and it is valid for the entityid
    //     var flow = await _flowAdapter.GetByIdAsync(targetFlowId.Value);
    //     if (flow == null)
    //     {
    //         _logger.LogCritical("Unknown {flowId}", targetFlowId.Value);
    //         return;
    //     }
    //
    //     var record = await UpdateFlowIdAsync(lead.Id, flow.Id);
    //
    //     _logger.LogInformation(
    //         "Updated Lead {LeadId} from {CurrFlowId} to {FlowId}: {Flow}",
    //         lead.Id,
    //         lead.FlowId,
    //         record.FlowId,
    //         flow.Name);
    //
    //     if (action.Options.NextEventId.HasValue)
    //     {
    //         var description = action.GetEventDescription(action.Options.NextEventId, $"Start flow '{flow.Name}'");
    //         await _leadEventService.FireAsync(
    //             action.Options.NextEventId.Value,
    //             record,
    //             description,
    //             null,
    //             action.Event.Appointment?.Appointment,
    //             action: nameof(ActionIds.AssignFlow),
    //             runId: action.Event.RunId
    //         );
    //     }
    // }

    private async Task<bool> AssignLeadAsync(AssignAction.Message action)
    {
        var lead = await _connection.GetByIdAsync<Lead>(action.LeadId);
        if (lead == null)
        {
            _logger.LogError("Lead {LeadId} not found", action.LeadId);
            return false;
        }

        if (lead.AssignedEntityId.HasValue &&
            lead.AssignedEntityId != action.CurrentEntityId)
        {
            _logger.LogWarning(
                "Lead {LeadId} out of sync: {ExpectedAssignedEntityId} vs {CurrentAssignedEntityId} => {TargetAssignedEntityId}",
                lead.Id,
                action.CurrentEntityId,
                lead.AssignedEntityId,
                action.TargetEntityId);

            return false;
        }

        if (!action.TargetEntityId.HasValue)
        {
            _logger.LogError("Missing option: TargetEntityId processing {LeadId}", lead.Id);
            return false;
        }

        var entity = await _identityAdapter.GetEntityByIdAsync(action.TargetEntityId.Value);
        if (entity == null)
        {
            _logger.LogError("Unknown {EntityId}", action.TargetEntityId.Value);
            return false;
        }

        var mutableLead = await UpdateAssignedEntityIdAsync(lead.Id, entity.Id);
        if (mutableLead == null)
        {
            _logger.LogError(
                "Failed to update {LeadId}, assignedEntityId to {TargetAssignedEntityIdd} from {AssignedEntityId}: {Entity}",
                lead.Id,
                action.TargetEntityId,
                lead.AssignedEntityId,
                entity.Name
            );

            return false;
        }

        _logger.LogInformation(
            "Updated Lead {LeadId} from {AssignedEntityId} to {TargetAssignedEntityId}: {Entity}",
            lead.Id,
            action.CurrentEntityId,
            action.TargetEntityId,
            entity.Name
        );

        if (action.Options.NextEventId.HasValue)
        {
            var description = action.GetEventDescription(action.Options.NextEventId, $"Assigned to {entity.Name}");
            await _leadEventService.FireAsync(
                action.Options.NextEventId.Value,
                mutableLead,
                description,
                action.ActorId,
                action.Event.Appointment?.Appointment,
                action: nameof(ActionIds.AssignLead),
                runId: action.Event.RunId
            );
        }

        return true;
    }

    public async Task<Lead> UpdateAssignedEntityIdAsync(Guid id, Guid assignedEntityId)
        => await _connection.UpdatePropertyAsync<Lead, Guid?>(id, x => x.AssignedEntityId, assignedEntityId);

    public async Task<Lead> UpdateFlowIdAsync(Guid id, Guid flowId)
        => await _connection.UpdatePropertyAsync<Lead, Guid?>(id, x => x.FlowId, flowId);
}