using Microsoft.AspNetCore.Mvc;
using PI.K8S.Services;
using k8s.Models;

namespace PI.K8S.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CronJobController : ControllerBase
{
    private readonly IKubernetesService _kubernetesService;
    private readonly ILogger<CronJobController> _logger;

    public CronJobController(IKubernetesService kubernetesService, ILogger<CronJobController> logger)
    {
        _kubernetesService = kubernetesService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CronJobResponse>>> GetCronJobs([FromQuery] string? @namespace = null)
    {
        try
        {
            var cronJobs = await _kubernetesService.GetCronJobsAsync(@namespace);
            var response = cronJobs.Select(cronJob => new CronJobResponse
            {
                Name = cronJob.Metadata.Name,
                Namespace = cronJob.Metadata.NamespaceProperty ?? "default",
                Schedule = cronJob.Spec.Schedule,
                Suspended = cronJob.Spec.Suspend ?? false,
                LastScheduleTime = cronJob.Status?.LastScheduleTime,
                CreationTimestamp = cronJob.Metadata.CreationTimestamp
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cron jobs from namespace {Namespace}", @namespace ?? "default");
            return StatusCode(500, new { error = "Failed to retrieve cron jobs", details = ex.Message });
        }
    }

    [HttpPost("{cronJobName}/run")]
    public async Task<ActionResult<RunCronJobResponse>> RunCronJobImmediately(
        string cronJobName, 
        [FromQuery] string? @namespace = null)
    {
        try
        {
            _logger.LogInformation("API request to run cron job {CronJobName} immediately", cronJobName);
            
            await _kubernetesService.RunCronJobImmediatelyAsync(cronJobName, @namespace);
            
            return Ok(new RunCronJobResponse
            {
                CronJobName = cronJobName,
                Namespace = @namespace ?? "default",
                Status = "Success",
                Message = $"Cron job '{cronJobName}' executed successfully",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running cron job {CronJobName} immediately", cronJobName);
            return StatusCode(500, new RunCronJobResponse
            {
                CronJobName = cronJobName,
                Namespace = @namespace ?? "default",
                Status = "Failed",
                Message = ex.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IEnumerable<JobResponse>>> GetJobs([FromQuery] string? @namespace = null)
    {
        try
        {
            var jobs = await _kubernetesService.GetJobsAsync(@namespace);
            var response = jobs.Select(job => new JobResponse
            {
                Name = job.Metadata.Name,
                Namespace = job.Metadata.NamespaceProperty ?? "default",
                Status = GetJobStatus(job),
                CreationTimestamp = job.Metadata.CreationTimestamp,
                CompletionTime = job.Status?.CompletionTime,
                Failed = job.Status?.Failed ?? 0,
                Succeeded = job.Status?.Succeeded ?? 0
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting jobs from namespace {Namespace}", @namespace ?? "default");
            return StatusCode(500, new { error = "Failed to retrieve jobs", details = ex.Message });
        }
    }

    private static string GetJobStatus(V1Job job)
    {
        var completedCondition = job.Status?.Conditions?.FirstOrDefault(c => c.Type == "Complete" && c.Status == "True");
        if (completedCondition != null)
            return "Completed";

        var failedCondition = job.Status?.Conditions?.FirstOrDefault(c => c.Type == "Failed" && c.Status == "True");
        if (failedCondition != null)
            return "Failed";

        return "Running";
    }
}

public class CronJobResponse
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public bool Suspended { get; set; }
    public DateTime? LastScheduleTime { get; set; }
    public DateTime? CreationTimestamp { get; set; }
}

public class JobResponse
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreationTimestamp { get; set; }
    public DateTime? CompletionTime { get; set; }
    public int Failed { get; set; }
    public int Succeeded { get; set; }
}

public class RunCronJobResponse
{
    public string CronJobName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}