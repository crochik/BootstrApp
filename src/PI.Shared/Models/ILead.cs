using System;
using System.Collections.Generic;
using System.Linq;

namespace PI.Shared.Models;

public interface IContact : IEntity
{
}

public interface IIntegrationLead
{
    Guid IntegrationId { get; }
    string ExternalId { get; }
    Guid LeadId { get; }
    string Tag { get; }
    string Status { get; }
    string Url { get; }
    object Data { get; }
    DateTime CreatedOn { get; }
    DateTime? LastModifiedOn { get; }
}

public static class LeadExtensions
{
    public static IIntegrationLead Find(this Lead lead, Guid integrationId, string externalId)
        => lead.GetIntegrations().FirstOrDefault(x => x.IntegrationId == integrationId && string.Equals(x.ExternalId, externalId));

    public static IEnumerable<KeyValuePair<string, object>> GetRefs(this Lead lead)
    {
        yield return new KeyValuePair<string, object>("EntityId", lead.EntityId.ToString());
        if (lead.AssignedEntityId.HasValue) yield return new KeyValuePair<string, object>("EntityId", lead.AssignedEntityId.Value.ToString());
    }

    public static IEnumerable<KeyValuePair<string, object>> GetMeta(this Lead lead)
    {
        yield return new KeyValuePair<string, object>("Lead", lead.Name);
        // ...
    }
}