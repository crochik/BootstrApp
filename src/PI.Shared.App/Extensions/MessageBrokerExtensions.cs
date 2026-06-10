using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Messaging;
using Messages;
using Messages.Flow;
using PI.Shared.Constants;

namespace PI.Shared.App.Message
{
    public static class IntegrationMappinExtensions
    {
        public static IEnumerable<IntegrationMapping> Add(this IEnumerable<IntegrationMapping> integrationMapping, Guid integrationId, string externalId)
        {
            var mapping = new IntegrationMapping
            {
                IntegrationId = integrationId,
                ExternalId = externalId,
            };

            if (integrationMapping.IsEmpty()) return new[] { mapping };

            var list = new List<IntegrationMapping>();
            list.AddRange(integrationMapping);
            foreach (var i in list)
            {
                if (i.IntegrationId == integrationId)
                {
                    // TODO: allow changing the externalid.
                    // right now it will probably break things :)
                    // if (externalId != null) i.ExternalId = externalId; 
                    return list;
                }
            }

            list.Add(mapping);
            return list;
        }
    }

    public static class MessageBrokerExtensions
    {
        public static async Task PublishEventAsync(
            this IMessageBroker messageBroker,
            LeadWithAppointmentEvent evt,
            Guid nextEventId,
            Guid integrationId,
            string externalId,
            string status,
            bool failed = false
            )
        {
            var evnt = new LeadWithAppointmentEvent
            {
                RunId = evt.RunId,
                Description = status,
                Lead = new LeadInfo
                {
                    IntegrationMapping = evt.Lead.IntegrationMapping.Add(integrationId, externalId),
                    Lead = evt.Lead.Lead,
                    SchedulerUrl = evt.Lead.SchedulerUrl,
                },
                Appointment = evt.Appointment,
                Context = new Context
                {
                    EntityId = evt.Context.EntityId,
                    ExternalIdentities = evt.Context.ExternalIdentities,
                    IntegrationId = integrationId,
                }
            };

            await messageBroker.PublishAsync(
                EventIds.GetRoute(nextEventId, failed),
                evnt
            );
        }

        public static async Task PublishAppointmentEventAsync(
            this IMessageBroker messageBroker,
            LeadWithAppointmentEvent evt,
            Guid nextEventId,
            Guid integrationId,
            string externalId,
            string status,
            bool failed = false
            )
        {
            var mapping = new IntegrationMapping
            {
                IntegrationId = integrationId,
                ExternalId = externalId,
            };

            var evnt = new LeadWithAppointmentEvent
            {
                RunId = evt.RunId,
                Description = status,
                Lead = evt.Lead,
                Appointment = new AppointmentInfo
                {
                    Appointment = evt.Appointment.Appointment,
                    IntegrationMapping = evt.Appointment.IntegrationMapping.Add(integrationId, externalId)
                },
                Context = new Context
                {
                    EntityId = evt.Context.EntityId,
                    ExternalIdentities = evt.Context.ExternalIdentities,
                    IntegrationId = integrationId,
                }
            };

            await messageBroker.PublishAsync(
                EventIds.GetRoute(nextEventId, failed),
                evnt
            );
        }
    }
}