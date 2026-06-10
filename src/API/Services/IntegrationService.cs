
// using System;
// using System.Linq;
// using System.Security.Claims;
// using System.Threading.Tasks;
// using AutoMapper;
// using Controllers.Models;
// using Crochik.Messaging;
// using Crochik.NET.APM;
// using Messages.Integration;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using Newtonsoft.Json;
// using Newtonsoft.Json.Converters;
// using PI.Shared.Constants;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Models;

// namespace Services
// {
//     [JsonConverter(typeof(StringEnumConverter))]
//     public enum IntegrationErrorCode
//     {
//         InvalidProvider,
//         Other
//     };

//     public class IntegrationServiceException : Exception
//     {
//         public IntegrationErrorCode Reason { get; }

//         public IntegrationServiceException(IntegrationErrorCode errorCode)
//         {
//             Reason = errorCode;
//         }

//         public IntegrationServiceException(IntegrationErrorCode errorCode, string message) :
//             base(message)
//         {
//             Reason = errorCode;
//         }
//     }

//     public class IntegrationService : AbstractMessageQueueService
//     {
//         private readonly IMapper _mapper;
//         private readonly IEventTrackerService _eventTrackerService;
//         private readonly LeadService _leadService;
//         private readonly IIntegrationLeadAdapter _integrationLeadAdapter;
//         private readonly IIntegrationAppointmentAdapter _integrationAppointmentAdapter;
//         private readonly AppointmentAdapter _appointmentAdapter;
//         private readonly IUserAdapter _userAdapter;
//         private readonly IEntityIdentityAdapter _identityAdapter;
//         private readonly ILeadAdapter _leadAdapter;
//         private readonly ILeadTypeAdapter _leadTypeAdapter;
//         private readonly IAppointmentTypeAdapter _appointmentTypeAdapter;
//         private readonly IEntityAdapter _entityAdapter;

//         public IntegrationService(
//             ILogger<IntegrationService> logger,
//             IMapper mapper,
//             IConfiguration configuration,
//             IMessageBroker messageBroker,
//             IAPMService apmService,
//             IEventTrackerService eventTrackerService,
//             LeadService leadService,
//             IUserAdapter userAdapter,
//             ILeadAdapter leadAdapter,
//             ILeadTypeAdapter leadTypeAdapter,
//             IAppointmentTypeAdapter appointmentTypeAdapter,
//             IEntityAdapter entityAdapter,
//             IEntityIdentityAdapter identityAdapter,
//             IIntegrationLeadAdapter integrationLeadAdapter,
//             IIntegrationAppointmentAdapter integrationAppointmentAdapter,
//             AppointmentAdapter appointmentAdapter
//             ) : base(logger, configuration, messageBroker, apmService)
//         {
//             this._mapper = mapper;
//             this._eventTrackerService = eventTrackerService;
//             this._leadService = leadService;
//             this._userAdapter = userAdapter;
//             this._leadAdapter = leadAdapter;
//             this._leadTypeAdapter = leadTypeAdapter;
//             this._appointmentTypeAdapter = appointmentTypeAdapter;
//             this._entityAdapter = entityAdapter;
//             this._identityAdapter = identityAdapter;

//             this._integrationLeadAdapter = integrationLeadAdapter;
//             this._integrationAppointmentAdapter = integrationAppointmentAdapter;
//             this._appointmentAdapter = appointmentAdapter;
//         }

//         protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
//         {
//             mapper.Register<Messages.Lead.AppointmentExported>();
//             mapper.Register<Messages.Integration.UpsertIntegration>();
//         }

//         protected async override Task OnMessageAsync(IMessage evt)
//         {
//             try
//             {
//                 switch (evt.Body)
//                 {
//                     case Messages.Lead.AppointmentExported appt:
//                         if (appt.CurrentState == Messages.Lead.AppointmentExported.State.Deleted)
//                         {
//                             await OnAppointmentRemovedAsync(appt);
//                         }
//                         else
//                         {
//                             await OnAppointmentAddedAsync(appt);
//                         }
//                         break;

//                     case Messages.Integration.UpsertIntegration upsert:
//                         await ProcessAsync(upsert);
//                         break;
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Logger.LogError(ex, "Error processing message");
//             }

//             evt.Acknowledge();
//         }

