using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Crochik.Extensions;

namespace PI.Shared.Data.Mongo.Adapters
{
    public static class BsonDocumentExtensions
    {
        public static IEnumerable<BsonDocument> Append(this BsonDocument bson, params string[] json)
            => bson.AsEnumerable().Concat(json.Select(x => BsonDocument.Parse(x)));

        public static PipelineDefinition<TInt, TOut> ToPipeline<TInt, TOut>(this IEnumerable<BsonDocument> docs)
            => PipelineDefinition<TInt, TOut>.Create(docs);
    }
}
