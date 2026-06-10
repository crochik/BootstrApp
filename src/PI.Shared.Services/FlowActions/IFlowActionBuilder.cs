using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Messages.Flow;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace FlowActions;

public class FlowActionContext
{
    public IEntityContext EntityContext { get; }
    public Flow Flow { get; }
    public FlowStep[] FlowSteps => Flow.Steps ?? Array.Empty<FlowStep>();
    public string ObjectType => Flow.ObjectType;
    public Guid EventTypeId { get; init;  }
    // public EventType EventType { get; init; }
    // public EventType[] EventTypes { get; init; }

    public FlowActionContext(IEntityContext context, Flow flow)
    {
        EntityContext = context;
        Flow = flow;
    }
}

public interface IFlowActionBuilder
{
    Guid Id { get; }
    string Name { get; }
    string IconName { get; }
    string Description { get; }
    string[] InputObjectTypes { get; }
    
    bool IsValidTrigger(string objectType);

    (IActionMessage Message, string Route) Build<T>(IEntityContext context, T evt, IActionOptions options) where T : FlowEvent;
    
    /// <summary>
    /// Action Form
    /// </summary>
    Task<Form> GetFormAsync(FlowActionContext flowActionContext, Guid? objectStatusId, FlowStep step = null);

    /// <summary>
    /// Add or update step 
    /// </summary>
    Task<FlowStep> AddOrUpdateStepAsync(IEntityContext context, Flow flow, Guid eventTypeId, Dictionary<string, object> requestParameters, FlowStep flowStep = null);

    /// <summary>
    /// Swap step Id, event ids, current status id, ... 
    /// </summary>
    ValueTask SwapAsync(IEntityContext context, FlowStep step, Dictionary<Guid, Guid?> swap);

    ValueTask<IEnumerable<Placeholder>> GetPlaceholdersForOutputAsync(IEntityContext context, Flow flow, ActionOptions triggerOptions, IEnumerable<Placeholder> placeholders, Guid stepEventIdTrigger);
}