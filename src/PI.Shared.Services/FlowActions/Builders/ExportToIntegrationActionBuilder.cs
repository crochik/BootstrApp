using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class ExportToIntegrationActionBuilder : AbstractFlowActionBuilder<ExportToIntegrationActionOptions, ExportToIntegrationAction.Message>
{
    public override string Name => "Export to Integration";

    public override Guid Id => ActionIds.ExportToIntegration;

    // public override string IconName => IntegrationIds.Integration.ToString();

    public override string[] InputObjectTypes => new[]
    {
        nameof(Lead)
    };

    public ExportToIntegrationActionBuilder(MongoConnection connection) : base(connection)
    {
    }

    protected override IActionMessage Build<T2>(T2 evt, IActionOptions options)
        => new ExportToIntegrationAction.Message(evt, options);

    protected override ValueTask<IEnumerable<FormField>> GetFieldsAsync(FlowActionContext flowActionContext, FlowStep step = null, ExportToIntegrationActionOptions opts = null)
    {
        return ValueTask.FromResult(getFields());

        IEnumerable<FormField> getFields()
        {
            yield return new SelectField
            {
                Name = nameof(ExportToIntegrationActionOptions.IntegrationId).ToCamelCase(),
                Label = "Integration",
                IsRequired = true,
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = new Dictionary<string, string>
                    {
                        { IntegrationIds.Lumin.ToString(), IntegrationIds.GetName(IntegrationIds.Lumin) },
                        { IntegrationIds.Verse.ToString(), IntegrationIds.GetName(IntegrationIds.Verse) },
                        { IntegrationIds.Convertros.ToString(), IntegrationIds.GetName(IntegrationIds.Convertros) },
                    }
                },
                DefaultValue = opts?.IntegrationId,
            };

            yield return new SelectField
            {
                Name = nameof(ExportToIntegrationActionOptions.UpdateOperation).ToCamelCase(),
                Label = "Operation",
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = new Dictionary<string, string>
                    {
                        { nameof(UpdateOperation.Create), "Create if missing" },
                        { nameof(UpdateOperation.Update), "Update existing" },
                        { nameof(UpdateOperation.Upsert), "Add or Update" },
                    }
                },
                DefaultValue = opts?.UpdateOperation,
            };
        }
    }

    protected override async Task AddOutputsAsync(IEntityContext context, Flow flow, Dictionary<string, object> requestParameters, FlowStep step, ExportToIntegrationActionOptions options)
    {
        var integration = IntegrationIds.GetName(options.IntegrationId.Value);

        step.Description = options.UpdateOperation switch
        {
            UpdateOperation.Create => $"Export {flow.ObjectType} to {integration} if doesn't exist",
            UpdateOperation.Update => $"Update {flow.ObjectType} in {integration} if already exists",
            UpdateOperation.Upsert => $"Create or update {flow.ObjectType} in {integration}",
            _ => null,
        };
        
        var evtDescription = options.UpdateOperation switch
        {
            UpdateOperation.Create => $"{flow.ObjectType} didn't exist and was exported to {integration}",
            UpdateOperation.Update => $"Existing {flow.ObjectType} in {integration} was updated",
            UpdateOperation.Upsert => $"{flow.ObjectType} exported to {integration}",
            _ => null, 
        };

        var (evt, output) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Export to {integration}", evtDescription, NextEventName);
        var (evt2, output2) = await AddEventAsync(context, flow.ObjectType, step.CurrentStatusId, $"Export to {integration} failed", $"Export to {integration} failed", ErrorEventName);
        options.NextEventId = evt.Id;
        options.ErrorEventId = evt2.Id;
        options.Output = new[]
        {
            output,
            output2,
        };
    }

    public override (IActionMessage Message, string Route) Build<T2>(IEntityContext context, T2 evt, IActionOptions options)
    {
        if (options is not ExportToIntegrationActionOptions opts || !opts.IntegrationId.HasValue)
        {
            throw new BadRequestException("Invalid options for action");
        }

        var message = Build<T2>(evt, options);
        var route = IntegrationIds.GetActionRoute(context, opts.IntegrationId.Value);
        return (message, route);
    }
}