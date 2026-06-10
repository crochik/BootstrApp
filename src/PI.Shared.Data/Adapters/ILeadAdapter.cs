using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Data;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface ILeadAdapter
    {
        /// <summary>
        /// Get Lead by id enforcing context has access  
        /// </summary>
        Task<Lead> GetByIdAsync(IEntityContext context, Guid id);
        
        Task<IEnumerable<(Lead, IIntegrationLead)>> GetByIntegrationsAsync(IEntityContext context, Guid integrationId, string externalId);
        Task<(Lead, IIntegrationLead)> GetFirstByIntegrationAsync(IEntityContext context, Guid integrationId, string externalId);

        Task<LeadSearchResults> SearchAsync(IEntityContext context, Search search);
        Task<IEnumerable<Lead>> GetByTypeAsync(IEntityContext context, Guid leadTypeId, IQueryParams parms);
        Task<LeadAggregation> AggregateAsync(IEntityContext context, DateTime startDate, DateTime endDate);
        Task<LeadAggregation> AggregatePerHourAsync(IEntityContext context, DateTime startDate, DateTime endDate);
    }
}