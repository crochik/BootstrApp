using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using PI.ProductCatalog.Postgres;

namespace PI.Shared.Models.Dashboards;

public class SqlColumn
{
    public string Type { get; set; }
    public int? Size { get; set; }
    public bool NotNull { get; set; }
    public string Resolved { get; set; }
}

[BsonDiscriminator("postgres")]
public class PostgresDataSource : DataSource
{
    public string TableName { get; set; }
    public Dictionary<string, SqlColumn> Columns { get; set; }
    public PostgresStoredProcedure[] AfterLoadStoredProcedures { get; set; }
}