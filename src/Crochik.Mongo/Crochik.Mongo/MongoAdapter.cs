using System.Threading.Tasks;

namespace Crochik.Mongo
{
    public class MongoAdapter
    {
        protected MongoConnection Connection { get; }

        public MongoAdapter(MongoConnection connection)
        {
            Connection = connection;
        }
    }

    public class MongoAdapter<T> : MongoAdapter
    {
        public MongoAdapter(MongoConnection connection) : base(connection)
        {
        }

        public async Task<T> CreateAsync(T obj)
        {
            await Connection.InsertAsync(obj);
            return obj;
        }

        public Task<T> MapCreateAsync<TSrc>(TSrc obj)
            => CreateAsync(Connection.Map<T>(obj));
    }

    public class MongoAdapter<T, U> : MongoAdapter<T>
        where T : IRow<U>
    {
        public MongoAdapter(MongoConnection connection) : base(connection) { }

        public Task<T> GetByIdAsync(U id)
        {
            return Connection.GetByIdAsync<T, U>(id);
        }

        public Task<bool> DeleteAsync(T obj)
        {
            return Connection.DeleteAsync<T, U>(obj);
        }

        public Task<bool> DeleteAsync(U id)
        {
            return Connection.DeleteAsync<T, U>(id);
        }

        public async Task<bool> UpdateAsync(T obj)
        {
            var row = await Connection.UpdateAsync<T, U>(obj);
            return row != null;
        }

        public Task<bool> UpdateAsync<T2>(T2 obj)
        {
            var dao = Connection.Mapper.Map<T>(obj);
            return UpdateAsync(dao);
        }
    }
}