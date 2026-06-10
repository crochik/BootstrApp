using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Crochik.Mongo;
using MongoDB.Bson;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Crochik.Dipper;

[BsonDiscriminator("Update")]
public class UpdateStoredProcedure : StoredProcedure
{
    public string Query { get; set; }
    public string Update { get; set; }
    public bool Multiple { get; set; }

    public override string Body
    {
        get
        {
            var parts = new[] {
                Query,
                Update,
                Multiple ? "\t{ \"multi\" : true }" : "\t{ \"multi\" : false }"
            };

            var pipeline = parts.Select(json => BsonDocument.Parse(json).ToString());
            return "[\n\t" + string.Join(",\n\t", pipeline) + "\n]";
        }
    }

    public override string ToString(IDictionary<string, object> parameters)
    {
        return string.Join('\n', GetLines(parameters));

        IEnumerable<string> GetLines(IDictionary<string, object> parameters)
        {
            var pipeline = ReplaceParameters(parameters);
            string query = pipeline.Query.ToString();
            string update = pipeline.Update.ToString();

            yield return $"db.getCollection('{Collection}').update(";
            yield return $"\t{query},";
            yield return $"\t{update},";
            yield return Multiple ? "\t{ \"multi\" : true }" : "\t{ \"multi\" : false }";
            yield return ")";
        }
    }

    private (BsonDocument Query, BsonDocument Update) ReplaceParameters(IDictionary<string, object> parameters)
    {
        var pipeline = Apply(new[] { Query, Update }, parameters);
        return (pipeline[0], pipeline[1]);
    }

    public override async Task<object> ExecuteAsync(MongoConnection connection, IDictionary<string, object> parameters)
    {
        connection.Logger.LogInformation("Execute: {storedProcedure}", Id);
            
        var pipeline = ReplaceParameters(parameters);
         
        if (!Multiple)
        {
            return await connection.GetCollection<BsonDocument>(Collection).UpdateOneAsync(pipeline.Query, pipeline.Update);
        }

        return await connection.GetCollection<BsonDocument>(Collection).UpdateManyAsync(pipeline.Query, pipeline.Update);
    }
}