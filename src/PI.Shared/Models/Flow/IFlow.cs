using System;

namespace PI.Shared.Models
{
    public interface IFlow : IEntityOwnedModel
    {
        // Guid[] EmbeddedFlowIds { get; }

        string ObjectType { get; }

        FlowStep[] GetSteps();
    }
}