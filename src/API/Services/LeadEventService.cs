using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Messaging;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LeadEventService : ILeadEventService
{
    private readonly IMapper _mapper;
    private readonly IUrlService _urlService;
    private readonly IEntityIdentityAdapter _identityAdapter;
    private readonly IIntegrationLeadAdapter _integrationLeadAdapter;
    private readonly IIntegrationAppointmentAdapter _integrationAppointmentAdapter;
    public readonly IMessageBroker _messageBroker;

    public LeadEventService(
        IMapper mapper,
        IMessageBroker messageBroker,
        IUrlService urlService,
        IEntityIdentityAdapter identityAdapter,
        IIntegrationLeadAdapter integrationLead,
        IIntegrationAppointmentAdapter integrationAppointment
    )
    {
        this._mapper = mapper;
        this._urlService = urlService;
        this._identityAdapter = identityAdapter;
        this._integrationLeadAdapter = integrationLead;
        this._integrationAppointmentAdapter = integrationAppointment;
        this._messageBroker = messageBroker;
    }

    public async Task FireAsync(
        Guid eventId,
        Lead lead,
        string description,
        Guid? entityId = null,
        Appointment appointment = null,
        Guid? integrationId = null,
        string action = null,
        Guid? runId = null
    )
    {
        if (!lead.FlowId.HasValue) return;

        var leadIntegration = await _integrationLeadAdapter.GetAsync(lead.Id);
        var leadMapping = leadIntegration?.ToList().ConvertAll(i => new Messages.IntegrationMapping
        {
            IntegrationId = i.IntegrationId,
            ExternalId = i.ExternalId
        });

        Messages.Flow.AppointmentInfo apptInfo = null;
        if (appointment != null)
        {
            var apptIntegration = await _integrationAppointmentAdapter.GetAsync(appointment.Id);
            var apptMapping = apptIntegration?.ToList().ConvertAll(i => new Messages.IntegrationMapping
            {
                IntegrationId = i.IntegrationId,
                ExternalId = i.ExternalId
            });

            apptInfo = new Messages.Flow.AppointmentInfo
            {
                Appointment = _mapper.Map<Appointment>(appointment),
                IntegrationMapping = apptMapping,
            };

            // if scheduling for a different user, add the other users external identities
            if (entityId.HasValue && entityId != appointment.EntityId)
            {
                apptInfo.ExternalIdentities = await _identityAdapter.GetEntityTrunkIdentitiesAsync(appointment.EntityId);
            }
        }

        // always add some context (fallback to appt, lead entities)
        var contextEntityId = entityId ?? appointment?.EntityId ?? lead.AssignedEntityId ?? lead.EntityId;

        var flowEvent = new Messages.Flow.LeadWithAppointmentEvent
        {
            Action = action,
            Context = new Messages.Flow.Context
            {
                EntityId = contextEntityId,
                ExternalIdentities = await _identityAdapter.GetEntityTrunkIdentitiesAsync(contextEntityId),
                IntegrationId = integrationId,
            },
            Lead = new Messages.Flow.LeadInfo
            {
                Lead = _mapper.Map<Lead>(lead),
                IntegrationMapping = leadMapping,
                SchedulerUrl = await _urlService.GetSchedulerUrlAsync(lead.Id)
            },
            Appointment = apptInfo,
            Description = description
        };

        if (runId.HasValue) flowEvent.RunId = runId.Value;

        await _messageBroker.PublishAsync(EventIds.GetRoute(eventId), flowEvent);
    }

    // public Task OnAppointmentCanceledAsync(IEntityContext context, Lead lead, IAppointment appointment, string description)
    // {
    //     return FireAsync(
    //         EventIds.OnAppointmentCanceled,
    //         lead,
    //         description,
    //         context.UserId,
    //         appointment,
    //         integrationId: context.GetIntegrationId());
    // }        
}