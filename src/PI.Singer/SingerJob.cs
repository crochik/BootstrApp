using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using PI.Shared.Models;
using PI.Shared.Services;
using Services;

namespace PI.Singer;

public class SingerJob : IRunJob
{
    public string Name => "Singer";

    private readonly SingerService _service;

    public SingerJob(
        IDataProtectionProvider dataProtectionProvider,
        SingerService service
    )
    {
        _service = service;

        dataProtectionProvider.ConfigureMongo();
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        await _service.ProcessAccountAsync(context);
            
        return new JobResult
        {
            Message = "Sync Complete"
        };
    }
}