using System;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Route("/dipper/v1/[controller]")]
public class DataViewController(IMapper mapper, MongoConnection connection, ObjectTypeService objectTypeService) : AbstractAggregateController(mapper, connection, objectTypeService)
{
    [HttpGet("/dipper/v1/[controller]({id})")]
    public async Task<AppDataView> GetByIdAsync([FromRoute] Guid id)
    {
        await CheckPermission(AppDataView.ObjectTypeFullName, ObjectTypePermission.Read);
        
        // TODO: should enforce other access rules?
        // ...
        
        var row = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (row == null) throw new NotFoundException(nameof(AppDataView), id);

        return row;
    }

    [HttpPost]
    public async Task<AppDataView> CreateAsync(
        [FromBody] AggregationRequest request,
        [FromQuery] string name,
        [FromQuery] string description
    )
    {
        var (dataView, storedProcedure) = await BuildAsync(request);

        dataView.Title = description ?? name;

        var dataview = new AppDataView
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor(),
            AccountId = Context.AccountId.Value,
            Name = name ?? request.Aggregation.Name,
            Description = description,
            StoredProcedure = storedProcedure,
            DataView = dataView,
        };

        await _connection.InsertAsync(dataview);

        return dataview;
    }
}