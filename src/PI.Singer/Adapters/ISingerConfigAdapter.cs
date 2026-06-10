using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Models;
using PI.Shared.Models;

namespace Adapters
{
    public interface ISingerConfigAdapter
    {
        Task<SingerImportConfig> GetByIdAsync(Guid id);
        Task<SingerImportConfig> GetDefaultForAccountAsync(Guid accountId);
        Task<SingerJob> InitExtractAsync(SingerImportConfig config);
        Task AddToExtractLogAsync(Guid id, string message);
        Task AddAsync(Guid id, SingerMetricMessage metric);
        Task EndExtractAsync(SingerJob extract);
        Task<SingerJob> MarkLoadStartAsync(Guid configId, string tag);
        Task UpdateAsync(Guid id, SingerState state);
        Task<SingerJob> MarkLoadCompleteAsync(Guid id, SingerState state);
        Task LogAsync(SingerLoadingLog log);
        Task<IEnumerable<SingerImportConfig>> GetAsync(IEntityContext context);
        Task<SingerJob> GetJobByIdAsync(Guid id);
        Task<IEnumerable<SingerJobSummary>> GetJobSummaryAsync(Guid id);        
    }
}