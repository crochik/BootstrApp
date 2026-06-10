using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Driver.Linq;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters;

public class IntegrationLeadAdapter : IIntegrationLeadAdapter
{
    private readonly MongoConnection _connection;

    public IntegrationLeadAdapter(MongoConnection connection)
    {
        this._connection = connection;
    }

    public async Task<IIntegrationLead> AddAsync(IEntityContext context, IIntegrationLead model)
    {
        // TODO: have to handle different integrations
        // ... 
        var dao = _connection.Map<LeadIntegration>(model);

        var result = await _connection.Filter<Lead>()
            .Eq(x => x.Id, model.LeadId)
            .Update
            .Push(x => x.Integrations, dao)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        var integration = result.Integrations
            .FirstOrDefault(x => string.Equals(x.ExternalId, model.ExternalId) && x.IntegrationId == model.IntegrationId);

        // update lead id as we don't serialize it
        if (integration != null) integration.LeadId = result.Id;

        return integration;
    }

    public async Task<(Lead, IIntegrationLead)> FindAsync(Guid integrationId, string externalId)
    {
        var lead = await _connection.Filter<Lead>()
            .ElemMatchBuilder(x => x.Integrations,
                f => f.Eq(i => i.ExternalId, externalId)
                    .Eq(i => i.IntegrationId, integrationId)
                // Builders<LeadIntegration>.Filter.Eq(i => i.ExternalId, externalId) &
                // Builders<LeadIntegration>.Filter.Eq(i => i.IntegrationId, integrationId)
            )
            .FirstOrDefaultAsync();

        var integration = lead?.Integrations
            .FirstOrDefault(x => string.Equals(x.ExternalId, externalId) && x.IntegrationId == integrationId);


        if (integration == null) return (null, null);

        // update leadid as we dont serialize it
        integration.LeadId = lead.Id;
        return (lead, integration);
    }

    public Task<IIntegrationLead> FindAsync(string serviceName, string externalId)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<IIntegrationLead>> GetAsync(Guid leadId)
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.Id, leadId).FirstOrDefaultAsync();

        if (lead?.Integrations == null || lead.Integrations.Length < 1)
        {
            return Array.Empty<IIntegrationLead>();
        }

        // update leadid as we dont serialize it
        foreach (var i in lead.Integrations) i.LeadId = lead.Id;

        return lead.Integrations;
    }

    public async Task<IIntegrationLead> PatchAsync(IEntityContext context, LeadIntegration src)
    {
        var model = _connection.Map<LeadIntegration>(src);

        var query = _connection.Filter<Lead>()
                .Eq(x => x.Id, model.LeadId)
                .ElemMatchBuilder(x => x.Integrations,
                    f => f.Eq(i => i.ExternalId, model.ExternalId).Eq(i => i.IntegrationId, model.IntegrationId)
                ).Update
                .Set(x => x.Integrations.FirstMatchingElement().LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
            ;

        var apply = false;

        if (model.SerializedData != null)
        {
            query.Set(x => x.Integrations.FirstMatchingElement().SerializedData, model.SerializedData);
            apply = true;
        }

        if (model.Url != null)
        {
            query.Set(x => x.Integrations.FirstMatchingElement().Url, model.Url);
            apply = true;
        }

        if (model.Status != null)
        {
            query.Set(x => x.Integrations.FirstMatchingElement().Status, model.Status);
            apply = true;
        }
        // if (model.Tag != null) query.Set(x => x.Integrations[UpdateQuery.FirstMatch].Tag, model.Tag);

        var lead = apply ?
            await query.UpdateAndGetOneAsync() :
            await _connection.Filter<Lead>().Eq(x => x.Id, model.LeadId).FirstOrDefaultAsync();

        return lead?.Integrations.FirstOrDefault(x => string.Equals(x.ExternalId, model.ExternalId) && x.IntegrationId == model.IntegrationId);
    }

    public Task<bool> UpdateAsync(IIntegrationLead iLead)
    {
        throw new NotImplementedException();
    }

    public async Task<IIntegrationLead> UpsertAsync(IEntityContext context, IIntegrationLead model)
    {
        var existing = await _connection.Filter<Lead>()
            .Eq(x => x.Id, model.LeadId)
            .ElemMatchBuilder(x => x.Integrations,
                f => f.Eq(i => i.ExternalId, model.ExternalId).Eq(i => i.IntegrationId, model.IntegrationId)
            ).FirstOrDefaultAsync();

        if (existing == null) return await AddAsync(context, model);

        // update
        var dao = _connection.Map<LeadIntegration>(model);

        // TODO: UPDATE
        // ... 

        throw new NotImplementedException();
    }
}