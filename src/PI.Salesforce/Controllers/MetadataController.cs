using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using NetCoreForce.Client.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Route("/salesforce/v1/[controller]")]
public class MetadataController : APIController
{
    private readonly SalesforceService _service;
    private readonly MongoConnection _connection;

    public MetadataController(
        SalesforceService service,
        MongoConnection connection
    )
    {
        _service = service;
        _connection = connection;
    }
    
    [HttpGet("/salesforce/v1/{sfObjectType}/[controller]/Describe")]
    [ProducesResponseType(typeof(SObjectDescribeFull), 200)]
    public async Task<IActionResult> GetAsync([FromRoute] string sfObjectType)
    {
        var result = await _service.DescribeAsync(Context, sfObjectType);
        return result != null ? Ok(result) : NotFound();
    }
    
    [HttpPut("/salesforce/v1/{sfObjectType}/[controller]/Picklist({fieldName})/Import")]
    public async Task<object> ImportPickListAsync(
        [FromRoute] string sfObjectType,
        [FromRoute] string fieldName)
    {
        var result = await _service.DescribeAsync(Context, sfObjectType);
        if (result == null) throw new NotFoundException($"{sfObjectType} not found in Salesforce");

        var field = result.Fields.Find(x => string.Equals(x.Name, fieldName));
        if (field == null) throw new NotFoundException($"{fieldName} not found in object {sfObjectType}");

        var objectType = await GetOrCreateAsync($"Sf{fieldName}");

        var dict = (await _connection.Filter<CustomObject>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.ObjectTypeId, objectType.Id)
                .FindAsync()
            ).ToDictionary(x => x.ExternalId);

        var batch = new List<WriteModel<CustomObject>>();
        foreach (var x in field.PicklistValues)
        {
            if (dict.TryGetValue(x.Value, out var existing))
            {
                // TODO: handle other properties?
                if (!string.Equals(existing.Name, x.Label))
                {
                    batch.Add(
                        _connection.Filter<CustomObject>()
                            .Eq(x => x.Id, existing.Id)
                            .Update.Set(x => x.Name, x.Label)
                            .UpdateOneModel()
                    );
                }

                continue;
            }

            var obj = new CustomObject
            {
                Id = Guid.NewGuid(),
                AccountId = Context.AccountId.Value,
                ObjectTypeId = objectType.Id,
                ObjectType = objectType.Name,
                Name = x.Label,
                ExternalId = x.Value,
                CreatedOn = DateTime.UtcNow,
                IsActive = x.Active,
            };

            if (x.DefaultValue)
            {
                obj.Properties = new Dictionary<string, object>
                {
                    { "DefaultValue", true }
                };
            }

            batch.Add(new InsertOneModel<CustomObject>(obj));
        }

        var modified = await _connection.BulkWriteAsync(batch);

        return modified;
    }

    private async Task<ObjectType> GetOrCreateAsync(string objectType)
    {
        var row = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Name, objectType)
            .Eq(x => x.Namespace, null)
            .FirstOrDefaultAsync();

        if (row != null)
        {
            if (row.NativeType != null) throw new BadRequestException($"{objectType} is not a custom type");
            return row;
        }

        row = new ObjectType
        {
            AccountId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            Name = objectType,
            NativeType = null,
            UniqueExternalId = true,
            // ...
        };

        await _connection.InsertAsync(row);
        return row;
    }

    // [HttpPut("/salesforce/v1/{objectType}/[controller]/Picklist({fieldName})/Import")]
    // [ProducesResponseType(typeof(IEnumerable<SFPickListValue>), 200)]
    // public async Task<IActionResult> ImportPickListAsync(
    //     [FromRoute] string objectType,
    //     [FromRoute] string fieldName,
    //     [FromServices] MongoConnection connection)
    // {
    //     var (token, error) = await _service.GetTokenAsync(Context.AccountId.Value);
    //     if (token == null) return BadRequest(error);

    //     var result = await _client.DescribeAsync(token, objectType);
    //     if (result == null) return NotFound($"{objectType} not found");

    //     var field = result.Fields.Find(x => string.Equals(x.Name, fieldName));
    //     if (field == null) return NotFound($"{fieldName} not found");

    //     var list = field.PicklistValues.Select(x => new SFPickListValue
    //     {
    //         AccountId = Context.AccountId.Value,
    //         ObjectType = objectType,
    //         FieldName = fieldName,
    //         CreatedOn = DateTime.UtcNow,
    //         IsActive = x.Active,
    //         IsDefaultValue = x.DefaultValue,
    //         Label = x.Label,
    //         Value = x.Value
    //     });

    //     var deleted = await connection.Filter<SFPickListValue>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Eq(x => x.ObjectType, objectType)
    //         .Eq(x => x.FieldName, fieldName)
    //         .DeleteAsync();

    //     await connection.InsertAsync<SFPickListValue>(list);

    //     return Ok(list);
    // }

    // [HttpGet("/salesforce/v1/{objectType}({id})")]
    // // [ProducesResponseType(typeof(SfLead), 200)]
    // public async Task<IActionResult> GetAsync([FromRoute] string objectType, [FromRoute] string id)
    // {
    //     var token = await GetTokenAsync();
    //     var result = await Client.QueryByIdAsync<SfLead>(token, S, id);
    //     return result != null ? (IActionResult)Ok(result) : NotFound();
    // }        
}

// [BsonCollection("sf.PickList")]
// public class SFPickListValue
// {
//     [BsonId]
//     public ObjectId Id { get; set; }

//     public Guid AccountId { get; set; }

//     public string ObjectType { get; set; }

//     public string FieldName { get; set; }

//     public DateTime CreatedOn { get; set; }

//     /// <summary>
//     /// Active
//     /// </summary>
//     public bool IsActive { get; set; }

//     /// <summary>
//     /// Default picklist value
//     /// </summary>
//     public bool IsDefaultValue { get; set; }

//     /// <summary>
//     /// Value label
//     /// </summary>
//     public string Label { get; set; }

//     /// <summary>
//     /// Picklist item value
//     /// </summary>
//     /// <returns></returns>
//     public string Value { get; set; }
// }