using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers;

[Route("/productcatalog/v1/[controller]")]
public class SpreadsheetController : APIController
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public SpreadsheetController(
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    private async Task<DataViewResponse> GetSpreadsheetAsync(ObjectDataViewBuilder builder, Guid id, DataViewRequest request, bool resetFields)
    {
        Prepare(request);

        var row = await _connection.Filter<Spreadsheet>().Eq(x => x.Id, id).FirstOrDefaultAsync();
        if (row == null)
        {
            throw new NotFoundException(nameof(SpreadsheetRow), id);
        }

        if (request.Criteria.TryGetEqCondition(nameof(SpreadsheetRow.ParentId), out var condition) && !id.Equals(condition.Value))
        {
            condition.Value = row.Id;
        }
        else
        {
            var criteria = request.Criteria ?? Enumerable.Empty<Condition>();
            request.Criteria = criteria.Append(
                new Condition
                {
                    FieldName = nameof(SpreadsheetRow.ParentId),
                    Value = id,
                }
            ).ToArray();
        }

        // TODO: use builder/handle request.Fields
        // ...

        var objectTypeName = nameof(SpreadsheetRow);
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException($"{objectTypeName} not found");

        var response = await builder.BuildDataViewAsync(Context, objectType, request, Projection.All);

        if (resetFields) response.View.Fields = Array.Empty<FormField>();

        response.View.Title = row.Name;
        response.View.Fields = response.View.Fields.Concat(
            row.Columns.OrderBy(x => x.Key).Select(x => new TextField
            {
                Name = x.Key,
                Label = x.Value
            })
        ).ToArray();

        // update list of fields included
        response.Request.Fields = response.View.Fields.Select(x => x.Name).ToArray();

        return response;
    }

    [Authorize("managerplus")]
    [HttpPost("/productcatalog/v1/[controller]({id})/DataView")]
    [Produces("text/csv", "application/json")]
    public virtual async Task<IDataViewResponse> DataViewAsync(
        [FromRoute] Guid id, 
        [FromBody] DataViewRequest request, 
        [FromServices] ObjectDataViewBuilder builder
        )
    {
        var spreadsheet = await _connection.Filter<Spreadsheet>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (spreadsheet == null) throw new NotFoundException(nameof(Spreadsheet), id);

        var response = await GetSpreadsheetAsync(builder, id, request, false);

        // for now, do not add the user actions
        await spreadsheet.UpdateAsync(
            _connection,
            Context,
            response
        );

        return response;
    }

    [Authorize("managerplus")]
    [HttpPost("/productcatalog/v1/[controller]({id})/Errors/DataFile")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> ErrorsDataFileAsync([FromRoute] Guid id, [FromBody] DataFormActionRequest _, [FromServices] ObjectDataViewBuilder builder)
    {
        var response = await GetSpreadsheetAsync(builder, id, new DataViewRequest(), true);

        // hack till can come up with the idea of a "filtered view"
        response.Result = response.Result?.Where(x =>
            ((IDictionary<string, object>)x).TryGetValue("HasErrors", out var hasError) &&
            hasError is bool hasErrorValue &&
            hasErrorValue).ToArray();

        return response;
    }

    [Authorize("managerplus")]
    [HttpGet("/productcatalog/v1/[controller]({id})/{name}/DataForm")]
    public async Task<Form> GetDataFormAsync([FromRoute] Guid id, [FromRoute] string name)
    {
        var spreadsheet = await _connection.Filter<Spreadsheet>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();
        if (spreadsheet == null) throw new NotFoundException(nameof(Spreadsheet), id);

        name = $"{nameof(Spreadsheet)}|{name}";
        var form = (await _connection.GetProfileElementAsync<AppForm>(Context, name))?.Form;
        if (form == null) throw new NotFoundException($"{name} not found");

        // var title = new TitleBuilder(spreadsheet.Name)
        //     .WithoutFileExtension()
        //     .WithMaxLengthOf(20, true)
        //     .Build();

        // form.Title = title;

        if (form.Actions != null)
        {
            foreach (var action in form.Actions)
            {
                action.Action = action.Action?.Replace("{{id}}", spreadsheet.Id.ToString());
            }
        }

        return form;
    }
}