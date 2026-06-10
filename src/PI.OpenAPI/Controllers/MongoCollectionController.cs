using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using PI.Shared.Controllers;

namespace PI.OpenAPI.Controllers;

[Route("/openapi/v1/Mongo/Collection")]
public class MongoCollectionController(MongoConnection connection) : APIController
{
    [Authorize("admin")]
    [HttpGet("{name}/Indices")]
    public async Task<IActionResult> GetIndicesAsync([FromRoute] string name)
    {
        var indices = new List<object>();
        using (var cursor = await connection.Database.GetCollection<BsonDocument>(name).Indexes.ListAsync())
        {
            // 4. Iterate over the indexes
            while (await cursor.MoveNextAsync())
            {
                foreach (var indice in cursor.Current)
                {
                    indices.Add(indice);
                }
            }
        }

        return Ok(indices);
    }
}