using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Driver;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters;

// TODO: add projection to exclude steps on queries?
// ...
public class FlowAdapter : MappedNewModelAdapter<IFlow, Flow>, IFlowAdapter // , IFlowTransitionAdapter
{
    public FlowAdapter(MongoConnection connection)
        : base(connection)
    {
    }

    public async Task<FlowStep> AddAsync(Guid flowId, FlowStep step)
    {
        var dao = Connection.Mapper.Map<FlowStep>(step);

        var result = await Connection
            .Filter<Flow>().Eq(x => x.Id, flowId)
            .Update.AddToSet(x => x.Steps, dao)
            .UpdateAndGetOneAsync();

        return result?.Steps?.FirstOrDefault(x => x.Id == step.Id);
    }

    public override async Task<bool> UpdateAsync(IFlow obj)
    {
        var result = await Connection.Filter<Flow>()
            .Eq(x => x.Id, obj.Id)
            .Eq(x => x.EntityId, obj.EntityId)
            .Update
            .Set(x => x.Name, obj.Name)
            .Set(x => x.Description, obj.Description)
            .UpdateAndGetOneAsync();

        return result != null;
    }

    public async Task<IEnumerable<FlowStep>> GetStepsAsync(Guid flowId, Guid eventId, Guid? leadStatusId)
    {
        var steps = await GetStepsAsync(flowId);

        // TODO: could filter in the query but...
        return steps.Where(x =>
            (!x.CurrentStatusId.HasValue || x.CurrentStatusId.Value == leadStatusId) && x.EventIdTrigger == eventId
        );
    }

    public async Task<IEnumerable<FlowStep>> GetStepsAsync(Guid flowId)
    {
        var flow = await Connection.GetByIdAsync<Flow>(flowId);
        return flow?.Steps ?? Array.Empty<FlowStep>();
    }

    public async Task<bool> DeleteStepsAsync(Guid flowId, Guid stepId)
    {
        var result = await Connection.Filter<Flow>()
            .Eq(x => x.Id, flowId)
            .Update
            .PullFilter(x => x.Steps, Builders<FlowStep>.Filter.Eq(s => s.Id, stepId))
            .UpdateAndGetOneAsync();

        return result != null;
    }

    public async Task<FlowStep> UpdateStepAsync(Guid flowId, Guid stepId, FlowStep step)
    {
        var flowStep = Connection.Map<FlowStep>(step);
        flowStep.Id = stepId;

        var any = -1;
        var result = await Connection.Filter<Flow>()
            .Eq(x => x.Id, flowId)
            .Where(x => x.Steps.Any(s => s.Id == flowStep.Id))
            .Update
            .Set(x => x.Steps[any], flowStep)
            .UpdateAndGetOneAsync();

        return result?.Steps.FirstOrDefault(x => x.Id == stepId);
    }

    // public async Task<IFlowTransition> GetAsync(Guid entityId, Guid flowId, string tag)
    // {
    //     var flow = await Connection.Filter<Flow>()
    //         .Eq(x => x.Id, flowId)
    //         .ElemMatchBuilder(x => x.Transitions,
    //             f => f.Eq(t => t.EntityId, entityId)
    //                 .Eq(t => t.Tag, tag)
    //             // Builders<FlowTransition>.Filter.Eq(t => t.EntityId, entityId) &
    //             // Builders<FlowTransition>.Filter.Eq(t => t.Tag, tag)
    //         )
    //         .FirstOrDefaultAsync();
    //
    //     return flow?.Transitions.FirstOrDefault(x => x.EntityId == entityId && string.Equals(x.Tag, tag));
    // }

    // public async Task<IFlowTransition> AddAsync(IEntityContext context, IFlowTransition transition)
    // {
    //     var existing = await GetAsync(transition.EntityId, transition.CurrentFlowId, transition.Tag);
    //     if (existing != null)
    //     {
    //         if (existing.TargetFlowId == transition.TargetFlowId) return existing;
    //         // TODO: update
    //         // ...
    //         throw new NotImplementedException("update existing transition");
    //     }
    //
    //     var dao = Connection.Map<FlowTransition>(transition);
    //     var flow = await Connection.Filter<Flow>()
    //         .Eq(x => x.Id, transition.CurrentFlowId)
    //         .Update
    //             .Push(x => x.Transitions, dao)
    //         .UpdateAndGetOneAsync();
    //
    //     return flow != null ? transition : null;
    // }

    // public async Task<bool> DeleteAsync(IEntityContext context, Guid entityId, Guid flowId, string tag)
    // {
    //     var result = await Connection.Filter<Flow>()
    //         .Eq(x => x.Id, flowId)
    //         .Update
    //         .PullFilter(x => x.Transitions,
    //             Builders<FlowTransition>.Filter.Eq(t => t.EntityId, entityId) &
    //             Builders<FlowTransition>.Filter.Eq(t => t.Tag, tag)
    //         )
    //         .UpdateAndGetOneAsync();
    //
    //     return result != null;
    // }
}