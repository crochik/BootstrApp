using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Authorize("default")]
[Route("/api/v1/[controller]")]
public class FlowActionBuilderController : APIController // AbstractNewModelController<IFlowActionAdapter, IFlowAction, Models.FlowAction>
{
    private readonly ObjectTypeService _objectTypeService;

    public FlowActionBuilderController(ObjectTypeService objectTypeService)
    {
        _objectTypeService = objectTypeService;
    }

    [Authorize("admin")]
    [HttpGet("{objectTypeName}/Add/DataForm")]
    public async Task<Form> GetRequestDataFormAsync(string objectTypeName)
    {
        var accountContext = new AccountContext(Context.AccountId.Value);
        var options = new ActionBuilderGetFormOptions(accountContext);
        var objectType = await _objectTypeService.GetAsync(accountContext, objectTypeName, options);
        var form = await _objectTypeService.GetAddDataFormAsync(accountContext, objectType, options);

        return form;
    }

    [Authorize("admin")]
    [HttpGet("EventType({eventTypeId})/Add/DataForm")]
    public async Task<Form> GetFormForEventType(Guid eventTypeId, [FromServices] MongoConnection connection)
    {
        var action = await connection.Filter<EventType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, eventTypeId)
            .FirstOrDefaultAsync();

        if (action == null) return Form.BuildErrorForm("Invalid Event");

        if (action.Trigger is not UserTrigger trigger || trigger.Form == null)
        {
            return new Form
            {
                Title = action.Name,
                Name = nameof(EventType),
                Fields =
                [
                    new LabelField
                    {
                        Name = "Message",
                        Label = "No parameters",
                    },
                ]
            };
        }

        return new Form
        {
            Title = action.Name,
            Name = nameof(EventType),
            Fields = trigger.Form.Fields.Select(x => new ExpressionField
            {
                Name = x.Name,
                Label = x.Label,
                ApiName = x.ApiName,
                Description = x.Description,
                DefaultValue = x.DefaultValue,
                // Enable = x.Enable,
                // Visible = x.Visible,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = x,
                }
            }).ToArray<FormField>(),
        };
    }
}