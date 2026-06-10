using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Crochik.Dipper;

public enum AggregationOperation
{
    Find,
    Merge,
    Update,
    Delete,
    Macro
}

[BsonDiscriminator("Aggregate")]
public class AggregateStoredProcedure : StoredProcedure
{
    // made up STAGES
    public const string STAGE_EXECUTE = "$EXECUTE";
    public const string STAGE_DELETE = "$DELETE";

    public const string STAGE_UPDATE = "$UPDATE";

    // valid stages
    public const string STAGE_MERGE = "$merge";
    public const string STAGE_MATCH = "$match";
    public const string STAGE_LIMIT = "$limit";

    public string[] Pipeline { get; set; }

    public AggregationOperation? Operation { get; set; }

    public override string Body => Pipeline.ToJsonArrayString();

    public IEnumerable<BsonDocument> ToBsonPipeline(IDictionary<string, object> parameters) => Apply(Pipeline, parameters);

    public override async Task<object> ExecuteAsync(MongoConnection connection, IDictionary<string, object> parameters = null)
    {
        return await GetCursor<BsonDocument>(connection, parameters).AnyAsync();
    }

    public List<T> Execute<T>(MongoConnection connection, IDictionary<string, object> parameters = null, int? batchSize = null)
    {
        return GetCursor<T>(connection, parameters, batchSize).ToList();
    }

    public async Task<List<T>> ExecuteAsync<T>(MongoConnection connection, IDictionary<string, object> parameters = null, int? batchSize = null)
    {
        return await GetCursor<T>(connection, parameters, batchSize).ToListAsync();
    }

    public IAsyncCursor<T> GetCursor<T>(MongoConnection connection, IDictionary<string, object> parameters = null, int? batchSize = null)
    {
        connection.Logger.LogInformation("Execute: {StoredProcedure}", Id ?? Name);

        var pipeline = PipelineDefinition<BsonDocument, T>.Create(ToBsonPipeline(parameters));

        return connection.GetCollection<BsonDocument>(Collection)
            .Aggregate(pipeline, new AggregateOptions
            {
                BatchSize = batchSize,
            });
    }

    public override string ToString(IDictionary<string, object> parameters)
    {
        return string.Join('\n', GetLines());

        IEnumerable<string> GetLines()
        {
            yield return $"db.getCollection('{Collection}').aggregate(";
            yield return "[";
            foreach (var bson in ToBsonPipeline(parameters)) yield return $"\t{bson},";
            yield return "]";
            yield return ")";
        }
    }
}