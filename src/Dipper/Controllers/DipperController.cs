using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Dipper;
using Crochik.Mongo;
using DataViews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.OpenAPI;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Produces("application/json")]
[Route("/dipper/v1/[controller]")]
public class DipperController(
    IMapper mapper,
    MongoConnection connection,
    ObjectTypeService objectTypeService
    ) : APIController
{
    [HttpPost("StoredProcedure/Dataview")]
    public async Task<DataViewResponse> GetStoredProceduresAsync(DataViewRequest request)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var query = connection.Filter<StoredProcedure>()
            .Ne(x => x.IsActive, false);
        // if (!string.IsNullOrEmpty(ns)) query.Eq(x => x.Namespace, ns);
        // if (typeof(T) != typeof(StoredProcedure)) query.OfType<StoredProcedure, T>();

        var sp = await query.SortAsc(x => x.Id).FindAsync();
        var list = sp.Select(mapper.Map<BasicProcedure>);

        var response = new DataViewResponse
        {
            Request = request,
            Result = list,
            View = new StoredProcedureDataView()
        };

        response.UpdateFields();

        return response;
    }

    [HttpGet("StoredProcedure")]
    public async Task<IEnumerable<Procedure>> GetStoredProceduresAsync(string ns)
    {
        return await GetStoredProceduresAsync<StoredProcedure>(ns);
    }

    [HttpGet("Aggregate")]
    public async Task<IEnumerable<Procedure>> GetAggregateStoredProceduresAsync(string ns)
    {
        return await GetStoredProceduresAsync<AggregateStoredProcedure>(ns);
    }

    [HttpGet("Update")]
    public async Task<IEnumerable<Procedure>> GetUpdateStoredProceduresAsync(string ns)
    {
        return await GetStoredProceduresAsync<UpdateStoredProcedure>(ns);
    }

    private async Task<IEnumerable<Procedure>> GetStoredProceduresAsync<T>(string ns)
        where T : StoredProcedure
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var query = connection.Filter<StoredProcedure>();
        if (!string.IsNullOrEmpty(ns)) query.Eq(x => x.Namespace, ns);
        if (typeof(T) != typeof(StoredProcedure)) query.OfType<StoredProcedure, T>();

        var sp = await query.SortAsc(x => x.Id).FindAsync();
        return sp.Select(mapper.Map<Procedure>);
    }

    [HttpGet("StoredProcedure({id})")]
    [Produces("application/json", "text/plain")]
    [ProducesResponseType(typeof(Procedure), 200)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] string id)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var sp = await connection.DipperOrDefaultAsync(id);
        if (sp == null) return NotFound();

        if (Request.Headers.TryGetValue("Accept", out var headers) && headers.Contains("text/plain"))
        {
            return Content(sp.Body, "text/plain");
        }

        var result = mapper.Map<Procedure>(sp);
        return Ok(result);
    }
    
    [HttpPost("StoredProcedure({id})")]
    [Produces("text/plain")]
    public async Task<IActionResult> GetStoredProcedureAsync(string id, [FromBody] Dictionary<string, object> parameters)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var sp = await connection.DipperOrDefaultAsync(id);
        var js = sp?.ToString(parameters);
        return js == null ? NotFound() : Content(js, "text/plain");
    }
    
    [HttpPost("Aggregate")]
    [Produces("text/plain")]
    [StringBody]
    public async Task<IActionResult> AddAggregateAsync(string collectionName, string ns, string name, string description)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var body = Request.GetBody()?.Trim();
        if (string.IsNullOrEmpty(body)) return BadRequest();
        if (!body.StartsWith("[") || !body.EndsWith("]")) return BadRequest();
        // 

        var array = BsonSerializer.Deserialize<BsonArray>(body);
        if (string.IsNullOrEmpty(ns)) ns = "global";
        if (string.IsNullOrEmpty(collectionName) || string.IsNullOrEmpty(name)) return BadRequest("name and/or collection missing");

        var sp = new AggregateStoredProcedure
        {
            Collection = collectionName,
            Id = $"{ns}.{name}",
            Description = description,
            Pipeline = array.Select(x => x.ToString()).ToArray()
        };

        await connection.InsertAsync<StoredProcedure>(sp);

        var js = sp?.ToString(null);
        return js == null ? (IActionResult)NotFound() : Content(js, "text/plain");
    }

    [HttpPost("Aggregate({id})/Exec")]
    [Produces("text/csv", "application/json")]
    public async Task<IActionResult> AggregateAsync([FromRoute] string id, [FromBody] Dictionary<string, object> parameters)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var sp = await connection.DipperOrDefaultAsync<AggregateStoredProcedure>(id);
        if (sp == null) return NotFound();

        var data = await sp.ExecuteAsync<object>(connection, parameters);
        return Ok(data);
    }

    [HttpPost("Update")]
    public async Task<IActionResult> AddUpdateAsync([FromBody] UpdateProcedure body)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var update = mapper.Map<UpdateStoredProcedure>(body);

        await connection.InsertAsync(update);

        return Ok(update);
    }

    [HttpPost("Update({id})/Exec")]
    // [ProducesResponseType(typeof(ApiModel), 200)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] Dictionary<string, object> parameters)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var sp = await connection.DipperOrDefaultAsync<UpdateStoredProcedure>(id);
        if (sp == null) return NotFound();

        var data = await sp.ExecuteAsync(connection, parameters);
        return Ok(data);
    }

    [HttpPost("Macro")]
    public async Task<MacroStoredProcedure> AddMacroAsync([FromBody] Macro body)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var macro = mapper.Map<MacroStoredProcedure>(body);

        await connection.InsertAsync(macro);

        return macro;
    }

    [HttpPost("Macro({id})")]
    [Produces("text/plain")]
    public async Task<IActionResult> GetMacroAsync([FromRoute] string id, [FromBody] Dictionary<string, object> parameters)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var macro = await connection.Filter<MacroStoredProcedure>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (macro == null) return NotFound();
        var result = await macro.GenerateAsync(connection, parameters);
        return Content(result, "text/plain");
    }

    [HttpPost("/dipper/v1/Aggregate({id})/Save")]
    public async Task<Procedure> CreateFromHistoryAsync([FromRoute] Guid id)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        var record = await connection.Filter<AggregationHistory>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .ExcludeField(x => x.Versions)
            .FirstOrDefaultAsync();

        if (record == null) throw new NotFoundException();

        var pipeline = record.Pipeline
            .Select(x => BsonDocument.Parse(x))
            // .Select(x => x.ReplaceFunctions())
            .Select(x => x.ToString());

        var aggregate = new AggregateStoredProcedure
        {
            Id = string.IsNullOrEmpty(record.Namespace) ? record.Name : $"{record.Namespace}.{record.Name}",
            Name = record.Name,
            Description = record.Description,
            Pipeline = pipeline.ToArray(),
            Collection = record.Collection,
        };

        var parameters = new Dictionary<string, Parameter>();
        if (record.Parameters?.Length > 0)
        {
            foreach (var param in record.Parameters)
            {
                parameters.TryAdd(param.Name, param);
            }
        }

        var regex = new Regex("^{{{Parameters\\.(?<value>.*)}}}$");
        foreach (var stage in aggregate.ToBsonPipeline(null))
        {
            stage.FindStringValue(regex, (value) =>
            {
                var match = regex.Match(value);
                var name = match.Groups["value"].Value;

                parameters.TryAdd(name, new Parameter
                {
                    Name = name,
                });
            });
        }

        aggregate.Parameters = parameters.Values.ToArray();

        var existing = await connection.Filter<AggregateStoredProcedure>()
            .Eq(x => x.Id, aggregate.Id)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (existing.Collection != aggregate.Collection) throw new BadRequestException("Can't change collection for exisitng stored procedure");

            aggregate.Version = existing.Version + 1;

            // save copy of old version
            existing.Id = $"{aggregate.Id}.v{existing.Version:D3}";
            existing.LastModifiedOn = DateTime.UtcNow;
            existing.IsActive = false;
            await connection.InsertAsync(existing);

            await connection.Filter<StoredProcedure>()
                .Eq(x => x.Id, aggregate.Id)
                .ReplaceOneAsync(aggregate);

            return mapper.Map<Procedure>(aggregate);
        }

        await connection.InsertAsync(aggregate);

        return mapper.Map<Procedure>(aggregate);
    }

    private async Task CheckPermission(string objectTypeName, ObjectTypePermission permission)
    {
        var hasPermission = await objectTypeService.HasPermission(Context, objectTypeName, permission);
        if (!hasPermission) throw new ForbiddenException("Access Forbidden");
    }

    /*
    [HttpPut("StoredProcedure({id})")]
    [Produces("application/json", "plain/text")]
    [ProducesResponseType(typeof(Procedure), 200)]
    [StringBody]
    public async Task<IActionResult> UpdateStoredProcedureAsync(string id)
    {
        var body = Request.GetBody()?.Trim();
        if (string.IsNullOrEmpty(body)) return BadRequest();
        if (!body.StartsWith("[") || !body.EndsWith("]")) return BadRequest();

        var sp = await _connection.DipperOrDefaultAsync(id);
        if (sp == null) return NotFound();

        var array = BsonSerializer.Deserialize<BsonArray>(body);
        var pipeline = array.Select(x => x.ToString()).ToArray();

        // TODO: check if anything really changed
        // ...

        StoredProcedure result;
        switch (sp)
        {
            case AggregateStoredProcedure aggregate:
                result = await _connection.Filter<AggregateStoredProcedure>()
                    .Eq(x => x.Id, id)
                    .Update.Set(x => x.Pipeline, pipeline)
                    .UpdateAndGetOneAsync();
                break;

            default:
                return BadRequest($"Update not supported for ${sp.GetType().Name}");
        }

        await _connection.Filter<StoredProcedureVersions>()
            .Eq(x => x.Id, result.Id)
            .Update
            .AddToSet(x => x.Versions, new StoredProcedureVersions.Version
            {
                Value = sp
            })
            .UpdateOneAsync();

        return Convert(result);
    }
    */
    
    // [HttpPost("Update")]
    // [StringBody]
    // public async Task<IActionResult> AddUpdateAsync(string collectionName, string ns, string name, string description)
    // {
    //     var body = Request.GetBody()?.Trim();
    //     if (string.IsNullOrEmpty(body)) return BadRequest();
    //     if (!body.StartsWith("[") || !body.EndsWith("]")) return BadRequest();
    //     var array = JsonConvert.DeserializeObject<object[]>(body);
    //     if (array.Length != 3) return BadRequest();

    //     if (string.IsNullOrEmpty(ns)) ns = "global";
    //     if (string.IsNullOrEmpty(collectionName) || string.IsNullOrEmpty(name)) return BadRequest("name and/or collection missing");

    //     var sp = new UpdateStoredProcedure
    //     {
    //         Id = $"{ns}.{name}",
    //         Description = description,
    //         Collection = collectionName,
    //         Query = JsonConvert.SerializeObject(array[0]),
    //         Update = JsonConvert.SerializeObject(array[1]),
    //     };

    //     dynamic options = array[2];
    //     sp.Multiple = options.multi == true;

    //     await _connection.InsertAsync<StoredProcedure>(sp);

    //     var js = sp?.ToString(null);
    //     return js == null ? (IActionResult)NotFound() : Content(js, "application/javascript");
    // }

}