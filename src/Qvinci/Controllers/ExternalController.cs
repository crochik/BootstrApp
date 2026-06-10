using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;

namespace Controllers
{
    [Produces("application/json")]
    [Route("/qvinci/v1/[controller]")]
    public class ExternalController : APIController
    {
        private readonly MongoConnection _connection;

        public ExternalController(MongoConnection connection)
        {
            this._connection = connection;
        }

        [Authorize("partner")]
        [HttpGet("{name}")]
        public async Task<IEnumerable<object>> ExportDataAsync([FromRoute] string name)
        {
            var accountId = Context.AccountId.Value;
            var clientId = (Context.Actor as PartnerActor).ClientId;

            var list = await _connection.DipperAggregateAsync<object>(
                $"external.{clientId}.{name}",
                accountId.ToString("N")
            );

            return list;
        }

        // [AllowAnonymous]
        // [HttpGet("SharedSecret")]
        // public string GetSha256(string secret)
        // {
        //     return secret.ToSha256();
        // }
    }
}