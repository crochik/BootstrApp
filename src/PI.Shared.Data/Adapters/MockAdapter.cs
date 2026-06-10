using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public abstract class MockAdapter<T> : IModelAdapter<T>
        where T : IRow<Guid>
    {
        public virtual Task<T> CreateAsync(T entity)
        {
            return Task.FromResult(entity);
        }

        public virtual Task<bool> DeleteAsync(Guid id)
        {
            return Task.FromResult(false);
        }

        public virtual Task<IEnumerable<T>> GetTrunkAsync(IEntityContext context)
        {
            return Task.FromResult((IEnumerable<T>)Array.Empty<T>());
        }

        public virtual Task<T> GetByIdAsync(Guid value)
        {
            return Task.FromResult(default(T));
        }

        public virtual Task<bool> UpdateAsync(T obj)
        {
            return Task.FromResult(false);
        }
    }
}
