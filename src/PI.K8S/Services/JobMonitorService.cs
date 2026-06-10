using k8s;
using k8s.Models;
using PI.K8S.Services;

namespace PI.K8S.Services;

public class JobMonitorService : BackgroundService
{
    private readonly IKubernetes _kubernetes;
    private readonly ILogger<JobMonitorService> _logger;

    public JobMonitorService(IKubernetes kubernetes, ILogger<JobMonitorService> logger)
    {
        _kubernetes = kubernetes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Kubernetes Job Monitor Service");

        try
        {
            await WatchJobsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in job monitoring service");
            throw;
        }
    }

    private async Task WatchJobsAsync(CancellationToken cancellationToken)
    {
        var jobWatch = _kubernetes.BatchV1.ListNamespacedJobWithHttpMessagesAsync(
            namespaceParameter: "default", 
            watch: true,
            cancellationToken: cancellationToken);

        await foreach (var (type, job) in jobWatch.WatchAsync<V1Job, V1JobList>(cancellationToken))
        {
            if (type == WatchEventType.Modified && IsJobCompleted(job))
            {
                _logger.LogInformation("Job {JobName} completed with status: {Status}", 
                    job.Metadata.Name, GetJobStatus(job));
                
                await ProcessCompletedJobAsync(job);
            }
        }
    }

    private static bool IsJobCompleted(V1Job job)
    {
        return job.Status?.Conditions?.Any(c => 
            c.Type == "Complete" && c.Status == "True" ||
            c.Type == "Failed" && c.Status == "True") == true;
    }

    private static string GetJobStatus(V1Job job)
    {
        var completedCondition = job.Status?.Conditions?.FirstOrDefault(c => c.Type == "Complete" && c.Status == "True");
        if (completedCondition != null)
            return "Successful";

        var failedCondition = job.Status?.Conditions?.FirstOrDefault(c => c.Type == "Failed" && c.Status == "True");
        if (failedCondition != null)
            return "Failed";

        return "Unknown";
    }

    private async Task ProcessCompletedJobAsync(V1Job job)
    {
        try
        {
            var pods = await _kubernetes.CoreV1.ListNamespacedPodAsync(
                namespaceParameter: job.Metadata.NamespaceProperty ?? "default",
                labelSelector: $"job-name={job.Metadata.Name}");

            foreach (var pod in pods.Items)
            {
                await CollectPodLogsAsync(pod, job);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing completed job {JobName}", job.Metadata.Name);
        }
    }

    private async Task CollectPodLogsAsync(V1Pod pod, V1Job job)
    {
        try
        {
            var logs = await _kubernetes.CoreV1.ReadNamespacedPodLogAsync(
                name: pod.Metadata.Name,
                namespaceParameter: pod.Metadata.NamespaceProperty ?? "default");

            var isSuccessful = DetermineSuccess(job, logs);
            
            _logger.LogInformation("Job {JobName} Pod {PodName} - Success: {IsSuccessful}", 
                job.Metadata.Name, pod.Metadata.Name, isSuccessful);
            
            _logger.LogDebug("Job {JobName} Pod {PodName} Logs:\n{Logs}", 
                job.Metadata.Name, pod.Metadata.Name, logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting logs for pod {PodName} in job {JobName}", 
                pod.Metadata.Name, job.Metadata.Name);
        }
    }

    private static bool DetermineSuccess(V1Job job, string logs)
    {
        var jobSucceeded = job.Status?.Conditions?.Any(c => 
            c.Type == "Complete" && c.Status == "True") == true;

        if (!jobSucceeded)
            return false;

        var lowerLogs = logs.ToLowerInvariant();
        var hasErrorKeywords = lowerLogs.Contains("error") || 
                              lowerLogs.Contains("exception") || 
                              lowerLogs.Contains("failed") ||
                              lowerLogs.Contains("fatal");

        return !hasErrorKeywords;
    }
}