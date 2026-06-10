using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;

namespace Crochik.Mongo;

public class MongoConnection
{
    public interface IRegisterClassMap
    {
        void Register(MongoConnection connection);
    }

    public ILogger Logger { get; }

    public IMapper Mapper { get; set; }

    public string ConnectionString { get; }
    public string DatabaseName { get; }

    public ConcurrentDictionary<int, string> Requests = new();

    private MongoClient Client { get; set; }

    public IMongoDatabase Database => Client.GetDatabase(DatabaseName);

    public MongoConnection(
        ILogger<MongoConnection> logger,
        IConfiguration configuration,
        IRegisterClassMap register
    ) :
        this(
            logger,
            Configuration.Get(configuration),
            register
        )
    {
    }

    protected MongoConnection(
        ILogger<MongoConnection> logger,
        Configuration configuration,
        IRegisterClassMap register
    )
    {
        Logger = logger;
        ConnectionString = configuration.ConnectionString;
        DatabaseName = configuration.DatabaseName;

        if (string.IsNullOrEmpty(DatabaseName))
        {
            var uri = new Uri(ConnectionString);
            var parts = uri.LocalPath.Split("/");
            if (parts.Length != 2)
            {
                throw new Exception("Missing database name");
            }

            DatabaseName = parts[1];
        }

        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
        settings.ClusterConfigurator = configureCluster;

        Client = new MongoClient(settings);

        register.Register(this);

        void configureCluster(ClusterBuilder cb)
        {
            if (configuration.LogCommands)
            {
                logger.LogInformation("Log Mongo events");
                cb.Subscribe<CommandStartedEvent>(e =>
                {
                    switch (e.CommandName)
                    {
                        case "findAndModify":
                        case "find":
                        case "aggregate":
                        default:
                            // TODO: sanitize
                            Requests[e.RequestId] = e.Command.ToJson();
                            break;
                    }
                });

                cb.Subscribe<CommandFailedEvent>(e =>
                {
                    logger.LogError("{commandName} failed after {duration}", e.CommandName, e.Duration);
                });

                cb.Subscribe<CommandSucceededEvent>(e =>
                {
                    if (Requests.TryGetValue(e.RequestId, out var command))
                    {
                        logger.LogInformation("{requestId}: {commandName} {query} completed in {duration}", e.CommandName, e.RequestId, command, e.Duration);
                        Requests.Remove(e.RequestId, out var _);
                        return;
                    }

                    logger.LogInformation("{requestId}: {commandName} completed in {duration}", e.RequestId, e.CommandName, e.Duration);
                });
            }

            // if (configuration.LogAPM)
            // {
            //     logger.LogInformation("Add APM subscriber to MongoDb events");
            //     cb.Subscribe(new Elastic.Apm.MongoDb.MongoDbEventSubscriber());
            // }
        }
    }

    public TTo Map<TTo>(object from) => Mapper.Map<TTo>(from);

    public Query<T> Filter<T>() => new(Database);
    public Query<T> Filter<T>(string collectionName, string databaseName = null)
        => new(GetDatabase(databaseName), collectionName);

    private IMongoDatabase GetDatabase(string databaseName = null)
    {
        return string.IsNullOrWhiteSpace(databaseName) ? Database : Client.GetDatabase(databaseName);
    }

    public Query<T> Filter<T>(IClientSessionHandle session) => new(Database, session);
    public Query<TOfType> Filter<TBase, TOfType>() where TOfType : TBase => new Query<TBase, TOfType>(Database);
    public Query<TOfType> Filter<TBase, TOfType>(string collectionName) where TOfType : TBase => new Query<TBase, TOfType>(Database, collectionName);

    // create query for a type
    // var query = Activator
    //     .CreateInstance(typeof(Query<>)
    //     .MakeGenericType(native), _connection.Database);

    public IMongoCollection<T> GetCollection<T>(string name, string databaseName = null) => GetDatabase(databaseName).GetCollection<T>(name);
    public IMongoCollection<T> GetCollection<T>(Type type, string databaseName = null) => GetDatabase(databaseName).GetCollection<T>(Database.GetCollectionName(type));
    public IMongoCollection<T> GetCollection<T>() => Database.GetCollection<T>();
    public string GetCollectionName<T>() => Database.GetCollectionName<T>();

    public class PagedBulkWriteResult
    {
        public long DeletedCount { get; set; }
        public long InsertedCount { get; set; }
        public bool IsAcknowledged { get; set; }
        public bool IsModifiedCountAvailable { get; set; }
        public long MatchedCount { get; set; }
        public long ModifiedCount { get; set; }
        public long RequestCount { get; set; }
    }

