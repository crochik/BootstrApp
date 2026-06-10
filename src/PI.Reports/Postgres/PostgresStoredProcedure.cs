using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NpgsqlTypes;

namespace PI.ProductCatalog.Postgres;

public class PostgresParameter
{
    public string Name { get; set; }
    public object DefaultValue { get; set; }
    public bool IsRequired { get; set; } = true;

    [BsonRepresentation(BsonType.String)]
    [JsonConverter(typeof(StringEnumConverter))]
    public NpgsqlDbType Type { get; set; } = NpgsqlDbType.Text;
}

public class PostgresStoredProcedure 
{
    [BsonId]
    public string Id { get; set; }
    
    public string DatabaseName { get; set; }
    public string Collection { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public string Description { get; set; }
    public PostgresParameter[] Parameters { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedOn { get; set; }
    public int Version { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public Guid? AccountId { get; set; }
    public string Body { get; set; }
}