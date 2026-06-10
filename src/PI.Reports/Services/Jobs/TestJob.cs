using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NpgsqlTypes;
using PI.ProductCatalog.Postgres;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Reports.Services.Jobs;

public class TestJob : IRunJob
{
    private readonly PostgresConnection _postgresConnection;
    public string Name => "Test";

    public TestJob(PostgresConnection postgresConnection)
    {
        _postgresConnection = postgresConnection;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var sp = new PostgresStoredProcedure
        {
            Body = @"
                UPDATE ""User"" 
                SET ""IsActive"" = B'0' 
                WHERE ""Name"" = @name;
                UPDATE ""User""
                SET ""IsActive"" = @IsActive
                WHERE ""Name"" = @name;",
            Parameters = new[]
            {
                new PostgresParameter
                {
                    Name = "name",
                    Type = NpgsqlDbType.Text,
                },
                new PostgresParameter
                {
                    Name = "IsActive",
                    Type = NpgsqlDbType.Bit,
                },
                new PostgresParameter
                {
                    Name = "Test",
                    Type = NpgsqlDbType.Bit,
                    DefaultValue = false
                }
                
            }
        };

        var result = await _postgresConnection.ExecuteAsync(sp, new Dictionary<string, object>
        {
            { "name", "Felipe Crochik" },
            { "IsActive", true }
        });

        return new JobResult
        {
            Message = $"Stored procedure \"{sp.Name}\" returned {result}",
        };
    }
}