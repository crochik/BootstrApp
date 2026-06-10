using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using AutoMapper;
using System.Collections.Generic;
using PI.Shared.Models;
using PI.Shared.Exceptions;
using Crochik.Dipper;
using Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Produces("application/json")]
[Route("/dipper/v1/[controller]")]
public class AggregateController(IMapper mapper, MongoConnection connection, ObjectTypeService objectTypeService) : AbstractAggregateController(mapper, connection, objectTypeService)
{
    [HttpGet("Last")]
    public async Task<Aggregation> GetLastAsync()
    {
        await CheckPermission(AggregationHistory.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var record = await _connection.Filter<AggregationHistory>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.UserId, Context.UserId.Value)
            .SortDesc(x => x.CreatedOn)
            .Limit(1)
            .ExcludeField(x => x.Versions)
            .FirstOrDefaultAsync();

        if (record == null) throw new NotFoundException();

        return _mapper.Map<Aggregation>(record);
    }

    [HttpGet("/dipper/v1/[controller]({id})")]
    public async Task<AggregationResponse> GetByIdAsync([FromRoute] Guid id)
    {
        await CheckPermission(AggregationHistory.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var record = await GetHistoryAsync(id);
        if (record == null) throw new NotFoundException(nameof(AggregationHistory), id);

        return _mapper.Map<AggregationResponse>(record);
    }

    [HttpPost("DataView")]
    [Produces("text/csv", "application/json")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [ProducesResponseType(typeof(string), 420)]
    public async Task<IActionResult> GetDataViewAsync([FromBody] AggregationRequest request)
    {
        await CheckPermission(AggregationHistory.ObjectTypeFullName, ObjectTypePermission.Create);
        
        if (string.IsNullOrWhiteSpace(request?.Aggregation?.Pipeline)) return BadRequest();
        if (string.IsNullOrWhiteSpace(request.Aggregation.Collection)) return BadRequest();

        var array = BsonSerializer.Deserialize<BsonArray>(request.Aggregation.Pipeline);
        var stages = array.Values.OfType<BsonDocument>().ToArray();
        var sourceJsonPipeline = stages.Select(x => x.ToString()).ToArray();

        try
        {
            var result = await GetPreviewAsync(request, stages);
            var historyId = await SerializeAsync(result.Response, sourceJsonPipeline);
            var response = BuildDynamicDataViewResponse(request, result.Bson);

            return Ok(response);
        }
        catch (MongoCommandException ex)
        {
            // ...
            Response.StatusCode = 420;
            return Content(ex.ErrorMessage, "text/plain");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(AggregationResponse), 200)]
    [ProducesResponseType(typeof(string), 420)]
    public async Task<IActionResult> AggregateAsync([FromBody] AggregationRequest request)
    {
        await CheckPermission(AggregationHistory.ObjectTypeFullName, ObjectTypePermission.Create);
        
        if (string.IsNullOrWhiteSpace(request?.Aggregation?.Pipeline)) return BadRequest();
        if (string.IsNullOrWhiteSpace(request.Aggregation.Collection)) return BadRequest();

        var array = BsonSerializer.Deserialize<BsonArray>(request.Aggregation.Pipeline);
        var stages = array.Values.OfType<BsonDocument>().ToArray();
        var sourceJsonPipeline = stages.Select(x => x.ToString()).ToArray();

        try
        {
            var response = request.Write ?
                await ExecuteAsync(request, stages) :
                await PreviewAsync(request, stages);

            response.Id = await SerializeAsync(response, sourceJsonPipeline);

            return Ok(response);
        }
        catch (MongoCommandException ex)
        {
            // ...
            Response.StatusCode = 420;
            return Content(ex.ErrorMessage, "text/plain");
        }
        catch (DipperException ex)
        {
            // ...
            Response.StatusCode = 420;
            return Content(ex.Message, "text/plain");
        }
    }

    private async Task<AggregationResponse> ExecuteAsync(AggregationRequest request, BsonDocument[] stages)
    {
        // var resolved = stages.Select(x => x.ReplaceISODates()).ToArray();
        var resolved = stages.Select(x => x.ReplaceFunctions()).ToArray();
        var resolvedJsonPipeline = resolved.ToJsonString();

        var sp = BuildStoredProcedure(request, resolved);
        if (sp.Parameters?.Length > 0)
        {
            throw new BadRequestException("cant handle parameters yet");
        }

        await sp.ExecuteAsync(_connection, request.Input);

        var response = _mapper.Map<AggregationResponse>(request.Aggregation);
        response.Pipeline = resolvedJsonPipeline;

        return response;
    }

    private async Task<AggregationResponse> PreviewAsync(AggregationRequest request, BsonDocument[] stages)
    {
        if (!request.Limit.HasValue) request.Limit = 100;

        var result = await GetPreviewAsync(request, stages);
        result.Response.Result = result.Bson.Select(x => BsonTypeMapper.MapToDotNetValue(x));

        return result.Response;
    }

    private async Task<Guid> SerializeAsync(AggregationResponse response, string[] sourcePipeline)
    {
        AggregationHistory record;

        if (response.Id.HasValue)
        {
            // add version
            record = await GetHistoryAsync(response.Id.Value);
            if (record != null)
            {
                if (!string.Equals(record.Pipeline, response.Pipeline))
                {
                    var query = _connection.Filter<AggregationHistory>()
                        .Eq(x => x.AccountId, Context.AccountId.Value)
                        .Eq(x => x.Id, response.Id.Value)
                        .Update
                        .Set(x => x.Pipeline, sourcePipeline)
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .AddToSet(x => x.Versions, new PipelineVersion { Value = response.Pipeline });

                    if (response.Name != null) query.Set(x => x.Name, response.Name);

                    await query.UpdateOneAsync();
                }

                return record.Id;
            }
        }

        if (string.IsNullOrWhiteSpace(response.Name)) response.Name = $"{response.Collection} - {DateTime.UtcNow}";

        // create 
        record = new AggregationHistory
        {
            AccountId = Context.AccountId.Value,
            UserId = Context.UserId.Value,
            Collection = response.Collection,
            Name = response.Name,
            Namespace = response.Namespace,
            Parameters = response.Parameters,
            Pipeline = sourcePipeline,
            Versions = new[] {
                new PipelineVersion
                {
                    Value = response.Pipeline
                }
            },
            LastModifiedOn = DateTime.UtcNow
        };

        await _connection.InsertAsync(record);

        return record.Id;
    }
}

public class Aggregation
{
    public Guid? Id { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }
    public Parameter[] Parameters { get; set; }
    public string Collection { get; set; }
    public string Pipeline { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
}

public class AggregationRequest
{
    public Aggregation Aggregation { get; set; }
    public bool Write { get; set; }
    public int? Limit { get; set; }
    public Dictionary<string, object> Input { get; set; }
}

public class AggregationResponse : Aggregation
{
    public IEnumerable<object> Result { get; set; }
}

public class AggregationProfile : Profile
{
    public AggregationProfile()
    {
        CreateMap<Aggregation, AggregationResponse>(MemberList.Source);

        CreateMap<AggregationHistory, Aggregation>(MemberList.Destination)
            .ForMember(d => d.Pipeline, o => o.MapFrom(s => s.Pipeline.ToJsonArrayString()));

        CreateMap<AggregationHistory, AggregationResponse>(MemberList.Destination)
            .ForMember(d => d.Pipeline, o => o.MapFrom(s => s.Pipeline.ToJsonArrayString()))
            .ForMember(d => d.Result, o => o.Ignore());
    }
}