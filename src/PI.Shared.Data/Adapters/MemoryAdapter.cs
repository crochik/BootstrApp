using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public abstract class MemoryAdapter<T> : MockAdapter<T>
        where T : IRow<Guid>
    {
        public Dictionary<Guid, T> All { get; }

        protected MemoryAdapter(IEnumerable<T> all)
        {
            All = all.ToDictionary(x => x.Id);
        }

        public override Task<IEnumerable<T>> GetTrunkAsync(IEntityContext context)
        {
            return Task.FromResult((IEnumerable<T>)All.Values);
        }

        public override Task<T> GetByIdAsync(Guid id)
        {
            var found = All.GetValueOrDefault(id);
            return Task.FromResult(found);
        }

    }
}
