using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Models.OpenAPI;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.OpenAPI.Controllers;

[Route("/openapi/v1/[controller]")]
public class OperationActionController : APIController
{
    private readonly ILogger<OperationActionController> _logger;
    private readonly ObjectTypeService _objectTypeService;
    private readonly MongoConnection _connection;

    public OperationActionController(ILogger<OperationActionController> logger, ObjectTypeService objectTypeService, MongoConnection connection)
    {
        _logger = logger;
        _objectTypeService = objectTypeService;
        _connection = connection;
    }

    [Authorize("admin")]
    [HttpGet("/openapi/v1/[controller]({operationId})/Parameters/DataForm")]
    public async Task<Form> GetParametersDataFormAsync([FromRoute] Guid operationId)
    {
        var operation = await _connection.Filter<Operation>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, operationId)
            .FirstOrDefaultAsync();

        if (operation?.Request == null) return Form.BuildErrorForm("Operation not found");

        var accountContext = new AccountContext(Context.AccountId.Value);
        
        if (string.IsNullOrEmpty(operation.Request.ParametersObjectType))
        {
            return new Form
            {
                Name = "Parameters",
                Fields = (operation.Request.Parameters?.Values ??  Enumerable.Empty<FormField>()).ToArray(),
            };
        }

        var options = new ActionBuilderGetFormOptions(accountContext);
        var objectType = await _objectTypeService.GetAsync(accountContext, operation.Request.ParametersObjectType, options);
        return await _objectTypeService.GetAddDataFormAsync(accountContext, objectType, options);
    }

    [Authorize("admin")]
    [HttpGet("/openapi/v1/[controller]({operationId})/Request/DataForm")]
    public async Task<Form> GetRequestDataFormAsync([FromRoute] Guid operationId)
    {
        var operation = await _connection.Filter<Operation>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, operationId)
            .FirstOrDefaultAsync();

        if (operation?.Request == null) return Form.BuildErrorForm("Operation not found");

        var accountContext = new AccountContext(Context.AccountId.Value);

        if (operation.Request.Payloads != null)
        {
            if (operation.Request.Payloads.TryGetValue("application/json", out var payload))
            {
                var options = new ActionBuilderGetFormOptions(accountContext);
                var objectType = await _objectTypeService.GetAsync(accountContext, payload.ObjectType, options);
                return await _objectTypeService.GetAddDataFormAsync(accountContext, objectType, options);
            }

            // TODO: handle other payloads?
            // ...
        }

        return new Form
        {
            Name = "Request",
            Fields = Array.Empty<FormField>(),
        };
    }
}