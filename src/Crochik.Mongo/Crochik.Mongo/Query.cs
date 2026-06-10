using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Crochik.Dipper;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Crochik.Mongo
{
    public class Query<T>
    {
        public IMongoCollection<T> Collection { get; }
        public FilterDefinition<T> Filter { get; protected set; }
        public int? BatchSize { get; protected set; }
        public SortDefinition<T> SortDefinition { get; protected set; } = null;
        public ProjectionDefinition<T> Projection { get; protected set; } = null;
        public int? SkipCount { get; protected set; }
        public int? LimitCount { get; protected set; }

        public UpdateQuery<T> Update
        {
            get
            {
                if (LimitCount.HasValue || SkipCount.HasValue)
                {
                    throw new NotImplementedException();
                }

                return new UpdateQuery<T>(this);
            }
        }

        public IClientSessionHandle Session { get; }

        /// <summary>
        /// Creates a Query<T> not using generic parameter
        /// </summary>
        public static object New(IMongoDatabase db, Type otherType)
        {
            var generic = typeof(Query<>);
            var type = generic.MakeGenericType(otherType);
            var constuctor = type.GetConstructor(new[] { typeof(IMongoDatabase) });
            return constuctor.Invoke(new[] { db });
        }

        public Query()
        {
        }

        public Query(IMongoCollection<T> collection)
        {
            Collection = collection;
        }

        public Query(IMongoDatabase db)
        {
            Collection = db.GetCollection<T>();
        }

        public Query(IMongoDatabase db, string collectionName)
        {
            Collection = db.GetCollection<T>(collectionName);
        }

        public Query(IMongoDatabase db, IClientSessionHandle session)
        {
            Collection = db.GetCollection<T>();
            Session = session;
        }

        public Query<T> WithBatchSize(int batchSize)
        {
            BatchSize = batchSize;
            return this;
        }

        public Query<T> Combine(FilterDefinition<T> filter)
        {
            Filter = Filter == null ? filter : filter & Filter;
            return this;
        }

        public Query<T> Combine(SortDefinition<T> sort)
        {
            SortDefinition = SortDefinition == null ? sort : Builders<T>.Sort.Combine(SortDefinition, sort);

            return this;
        }

        public Query<T> Skip(int? skip)
        {
            SkipCount = skip;
            return this;
        }

        public Query<T> Limit(int? limit)
        {
            LimitCount = limit;
            return this;
        }

        public Query<T> ExcludeField(Expression<Func<T, object>> field)
        {
            Projection = Projection == null ? Builders<T>.Projection.Exclude(field) : Projection.Exclude(field);

            return this;
        }

        public Query<T> ExcludeField(FieldDefinition<T> field)
        {
            Projection = Projection == null ? Builders<T>.Projection.Exclude(field) : Projection.Exclude(field);

            return this;
        }

        public Query<T> IncludeFields(params Expression<Func<T, object>>[] fields)
        {
            foreach (var field in fields)
            {
                Projection = Projection == null ? Builders<T>.Projection.Include(field) : Projection.Include(field);
            }

            return this;
        }

        public Query<T> IncludeField(Expression<Func<T, object>> field)
        {
            Projection = Projection == null ? Builders<T>.Projection.Include(field) : Projection.Include(field);

            return this;
        }

        public Query<T> IncludeField(FieldDefinition<T> field)
        {
            Projection = Projection == null ? Builders<T>.Projection.Include(field) : Projection.Include(field);

            return this;
        }

        public Query<T> IncludeField(string alias, FieldDefinition<T> field)
        {
            var sourceSerializer = Collection.DocumentSerializer;
            var serializerRegistry = Collection.Database.Settings.SerializerRegistry;
            var LinqProviderAdapterV3 = MongoDB.Driver.Linq.LinqProvider.V3;
            var renderedField = field.Render(sourceSerializer, serializerRegistry, LinqProviderAdapterV3);

            return IncludeField(alias, $"${renderedField.FieldName}");
        }

        public Query<T> IncludeField(string alias, string fieldPath)
        {
            ProjectionDefinition<T> projection = new BsonDocument(alias, fieldPath);

            Projection = Projection == null ? projection : Builders<T>.Projection.Combine(Projection, projection);

            return this;
        }

        public Query<T> SortAsc(string field)
            => Combine(Builders<T>.Sort.Ascending(field));

        public Query<T> SortDesc(string field)
            => Combine(Builders<T>.Sort.Descending(field));

        public Query<T> SortAsc(Expression<Func<T, object>> field)
            => Combine(Builders<T>.Sort.Ascending(field));

        public Query<T> SortDesc(Expression<Func<T, object>> field)
            => Combine(Builders<T>.Sort.Descending(field));

        public IFindFluent<T, T> FindFluent()
        {
            var find = Collection.Find(Filter ?? Builders<T>.Filter.Empty);

            if (SkipCount.HasValue && SkipCount > 0) find = find.Skip(SkipCount);
            if (LimitCount.HasValue && LimitCount > 0) find = find.Limit(LimitCount);
            if (SortDefinition != null) find = find.Sort(SortDefinition);
            if (Projection != null) find = find.Project<T>(Projection);

            if (BatchSize.HasValue) find.Options.BatchSize = BatchSize;

            return find;
        }

        public T FirstOrDefault() => FindFluent().FirstOrDefault();
        public async Task<T> FirstOrDefaultAsync() => await FindFluent().FirstOrDefaultAsync();
        public async Task<TProj> FirstOrDefaultAsync<TProj>() => await FindFluent().Project<TProj>(Projection).FirstOrDefaultAsync();
        public List<T> Find() => FindFluent().ToList();
        public async Task<List<T>> FindAsync() => await FindFluent().ToCursor().ToListAsync();

        public async Task<IAsyncCursor<TProj>> ToCursor<TProj>()
        {
            // var projection = Builders<T>.Projection.As<TProj>();
            var find = FindFluent().Project<TProj>(Projection);
            return await find.ToCursorAsync();
        }

        public async Task<List<TProj>> FindAsync<TProj>()
        {
            var cursor = await ToCursor<TProj>();
            return await cursor.ToListAsync();
        }

        public async Task<IAsyncCursor<T1>> DistinctAsync<T1>(string fieldName, TimeSpan? maxTime = null) => await Collection.DistinctAsync<T1>(
            fieldName, Filter,
            maxTime != null
                ? new DistinctOptions
                {
                    MaxTime = maxTime
                }
                : null
        );

        public async Task<long> CountDocumentsAsync() => await FindFluent().CountDocumentsAsync();

        // public IFindFluent<T, TProjection> Project<TProjection>(Expression<Func<T, TProjection>> expression)
        // {
        //     var find = Collection.Find(Filter ?? Builders<T>.Filter.Empty);
        //
        //     if (SkipCount.HasValue && SkipCount > 0) find = find.Skip(SkipCount);
        //     if (LimitCount.HasValue && LimitCount > 0) find = find.Limit(LimitCount);
        //     if (SortDefinition != null) find = find.Sort(SortDefinition);
        //     // if (Projection != null) find = find.Project<T>(Projection);
        //
        //     return find.Project<TProjection>(Builders<T>.Projection.Expression(expression));
        // }

        public Query<T> WithBatchSize(int? batchSize)
        {
            BatchSize = batchSize;
            return this;
        }

        public IAsyncCursor<T> ToCursor() => FindFluent().ToCursor();

        public async Task<int> DeleteAsync()
        {
            if (LimitCount.HasValue || SkipCount.HasValue)
            {
                throw new NotImplementedException();
            }

            var result = Session != null ? await Collection.DeleteManyAsync(Session, Filter) : await Collection.DeleteManyAsync(Filter);

            return (int)result.DeletedCount;
        }

        public async Task<bool> DeleteOneAsync()
        {
            if (LimitCount.HasValue || SkipCount.HasValue)
            {
                throw new NotImplementedException();
            }

            var result = Session != null ? await Collection.DeleteOneAsync(Session, Filter) : await Collection.DeleteOneAsync(Filter);

            return result.DeletedCount == 1;
        }

        public async Task<ReplaceOneResult> ReplaceOneAsync(T obj, bool isUpsert = false)
        {
            if (LimitCount.HasValue || SkipCount.HasValue)
            {
                throw new NotImplementedException();
            }

            return Session != null ? await Collection.ReplaceOneAsync(Session, Filter, obj, new ReplaceOptions { IsUpsert = isUpsert }) : await Collection.ReplaceOneAsync(Filter, obj, new ReplaceOptions { IsUpsert = isUpsert });
        }

        public async Task<T> ReplaceAndGetOneAsync(T obj, bool isUpsert = false)
        {
            if (LimitCount.HasValue || SkipCount.HasValue)
            {
                throw new NotImplementedException();
            }

            return Session != null
                ? await Collection.FindOneAndReplaceAsync(
                    Session,
                    Filter,
                    obj,
                    new FindOneAndReplaceOptions<T, T>
                    {
                        ReturnDocument = ReturnDocument.After,
                        IsUpsert = isUpsert,
                    })
                : await Collection.FindOneAndReplaceAsync(
                    Filter,
                    obj,
                    new FindOneAndReplaceOptions<T, T>
                    {
                        ReturnDocument = ReturnDocument.After,
                        IsUpsert = isUpsert,
                    });
        }

        private async Task<TSp> GetStoredProcedureAsync<TSp>(string id)
            where TSp : StoredProcedure
        {
            return await Collection.Database.Filter<StoredProcedure>()
                    .OfType<StoredProcedure, TSp>()
                    .Eq(x => x.Id, id)
                    .FirstOrDefaultAsync()
                as TSp;
        }

        public BsonDocument GetFilterAsBsonDocument()
            => Filter != null ? Collection.Database.ToBsonDocument(Filter) : null;

        public async Task<List<TOut>> DipperAsync<TOut>(string aggregationId, IDictionary<string, object> parameters = null)
        {
            var sp = await GetStoredProcedureAsync<AggregateStoredProcedure>(aggregationId);
            if (sp == null) throw new DipperException($"{aggregationId} not found");

            return await DipperAsync<TOut>(sp, parameters);
        }

        public async Task<List<TOut>> DipperAsync<TOut>(AggregateStoredProcedure sp, IDictionary<string, object> parameters = null)
        {
            var pipeline = BuildPipelineAsync<TOut>(sp, parameters);
            return Session != null ? await Collection.Aggregate(Session, pipeline).ToListAsync() : await Collection.Aggregate(pipeline).ToListAsync();
        }

        public async Task<bool> DipperMergeAsync(string aggregationId, IDictionary<string, object> parameters = null)
        {
            var sp = await GetStoredProcedureAsync<AggregateStoredProcedure>(aggregationId);
            if (sp == null) throw new DipperException($"{aggregationId} not found");
            var pipeline = BuildPipelineAsync<BsonDocument>(sp, parameters);

            if (Session != null)
            {
                return await Collection.Aggregate(Session, pipeline).AnyAsync();
            }
            else
            {
                return await Collection.Aggregate(pipeline).AnyAsync();
            }
        }

        public IEnumerable<BsonDocument> GetSelectPipelineStages()
        {
            if (Filter != null)
            {
                yield return new BsonDocument("$match", GetFilterAsBsonDocument());
            }

            if (SortDefinition != null)
            {
                yield return new BsonDocument("$sort", Collection.Database.ToBsonDocument(SortDefinition));
            }

            if (LimitCount.HasValue && LimitCount.Value > 0)
            {
                if (SkipCount.HasValue && SkipCount.Value > 0)
                {
                    yield return new BsonDocument("$skip", SkipCount.Value);
                }

                yield return new BsonDocument("$limit", LimitCount.Value);
            }
        }

        public IEnumerable<BsonDocument> GetProjectPipelineStages()
        {
            if (Projection != null)
            {
                yield return new BsonDocument("$project", Collection.Database.ToBsonDocument(Projection));
            }
        }

        /// <summary>
        /// Aggregate using additional stages
        /// - match, sort, limit and skip stages will be added before
        /// - project (if any) will be added after 
        /// </summary>
        public Task<List<TOut>> AggregateAsync<TOut>(IEnumerable<string> additionalStages)
            => AggregateAsync<TOut>(additionalStages.Select(BsonDocument.Parse));

        /// <summary>
        /// Aggregate using additional stages
        /// - match, sort, limit and skip stages will be added before
        /// - project (if any) will be added after 
        /// </summary>
        public async Task<List<TOut>> AggregateAsync<TOut>(IEnumerable<BsonDocument> additionalStages)
        {
            var pipeline = BuildPipelineAsync<TOut>(additionalStages);
            return Session != null ? await Collection.Aggregate(Session, pipeline).ToListAsync() : await Collection.Aggregate(pipeline).ToListAsync();
        }

        /// <summary>
        /// Build pipeline using current query
        /// - match, sort, limit and skip stages will be added before
        /// - project (if any) will be added after 
        /// </summary>
        public PipelineDefinition<T, TOut> BuildPipelineAsync<TOut>(IEnumerable<BsonDocument> additionalStages)
        {
            var stages = GetSelectPipelineStages()
                .Concat(additionalStages)
                .Concat(GetProjectPipelineStages());

            var pipeline = PipelineDefinition<T, TOut>.Create(stages);

            return pipeline;
        }

        private PipelineDefinition<T, TOut> BuildPipelineAsync<TOut>(AggregateStoredProcedure sp, IDictionary<string, object> parameters = null)
        {
            if (!string.Equals(sp.Collection, Collection.CollectionNamespace.CollectionName)) throw new DipperException("Collection Mismatch");
            if (sp.Parameters?.Length > 0 && parameters == null) throw new DipperException("Missing parameters");

            var stages = GetSelectPipelineStages()
                .Concat(sp.ToBsonPipeline(parameters))
                .Concat(GetProjectPipelineStages());

            var pipeline = PipelineDefinition<T, TOut>.Create(stages);

            return pipeline;
        }

        public InsertOneModel<T> InsertOneModel(T obj) => new(obj);
        public ReplaceOneModel<T> ReplaceOneModel(T obj, bool isUpsert = false) => new(Filter, obj) { IsUpsert = isUpsert };
    }

    public class Query<TBase, TOfType> : Query<TOfType> where TOfType : TBase
    {
        public Query(IMongoDatabase database)
            : base(database.GetCollection<TBase>().OfType<TOfType>())
        {
        }

        public Query(IMongoDatabase database, string collectionName)
            : base(database.GetCollection<TBase>(collectionName).OfType<TOfType>())
        {
        }
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<BsonDocument> AsEnumerable(this BsonDocument single)
        {
            yield return single;
        }
    }
}