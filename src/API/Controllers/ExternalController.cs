using System.Security.Cryptography;
using System.Text;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace Controllers;

[Produces("text/csv", "application/json")]
[Route("/api/v1/[controller]")]
public class ExternalController : APIController
{
    private readonly MongoConnection _connection;

    public ExternalController(MongoConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Returns zip codes associated with each branch (for web site)
    /// used by the OnlineImage client Id
    /// </summary>
    [Authorize("partner")]
    [HttpGet("OrganizationTerritory")]
    public async Task<IEnumerable<object>> GetOrganizationTerritoriesAsync()
    {
        var list = await _connection.DipperAggregateAsync<object>("external.OrganizationTerritory", "fci");
        return list;
    }

    /// <summary>
    /// Generic end point to expose data 
    /// </summary>
    [Authorize("partner")]
    [HttpGet("{name}")]
    public async Task<IEnumerable<object>> ExportDataAsync([FromRoute] string name)
    {
        var accountId = Context.AccountId.Value;
        var clientId = (Context.Actor as PartnerActor).ClientId;

        // var list = await _connection.DipperAggregateAsync<object>(
        //     $"external.{clientId}.{name}",
        //     accountId.ToString("N")
        // );

        return await GetDataAsync(name, accountId, clientId);
    }

    [Authorize("admin")]
    [HttpGet("{clientId}/{name}")]
    public async Task<IEnumerable<object>> TestExportDataAsync([FromRoute] string clientId, [FromRoute] string name)
    {
        return await GetDataAsync(name, Context.AccountId.Value, clientId);
    }

    private async Task<IEnumerable<object>> GetDataAsync(string name, Guid accountId, string clientId)
    {
        var objContext = new Dictionary<string, object>
        {
            { "Query", Request.Query.ToDictionary(x => x.Key, object (x) => x.Value.FirstOrDefault()) },
        };
        
        var parameters = default(IDictionary<string, object>);
        var dipper = await _connection.DipperAsync<AggregateStoredProcedure>($"{accountId:N}.external.{clientId}.{name}");
        if (dipper.Parameters?.Length > 0)
        {
            foreach (var p in dipper.Parameters)
            {
                if (p.DefaultValue != null)
                {
                    // when default value is set, use it ALWAYS
                    if (p.DefaultValue is string strParam && strParam.Contains("{{"))
                    {
                        // value is an expression
                        if (!ExpressionEvaluatorService.TryResolve(Context, objContext, strParam, out var resolvedValue))
                        {
                            throw new BadRequestException("Can't resolve expression");
                        }

                        p.DefaultValue = resolvedValue;
                    }

                    parameters ??= new Dictionary<string, object>();
                    parameters.Add(p.Name, p.DefaultValue);
                    continue;
                }

                // try to find query parameter
                var values = Request.Query[p.Name];
                var value = values.Count == 1 ? values[0] : null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new BadRequestException($"Missing required {p.Name} parameter");
                }

                // special handling for ids so ObjectId works?
                // ...

                parameters ??= new Dictionary<string, object>();
                parameters.Add(p.Name, value);
            }
        }

        return await dipper.ExecuteAsync<object>(_connection, parameters);
    }

    [Authorize("admin")]
    [HttpGet("SharedSecret/Hash")]
    public ActionResult GetSha256(string secret)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(secret);
        var hash = sha.ComputeHash(bytes);
        return Ok(
            new
            {
                SHA256 = Convert.ToBase64String(hash)
            }
        );
    }
}