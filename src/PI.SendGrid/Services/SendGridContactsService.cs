using System;
using System.Net;
using System.Threading.Tasks;
using Crochik.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using SendGrid;

namespace Services
{
    public class SendGridContactsService : AbstractMessageQueueService
    {
        private readonly Config _config;
        private readonly ILeadTypeIntegrationAdapter _leadTypeIntegrationAdapter;

        public SendGridContactsService(
            ILogger<SendGridContactsService> logger,
            IConfiguration configuration,
            IMessageBroker messageBroker,
            // IAPMService apmService,
            ILeadTypeIntegrationAdapter leadTypeIntegrationAdapter
            ) : base(logger, configuration, messageBroker)
        {
            this._config = configuration.GetSection(nameof(SendGridContactsService)).Get<Config>();
            this._leadTypeIntegrationAdapter = leadTypeIntegrationAdapter;
        }

        protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
        {
            // mapper.Register<Messages.Lead.LeadEvent>();
        }

        protected async override Task OnMessageAsync(IMessage evt)
        {
            try
            {
                var parts = evt.RoutingKey.Split('.');
                Logger.LogTrace(evt.RoutingKey);

                // switch (evt.Body)
                // {

                    // case Messages.Lead.LeadEvent lead:
                    //     await OnLeadCreatedAsync(parts[1], lead);
                    //     break;

                        //case Messages.Lead.LeadIntegration leadIntegration:
                        //await OnLeadIntegrationCreatedAsync(parts[1], leadIntegration);
                        //break;
                // }

                await Task.CompletedTask;

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);

            }

            evt.Acknowledge();
        }

        private async Task OnLeadCreatedAsync(string leadTypeIdStr, Lead lead) 
        {
            var leadTypeId = Guid.Parse(leadTypeIdStr);
            var integrationId = Guid.Parse(_config.IntegrationId);

            // TODO: only reason this service needs access to database
            // should it use the API instead?
            // ...
            var integration = await _leadTypeIntegrationAdapter.GetByIdAsync(leadTypeId, integrationId);
            if (integration == null)
            {
                // TODO: cache result?
                // ...
                // nothing to
                return;
            }

            var auth = integration.GetAuthentication<SendGridIntegration.Authentication>();
            if (auth == null)
            {
                // error
                Logger.LogError("Bad configuration for {leadTypeId}", leadTypeId);
                return;
            }

            // TODO: allow user to use their own 
            // ...

            // var auth = integration.GetAuthentication<SendGridIntegration.Authentication>();
            // if (data == null || data.HookUrl==null)
            // {
            //     // error
            //     Logger.LogError("Bad configuration for {leadTypeId}", leadTypeId);
            //     return;
            // }

            Logger.LogInformation(
                "Lead Created {leadId}: {name}",
                lead.Id,
                lead.Name
            );

            // TODO: allow to configure field name?
            var email = lead[Lead.PropertyName_Email];
            if (string.IsNullOrEmpty(email))
            {
                Logger.LogInformation("Can't add contact {leadId} ({name}), missing email.", lead.Id, lead.Name);
                return;
            }

            var contact = new SGRecipient[] {
                new SGRecipient {
                    FirstName = lead.GetFirstName(),
                    LastName = lead.GetLastName(),
                    Email = email.ToString()
                    //LeadId = Guid.NewGuid().ToString()
                }
            };

            var client = new SendGridClient(auth.APIKey);
            var resp = await AddAsync(client, contact);
            if (resp == null)
            {
                Logger.LogError("Failed to Create Contact {leadId}", lead.Id);
                return;
            }

            if (resp.NewCount == 1 || resp.UpdatedCount == 1)
            {
                var id = resp.PersistedReceipients[0];
                Logger.LogInformation("Contact Exported {leadId} as {id}", lead.Id, id);
                await MessageBroker.PublishAsync(
                    Messages.Integration.UpsertIntegration.UpsertLead(integrationId),
                    new Messages.Integration.UpsertIntegration
                    {
                        ObjectType = SystemObjectType.Lead,
                        Integration = new Messages.Integration.IntegrationUpdate
                        {
                            IntegrationId = integrationId,
                            Id = lead.Id,
                            ExternalId = id,
                            Status = "Contact exported"
                        }
                    }
                );
                return;
            }

            if (resp.UnmodifiedIndices.Length == 1)
            {
                Logger.LogInformation("Contact was up to date {leadId}", lead.Id);
                return;
            }

            Logger.LogError("Failed to add/update {leadId}: {error}", lead.Id, resp.Errors);

            // TODO: post leadintegration update message 
            // can't because it can't add id :(
            // ...
        }

        private async Task<SGRecipientsResponse> AddAsync(SendGridClient client, SGRecipient[] contact)
        {
            var url = "contactdb/recipients";
            var requestBody = JsonConvert.SerializeObject(contact, SerializationSettings.Default);
            var resp = await client.RequestAsync(SendGridClient.Method.POST, requestBody: requestBody, urlPath: url);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.Created:
                    // case HttpStatusCode.Accepted:
                    // case HttpStatusCode.NonAuthoritativeInformation:
                    // case NoContent:
                    // case ResetContent:
                    // case PartialContent:
                    // case MultiStatus:
                    // case AlreadyReported:
                    var body = JsonConvert.DeserializeObject<SGRecipientsResponse>(await resp.Body.ReadAsStringAsync());
                    Logger.LogInformation("Add Contact: {statusCode}: {body}", resp.StatusCode, body);
                    return body;

                default:
                    Logger.LogError("Error adding contact {statusCode}", resp.StatusCode);
                    break;
            }

            return null;
        }

        public class Config : QueueConfig
        {
            public string IntegrationId { get; set; }
        }

        public class SGRecipient
        {
            [JsonProperty("first_name")]
            public string FirstName { get; set; }
            [JsonProperty("last_name")]
            public string LastName { get; set; }
            public string Email { get; set; }
        }

        public class SGRecipientsResponse
        {
            [JsonProperty("new_count")]
            public int NewCount { get; set; }

            [JsonProperty("updated_count")]
            public int UpdatedCount { get; set; }

            [JsonProperty("error_count")]
            public int ErrorCount { get; set; }

            [JsonProperty("error_indices")]
            public int[] ErrorIndices { get; set; }

            [JsonProperty("unmodified_indices")]
            public int[] UnmodifiedIndices { get; set; }

            [JsonProperty("persisted_recipients")]
            public string[] PersistedReceipients { get; set; }

            [JsonProperty("errors")]
            public string[] Errors { get; set; }
        }
    }
}