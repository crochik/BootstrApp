using System;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("admin")]
[Route("/dipper/v1/[controller]")]
public class ReportController(IMapper mapper, MongoConnection connection, ObjectTypeService objectTypeService) : AbstractAggregateController(mapper, connection, objectTypeService)
{
    [HttpGet("/dipper/v1/[controller]({id})")]
    public async Task<AppReport> GetByIdAsync([FromRoute] Guid id)
    {
        await CheckPermission(AppReport.ObjectTypeFullName, ObjectTypePermission.Read);
        
        // TODO: should enforce other access rules?
        // ...
        
        var row = await _connection.Filter<AppReport>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (row == null) throw new NotFoundException(nameof(AppReport), id);

        return row;
    }

    [HttpPost]
    public async Task<AppReport> CreateAsync(
        [FromBody] AggregationRequest request, 
        [FromQuery] string name, 
        [FromQuery] string description, 
        [FromQuery] string group,
        [FromQuery] ReportTemplate template = ReportTemplate.None
    )
    {
        var (dataView, storedProcedure) = await BuildAsync(request);

        description ??= name ?? request.Aggregation.Name ?? "Report";
            
        dataView.Title = description ?? name;
        dataView.Menu = new Menu
        {
            Name = "Edit",
            Items = 
            [
                new ActionMenuItem
                {
                    Name = "Export",
                    Icon = nameof(Icons.Download),
                    Action = FormAction.Client_DonwloadCsv,
                }
            ],
        };
            
        var report = new AppReport
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor(),
            AccountId = Context.AccountId.Value,
            Name = name ?? request.Aggregation.Name,
            Description = description,
            StoredProcedure = storedProcedure,
            DataView = dataView,
                
            Template = template,
            Group = group,
        };

        await _connection.InsertAsync(report);

        return report;
    }

    // [HttpGet]
    // [ProducesResponseType(typeof(IEnumerable<AppReport>), 200)]
    // public async Task<IActionResult> GetReportsAsync()
    // {
    //     var rows = await _connection.Filter<AppReport>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .ExcludeField(x => x.StoredProcedure)
    //         .ExcludeField(x => x.DataView)
    //         .ExcludeField("View")
    //         .FindAsync();

    //     return Ok(rows);
    // }

    // [HttpPost]
    // [ProducesResponseType(typeof(AppReport), 200)]
    // public async Task<IActionResult> AddReportByIdAsync(
    //     string ns,
    //     string name,
    //     ReportTemplate template,
    //     EntityRoleId minRole
    //     )
    // {
    //     var dataView = await _connection.Filter<AppDataView>()
    //         .Eq(x => x.AccountId, Context.AccountId.Value)
    //         .Eq(x => x.Name, name)
    //         .FirstOrDefaultAsync();

    //     if (dataView == null) return NotFound("DataView");

    //     var aggregate = await _connection.Filter<AggregateStoredProcedure>()
    //         .Eq(x => x.Id, $"{ns}.{name}")
    //         .FirstOrDefaultAsync();

    //     if (aggregate == null) return NotFound("Aggregation");

    //     var row = new AppReport
    //     {
    //         AccountId = Context.AccountId.Value,
    //         Template = template, 
    //         MinRole = minRole,
    //         Name = name,
    //         StoredProcedure = aggregate,
    //         DataView = dataView.DataView
    //     };

    //     await _connection.InsertAsync(row);

    //     return row != null ? (IActionResult)Ok(row) : NotFound();
    // }

    // [HttpPost("DataView")]
    // [ProducesResponseType(typeof(DataViewResponse), 200)]
    // [Produces("text/csv", "application/json")]
    // public async Task<IActionResult> GetReportsDataViewAsync([FromBody] DataViewRequest request)
    // {
    //     if (Request.Headers.TryGetValue("Accept", out var headers))
    //     {
    //         request.ContentType = headers.FirstOrDefault();
    //     }

    //     var result = await _reportsService.GetAllReportsAsync(Context, request);

    //     return result == null ? (IActionResult)NotFound() : Ok(result);
    // }
}