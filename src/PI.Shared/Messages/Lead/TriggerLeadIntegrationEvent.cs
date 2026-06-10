using System;
using System.Collections.Generic;
using PI.Shared.Models;

namespace Messages.Lead
{
    /// <summary>
    /// Trigger an event on the integration
    /// </summary>
    public class TriggerLeadIntegrationEvent
    {
        public Guid EntityId { get; set; }
        public Guid LeadId { get; set; }
        public Guid IntegrationId { get; set; }
        public string Event { get; set; }
        public string Notes { get; set; }
        public object Data { get; set; }

        public IEnumerable<TrunkIdentity> ExternalIdentities { get; set; }
        public IEnumerable<IntegrationMapping> IntegrationMapping { get; set; }

        public static string AddRoute(Guid integrationId)
        {
            return $"integration.{integrationId}.trigger";
        }
    }
}