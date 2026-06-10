using System.Threading;
using System.Threading.Tasks;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services.Jobs;

public class BulkEmailSendJob : IRunJob
{
    public string Name => "BulkEmailSend";

    private readonly BulkEmailService _service;

    public BulkEmailSendJob(BulkEmailService service)
    {
        _service = service;
    }
    
    public Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        return _service.QueueAsync(context, stoppingToken);
    }
}