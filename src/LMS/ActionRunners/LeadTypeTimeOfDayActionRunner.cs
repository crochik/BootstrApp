using System;
using System.Threading.Tasks;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Services.ActionRunners;

namespace LMS.ActionRunners;

public class LeadTypeTimeOfDayActionRunner : AbstractRunner<LeadTypeTimeOfDayActionOptions>
{
    public override Guid ActionId => ActionIds.LeadTypeTimeOfDay;
    
    protected override ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, LeadTypeTimeOfDayActionOptions options)
    {
        throw new NotImplementedException();
    }
}