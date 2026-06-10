using System.Threading;
using System.Threading.Tasks;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class ExportLeadsJob : IRunJob
{
    private readonly VerseService _service;

    public string Name => "verse.ExportLeads";

    public ExportLeadsJob(VerseService service)
    {
        this._service = service;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        return await _service.SendLeadsAsync(context);
    }
}