using System.Linq;
using MongoDB.Bson;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class B
    {
        public static BsonDocument S(string name, BsonValue value) => new BsonDocument(name, value);

        public static BsonDocument DateToString(string field, string format, string timeZone)
            => new BsonDocument {
                {"$dateToString",
                    new BsonDocument {
                        {"date", $"${field}"},
                        {"format", format},
                        {"timezone", timeZone}
                    }
                }
            };

        public static BsonArray Pipeline(params string[] str)
            => new BsonArray(str.Select(x => BsonDocument.Parse(x)));
    }
}
