using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace Controllers;

public abstract class ModelController<TModel, TApi> : APIController
    where TModel : Model
    where TApi : ApiModel
{
    protected ILogger _logger;
    protected IMapper _mapper;
    protected MongoConnection _connection;

    protected ModelController(ILogger<ModelController<TModel,TApi>> logger, IMapper mapper, MongoConnection connection)
    {
        _logger = logger;
        _mapper = mapper;
        _connection = connection;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]")]
    public async Task<IEnumerable<TApi>> GetAsync()
    {
        var rows = await _connection.Filter<TModel>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .FindAsync();

        var result = _mapper.Map<IEnumerable<TApi>>(rows);
        return result;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({id})")]
    public async Task<TApi> GetByIdAsync(Guid id)
    {
        var row = await _connection.Filter<TModel>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (row==null) throw new NotFoundException();

        var result = _mapper.Map<TApi>(row);
        return result;
    }

    [Authorize("managerplus")]
    [HttpDelete("/api/v1/[controller]({id})")]
    public async Task<TApi> DeleteByIdAsync(Guid id)
    {
        var row = await GetByIdAsync(id);
        if (row == null)
        {
            // ???
            // ...
            return null;
        }

        var deleted = await _connection.Filter<TModel>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .DeleteOneAsync();

        if (!deleted)
        {
            // ...
            return null;
        }

        var result = _mapper.Map<TApi>(row);
        return result;
    }

    [Authorize("managerplus")]
    [HttpPut("/api/v1/[controller]({id})")]
    public async Task<TApi> ReplaceAsync(Guid id, [FromBody] TApi api)
    {
        if (id != api.Id) throw new BadRequestException("Id mismatch");

        var row = await GetByIdAsync(id);
        if (row == null) throw new NotFoundException();

        var model = _mapper.Map<TModel>(api);
        var updated = await _connection.Filter<TModel>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .ReplaceOneAsync(model);

        if (updated.MatchedCount != 1)
        {
            // ...
            throw new Exception("Update failed");
        }

        return _mapper.Map<TApi>(model);
    }

    [Authorize("managerplus")]
    [HttpPost("/api/v1/[controller]")]
    public async Task<TApi> CreateAsync([FromBody] TApi api)
    {
        var model = _mapper.Map<TModel>(api);

        try
        {
            await _connection.InsertAsync(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create {objectType}", typeof(TModel).Name);
            throw new Exception("Insert failed");
        }

        var result = _mapper.Map<TApi>(model);

        return result;
    }
}