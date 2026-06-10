using k8s;
using k8s.Models;

namespace PI.K8S.Services;

public interface IKubernetesService
{
    Task<IList<V1CronJob>> GetCronJobsAsync(string? namespaceParameter = null);
    Task RunCronJobImmediatelyAsync(string cronJobName, string? namespaceParameter = null);
    Task<V1Job> GetJobAsync(string jobName, string? namespaceParameter = null);
    Task<IList<V1Job>> GetJobsAsync(string? namespaceParameter = null);
}

public class KubernetesService : IKubernetesService
{
    private readonly IKubernetes _kubernetes;
    private readonly ILogger<KubernetesService> _logger;

    public KubernetesService(IKubernetes kubernetes, ILogger<KubernetesService> logger)
    {
        _kubernetes = kubernetes;
        _logger = logger;
    }

    public async Task<IList<V1CronJob>> GetCronJobsAsync(string? namespaceParameter = null)
    {
        try
        {
            var cronJobs = await _kubernetes.BatchV1.ListNamespacedCronJobAsync(
                namespaceParameter: namespaceParameter ?? "default");
            
            _logger.LogInformation("Found {Count} cron jobs in namespace {Namespace}", 
                cronJobs.Items.Count, namespaceParameter ?? "default");
            
            return cronJobs.Items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cron jobs from namespace {Namespace}", 
                namespaceParameter ?? "default");
            throw;
        }
    }

    public async Task RunCronJobImmediatelyAsync(string cronJobName, string? namespaceParameter = null)
    {
        var ns = namespaceParameter ?? "default";
        
        try
        {
            _logger.LogInformation("Starting immediate execution of cron job {CronJobName} in namespace {Namespace}", 
                cronJobName, ns);

            var cronJob = await GetCronJobAsync(cronJobName, ns);
            var originalSuspendState = cronJob.Spec.Suspend ?? false;

            await PauseCronJobAsync(cronJobName, ns);
            
            var job = await CreateJobFromCronJobAsync(cronJob);
            
            await WaitForJobCompletionAsync(job);
            
            if (!originalSuspendState)
            {
                await ResumeCronJobAsync(cronJobName, ns);
            }

            _logger.LogInformation("Successfully completed immediate execution of cron job {CronJobName}", cronJobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running cron job {CronJobName} immediately", cronJobName);
            throw;
        }
    }

    public async Task<V1Job> GetJobAsync(string jobName, string? namespaceParameter = null)
    {
        try
        {
            var job = await _kubernetes.BatchV1.ReadNamespacedJobAsync(
                name: jobName,
                namespaceParameter: namespaceParameter ?? "default");
            
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job {JobName} from namespace {Namespace}", 
                jobName, namespaceParameter ?? "default");
            throw;
        }
    }

    public async Task<IList<V1Job>> GetJobsAsync(string? namespaceParameter = null)
    {
        try
        {
            var jobs = await _kubernetes.BatchV1.ListNamespacedJobAsync(
                namespaceParameter: namespaceParameter ?? "default");
            
            _logger.LogInformation("Found {Count} jobs in namespace {Namespace}", 
                jobs.Items.Count, namespaceParameter ?? "default");
            
            return jobs.Items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving jobs from namespace {Namespace}", 
                namespaceParameter ?? "default");
            throw;
        }
    }

    private async Task<V1CronJob> GetCronJobAsync(string cronJobName, string namespaceParameter)
    {
        try
        {
            var cronJob = await _kubernetes.BatchV1.ReadNamespacedCronJobAsync(
                name: cronJobName,
                namespaceParameter: namespaceParameter);
            
            return cronJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cron job {CronJobName} from namespace {Namespace}", 
                cronJobName, namespaceParameter);
            throw;
        }
    }

    private async Task PauseCronJobAsync(string cronJobName, string namespaceParameter)
    {
        try
        {
            var cronJob = await GetCronJobAsync(cronJobName, namespaceParameter);
            
            if (cronJob.Spec.Suspend == true)
            {
                _logger.LogInformation("Cron job {CronJobName} is already paused", cronJobName);
                return;
            }

            cronJob.Spec.Suspend = true;
            
            await _kubernetes.BatchV1.PatchNamespacedCronJobAsync(
                new V1Patch(cronJob, V1Patch.PatchType.MergePatch),
                name: cronJobName,
                namespaceParameter: namespaceParameter);
            
            _logger.LogInformation("Paused cron job {CronJobName}", cronJobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing cron job {CronJobName}", cronJobName);
            throw;
        }
    }

    private async Task ResumeCronJobAsync(string cronJobName, string namespaceParameter)
    {
        try
        {
            var cronJob = await GetCronJobAsync(cronJobName, namespaceParameter);
            cronJob.Spec.Suspend = false;
            
            await _kubernetes.BatchV1.PatchNamespacedCronJobAsync(
                new V1Patch(cronJob, V1Patch.PatchType.MergePatch),
                name: cronJobName,
                namespaceParameter: namespaceParameter);
            
            _logger.LogInformation("Resumed cron job {CronJobName}", cronJobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming cron job {CronJobName}", cronJobName);
            throw;
        }
    }

    private async Task<V1Job> CreateJobFromCronJobAsync(V1CronJob cronJob)
    {
        try
        {
            var jobName = $"{cronJob.Metadata.Name}-manual-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            
            var job = new V1Job
            {
                ApiVersion = "batch/v1",
                Kind = "Job",
                Metadata = new V1ObjectMeta
                {
                    Name = jobName,
                    NamespaceProperty = cronJob.Metadata.NamespaceProperty,
                    Labels = new Dictionary<string, string>
                    {
                        ["app"] = cronJob.Metadata.Name,
                        ["manual-run"] = "true"
                    }
                },
                Spec = new V1JobSpec
                {
                    Template = cronJob.Spec.JobTemplate.Spec.Template,
                    BackoffLimit = cronJob.Spec.JobTemplate.Spec.BackoffLimit,
                    ActiveDeadlineSeconds = cronJob.Spec.JobTemplate.Spec.ActiveDeadlineSeconds
                }
            };

            var createdJob = await _kubernetes.BatchV1.CreateNamespacedJobAsync(
                body: job,
                namespaceParameter: cronJob.Metadata.NamespaceProperty ?? "default");
            
            _logger.LogInformation("Created manual job {JobName} from cron job {CronJobName}", 
                jobName, cronJob.Metadata.Name);
            
            return createdJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job from cron job {CronJobName}", cronJob.Metadata.Name);
            throw;
        }
    }

    private async Task WaitForJobCompletionAsync(V1Job job)
    {
        try
        {
            _logger.LogInformation("Waiting for job {JobName} to complete", job.Metadata.Name);
            
            var timeout = TimeSpan.FromMinutes(30);
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var currentJob = await _kubernetes.BatchV1.ReadNamespacedJobAsync(
                    name: job.Metadata.Name,
                    namespaceParameter: job.Metadata.NamespaceProperty ?? "default");
                
                if (IsJobCompleted(currentJob))
                {
                    var status = GetJobStatus(currentJob);
                    _logger.LogInformation("Job {JobName} completed with status: {Status}", 
                        job.Metadata.Name, status);
                    
                    await ProcessCompletedJobAsync(currentJob);
                    return;
                }
                
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);
            }
            
            _logger.LogWarning("Job {JobName} did not complete within timeout", job.Metadata.Name);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job {JobName} completion wait was cancelled or timed out", job.Metadata.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for job {JobName} completion", job.Metadata.Name);
            throw;
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