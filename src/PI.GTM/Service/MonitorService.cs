// using System;
// using System.Linq;
// using System.Net.Http;
// using System.Threading.Tasks;
// using Crochik.Messaging;
// using Crochik.NET.APM;
// using Messages.Integration;
// using Messages.Lead;
// using Microsoft.AspNetCore.Authentication.OAuth;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using PI.GTM.API.Api;
// using PI.GTM.API.Model;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Data.Models;
// using PI.Shared.Models;
//
// namespace Services;
//
// public class MonitorService : AbstractMessageQueueService
// {
//     private const string BaseUrl = "https://api.getgo.com/G2M/rest";
//
//     private readonly ILogger<MonitorService> _logger;
//     private readonly IEntityIdentityAdapter _identityAdapter;
//     private readonly IAppointmentTypeIntegrationAdapter _appointmentTypeIntegrationAdapter;
//     private readonly Config _config;
//     private readonly HttpClient _client;
//
//     public MonitorService(
//         ILogger<MonitorService> logger,
//         IConfiguration configuration,
//         IMessageBroker messageBroker,
//         IAPMService apmService,
//         IEntityIdentityAdapter identityAdapter,
//         IAppointmentTypeIntegrationAdapter appointmentTypeIntegrationAdapter
//         // IHttpClientFactory clientFactory
//     ) : base(logger, configuration, messageBroker, apmService)
//     {
//         _logger = logger;
//         _identityAdapter = identityAdapter;
//         _appointmentTypeIntegrationAdapter = appointmentTypeIntegrationAdapter;
//         _config = configuration.GetSection(nameof(MonitorService)).Get<Config>();
//
//         // _client = clientFactory.CreateClient();
//         _client = new HttpClient();
//     }
//
//     protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
//     {
//         mapper.Register<AppointmentEvent>();
//     }
//
//     protected async override Task OnMessageAsync(IMessage evt)
//     {
//         try
//         {
//             var parts = evt.RoutingKey.Split('.');
//             Logger.LogTrace(evt.RoutingKey);
//
//             switch (evt.Body)
//             {
//                 case AppointmentEvent appt:
//                     await OnAppointmentAsync(appt);
//                     break;
//             }
//         }
//         catch (Exception ex)
//         {
//             Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
//         }
//
//         evt.Acknowledge();
//     }
//
//     private async Task OnAppointmentAsync(AppointmentEvent appt)
//     {
//         var integrationId = Guid.Parse(_config.IntegrationId);
//         var integration = await _appointmentTypeIntegrationAdapter.GetByIdAsync(appt.Appointment.AppointmentTypeId, integrationId);
//         if (integration == null)
//         {
//             // TODO: cache result?
//             // ...
//             // nothing to
//             return;
//         }
//
//         var data = integration.GetData<ZoomIntegration.Data>();
//         if (data == null)
//         {
//             // error
//             Logger.LogError("Bad configuration for {appointmentTypeId}", appt.Appointment.AppointmentTypeId);
//             return;
//         }
//
//         var meeting = await CreateMeetingAsync(data.EntityId, appt);
//         Logger.LogInformation("Created Meeting {meetingId} for {appointmentId} using {entityId}'s credentials", meeting.Id, appt.Appointment.Id, data.EntityId);
//
//         await MessageBroker.PublishAsync(
//             UpsertIntegration.UpsertAppointment(integrationId),
//             new UpsertIntegration
//             {
//                 ObjectType = SystemObjectType.Appointment,
//                 Integration = new IntegrationUpdate
//                 {
//                     IntegrationId = integrationId,
//                     Id = appt.Appointment.Id,
//                     Status = $"Meeting Created: {meeting.JoinUrl}",
//                     Url = meeting.StartUrl,
//                     ExternalId = meeting.Id,
//                     Data = new
//                     {
//                         meeting.JoinUrl, meeting.Invitation
//                     }
//                 }
//             }
//         );
//     }
//
//     public async Task<Result> CreateMeetingAsync(Guid entityId, AppointmentEvent appt)
//     {
//         var identities = await _identityAdapter.GetByEntityAsync(entityId, ExternalProvider.GoToMeeting);
//         var array = identities.ToArray();
//         if (array.Length != 1)
//         {
//             Logger.LogError("{appointmentId}: Couldn't find identity for {entityId}", appt.Appointment.Id, entityId);
//             throw new ApplicationException("None or Too many identities");
//         }
//
//         var externalIdentity = array[0].ExternalIdentity;
//
//         if (externalIdentity.Token.HasExpired)
//         {
//             Logger.LogDebug("Renew GoToMeeting Token for {entityId}", entityId);
//
//             var options = new OAuthOptions
//             {
//                 AuthorizationEndpoint = _config.AuthorizationEndpoint,
//                 TokenEndpoint = _config.TokenEndpoint,
//                 ClientId = _config.ClientId,
//                 ClientSecret = _config.ClientSecret
//             };
//
//             // externalIdentity.Token = await _client.RefreshGTMTokenAsync(options, externalIdentity.Token.RefreshToken);
//             throw new NotImplementedException("have to fix token refresh (dotnetcore 3.0)");
//
//             await _identityAdapter.UpdateValueAsync(externalIdentity);
//
//             Logger.LogInformation("Renewed GoToMeeting Token for {entityId}", entityId);
//         }
//
//         var api = new MeetingsApi(BaseUrl);
//         var meetings = await api.CreateMeetingAsync(
//             externalIdentity.Token.AccessToken,
//             new MeetingReqCreate(                
//                 subject: "Meeting", // ???
//                 starttime: appt.Appointment.Start,
//                 endtime: appt.Appointment.End,
//                 passwordrequired: false,
//                 conferencecallinfo: "Free", // ????
//                 meetingtype: MeetingType.Scheduled,
//                 timezonekey: ""
//             )
//         );
//
//         var meeting = meetings[0];
//         Logger.LogInformation("GTM Meeting {meetingId} scheduled for {appointmentId}", meeting.Meetingid, appt.Appointment.Id);
//
//         return new Result
//         {
//             Id = meeting.Meetingid.ToString(),
//             StartUrl = meeting.JoinURL,
//             JoinUrl = meeting.JoinURL,
//             Invitation = meeting.ConferenceCallInfo
//         };
//     }
//
//     public class MeetingInvitation
//     {
//         public string Invitation { get; set; }
//     }
//
//     public class Result
//     {
//         public string Id { get; set; }
//         public string StartUrl { get; set; }
//         public string JoinUrl { get; set; }
//         public string Invitation { get; set; }
//     }
//
//     public class Config : QueueConfig
//     {
//         public string AuthorizationEndpoint { get; set; } = "https://zoom.us/oauth/authorize";
//         public string TokenEndpoint { get; set; } = "https://api.zoom.us/oauth/token";
//         public string ClientId { get; set; }
//         public string ClientSecret { get; set; }
//         public string IntegrationId { get; set; }
//     }
// }