using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.OpenAPI.Controllers;
using PI.OpenAPI.Services;
using PI.OpenAPI.Services.Jobs;
using PI.Shared.App;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using PI.Shared.Services.OpenApiGenerator;
using Serilog;

namespace PI.OpenAPI;

public class Program : MicroserviceApp
{
    protected override string Name => "OpenAPI";
    
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
            .AddSingleton<RemoteFileService>()
                .AddSingleton<IRemoteFileServiceProvider, AwsS3RemoteFileServiceProvider>()
                .AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>()
            .AddSingleton<IntegrationAuthService>()
            ;

        services.AddSingleton<GitHubService>();
        
        AddLifetimeService<ActionService>(services);
        
        services.AddSyncService()
            .AddSingleton<IRunJob, ExportObjectTypesJob>()
            .AddSingleton<IRunJob, ImportObjectTypesJob>()
            ;
        
        services.AddTransient<OpenApiParser>();
        services.AddTransient<OpenApiSpecGenerator>();
    }
}