    /// <summary>
    /// Write changes in batches of {pageSize} records
    /// probably can avoid the list but...
    /// </summary>
    public async Task<PagedBulkWriteResult> BulkWriteAsync<T>(IEnumerable<WriteModel<T>> batch, int pageSize)
    {
        var result = new PagedBulkWriteResult();

        int start = 0;
        var queue = new List<WriteModel<T>>();
        foreach (var item in batch)
        {
            queue.Add(item);
            if (queue.Count >= pageSize)
            {
                var bwr = await BulkWriteAsync(queue);
                Logger.LogDebug("Wrote {count} from {start}: {deleted}, {inserted}, {matched}", queue.Count, start, bwr.DeletedCount, bwr.InsertedCount, bwr.MatchedCount);
                start += queue.Count;
                queue.Clear();

                result.DeletedCount += bwr.DeletedCount;
                result.InsertedCount += bwr.InsertedCount;
                result.MatchedCount += bwr.MatchedCount;
                result.ModifiedCount += bwr.IsModifiedCountAvailable ? bwr.ModifiedCount : 0;
                result.RequestCount += bwr.RequestCount;
            }
        }

        if (queue.Count > 0)
        {
            var bwr = await BulkWriteAsync(queue);
            Logger.LogDebug("Wrote {count} from {start}: {deleted}, {inserted}, {matched}", queue.Count, start, bwr.DeletedCount, bwr.InsertedCount, bwr.MatchedCount);
            queue.Clear();

            result.DeletedCount += bwr.DeletedCount;
            result.InsertedCount += bwr.InsertedCount;
            result.MatchedCount += bwr.MatchedCount;
            result.ModifiedCount += bwr.IsModifiedCountAvailable ? bwr.ModifiedCount : 0;
            result.RequestCount += bwr.RequestCount;
        }

        return result;
    }

    public Task<BulkWriteResult<T>> BulkWriteAsync<T>(IEnumerable<WriteModel<T>> batch, string databaseName = null)
        => GetDatabase(databaseName).GetCollection<T>().BulkWriteAsync(batch);
    public Task<BulkWriteResult<T>> BulkWriteAsync<T>(string collectionName, IEnumerable<WriteModel<T>> batch, string databaseName = null)
        => GetDatabase(databaseName).GetCollection<T>(collectionName).BulkWriteAsync(batch);
    public Task<BulkWriteResult<T>> BulkWriteAsync<T>(IClientSessionHandle session, IEnumerable<WriteModel<T>> batch, string databaseName = null)
        => GetDatabase(databaseName).GetCollection<T>().BulkWriteAsync(session, batch);

    public IMongoCollection<T> GetCollection<TBase, T>() where T : TBase
        => Database.GetCollection<TBase, T>();

    public async Task<T> GetByIdAsync<T, K>(K id) where T : IRow<K>
    {
        var rows = await GetCollection<T>().FindAsync(row => row.Id.Equals(id));
        return await rows.FirstOrDefaultAsync();
    }

    public async Task<bool> DeleteAsync<T, K>(K id) where T : IRow<K>
    {
        var result = await GetCollection<T>().DeleteOneAsync(row => row.Id.Equals(id));
        return result.DeletedCount == 1;
    }

    public Task<bool> DeleteAsync<T, K>(T obj) where T : IRow<K>
        => DeleteAsync<T, K>(obj.Id);

    public Task<T> UpdateAsync<T, K>(T obj) where T : IRow<K>
        => GetCollection<T>().FindOneAndReplaceAsync(
            Builders<T>.Filter.Eq(row => row.Id, obj.Id),
            obj
        );

    public Task<T> UpdatePropertyAsync<T, TField>(Guid id, Expression<Func<T, TField>> field, TField value)
        where T : IRow<Guid>
        => UpdatePropertyAsync<T, Guid, TField>(id, field, value);

    public Task<T> UpdatePropertyAsync<T, K, TField>(K id, Expression<Func<T, TField>> field, TField value)
        where T : IRow<K>
        => FindOneAndUpdateAsync(id, Builders<T>.Update.Set(field, value));

    public Task<T> FindOneAndUpdateAsync<T, K>(K id, UpdateDefinition<T> update)
        where T : IRow<K>
    {
        return FindOneAndUpdateAsync<T, T, K>(id, update);
    }

    public async Task<T> FindOneAndUpdateAsync<TBase, T, K>(K id, UpdateDefinition<T> update)
        where T : IRow<K>, TBase
    {
        var result = await GetCollection<TBase, T>().FindOneAndUpdateAsync(
            Builders<T>.Filter.Eq(row => row.Id, id),
            update,
            new FindOneAndUpdateOptions<T, T>
            {
                ReturnDocument = ReturnDocument.After
            }
        );

        return result;
    }

