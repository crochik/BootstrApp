using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace FlowActions;

public class SetObjectStatusActionBuilder : AbstractFlowActionBuilder<SetObjectStatusActionOptions, SetObjectStatusAction.Message>
{
    private readonly ObjectTypeService _objectTypeService;

    public override Guid Id => ActionIds.SetObjectStatus;

    public override string Name => "Set Status";

    public override string[] InputObjectTypes => null; // any

    public SetObjectStatusActionBuilder(
        MongoConnection connection,
        ObjectTypeService objectTypeService
    ) : base(connection)
    {
        _objectTypeService = objectTypeService;
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new SetObjectStatusAction.Message(evt, options);

    protected override async ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, SetObjectStatusActionOptions opts = null)
    {
        var statuses = await _objectTypeService.GetStatusesAsync(flowActionContext.EntityContext, flowActionContext.ObjectType);
        var items = statuses.ToDictionary(x => x.Id.ToString(), x => x.Name);
        // items.Add("", "-- New Status --");

        return getFields();

        IEnumerable<FormField> getFields()
        {
            yield return new SelectField
            {
                Name = nameof(SetObjectStatusActionOptions.ObjectStatusId).ToCamelCase(),
                Label = "Status",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = items
                },
                DefaultValue = opts?.ObjectStatusId,
            };

            // yield return new TextField
            // {
            //     Name = "newObjectStatusName",
            //     Label = "Name",
            //     Visible = new[]
            //     {
            //         $"!{nameof(SetObjectStatusActionOptions.ObjectStatusId).ToCamelCase()}"
            //     },
            //     IsRequired = true,
            // };
        }
    }

    protected override Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, SetObjectStatusActionOptions options)
    {
        // DO NOT ADD EVENT... IT WILL FIRE THE "DEFAULT" OnEnterStatus EVENT
        // var objectStatus = await _connection.Filter<ObjectStatus>()
        //     .Eq(x => x.AccountId, context.AccountId.Value)
        //     .Eq(x => x.Id, options.ObjectStatusId)
        //     .Eq(x => x.ObjectType, flow.ObjectType)
        //     .FirstOrDefaultAsync();
        //
        // if (objectStatus == null) throw NotFoundException.New<ObjectStatus>(options.ObjectStatusId.Value);
        //
        // step.Description = $"Set Status to '{objectStatus.Name}'";
        // var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, "Set Object Status", $"Status changed to '{objectStatus.Name}'", NextEventName);
        // options.NextEventId = evt.Id;
        // options.Output = new[]
        // {
        //     output,
        // };

        return Task.CompletedTask;
    }
}