//         private async Task ProcessAsync(UpsertIntegration upsert)
//         {
//             var integration = upsert.Integration;
//             switch (upsert.ObjectType)
//             {
//                 case ObjectType.Appointment:
//                     await OnAppointmentUpsertedAsync(integration);
//                     break;

//                 case ObjectType.Lead:
//                     await OnLeadUpsertedAsync(integration);
//                     break;
//             }
//         }

//         private async Task OnLeadUpsertedAsync(IntegrationUpdate integration)
//         {
//             using var apm = ApmService.StartTransaction("LeadIntegration", "Integration Update");
//             apm.Context = new
//             {
//                 IntegrationId = integration.IntegrationId,
//                 LeadId = integration.Id,
//                 ExternalId = integration.ExternalId ?? integration.Id.ToString(),
//             };

//             var integrationLead = await _integrationLeadAdapter.UpsertAsync(new IntegrationLead
//             {
//                 IntegrationId = integration.IntegrationId,
//                 LeadId = integration.Id,
//                 ExternalId = integration.ExternalId ?? integration.Id.ToString(),
//                 Data = integration.Data,
//                 Status = integration.Status,
//                 Url = integration.Url
//             });

//             await _eventTrackerService.LogAsync(integrationLead);
//         }

//         private async Task OnAppointmentUpsertedAsync(IntegrationUpdate integration)
//         {
//             using var apm = ApmService.StartTransaction("AppointmentIntegration", "Appointment Integration Update");
//             apm.Context = new
//             {
//                 IntegrationId = integration.IntegrationId,
//                 LeadId = integration.Id,
//                 ExternalId = integration.ExternalId ?? integration.Id.ToString(),
//             };

//             var iAppointment = await _integrationAppointmentAdapter.UpsertAsync(new IntegrationAppointment
//             {
//                 IntegrationId = integration.IntegrationId,
//                 AppointmentId = integration.Id,
//                 ExternalId = integration.ExternalId ?? integration.Id.ToString(),
//                 Data = integration.Data,
//                 Status = integration.Status,
//                 Url = integration.Url
//             });
//             await _eventTrackerService.LogAsync(iAppointment);
//         }

//         public async Task<IIntegrationLead> AddToLeadAsync(ClaimsPrincipal user, Guid integrationId, string externalId, AddIntegrationToLead req)
//         {
//             // TODO: figure out the integrationid based on the client Id
//             // ...

//             // TODO: check whether integration should have access to this lead
//             // check if entity for the lead type of the lead has integration
//             // ... 

//             var lead = await _leadAdapter.GetByIdAsync(req.LeadId);
//             if (lead == null)
//             {
//                 return null;
//             }

//             var iLead = await _integrationLeadAdapter.FindAsync(integrationId, externalId);
//             if (iLead != null)
//             {
//                 var mutable = Mapper.Map<IntegrationLead>(iLead);
//                 mutable.Data = req.Data;
//                 mutable.Url = req.Url;
//                 mutable.Status = req.Status;

//                 await _integrationLeadAdapter.UpdateAsync(mutable);

//                 return mutable;
//             }

//             var integegrationLead = await _integrationLeadAdapter.AddAsync(new IntegrationLead
//             {
//                 IntegrationId = integrationId,
//                 ExternalId = externalId,
//                 LeadId = req.LeadId,
//                 Status = req.Status,
//                 Url = req.Url,
//                 Data = req.Data
//             });

//             await _eventTrackerService.LogAsync(integegrationLead);

//             return integegrationLead;
//         }

//         public async Task<bool> AddIntegrationToAppointmentAsync(ClaimsPrincipal user, Guid integrationId, string externalId, UpdateAppointmentIntegration req)
//         {
//             // TODO: figure out the integrationid based on the client Id
//             // ...

//             // TODO: check whether integration should have access to this appt
//             // ... 

//             var appointment = await _appointmentAdapter.GetByIdAsync(req.AppointmentId);
//             if (appointment == null)
//             {
//                 return false;
//             }

//             var existing = await _integrationAppointmentAdapter.FindAsync(integrationId, externalId);
//             if (existing != null)
//             {
//                 // TODO: should update data,status,url?
//                 // ...

//                 return false;
//             }

//             // We do not trigger event here, as we monitor this messages
//             // and will log them once we received
//             // (they can also be published by integrations (e.g. o365)
//             // and we don't want to log twice
//             var message = new Messages.Lead.AppointmentExported
//             {
//                 CurrentState = Messages.Lead.AppointmentExported.State.Added,
//                 Id = req.AppointmentId,
//                 IntegrationId = integrationId,
//                 ExternalId = externalId,
//                 Status = req.Status,
//                 Url = req.Url,
//                 Data = req.Data,
//                 IsCalendar = false
//             };

