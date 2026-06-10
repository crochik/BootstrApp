using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Salesforce;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Route("/salesforce/v1/[controller]")]
public class ObjectTypeController : APIController
{
    private readonly SalesforceService _service;
    private readonly MongoConnection _connection;

    public ObjectTypeController(
        SalesforceService service,
        MongoConnection connection
    )
    {
        _service = service;
        _connection = connection;
    }

    [HttpPut("/salesforce/v1/[controller](salesforce.api.{sfObjectType})")]
    public async Task<ObjectType> GetObjectTypeAsync([FromRoute] string sfObjectType)
    {
        var result = await _service.DescribeAsync(Context, sfObjectType);
        if (result == null) throw NotFoundException.New($"{sfObjectType} not found");

        var objectType = ObjectTypeBuilder.Build(Context, result);

        var existing = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.FullName, objectType.FullName)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            // TODO: create a draft instead?
            // ...
            objectType.Id = existing.Id;

            throw new BadRequestException("There is already an object type");
        }

        var upsert = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.FullName, objectType.FullName)
            .Eq(x => x.Namespace, "salesforce.api")
            .ReplaceOneAsync(objectType, true);

        objectType = ObjectTypeBuilder.BuildWrapper(objectType);

        existing = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.FullName, objectType.FullName)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            // TODO: create a draft instead?
            // ...
            objectType.Id = existing.Id;
            
            throw new BadRequestException("There is already an object type");
        }

        upsert = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.FullName, objectType.FullName)
            .Eq(x => x.Namespace, "salesforce")
            .ReplaceOneAsync(objectType, true);

        return objectType;
    }

}