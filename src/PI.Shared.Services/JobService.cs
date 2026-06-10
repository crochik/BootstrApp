using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Models;
using Serilog;

namespace PI.Shared.Services;

public class JobResult
{
    public string Message { get; set; }
    public Dictionary<string, object> Result { get; set; }
}

public interface IRunJob
{
    string Name { get; }
    Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken);
}

public class JobService : BackgroundService
{
    private readonly ILogger<JobService> _logger;
    private readonly IHostApplicationLifetime _host;
    private readonly IConfiguration _configuration;
    private readonly JobStatusService _jobStatusService;
    // private readonly IAPMService _apm;
    private readonly IEnumerable<IRunJob> _jobs;

    public JobService(
        ILogger<JobService> logger,
        IHostApplicationLifetime host,
        IConfiguration configuration,
        JobStatusService jobStatusService,
        // IAPMService apm,
        IEnumerable<IRunJob> jobs
    )
    {
        _logger = logger;
        _host = host;
        _configuration = configuration;
        _jobStatusService = jobStatusService;
        // _apm = apm;
        _jobs = jobs;

        host.ApplicationStarted.Register(() =>
        {
            Log.Logger.Information("Application Started");
        });

        host.ApplicationStopping.Register(() =>
        {
            Log.Logger.Information("Application stopping...");
        });

        host.ApplicationStopped.Register(() =>
        {
            Log.Logger.Information("Application stopped");
            Log.CloseAndFlush();
            Thread.Sleep(5000);
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: load from config and allow to be root
        // ...
        IEntityContext context = new AccountContext(AccountIds.FCI);

        var jobName = _configuration.GetValue<string>("PI_RUN_JOB");
        using var scope = _logger.AddScope(new
        {
            context.AccountId,
            JobName = jobName,
        });

        try
        {
            // using var apm = _apm.StartTransaction("ScheduledJob", jobName);
            // apm.Context = new
            // {
            //     context.AccountId,
            // };

            _logger.LogInformation("Start Job");
            foreach (var job in _jobs.Where(x => x.Name == jobName))
            {
                await ExecuteAsync(context, job, stoppingToken);
            }
            _logger.LogInformation("End Job");
        }
        catch (Exception ex)
        {
            // _apm.SetResult($"Failed: {ex.Message}");
            Environment.ExitCode = -1;
        }
        finally
        {
            _host.StopApplication();
        }
    }

    private async Task ExecuteAsync(IEntityContext context, IRunJob job, CancellationToken stoppingToken)
    {
        var start = DateTime.UtcNow;
        var transactionId = Guid.NewGuid();

        // using var apm = _apm.StartTransaction("Job", job.GetType().FullName);

        _logger.LogInformation("Initialize {JobType}", job.GetType().FullName);

        var service = await _jobStatusService.StartAsync(context, job.Name, transactionId);
        if (service == null)
        {
            // _apm.SetResult("Failed to initiate job");
            _logger.LogError("Failed to initialize Job");

            throw new Exception("Failed to initialize job");
        }

        // apm.Context = new
        // {
        //     Start = start,
        //     ServiceId = service.Id,
        //     TransactionId = transactionId
        // };

        context = context.With(new JobActor
        {
            ServiceId = service.Id,
            TransactionId = transactionId.ToString()
        });

        using var scope = _logger.AddScope(new
        {
            context.AccountId,
            Service = service.Name,
            ServiceId = service.Id,
            TransactionId = transactionId
        });

        _logger.LogInformation("Start Execution");

        try
        {
            var result = await job.ExecuteAsync(context, stoppingToken);
            var meta = result.Result ?? new Dictionary<string, object>();
            var elapsed = DateTime.UtcNow - start;

            _logger.LogInformation("Finished Execution in {Elapsed} seconds: {Result}", elapsed.TotalSeconds, result.Message);

            await _jobStatusService.SucceededAsync(
                context,
                service,
                result.Message,
                meta,
                transactionId,
                elapsed
            );

            // _apm.SetResult(result.Message);
        }
        catch (Exception ex)
        {
            // _apm.SetResult($"Failed: {ex.Message}");

            await _jobStatusService.FailedAsync(context, service, ex, new
            {
                service.Name,
                ServiceId = service.Id,
                TransactionId = transactionId,
            });

            throw;
        }
    }
}