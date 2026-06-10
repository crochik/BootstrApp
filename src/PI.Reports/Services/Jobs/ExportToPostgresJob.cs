using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using Npgsql;
using NpgsqlTypes;
using PI.ProductCatalog.Postgres;
using PI.Shared.Models;
using PI.Shared.Models.Dashboards;
using PI.Shared.Services;

namespace Reports.Services.Jobs;

public class ExportToPostgresJob : IRunJob
{
    private readonly ILogger<ExportToPostgresJob> _logger;
    private readonly MongoConnection _connection;
    private readonly PostgresConnection _postgresConnection;
    public string Name => "ExportToPostgres";

    public ExportToPostgresJob(ILogger<ExportToPostgresJob> logger, MongoConnection connection, PostgresConnection postgresConnection)
    {
        _logger = logger;
        _connection = connection;
        _postgresConnection = postgresConnection;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var query = _connection.Filter<DataSource, PostgresDataSource>()
            .Eq(x => x.AccountId, context.AccountId)
            .Ne(x => x.LoadSource, null)
            .Eq(x => x.IsActive, true)
            .Ne(x => x.AutoRefreshInterval, null)
            // .OrBuilder(
            //     q => q.Eq(x => x.NextRefreshOn, null),
            //     q => q.Lt(x => x.NextRefreshOn, DateTime.UtcNow)
            // )
            .SortAsc(x => x.LoadSource.Order);
     
        var tag = Environment.GetEnvironmentVariable("PI_DATASOURCE_TAG");
        if (string.IsNullOrWhiteSpace(tag))
        {
            query.Eq(x => x.Tags, null);
        }
        else
        {
            query.AnyEq(x => x.Tags, tag);
        }
        
        var sources = await query
            .FindAsync();

        // https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service?pivots=dotnet-6-0
        // TODO: create queue to run tasks in parallel?
        // ..
        foreach (var ds in sources)
        {
            using var scope = _logger.AddScope(new
            {
                DataSource = ds.Name,
                ds.TableName,
                DataSourceId = ds.Id,
                Tag = tag,
            });

            _logger.LogInformation("Start Refresh");

            var start = DateTime.UtcNow;
            try
            {
                if (ds.BeforeLoad == BeforeLoad.Drop)
                {
                    _logger.LogInformation("Drop table before Load");
                    await _postgresConnection.DataSource.CreateCommand($"DROP TABLE IF EXISTS \"{ds.TableName}\"").ExecuteNonQueryAsync(stoppingToken);
                }

                if (!ds.LastRefreshedOn.HasValue || ds.BeforeLoad == BeforeLoad.Drop)
                {
                    _logger.LogInformation("Create table before Load");
                    await CreateTableAsync(ds, stoppingToken);
                }

                if (ds.BeforeLoad == BeforeLoad.Truncate)
                {
                    _logger.LogInformation("Truncate table before Load");
                    await _postgresConnection.DataSource.CreateCommand($"TRUNCATE TABLE \"{ds.TableName}\"").ExecuteNonQueryAsync(stoppingToken);
                }

                var defaultParameters = GetDefaultParameters(ds, start);

                _logger.LogInformation("Start Copy");
                await CopyAsync(ds, stoppingToken, defaultParameters);
                _logger.LogInformation("Finished Copy in {ms}", (DateTime.UtcNow - start).TotalMilliseconds);

                if (ds.AfterLoadStoredProcedures != null)
                {
                    _logger.LogInformation("Run AfterLoadStoredProcedures");

                    foreach (var sp in ds.AfterLoadStoredProcedures)
                    {
                        _logger.LogInformation("Execute {StoredProcedure}", ds.Name);
                        var result = await _postgresConnection.ExecuteAsync(sp, defaultParameters);
                        _logger.LogInformation("{StoredProcedure} changed {Rows}", ds.Name, result);
                    }
                }

                _logger.LogInformation("Finished in {ms}", (DateTime.UtcNow - start).TotalMilliseconds);

                var update = _connection.Filter<DataSource, PostgresDataSource>()
                    .Eq(x => x.AccountId, context.AccountId)
                    .Eq(x => x.Id, ds.Id)
                    .Update
                    .Set(x => x.LastRefreshedOn, start)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor());

                if (ds.AutoRefreshInterval.HasValue)
                {
                    update.Set(x => x.NextRefreshOn, start.Add(ds.AutoRefreshInterval.Value));
                }

                var updated = await update.UpdateAndGetOneAsync();

                // TODO: fire event?
                // ...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh");

                // TODO: update ?
                // ...

                // TODO: fire event
                // ...
            }
        }

