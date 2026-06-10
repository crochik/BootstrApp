using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Controllers;

[Authorize("default")]
public abstract class AbstractModelController<TAdapter, TModel, TApi> : APIController
    where TAdapter : IModelAdapter<TModel>
    where TModel : IModel
    where TApi : ApiModel, TModel
{
    protected readonly ILogger _logger;
    protected readonly IMapper _mapper;
    protected readonly TAdapter _adapter;

    protected AbstractModelController(
        ILogger logger,
        IMapper mapper,
        TAdapter adapter)
    {
        this._logger = logger;
        this._mapper = mapper;
        this._adapter = adapter;
    }

    [HttpGet("/api/v1/[controller]")]
    [ProducesResponseType(typeof(IEnumerable<ApiModel>), 200)]
    public async Task<IActionResult> GetAsync()
    {
        var context = Context;
        var rows = await _adapter.GetTrunkAsync(context);
        var result = _mapper.Map<IEnumerable<TApi>>(rows);
        return Ok(result);
    }

    [HttpGet("/api/v1/[controller]({id})")]
    [ProducesResponseType(typeof(ApiModel), 200)]
    public async Task<IActionResult> GetByIdAsync(Guid id)
    {
        var context = Context;
        var row = await _adapter.GetByIdAsync(id);
        if (row == null)
        {
            return NotFound();
        }

        if (!CanRead(context, row))
        {
            return Forbid();
        }

        var result = _mapper.Map<TApi>(row);
        return Ok(result);
    }

    [HttpDelete("/api/v1/[controller]({id})")]
    [ProducesResponseType(typeof(ApiModel), 200)]
    public async Task<IActionResult> DeleteByIdAsync(Guid id)
    {
        var context = Context;
        var row = await _adapter.GetByIdAsync(id);
        if (row == null)
        {
            return NotFound();
        }

        if (!CanDelete(context, row))
        {
            return Forbid();
        }

        var deleted = await _adapter.DeleteAsync(row.Id);
        if (!deleted)
        {
            _logger.LogError($"Failed to delete {typeof(TModel).Name} with {id}", id);
            return StatusCode(500, "Internal Error");
        }

        var result = _mapper.Map<TApi>(row);
        return Ok(result);
    }

    [HttpPut("/api/v1/[controller]({id})")]
    [ProducesResponseType(typeof(ApiModel), 200)]
    public async Task<IActionResult> UpdateIdAsync(Guid id, [FromBody] TApi model)
    {
        var context = Context;
        var row = await _adapter.GetByIdAsync(id);
        if (row == null)
        {
            return NotFound();
        }

        if (!CanUpdate(context, row))
        {
            return Forbid();
        }

        row = Convert(context, model, row);
        if (row == null)
        {
            return BadRequest();
        }

        var updated = await _adapter.UpdateAsync(row);
        if (!updated)
        {
            _logger.LogError($"Failed to update {typeof(TModel).Name} with {id}", id);
            return StatusCode(500, "Internal Error");
        }

        var result = _mapper.Map<TApi>(row);
        return Ok(result);
    }

    [HttpPost("/api/v1/[controller]")]
    [ProducesResponseType(typeof(ApiModel), 200)]
    public async Task<IActionResult> CreateAsync([FromBody] TApi model)
    {
        var context = Context;
        var row = Convert(context, model);
        if (row == null)
        {
            return BadRequest();
        }

        row = await _adapter.CreateAsync(row);
        if (row == null)
        {
            _logger.LogError($"Failed to create {typeof(TModel).Name}");
            return StatusCode(500, "Internal Error");
        }

        var result = _mapper.Map<TApi>(row);
        return Ok(result);
    }

    protected virtual TModel Convert(IEntityContext context, TApi api, TModel current = default(TModel))
    {
        if (current == null)
        {
            // create
        }
        else
        {
            api.Id = current.Id;
        }

        return api;
    }

    protected virtual bool CanRead(IEntityContext context, TModel row) => true;

    protected virtual bool CanUpdate(IEntityContext context, TModel row) =>
        CanRead(context, row);

    protected virtual bool CanDelete(IEntityContext context, TModel row) =>
        CanUpdate(context, row);
}