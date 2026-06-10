using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Crochik.Data;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class LeadAdapter : ILeadAdapter
    {
        private readonly MongoConnection _connection;

        public LeadAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        // public async Task<Lead> CreateAsync(IEntityContext context, Lead lead, params IIntegrationLead[] iLeads)
        // {
        //     var entityIds = context.GetEntityIds().ToArray();
        //     if (entityIds.Length < 1)
        //     {
        //         _logger.LogError("Invalid Context {role}", context.Role);
        //         return null;
        //     }
        //
        //     var dao = _connection.Map<Lead>(lead);
        //     dao.EntityIds = entityIds;
        //     dao.AccountId = context.AccountId.Value;
        //     dao.LastModifiedOn = DateTime.UtcNow;
        //     dao.LastActor = context.Actor();
        //
        //     // little "name" magic
        //     if (string.IsNullOrEmpty(dao.Name)) dao.Name = dao[Lead.PropertyName_Name];
        //     dao.AddIfMissing(Lead.PropertyName_FirstName, dao.GetFirstName());
        //     dao.AddIfMissing(Lead.PropertyName_LastName, dao.GetLastName());
        //
        //     if (iLeads?.Length > 0)
        //     {
        //         dao.Integrations = iLeads.Select(x =>
        //         {
        //             var iDao = _connection.Map<LeadIntegration>(x);
        //             iDao.LeadId = dao.Id;
        //             return iDao;
        //
        //         }).ToArray();
        //     }
        //
        //     await _connection.InsertAsync<Lead>(dao);
        //
        //     return dao;
        // }
        //
        // public async Task<bool> UpdatePropertiesAsync(IEntityContext context, Lead lead)
        // {
        //     var properties = new Dictionary<string, object>(lead.AllProperties());
        //     var query = _connection.Filter<Lead>()
        //         .Eq(x => x.Id, lead.Id)
        //         .Update
        //             .Set(x => x.Properties, properties)
        //             .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //             .Set(x => x.LastActor, context.Actor())
        //             ;
        //
        //     if (!string.IsNullOrEmpty(lead.Name))
        //     {
        //         query.Set(x => x.Name, lead.Name);
        //     }
        //     else if (!string.IsNullOrEmpty(lead[Lead.PropertyName_Name]))
        //     {
        //         query.Set(x => x.Name, lead[Lead.PropertyName_Name]);
        //     }
        //
        //     // if (!string.IsNullOrEmpty(json)) query.Push(x => x.RawBodies, json);
        //
        //     var result = await query.UpdateAndGetOneAsync();
        //
        //     return result != null;
        // }
        
        public async Task<Lead> GetByIdAsync(IEntityContext context, Guid id)
            => await Query(context)
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

        public async Task<(Lead, IIntegrationLead)> GetFirstByIntegrationAsync(IEntityContext context, Guid integrationId, string externalId)
            => (await GetByIntegrationsAsync(context, integrationId, externalId)).FirstOrDefault();

        public async Task<IEnumerable<(Lead, IIntegrationLead)>> GetByIntegrationsAsync(IEntityContext context, Guid integrationId, string externalId)
        {
            var leads = await _connection.Filter<Lead>()
                .Exists(x => x.ReplacedById, false)
                .ElemMatchBuilder(x => x.Integrations, f => f.Eq(i => i.ExternalId, externalId).Eq(i => i.IntegrationId, integrationId))
                .FindAsync();

            return leads?.Select(x =>
            {
                var iLead = x?.Integrations.FirstOrDefault(x => x.IntegrationId == integrationId && string.Equals(x.ExternalId, externalId));
                return iLead == null ? (null, null) : ((Lead)x, (IIntegrationLead)iLead);
            });
        }

        public async Task<IEnumerable<Lead>> GetAllAsync(IEntityContext context, IQueryParams parms)
            => await Query(context, parms).FindAsync();

        public async Task<IEnumerable<Lead>> GetByTypeAsync(IEntityContext context, Guid leadTypeId, IQueryParams parms)
            => await Query(context, parms)
                .Eq(x => x.LeadTypeId, leadTypeId)
                .FindAsync();

        public async Task<LeadSearchResults> SearchAsync(IEntityContext context, Search search)
        {
            var result = await Search(context, search).FindAsync();

            return new LeadSearchResults
            {
                Search = search,
                Results = result.Select(x => _connection.Map<LeadSearchResult>(x))
            };
        }

        private Query<Lead> Query(IEntityContext context, IQueryParams parms = null)
        {
            var query = _connection.Filter<Lead>()
                .Skip(parms?.Skip)
                .Limit(parms?.Top ?? 100)
                .SortDesc(f => f.CreatedOn);

            query.Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ReplacedById, null);

            switch (context.Role)
            {
                case EntityRoleId.Account:
                case EntityRoleId.Admin:
                    break;

                case EntityRoleId.Manager:
                case EntityRoleId.Organization:
                    query.Eq(x => x.EntityId, context.OrganizationId.Value);
                    break;

                case EntityRoleId.User:
                    query.Eq(x => x.AssignedEntityId, context.UserId.Value);
                    break;

                default:
                    throw new ForbiddenException(context);
            }

            return query;
        }

        private Query<Lead> Search(IEntityContext context, Search search)
        {
            var query = Query(context, search);

            foreach (var criteria in search.Criteria)
            {
                switch (criteria.FieldName)
                {
                    case nameof(Lead.AccountId):
                        if (criteria.Operator != Operator.Eq) throw new NotImplementedException("Operation not yet supported");
                        if (criteria.TryGetUidValue(out var accountId)) query.Eq(x => x.EntityId, accountId);
                        break;

                    case "OrganizationId":
                        if (criteria.Operator != Operator.Eq) throw new NotImplementedException("Operation not yet supported");
                        if (criteria.TryGetUidValue(out var orgId)) query.Eq(x => x.EntityId, orgId);
                        break;

                    case nameof(Lead.Name):
                        if (criteria.Operator != Operator.Eq) throw new NotImplementedException("Operation not yet supported");
                        if (criteria.Value is string strValue && !string.IsNullOrWhiteSpace(strValue))
                        {
                            query.Regex(x => x.Name, new BsonRegularExpression($"^{Regex.Escape(criteria.Value.ToString())}", "i"));
                        }
                        break;

                    default:
                        // ignore for now
                        break;
                }
            }

            // date range
            var dateRange = search.Criteria.Where(x => string.Equals(x.FieldName, nameof(Lead.CreatedOn))).ToArray();
            if (dateRange.Length > 0)
            {
                var first = dateRange[0].TryGetDate(out var date);
                if (dateRange.Length == 1 && first)
                {
                    switch (dateRange[0].Operator)
                    {
                        case Operator.Gte:
                            query.Gte(x => x.CreatedOn, date);
                            break;

                        case Operator.Lte:
                            query.Lte(x => x.CreatedOn, date);
                            break;
                    }
                }
                else if (dateRange.Length == 2 && first && dateRange[1].TryGetDate(out var date2))
                {
                    query.Between(x => x.CreatedOn, date, date2);
                }
            }

            return query;
        }

        // public async Task<Lead> UpdateAssignedEntityIdAsync(Guid id, Guid assignedEntityId)
        //     => await _connection.UpdatePropertyAsync<Lead, Guid?>(id, x => x.AssignedEntityId, assignedEntityId);
        //
        // public async Task<Lead> UpdateFlowIdAsync(Guid id, Guid flowId)
        //     => await _connection.UpdatePropertyAsync<Lead, Guid?>(id, x => x.FlowId, flowId);

        private async Task<LeadAggregation> AggregateAsync(IEntityContext context, DateTime startDate, DateTime endDate, BsonDocument groupBy)
        {
            // db.getCollection('Lead').aggregate(
            // [
            //     {$match: {'CreatedOn': {$gt: ISODate('2020-04-05 16:57:23.064Z')}, EntityIds: {$in: ['fc100000-0000-0000-0000-000000000000']}}},
            //     {$group: {
            //         _id: {Date: {$dateToString: {date: '$CreatedOn', format: '%m/%d/%Y', timezone: 'America/New_York'}}, LeadTypeId: '$LeadTypeId'}, 
            //         Count: {$sum: 1}
            //     }},
            //     {$lookup: {from: 'LeadType', localField: '_id.LeadTypeId', foreignField: '_id', as: 'LeadType'}},
            //     {$unwind: '$LeadType'},
            //     {$project: {_id: 0, Date: '$_id.Date', LeadTypeId: '$_id.LeadTypeId', Name: '$LeadType.Name', Count: 1, LeadType: 1 }}
            // ]
            // )            

            var entityId = context.Role switch
            {
                EntityRoleId.Account => context.AccountId,
                EntityRoleId.Admin => context.AccountId,
                EntityRoleId.Manager => context.OrganizationId,
                EntityRoleId.Organization => context.OrganizationId,
                EntityRoleId.User => context.UserId,
                _ => default
            };

            if (!entityId.HasValue)
            {
                return null;
            }

            var group = new BsonDocument {
                {
                    Model.IdFieldName, new BsonDocument {
                        {"Date", groupBy},
                        {"LeadTypeId", "$LeadTypeId"}
                    }
                },
                {
                    "Count", BsonDocument.Parse(@"{$sum: 1}")
                }
            };

            var data = await _connection.GetCollection<Lead>().Aggregate()
                .Match(
                    Builders<Lead>.Filter
                        .Gte(x => x.CreatedOn, startDate)
                        .Lte(x => x.CreatedOn, endDate)
                        .AnyEq(x => x.EntityIds, entityId.Value)
                        .Exists(x => x.ReplacedById, false)
                )
                .Group(group)
                .Lookup("LeadType", "_id.LeadTypeId", Model.IdFieldName, "LeadType")
                .Unwind("LeadType")
                .Project<LeadAggregation.Row>(BsonDocument.Parse(@"{_id: 0, CreatedOn: {$dateFromString: {dateString: '$_id.Date', timezone: 'America/New_York'}}, LeadTypeId: '$_id.LeadTypeId', Name: '$LeadType.Name', Count: {$toInt: '$Count'}, LeadType: 1 }"))
                .ToListAsync();

            return new LeadAggregation
            {
                Start = startDate,
                End = endDate,
                EntityId = context.EntityId.Value,
                Data = data
            };
        }

        public Task<LeadAggregation> AggregateAsync(IEntityContext context, DateTime startDate, DateTime endDate)
            => AggregateAsync(context, startDate, endDate, B.DateToString(
                "CreatedOn", "%m/%d/%Y", "America/New_York"
            ));

        public Task<LeadAggregation> AggregatePerHourAsync(IEntityContext context, DateTime startDate, DateTime endDate)
            => AggregateAsync(context, startDate, endDate, B.DateToString(
                "CreatedOn", "%m/%d/%Y %H:00", "America/New_York"
            ));

        // private async Task<(Lead, Lead)> SortByPriority(Lead l, Lead r)
        // {
        //     var appt1 = await _connection.Filter<Appointment>()
        //         .Eq(x => x.LeadId, l.Id)
        //         .SortAsc(x => x.CreatedOn)
        //         .FirstOrDefaultAsync();
        //
        //     var appt2 = await _connection.Filter<Appointment>()
        //         .Eq(x => x.LeadId, r.Id)
        //         .SortAsc(x => x.CreatedOn)
        //         .FirstOrDefaultAsync();
        //
        //     if (appt1 != null)
        //     {
        //         if (appt2 != null)
        //         {
        //             if (appt1.CreatedOn > appt2.CreatedOn) SwapLeads();
        //             _logger.LogWarning("Merging two leads with appts: {LeadId} {OtherLeadId}", l.Id, r.Id);
        //         }
        //     }
        //     else if (appt2 != null)
        //     {
        //         SwapLeads();
        //     }
        //     else if (l.CreatedOn > r.CreatedOn)
        //     {
        //         SwapLeads();
        //     }
        //
        //     return (l, r);
        //
        //     void SwapLeads()
        //     {
        //         var tmp = l;
        //         l = r;
        //         r = tmp;
        //     }
        // }

        // public async Task<Lead> MergeAsync(IEntityContext context, Lead left, Lead right)
        // {
        //     var l = left as Lead;
        //     var r = right as Lead;
        //     if (l == null || r == null) throw new NotSupportedException("Only support Lead");
        //
        //     (l, r) = await SortByPriority(l, r);
        //
        //     var query = _connection.Filter<Lead>()
        //         .Eq(x => x.Id, l.Id)
        //         .Update
        //             .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //             .Set(x => x.LastActor, context.Actor());
        //
        //     // copy properties
        //     var modified = new List<string>();
        //     foreach (var prop in r.AllProperties())
        //     {
        //         if (prop.Value == null) continue;
        //         if (l.Properties.TryGetValue(prop.Key, out var value) && prop.Value.Equals(value)) continue;
        //
        //         l.Properties[prop.Key] = prop.Value;
        //         modified.Add(prop.Key);
        //     }
        //
        //     if (modified.Count > 0)
        //     {
        //         query.SetProperties(l);
        //     }
        //     
        //     // copy integrations
        //     if (MergeIntegrations(l, r))
        //     {
        //         query.Set(x => x.Integrations, l.Integrations);
        //     }
        //
        //     l = await query.UpdateAndGetOneAsync();
        //     if (l == null)
        //     {
        //         throw new Exception($"Failed to update lead: {l.Id}");
        //     }
        //
        //     // mark as replaced
        //     var result = await _connection.Filter<Lead>()
        //         .Eq(x => x.Id, r.Id)
        //         .Update
        //             .Set(x => x.ReplacedById, l.Id)
        //             .Set(x => x.IsActive, false)
        //             .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //             .Set(x => x.LastActor, context.Actor())
        //         .UpdateOneAsync();
        //
        //     if (result.ModifiedCount != 1)
        //     {
        //         _logger.LogError($"Failed to mark {r.Id} as replaced by {l.Id}");
        //     }
        //
        //     // move appointments to other lead
        //     // shouldn't happen but...
        //     result = await _connection.Filter<Appointment>()
        //         .Eq(x => x.LeadId, r.Id)
        //         .Update
        //             .Set(x => x.LeadId, l.Id)
        //             .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //             .Set(x => x.LastActor, context.Actor())
        //         .UpdateManyAsync();
        //
        //     return l;
        // }
        //
        // private static bool MergeIntegrations(Lead into, Lead from)
        // {
        //     if (from.Integrations == null || from.Integrations.Length < 1)
        //     {
        //         // nothing to do
        //         return false;
        //     }
        //
        //     if (into.Integrations == null || into.Integrations.Length < 1)
        //     {
        //         // copy
        //         into.Integrations = from.Integrations;
        //         return true;
        //     }
        //
        //     // combine
        //     var integrations = new List<LeadIntegration>();
        //     integrations.AddRange(into.Integrations);
        //
        //     foreach (var i in from.Integrations)
        //     {
        //         var existing = into.Integrations?.FirstOrDefault(x => x.IntegrationId == i.IntegrationId && string.Equals(x.ExternalId, i.ExternalId));
        //         if (existing == null)
        //         {
        //             integrations.Add(i);
        //             continue;
        //         }
        //
        //         if (i.GetLastModified() > existing.LastModifiedOn)
        //         {
        //             existing.Data = i.Data ?? existing.Data;
        //             existing.Url = i.Url ?? existing.Url;
        //             existing.Status = i.Status ?? existing.Status;
        //         }
        //         else
        //         {
        //             if (existing.Data == null) existing.Data = i.Data;
        //             if (existing.Url == null) existing.Url = i.Url;
        //             if (existing.Status == null) existing.Status = i.Status;
        //         }
        //
        //         existing.LastModifiedOn = DateTime.UtcNow;
        //     }
        //
        //     into.Integrations = integrations.ToArray();
        //
        //     return true;
        // }

        // public async Task<bool> MoveToEntityAsync(IEntityContext context, Guid id)
        // {
        //     var result = await _connection.Filter<Lead>()
        //         .Eq(x => x.Id, id)
        //         .Update
        //             .Set(x => x.EntityId, context.EntityId)
        //             .Set(x => x.EntityIds, context.GetEntityIds())
        //             .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //             .Set(x => x.LastActor, context.Actor())
        //         .UpdateOneAsync();
        //
        //     return result.ModifiedCount == 1;
        // }
    }
}
