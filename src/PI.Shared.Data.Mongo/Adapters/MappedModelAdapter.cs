using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public abstract class MappedModelAdapter<TInt, TObj> : MongoAdapter<TObj>, IModelAdapter<TInt>
        where TObj : class, TInt
        where TInt : IRow<Guid>
    {

        protected MongoAdapter<TObj, Guid> Adapter { get; }
        protected IMapper Mapper => Connection.Mapper;

        public MappedModelAdapter(
            MongoConnection connection
            ) : base(connection)
        {
            this.Adapter = new MongoAdapter<TObj, Guid>(connection);
        }

        public async Task<TInt> CreateAsync(TInt entity)
        {
            var dao = Mapper.Map<TObj>(entity);
            return await Adapter.CreateAsync(dao);
        }

        public Task<bool> DeleteAsync(Guid id)
            => Adapter.DeleteAsync(id);

        public abstract Task<IEnumerable<TInt>> GetTrunkAsync(IEntityContext context);

        public virtual async Task<TInt> GetByIdAsync(Guid id) => await Adapter.GetByIdAsync(id);

        public virtual Task<bool> UpdateAsync(TInt obj)
        {
            var dao = Mapper.Map<TObj>(obj);
            return Adapter.UpdateAsync(dao);
        }
    }
}