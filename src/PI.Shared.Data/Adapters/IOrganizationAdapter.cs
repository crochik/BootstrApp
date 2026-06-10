using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IOrganizationAdapter : IEntityAdapter<Organization>
    {
        Task<Organization> CreateAsync(IEntityContext context, Organization org, EntityIdentity orgIdentity);
        Task<IEnumerable<Organization>> GetAsync(IEntityContext context);
        Task<IEnumerable<Organization>> GetByAccountAsync(Guid accountId, bool? isActive = true);
        
        Task<bool> UpdatePropertyAsync<TField>(IEntityContext context, Guid entityId, Expression<Func<Organization, TField>> field, TField value);
    }
}