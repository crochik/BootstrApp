using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.O365.Extensions;
using Services;
using PI.Shared.App;
using AutoMapper;
using Microsoft.Extensions.Hosting;
using PI.Shared.O365;
using PI.Shared.Services;
using Serilog;

namespace O365;

public class Program : MicroserviceApp
{
    protected override string Name => "O365";

    public static async Task<int> Main(string[] args)
    {
        Serilog.Debugging.SelfLog.Enable(Console.Error);
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting...");

            if (IsWebApi)
            {
                await new Program().RunWebApplication(args);
            }
            else
            {
                // job
                var builder = new Program().RunJob(args);
                
                builder.Services.AddHostedService<JobService>();
                
                var app = builder.Build();
                await app.RunAsync();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            await Console.Error.WriteLineAsync(ex.Message);
            Console.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }   

    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);

        services
            .AddObjectTypeService()
            ;
            
        services
            .AddSingleton<O365AuthClient>()
            .AddSingleton<O365Service>()
            .AddSingleton<UserActionService>()
            .AddSingleton<AppointmentSchedulerService>()
            .AddSyncService()
            .AddSingleton<IRunJob, LoadEventsJob>()
            .AddSingleton<IRunJob, RenewSubsriptionsJob>()
            .AddSingleton<IRunJob, ImportUsersJob>()
            .AddSingleton<IRunJob, AvailabilityFromSalesforceJob>()
            .AddSingleton<IRunJob, CalculateAvailabilityJob>()
            ;

        AddLifetimeService<O365CalendarService>(services);
        AddLifetimeService<ActionService>(services);
    }

    protected override void ConfigureMapper(IMapperConfigurationExpression cfg)
    {
        base.ConfigureMapper(cfg);
            
        cfg.AddCalendar();
    }        
}