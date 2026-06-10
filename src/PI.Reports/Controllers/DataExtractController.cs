using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Postgres;
using PI.Shared.Controllers;

namespace Reports.Controllers;

[Route("/reports/v1/Config")]
[Authorize("admin")]
public class DataExtractController : APIController
{
    private readonly PostgresConnection _connection;

    public DataExtractController(PostgresConnection connection)
    {
        _connection = connection;
    }

    [HttpPost("Query")]
    public async Task<IActionResult> QueryAsync([FromBody] QueryRequest query)
    {
        await using var cmd = _connection.CreateCommand(query.Query, query.Arguments);
        await using var reader = await cmd.ExecuteReaderAsync();

        var columns = await reader.GetColumnSchemaAsync();
        
        using var memStream = new MemoryStream();
        await using var writer = new StreamWriter(memStream);
        using (var csvWriter = new CsvWriter(writer, true))
        {
            // header?
            foreach (var t in columns)
            {
                csvWriter.WriteField(t.ColumnName);
            }
            
            await csvWriter.NextRecordAsync();

            while (await reader.ReadAsync())
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    var value = reader.GetValue(c);
                    csvWriter.WriteField(value);
                }
                await csvWriter.NextRecordAsync();
            }            
        }

        await writer.FlushAsync();
        var csv = Encoding.UTF8.GetString(memStream.GetBuffer(), 0, (int)memStream.Length);
        
        return Content(csv, "text/csv");

    }
}

public class QueryRequest
{
    public string Query { get; set; }
    public Dictionary<string, object> Arguments { get; set; }
}