//             await MessageBroker.PublishAsync(
//                 Messages.Lead.AppointmentExported.IntegrationAddedRoute(appointment.AppointmentTypeId),
//                 message
//             );

//             return true;
//         }

//         internal async Task<bool> OnAppointmentRemovedAsync(ClaimsPrincipal user, Guid integrationId, string externalId, UpdateAppointmentIntegration req)
//         {
//             var appointment = await _appointmentAdapter.GetByIdAsync(req.AppointmentId);
//             if (appointment == null)
//             {
//                 return false;
//             }

//             var iAppointment = await _integrationAppointmentAdapter.FindAsync(integrationId, externalId);
//             if (iAppointment == null)
//             {
//                 return false;
//             }


//             // We do not trigger event here, as we monitor this messages
//             // and will log them once we received
//             // (they can also be published by integrations (e.g. o365)
//             // and we don't want to log twice
//             var message = new Messages.Lead.AppointmentExported
//             {
//                 CurrentState = Messages.Lead.AppointmentExported.State.Deleted,
//                 Id = iAppointment.AppointmentId,
//                 IntegrationId = iAppointment.IntegrationId,
//                 ExternalId = iAppointment.ExternalId,
//                 Status = req.Status,
//                 IsCalendar = false
//             };

//             await MessageBroker.PublishAsync(
//                 Messages.Lead.AppointmentExported.IntegrationRemovedRoute(appointment.AppointmentTypeId),
//                 message
//             );

//             return true;
//         }

//         private async Task OnAppointmentAddedAsync(Messages.Lead.AppointmentExported appt)
//         {
//             using var apm = ApmService.StartTransaction("AppointmentAdded", "Appointment Exported");
//             apm.Context = new
//             {
//                 AppointmentId = appt.Id,
//                 appt.IntegrationId,
//                 appt.ExternalId
//             };

//             Logger.LogDebug("{appointmentId} exported to {integrationId} with {externalId}",
//                 appt.Id, appt.IntegrationId, appt.ExternalId
//             );

//             await _integrationAppointmentAdapter.AddAsync(
//                 new IntegrationAppointment
//                 {
//                     IntegrationId = appt.IntegrationId,
//                     ExternalId = appt.ExternalId,
//                     AppointmentId = appt.Id,
//                     Status = appt.Status,
//                     Url = appt.Url,
//                     Data = appt.Data
//                 }
//             );

//             if (appt.IsCalendar)
//             {
//                 await _appointmentAdapter.FlagAsExportedAsync(
//                     appt.Id,
//                     appt.Url
//                 );
//             }

//             await _eventTrackerService.LogAsync(appt);
//         }

//         private async Task OnAppointmentRemovedAsync(Messages.Lead.AppointmentExported appt)
//         {
//             using var apm = ApmService.StartTransaction("AppointmentRemoved", "Appointment Removed");
//             apm.Context = new
//             {
//                 AppointmentId = appt.Id,
//                 appt.IntegrationId,
//                 appt.ExternalId
//             };

//             Logger.LogDebug("{appointmentId}: {integrationId} with {externalId} was deleted",
//                 appt.Id, appt.IntegrationId, appt.ExternalId
//             );

//             await _integrationAppointmentAdapter.UpdateStatusAsync(
//                 appt.Id,
//                 appt.IntegrationId,
//                 appt.ExternalId,
//                 appt.Status,
//                 appt.Url
//             );

//             await _eventTrackerService.LogAsync(appt);
//         }

//         internal async Task<ImportedLead> ImportLeadAsync(ClaimsPrincipal claims, Guid integrationId, ImportLead request)
//         {
//             // TODO: find integration based on the client or lead type
//             // ... 

//             // TODO: make generic by checking scope
//             // as now it will only get here if it has the account:fci scope
//             if (request.ProviderId != ExternalProvider.InspireNet)
//             {
//                 throw new IntegrationServiceException(IntegrationErrorCode.InvalidProvider);
//             }

//             // TODO: validate the client should have access to the leadTypeId ??
//             // ...