        return new JobResult
        {
            Message = "Done",
        };
    }

    private static Dictionary<string, object> GetDefaultParameters(PostgresDataSource ds, DateTime start)
    {
        var now = DateTime.UtcNow;
        var defaultParameters = new Dictionary<string, object>
        {
            { "FirstRefresh", !ds.LastRefreshedOn.HasValue },
            { "LastRefresh", BsonDateTime.Create(ds.LastRefreshedOn ?? DateTime.MinValue) },
            { "Start", BsonDateTime.Create(start) },
            { "OneHourAgo", BsonDateTime.Create(now.AddHours(-1)) },
            { "OneDayAgo", BsonDateTime.Create(now.AddDays(-1)) },
            { "30DaysAgo", BsonDateTime.Create(now.AddDays(30)) },
            { "365DaysAgo", BsonDateTime.Create(now.AddDays(365)) },
        };
        return defaultParameters;
    }

    private async Task CopyAsync(PostgresDataSource dataSource, CancellationToken stoppingToken, IDictionary<string, object> defaultParameters)
    {
        var start = DateTime.UtcNow;

        if (dataSource.LoadSource is not MongoDbLoadSource loadSource)
        {
            throw new Exception("Don't know how to refresh data");
        }

        if (loadSource.PrepareStoredProcedures != null)
        {
            _logger.LogInformation("Running Prepare Stored Procedures");

            foreach (var sp in loadSource.PrepareStoredProcedures)
            {
                _logger.LogInformation("Execute {StoredProcedure}", sp.Name);
                await sp.ExecuteAsync(_connection, defaultParameters);
            }
        }

        await using var connection = await _postgresConnection.DataSource.OpenConnectionAsync(stoppingToken);

        var createSql = BuildCreateTableSql(dataSource, true);
        var createTempTableCmd = new NpgsqlCommand(createSql);
        createTempTableCmd.Connection = connection;
        await createTempTableCmd.ExecuteNonQueryAsync(stoppingToken);

        var cols = dataSource.Columns.ToArray();
        var types = cols.Select(x => Enum.Parse<NpgsqlDbType>(x.Value.Type)).ToArray();

        var upsertSql = $"MERGE INTO \"{dataSource.TableName}\" d ";
        upsertSql += $"USING \"temp_{dataSource.TableName}\" s ";
        upsertSql += "ON s._id = d._id ";
        upsertSql += "WHEN MATCHED THEN UPDATE SET ";
        upsertSql += string.Join(", ", cols.Select(x => $"\"{x.Key}\" = s.\"{x.Key}\"")) + " ";
        upsertSql += "WHEN NOT MATCHED THEN INSERT (" + string.Join(", ", cols.Select(x => $"\"{x.Key}\"")) + ") VALUES (";
        upsertSql += string.Join(", ", cols.Select(x => $"s.\"{x.Key}\""));
        upsertSql += ")";
        var upsertCmd = new NpgsqlCommand(upsertSql);
        upsertCmd.Connection = connection;

        var truncateCmd = new NpgsqlCommand($"TRUNCATE TABLE \"temp_{dataSource.TableName}\"");
        truncateCmd.Connection = connection;

        var sql = $"COPY \"temp_{dataSource.TableName}\" (";
        sql += string.Join(", ", cols.Select(x => $"\"{x.Key}\""));
        sql += ") FROM STDIN (FORMAT BINARY)";


        if (loadSource.StoredProcedure is not AggregateStoredProcedure storedProcedure) throw new Exception("Invalid Stored Procedure");
        if (storedProcedure.Operation.HasValue && storedProcedure.Operation != AggregationOperation.Find) throw new Exception("Invalid Stored Procedure Operation");

        var cursor = storedProcedure.GetCursor<ExpandoObject>(_connection, defaultParameters, 10_000);

        var count = 0;
        var lastId = default(string);
        var lastFieldName = default(string);
        while (await cursor.MoveNextAsync(stoppingToken))
        {
            await using (var writer = await connection.BeginBinaryImportAsync(sql, stoppingToken))
            {
                foreach (var row in cursor.Current)
                {
                    count++;

                    var dict = (IDictionary<string, object>)row;
                    lastId = dict[Model.IdFieldName].ToString();

                    try
                    {
                        await writer.StartRowAsync(stoppingToken);

                        for (var c = 0; c < cols.Length; c++)
                        {
                            lastFieldName = cols[c].Key;
                            if (!dict.TryGetValue(cols[c].Key, out var value) || value == null)
                            {
                                if (cols[c].Value.NotNull)
                                {
                                    switch (types[c])
                                    {
                                        case NpgsqlDbType.Varchar:
                                        case NpgsqlDbType.Text:
                                            await writer.WriteAsync(string.Empty, types[c], stoppingToken);
                                            continue;
                                        case NpgsqlDbType.Array:
                                            await writer.WriteAsync(string.Empty, NpgsqlDbType.Array | NpgsqlDbType.Text, stoppingToken);
                                            continue;
                                    }

                                    _logger.LogError("Required {Field} missing for {Id}", lastFieldName, lastId);
                                }

                                await writer.WriteNullAsync(stoppingToken);
                                continue;
                            }

                            value = value switch
                            {
                                ObjectId oid => oid.ToGuid(),
                                Decimal128 dec => (decimal)dec,
                                _ => value,
                            };

                            value = types[c] switch
                            {
                                NpgsqlDbType.Numeric => value switch
                                {
                                    string str => decimal.TryParse(str, out var dec) ? dec : 0,
                                    _ => value,
                                },
                                NpgsqlDbType.Varchar => value?.ToString(),
                                NpgsqlDbType.Text => value?.ToString(),
                                // NpgsqlDbType.Array => // ??? 
                                _ => value,
                            };

                            // intercept for some types
                            switch (types[c])
                            {
                                case NpgsqlDbType.Array:
                                    await writer.WriteAsync(value, NpgsqlDbType.Array | NpgsqlDbType.Text, stoppingToken);
                                    continue;
                            }

                            if (cols[c].Value.Size.HasValue && value is string strValue && strValue.Length > cols[c].Value.Size.Value)
                            {
                                // for now only for strings
                                value = strValue[..cols[c].Value.Size.Value];
                            }

                            await writer.WriteAsync(value, types[c], stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to export {Id} {Field}", lastId, lastFieldName);
                        throw new Exception($"Failed to export {lastId} {lastFieldName}", ex);
                    }
                }

                var copyResult = await writer.CompleteAsync(stoppingToken);

                _logger.LogInformation("Copied {Count} {Total} {LastId} {ElapsedMs}", copyResult, count, lastId, (DateTime.UtcNow - start).TotalMilliseconds);
                start = DateTime.UtcNow;
            }

            await upsertCmd.ExecuteNonQueryAsync(stoppingToken);
            await truncateCmd.ExecuteNonQueryAsync(stoppingToken);
            _logger.LogInformation("Finished merging in {Ms}", (DateTime.UtcNow - start).TotalMilliseconds);
        }

        upsertCmd = new NpgsqlCommand($"DROP TABLE \"temp_{dataSource.TableName}\"");
        upsertCmd.Connection = connection;
        await upsertCmd.ExecuteNonQueryAsync(stoppingToken);

        _logger.LogInformation("Took {ms}", (DateTime.UtcNow - start).TotalMilliseconds);
    }

    private async Task CreateTableAsync(PostgresDataSource dataSource, CancellationToken stoppingToken)
    {
        var sql = BuildCreateTableSql(dataSource);
        _logger.LogInformation("Create {TableName}: {SQL}", dataSource.TableName, sql);
        await using var cmd = _postgresConnection.DataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(stoppingToken);
    }

    private string BuildCreateTableSql(PostgresDataSource dataSource, bool temp = false)
    {
        var tableName = temp ? $"temp_{dataSource.TableName}" : dataSource.TableName;

        var sql = "CREATE ";
        if (temp) sql += "TEMP ";
        sql += $"TABLE IF NOT EXISTS \"{tableName}\" (";
        sql += string.Join(", ", getFields().Where(x => !string.IsNullOrEmpty(x)));
        sql += ")";

        return sql;

        IEnumerable<string> getFields()
        {
            foreach (var kvp in dataSource.Columns)
            {
                yield return $"\"{kvp.Key}\" {kvp.Value.Resolved}";
            }

            // if (!temp) yield return $"CONSTRAINT \"{tableName}_PK\" PRIMARY KEY (\"_id\")";
        }
    }
}