    public async Task<T> ReplaceAsync<TBase, T, K>(K id, T row)
        where T : IRow<K>, TBase
    {
        var result = await GetCollection<TBase, T>().FindOneAndReplaceAsync(
            Builders<T>.Filter.Eq(row => row.Id, id),
            row,
            new FindOneAndReplaceOptions<T>
            {
                ReturnDocument = ReturnDocument.After
            }
        );

        return result;
    }

    public Task<T> ReplaceAsync<TBase, T, K>(T row) where T : IRow<K>, TBase
        => ReplaceAsync<T, T, K>(row.Id, row);

    public Task<T> ReplaceAsync<T, K>(T row) where T : IRow<K>
        => ReplaceAsync<T, T, K>(row);

    public Task<T> GetByIdAsync<T>(string id) where T : IRow<string>
        => GetByIdAsync<T, string>(id);

    public Task<T> GetByIdAsync<T>(Guid id) where T : IRow<Guid>
        => GetByIdAsync<T, Guid>(id);

    public async Task<IEnumerable<T>> GetAsync<T>(FilterDefinition<T> filter)
    {
        var rows = await GetCollection<T>().FindAsync(filter);
        return rows.ToEnumerable();
    }

    public async Task<T> FirstAsync<T>(FilterDefinition<T> filter)
    {
        var rows = await GetCollection<T>().FindAsync(filter);
        return await rows.FirstOrDefaultAsync();
    }

    public Task InsertAsync<T>(IEnumerable<T> rows)
        => GetCollection<T>().InsertManyAsync(rows);

    public async Task<T> InsertAsync<T>(T row)
    {
        await GetCollection<T>().InsertOneAsync(row);
        return row;
    }

    public Task InsertAsync<T>(IClientSessionHandle session, T row)
        => GetCollection<T>().InsertOneAsync(session, row);

    public Task InsertAsync<T, TCollection>(T row)
        => GetCollection<TCollection>().InsertOneAsync(Map<TCollection>(row));

    public Task<ReplaceOneResult> InsertOrUpdateAsync<T, K>(T row) where T : IRow<K>
        => GetCollection<T>().ReplaceOneAsync(
            Builders<T>.Filter.Eq(f => f.Id, row.Id),
            row,
            new ReplaceOptions { IsUpsert = true }
        );

    // public Task<ReplaceOneResult> InsertOrUpdateAsync<T>(T row) where T : IRow<string>
    //     => InsertOrUpdateAsync<T, string>(row);

    public Task<ReplaceOneResult> InsertOrUpdateAsync<T>(T row) where T : IRow<Guid>
        => InsertOrUpdateAsync<T, Guid>(row);

    public Task InsertManyAsync<T>(params T[] rows) => GetCollection<T>().InsertManyAsync(rows);

    public Task InsertManyAsync<T>(IEnumerable<T> rows) => GetCollection<T>().InsertManyAsync(rows);

    public Task InsertManyAsync<T>(IClientSessionHandle session, params T[] rows) => GetCollection<T>().InsertManyAsync(session, rows);

    public Task InsertManyAsync<T>(IClientSessionHandle session, IEnumerable<T> rows) => GetCollection<T>().InsertManyAsync(session, rows);

    public BsonDocument GetBsonDocument<T>(FilterDefinition<T> filter)
        => filter.Render(
            Database.Settings.SerializerRegistry.GetSerializer<T>(),
            Database.Settings.SerializerRegistry
        );

    public async Task<IClientSessionHandle> StartSessionAsync() => await Client.StartSessionAsync();

    public async Task ExecuteTransactionAsync(Func<IClientSessionHandle, Task> operations)
    {
        using (var session = await StartSessionAsync())
        {
            session.StartTransaction();

            try
            {
                await operations(session);
            }
            catch (Exception)
            {
                await session.AbortTransactionAsync();
                throw;
            }

            await session.CommitTransactionAsync();
        }
    }

    public async Task<int> BulkWriteAsync<T>(IAsyncEnumerable<WriteModel<T>> updates, int batchSize = 100)
    {
        var count = 0;
        var list = new List<WriteModel<T>>();
        await foreach (var update in updates)
        {
            list.Add(update);
            if (list.Count >= batchSize)
            {
                var result = await GetCollection<T>().BulkWriteAsync(list, new BulkWriteOptions { IsOrdered = false });

                count += list.Count;
                list.Clear();
            }
        }

        if (list.Count > 0)
        {
            await GetCollection<T>().BulkWriteAsync(list, new BulkWriteOptions { IsOrdered = false });

            count += list.Count;
            list.Clear();
        }

        return count;
    }
}

public class Configuration
{
    public static Configuration Get(IConfiguration configuration) => configuration.GetSection("MongoConnection").Get<Configuration>();

    public string ConnectionString { get; set; }
    public string DatabaseName { get; set; }
    public bool LogCommands { get; set; }
    public bool LogAPM { get; set; }
}