//             if (request.ExternalLeadId == null)
//             {
//                 Logger.LogError("Missing lead external key: {provider} {providerId}", request.ProviderId, request.ExternalLeadId);
//                 throw new IntegrationServiceException(IntegrationErrorCode.Other, "Missing External key");
//             }

//             var identity = await _identityAdapter.FindAsync(ExternalProvider.InspireNet, request.ExternalEntityId);
//             if (identity == null)
//             {
//                 Logger.LogError("Didn't find Identity: {provider} {providerId}", request.ProviderId, request.ExternalEntityId);
//                 throw new IntegrationServiceException(IntegrationErrorCode.Other, "Invalid or Missing Identity");
//             }

//             var entity = await _entityAdapter.GetByIdAsync(identity.EntityId);
//             var leadTypes = await _leadTypeAdapter.GetForEntityAsync(entity.Context);
//             var leadType = leadTypes.FirstOrDefault(l => l.Id == request.LeadTypeId);
//             if (leadType == null)
//             {
//                 Logger.LogError("{leadTypeId} not found for {provider} {providerId}", request.LeadTypeId, request.ProviderId, request.ExternalEntityId);
//                 throw new IntegrationServiceException(IntegrationErrorCode.Other, "Invalid or Missing leadTypeId");
//             }

//             Lead lead;
//             Guid leadId;
//             var iLead = await _integrationLeadAdapter.FindAsync(integrationId, request.ExternalLeadId);
//             // string iLeadJson = JsonConvert.SerializeObject(request.Data);
//             // string leadJson = request.LeadData != null ? JsonConvert.SerializeObject(request.LeadData) : iLeadJson;
//             var body = request.LeadData ?? request.Data;

//             if (iLead != null)
//             {
//                 var mutableILead = Mapper.Map<IntegrationLead>(iLead);
//                 // existing: update
//                 // mutableILead.SeriliazedData = iLeadJson;
//                 mutableILead.Data = request.Data;
//                 mutableILead.Status = request.Status;
//                 mutableILead.Url = request.Url;

//                 await _integrationLeadAdapter.UpdateAsync(mutableILead);

//                 iLead = mutableILead;
//                 leadId = mutableILead.LeadId;
//                 lead = await _leadAdapter.GetByIdAsync(leadId);

//                 // only update lead if it was created by the integration
//                 if (lead.LeadTypeId == request.LeadTypeId)
//                 {
//                     lead = await _leadService.UpdateAsync(new IntegrationContext(IntegrationIds.InspireNet), lead, body);
//                 }

//                 Logger.LogInformation("Updated lead {leadId} for {externalId}", leadId, request.ExternalLeadId);

//                 // TODO: publish event? 
//                 // ...
//             }
//             else
//             {
//                 // TODO: ...
//                 var properLeadType = await _leadTypeAdapter.GetByIdAsync(leadType.Id);
//                 var integrationLead = new IntegrationLead
//                 {
//                     IntegrationId = integrationId,
//                     ExternalId = request.ExternalLeadId,
//                     Status = request.Status,
//                     Url = request.Url,
//                     // SeriliazedData = iLeadJson
//                     Data = request.Data
//                 };

//                 string json = JsonConvert.SerializeObject(body);
//                 var builder = await _leadService.AddAsync(entity.Context, properLeadType, json, integrationLead);
//                 if (builder.Failed)
//                 {
//                     Logger.LogError(
//                         "Failed to create lead for {leadTypeId} with {error}: {provider} {providerId}",
//                         request.LeadTypeId,
//                         builder.Error,
//                         request.ProviderId,
//                         request.ExternalEntityId);

//                     throw new IntegrationServiceException(IntegrationErrorCode.Other, builder.Error);
//                 }

//                 lead = builder.Result;
//                 leadId = lead.Id;
//             }

//             var apptType = await _appointmentTypeAdapter.GetDefaultForOrgAsync(entity.Context, lead.LeadTypeId);

//             var ret = new ImportedLead
//             {
//                 LeadId = leadId,
//                 ReferenceId = request.ExternalLeadId,
//                 AppointmentTypeId = apptType?.Id,
//                 EntityId = entity.Id,
//                 // limit to a user?
//                 // EntityId = request.Level=="User" && entityId.Value != apptType.EntityId ? (Guid?)entityId.Value : null
//             };

//             Logger.LogInformation("Created {leadId} for {key}: {provider} {providerId} ", leadId, request.ExternalLeadId, request.ProviderId, request.ExternalEntityId);

//             return ret;
//         }
//     }
// }