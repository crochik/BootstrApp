using System;
using System.Threading.Tasks;

namespace PI.Shared.Services
{
    public interface IUrlService
    {
        Task<string> GetSchedulerUrlAsync(Guid leadId);
        Guid? UnprotectLeadId(string id);
    }
}