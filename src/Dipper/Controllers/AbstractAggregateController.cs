using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Produces("application/json")]
public abstract class AbstractAggregateController(IMapper mapper, MongoConnection connection, ObjectTypeService objectTypeService) : APIController
{
    protected readonly IMapper _mapper = mapper;
    protected readonly MongoConnection _connection = connection;

    protected async Task<AggregationHistory> GetHistoryAsync(Guid id)
    {
        var query = _connection.Filter<AggregationHistory>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .ExcludeField(x => x.Versions);

        return await query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get preview results for pipeline
    /// </summary>
    protected async Task<(AggregationResponse Response, List<BsonDocument> Bson)> GetPreviewAsync(AggregationRequest request, BsonDocument[] stages)
    {
        var resolved = stages.Select(x => x.ReplaceFunctions()).ToArray();
        var originalStoredProcedure = BuildStoredProcedure(request, resolved);

        var filtered = (IEnumerable<BsonDocument>)resolved;
        switch (originalStoredProcedure.Operation)
        {
            case AggregationOperation.Merge:
                // exclude merge operations...
                filtered = filtered.Exclude(AggregateStoredProcedure.STAGE_MERGE);
                break;
            case AggregationOperation.Update:
                // TODO: replace $UPDATE with "$set"?
                // ..
                throw new BadRequestException("Not implemented yet");
            case AggregationOperation.Delete:
                // TODO: replace $DELETE with "$match"?
                // ...
                throw new BadRequestException("Not implemented yet");
            case AggregationOperation.Macro:
                // TODO: replace $DELETE with "$match"?
                // ...
                throw new BadRequestException("Not implemented yet");
        }

        if (request.Limit.HasValue)
        {
            var limitStage = new BsonDocument(AggregateStoredProcedure.STAGE_LIMIT, request.Limit.Value);
            filtered = filtered.Append(limitStage);
        }

        var response = _mapper.Map<AggregationResponse>(request.Aggregation);

        resolved = filtered.ToArray();
        response.Pipeline = resolved.ToJsonString();

        var previewStoredProcedure = BuildStoredProcedure(request, resolved);
        var parameters = previewStoredProcedure.Parameters.ToDictionary(x => x.Name, x => request.Input != null && request.Input.TryGetValue(x.Name, out var value) ? value : default(object));
        if (Context.AccountId.HasValue && parameters.ContainsKey(nameof(IEntityContext.AccountId)))
        {
            parameters[nameof(IEntityContext.AccountId)] = Context.AccountId.Value.AsSerializedId();
        }

        if (Context.OrganizationId.HasValue && parameters.ContainsKey(nameof(IEntityContext.OrganizationId)))
        {
            parameters[nameof(IEntityContext.OrganizationId)] = Context.OrganizationId.Value.AsSerializedId();
        }

        if (Context.UserId.HasValue && parameters.ContainsKey(nameof(IEntityContext.UserId)))
        {
            parameters[nameof(IEntityContext.UserId)] = Context.UserId.Value.AsSerializedId();
        }

        var list = await previewStoredProcedure.ExecuteAsync<BsonDocument>(_connection, parameters);

        return (response, list);
    }

    protected async Task<(DataView DataView, AggregateStoredProcedure StoredProcedure)> BuildAsync(AggregationRequest request)
    {
        await CheckPermission(StoredProcedure.ObjectTypeFullName, ObjectTypePermission.Create);
        
        if (string.IsNullOrWhiteSpace(request?.Aggregation?.Pipeline)) throw new BadRequestException("Missing pipeline");

        var array = BsonSerializer.Deserialize<BsonArray>(request.Aggregation.Pipeline);
        var stages = array.Values.OfType<BsonDocument>().ToArray();

        if (string.IsNullOrWhiteSpace(request.Aggregation.Collection)) throw new BadRequestException("Missing collection");

        if (stages.AnyStageOfType(AggregateStoredProcedure.STAGE_MERGE))
        {
            throw new BadRequestException("Can't have a $merge in a report");
        }

        var hasParameters = false;
        if (string.Equals(stages[0].GetStageType(), AggregateStoredProcedure.STAGE_MATCH))
        {
            var regex = new Regex("^{{{Parameters\\.(?<value>.*)}}}$");
            stages[0].FindStringValue(regex, value => hasParameters = true);
        }

        var preview = hasParameters ? stages.Skip(1).ToArray() : stages;
        var result = await GetPreviewAsync(request, preview);
        var dvr = BuildDynamicDataViewResponse(request, result.Bson);

        var resolved = stages.Select(x => x.ReplaceFunctions()).ToArray();
        var sp = BuildStoredProcedure(request, resolved);

        return (dvr.View, sp);
    }

    private static List<Parameter> BuildParameters(IEnumerable<BsonDocument> pipeline)
    {
        var parameters = new List<Parameter>();

        var regex = new Regex("^{{{Parameters\\.(?<value>.*)}}}$");
        foreach (var stage in pipeline)
        {
            stage.FindStringValue(regex, value =>
            {
                var match = regex.Match(value);

                parameters.Add(new Parameter
                {
                    Name = match.Groups["value"].Value,
                });
            });
        }

        return parameters;
    }

    protected static AggregateStoredProcedure BuildStoredProcedure(AggregationRequest request, BsonDocument[] stages)
    {
        var aggregate = new AggregateStoredProcedure
        {
            Id = Guid.NewGuid().ToString(),
            Description = request.Aggregation.Name,
            Collection = request.Aggregation.Collection
        };

        if (stages.AnyStageOfType(AggregateStoredProcedure.STAGE_MERGE))
        {
            if (!stages[^1].IsStageOfType(AggregateStoredProcedure.STAGE_MERGE))
            {
                throw new BadRequestException($"$merge must be last stage, not {stages[^1].GetStageType()}");
            }

            aggregate.Operation = AggregationOperation.Merge;
        }
        else if (stages.AnyStageOfType(AggregateStoredProcedure.STAGE_UPDATE)) // $UPDATE - $set
        {
            // TODO: support $match => $and all $match(es) 
            // ...
            // TODO: support $limit and $order 
            // ...
            if (!stages.AllStageOfType(AggregateStoredProcedure.STAGE_UPDATE, AggregateStoredProcedure.STAGE_MATCH))
            {
                throw new BadRequestException("Only $match and $UPDATE supported for updates");
            }

            if (!stages[^1].IsStageOfType(AggregateStoredProcedure.STAGE_UPDATE))
            {
                throw new BadRequestException("$UPDATE must be last stage");
            }

            aggregate.Operation = AggregationOperation.Update;
        }
        else if (stages.AnyStageOfType(AggregateStoredProcedure.STAGE_DELETE)) // $DELETE ~ $match
        {
            // TODO: support $match => $and all $match(es) and $DELETE(s)
            // ...
            // TODO: support $limit and $order 
            // ...
            if (stages.Length != 1)
            {
                throw new BadRequestException("Only one stage supported for $DELETE");
            }

            aggregate.Operation = AggregationOperation.Delete;
        }
        else if (stages.AnyStageOfType(AggregateStoredProcedure.STAGE_EXECUTE))
        {
            if (!stages.AllStageOfType(AggregateStoredProcedure.STAGE_EXECUTE))
            {
                throw new BadRequestException("Can only have $EXECUTE stages in a pipeline with one $EXECUTE");
            }

            aggregate.Operation = AggregationOperation.Macro;
        }
        else
        {
            aggregate.Operation = AggregationOperation.Find;
        }

        // TODO: validate only expected stages
        // ...

        aggregate.Pipeline = stages.Select(x => x.ToString()).ToArray();
        aggregate.Parameters = BuildParameters(aggregate.ToBsonPipeline(null)).ToArray();

        return aggregate;
    }

    protected static DataViewResponse BuildDynamicDataViewResponse(AggregationRequest request, List<BsonDocument> bson)
    {
        var response = new DataViewResponse
        {
            Request = new DataViewRequest
            {
                Top = request.Limit.GetValueOrDefault(0),
            },
            View = new DataView
            {
                Name = request.Aggregation.Collection,
                DefaultSort = Model.IdFieldName,
                KeyField = Model.IdFieldName,
                Title = $"{request.Aggregation.Collection} Aggregation",
                // PageSize = request.Limit.Value
            }
        };

        var fields = InferProperties(bson);
        response.View.Fields = fields.Select(x => CreateFormField(x)).ToArray();
        response.Result = bson.Select(x => BsonTypeMapper.MapToDotNetValue(x));

        return response.UpdateFields();
    }

    private static Dictionary<string, BsonType> InferProperties(List<BsonDocument> list)
    {
        var fields = new Dictionary<string, BsonType>();
        for (var c = 0; c < list.Count && c < 10; c++)
        {
            foreach (var prop in list[c])
            {
                switch (prop.Value.BsonType)
                {
                    case BsonType.Array:
                    case BsonType.Document:
                    case BsonType.EndOfDocument:
                    case BsonType.Binary:
                    case BsonType.JavaScript:
                    case BsonType.JavaScriptWithScope:
                    case BsonType.RegularExpression:
                    case BsonType.Symbol:
                    case BsonType.Undefined:
                        break;

                    default:
                        fields.TryAdd(prop.Name, prop.Value.BsonType);
                        break;
                }
            }
        }

        return fields;
    }

    private static FormField CreateFormField(KeyValuePair<string, BsonType> field)
    {
        var type = field.Value switch
        {
            BsonType.DateTime => new DateTimeField { Name = field.Key },
            BsonType.Decimal128 => new NumberField { Name = field.Key },
            BsonType.Double => new NumberField { Name = field.Key },
            BsonType.Int32 => new NumberField { Name = field.Key },
            BsonType.Int64 => new NumberField { Name = field.Key },
            BsonType.Boolean => new CheckboxField { Name = field.Key },

            _ => (FormField)new TextField { Name = field.Key }
        };

        return type;
    }

    protected async Task CheckPermission(string objectTypeName, ObjectTypePermission permission)
    {
        var hasPermission = await objectTypeService.HasPermission(Context, objectTypeName, permission);
        if (!hasPermission) throw new ForbiddenException("Access Forbidden");
    }
}