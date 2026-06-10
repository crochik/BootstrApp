using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using PI.Shared.Controllers;

namespace Controllers;

[Authorize("admin")]
[Produces("application/json")]
[Route("/stripe/v1/[controller]")]
public class SyncController : APIController
{
    private readonly MongoConnection _connection;

    public SyncController(MongoConnection connection)
    {
        _connection = connection;
    }

    [HttpPost]
    [ProducesResponseType(typeof(StripeSync), 200)]
    public async Task<IActionResult> RegisterAsync(string apiKey, string endpointSecret)
    {
        var config = await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, Context.AccountId.Value)
            .FirstOrDefaultAsync();

        if (config == null)
        {
            config = new StripeSync
            {
                Id = Context.AccountId.Value,
                AccountId = Context.AccountId.Value,
                Name = "Sync",
                ApiKey = apiKey,
                EndpointSecret = endpointSecret,
                LastModifiedOn = DateTime.UtcNow
            };

            await _connection.InsertAsync(config);
        }
        else
        {
            var query = _connection.Filter<StripeSync>().Eq(x => x.Id, config.Id)
                .Update
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, Context.Actor);

            if (!string.IsNullOrWhiteSpace(apiKey)) query.Set(x => x.ApiKey, apiKey);
            if (!string.IsNullOrWhiteSpace(endpointSecret)) query.Set(x => x.EndpointSecret, endpointSecret);

            config = await query.UpdateAndGetOneAsync();
        }

        config.ApiKey = "<secret>";
        config.EndpointSecret = "<secret>";

        return Ok(config);
    }

    [HttpGet]
    [ProducesResponseType(typeof(StripeSync), 200)]
    public async Task<IActionResult> GetAsync()
    {
        var obj = await _connection.Filter<StripeSync>()
            .Eq(x => x.Id, Context.AccountId.Value)
            .FirstOrDefaultAsync();

        if (obj == null) return NotFound();

        obj.ApiKey = "<secret>";
        obj.EndpointSecret = "<secret>";

        return Ok(obj);
    }
}