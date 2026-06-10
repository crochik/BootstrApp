using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IModelAdapter<T> where T : IRow<Guid>
    {
        Task<T> CreateAsync(T entity);
        Task<bool> UpdateAsync(T obj);
        Task<bool> DeleteAsync(Guid id);
        Task<T> GetByIdAsync(Guid value);
        Task<IEnumerable<T>> GetTrunkAsync(IEntityContext context);
    }

    public interface ILeadStatusAdapter : IModelAdapter<ILeadStatus>
    {
    }
    
    public interface IFlowAdapter : IModelAdapter<IFlow>
    {
        // Task<IEnumerable<FlowStep>> GetStepsAsync(Guid flowId, Guid eventId, Guid? leadStatusId);
        // Task<IEnumerable<FlowStep>> GetStepsAsync(Guid id);
        //
        // Task<FlowStep> AddAsync(Guid flowId, FlowStep step);
        Task<bool> DeleteStepsAsync(Guid flowId, Guid stepId);
        // Task<FlowStep> UpdateStepAsync(Guid id, Guid stepId, FlowStep step);
    }

    public interface IEventTypeAdapter : IModelAdapter<IEventType>
    {
    }
}