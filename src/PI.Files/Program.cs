using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Files.Services;
using PI.Files.Services.Jobs;
using PI.Shared.App;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Serilog;

namespace PI.Files;

public class Program : MicroserviceApp
{
    protected override string Name => "Files";

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
            .AddRemoteFileService()
            .AddObjectTypeService()
            .AddSyncService()
                .AddSingleton<IRunJob, ScanS3FilesJob>()
                .AddSingleton<IRunJob, ImportObjectsJob>()
                .AddSingleton<IRunJob, ImportBusinessPlanJob>()
            ;

        services.AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>();

        // used by the message service
        services.AddTransient<ImportObjectsJob>();
        
        AddLifetimeService<CopyFileActionService>(services);
        AddLifetimeService<ImportObjectsActionService>(services);

        // run actions synchronously 
        // services
        //     .AddSingleton<UserActionService, UserActionWithRunnersService>()
        //     .AddSingleton<ActionRunnerService>()
        //     .AddRunner<CreateObjectActionRunner>()
        //     .AddRunner<CreateObjectUsingFormActionRunner>()
        //     .AddRunner<UpdateObjectActionRunner>()
        //     .AddRunner<LookupObjectActionRunner>()
        //     .AddRunner<SwitchActionRunner>()
        //     .AddRunner<ConditionalActionRunner>()
        //     .AddRunner<TagObjectActionRunner>()
        //     .AddRunner<SetObjectStatusActionRunner>()
        //     // files only
        //     .AddRunner<GetPresignedUrlActionRunner>()
        //     ;
        
        // ExcelDataReader
        // https://github.com/crochik/ExcelDataReader
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }
}