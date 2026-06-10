using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Crochik.Mongo
{
    public class UpdateQuery
    {
    }

    public class UpdateQuery<T> : UpdateQuery
    {
        public IMongoCollection<T> Collection { get; }
        public FilterDefinition<T> Filter { get; protected set; }
        public SortDefinition<T> SortDefinition { get; protected set; }
        public UpdateDefinition<T> UpdateDefinition { get; protected set; }
        
        public ArrayFilterDefinition[] ArrayFilters { get; set; }
        
        public IClientSessionHandle Session { get; }

        public UpdateQuery(Query<T> query)
        {
            Collection = query.Collection;
            Filter = query.Filter;
            Session = query.Session;
            SortDefinition = query.SortDefinition;
        }

        public UpdateQuery<T> Combine(UpdateDefinition<T> update)
        {
            UpdateDefinition = UpdateDefinition != null ? Builders<T>.Update.Combine(UpdateDefinition, update) : update;
            return this;
        }

        public BsonDocument GetFilterAsBsonDocument()
            => Filter != null ? Collection.Database.ToBsonDocument(Filter) : null;

        public BsonDocument GetUpdateAsBsonDocument()
            => UpdateDefinition != null ? Collection.Database.ToBsonDocument(UpdateDefinition) : null;

        public Task<UpdateResult> UpdateManyAsync()
            => Session != null ?
                Collection.UpdateManyAsync(Session, Filter, UpdateDefinition, new UpdateOptions{ArrayFilters = ArrayFilters}) :
                Collection.UpdateManyAsync(Filter, UpdateDefinition, new UpdateOptions{ArrayFilters = ArrayFilters});


        public Task<UpdateResult> UpdateOneAsync()
            => Session != null ?
                Collection.UpdateOneAsync(Session, Filter, UpdateDefinition, new UpdateOptions{ArrayFilters = ArrayFilters}) :
                Collection.UpdateOneAsync(Filter, UpdateDefinition, new UpdateOptions{ArrayFilters = ArrayFilters});

        public Task<UpdateResult> UpdateOneAsync(bool isUpsert)
            => Session != null ?
                Collection.UpdateOneAsync(Session, Filter, UpdateDefinition, new UpdateOptions { IsUpsert = isUpsert, ArrayFilters = ArrayFilters}) :
                Collection.UpdateOneAsync(Filter, UpdateDefinition, new UpdateOptions { IsUpsert = isUpsert, ArrayFilters = ArrayFilters});

        public async Task<T> UpdateAndGetOneAsync(bool isUpsert = false)
        {
            var result = Session != null ?
                await Collection.FindOneAndUpdateAsync(
                    Session,
                    Filter,
                    UpdateDefinition,
                    new FindOneAndUpdateOptions<T, T>
                    {
                        ReturnDocument = ReturnDocument.After,
                        IsUpsert = isUpsert,
                        Sort = SortDefinition,
                        ArrayFilters = ArrayFilters,
                    }) :
                await Collection.FindOneAndUpdateAsync(
                    Filter,
                    UpdateDefinition,
                    new FindOneAndUpdateOptions<T, T>
                    {
                        ReturnDocument = ReturnDocument.After,
                        IsUpsert = isUpsert,
                        Sort = SortDefinition,
                        ArrayFilters = ArrayFilters,
                    });

            return result;
        }

        public UpdateOneModel<T> UpdateOneModel(bool isUpsert = false)
            => new UpdateOneModel<T>(Filter, UpdateDefinition) { IsUpsert = isUpsert };
    }
}
