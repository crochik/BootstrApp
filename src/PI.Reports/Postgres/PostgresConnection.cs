using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using Npgsql;
using NpgsqlTypes;

namespace PI.ProductCatalog.Postgres;

public class PostgresConnection : IAsyncDisposable
{
    public PostgresConfiguration Configuration { get; }
    public NpgsqlDataSource DataSource { get; }

    public PostgresConnection(IConfiguration configuration)
    {
        Configuration = configuration.GetSection(nameof(PostgresConnection)).Get<PostgresConfiguration>();
        DataSource = NpgsqlDataSource.Create(Configuration.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (DataSource != null) await DataSource.DisposeAsync();
    }

    public async Task<int> ExecuteAsync(PostgresStoredProcedure sp, Dictionary<string, object> parameters = null)
    {
        await using var cmd = CreateCommand(sp, parameters);
        return await cmd.ExecuteNonQueryAsync();
    }
    
    public NpgsqlCommand CreateCommand(PostgresStoredProcedure sp, Dictionary<string, object> parameters)
    {
        var cmd = DataSource.CreateCommand(sp.Body);
        if (sp.Parameters != null && parameters != null)
        {
            foreach (var p in sp.Parameters)
            {
                if (!parameters.TryGetValue(p.Name, out var value))
                {
                    value = p.DefaultValue;
                }

                cmd.Parameters.AddWithValue(
                    p.Name,
                    p.Type,
                    value
                );
            }
        }

        return cmd;
    }

    public NpgsqlCommand CreateCommand(string sql, Dictionary<string, object> parameters)
    {
        var cmd = DataSource.CreateCommand(sql);
        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                cmd.Parameters.AddWithValue(p.Key, p.Value);
            }
        }

        return cmd;
    }
    
    public class PostgresConfiguration
    {
        public const string HostParameter = "Host";
        public const string UsernameParameter = "Username";
        public const string PasswordParameter = "Password";
        public const string DatabaseParameter = "Database";

        public string ConnectionString { get; set; }

        public Dictionary<string, string> GetParameters()
        {
            if (ConnectionString == null) return null;

            var dict = new Dictionary<string, string>();
            foreach (var arg in ConnectionString.Split(";", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = arg.Split("=", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                dict.Add(parts[0], parts[1]);
            }

            return dict;
        }
    }
}