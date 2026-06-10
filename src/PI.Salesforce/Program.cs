using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Salesforce.Models;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Serilog;
using Services;
using Services.ActionRunners;
using Services.Jobs;

namespace PI.Salesforce;

public class Program : MicroserviceApp
{
    protected override string Name => "Salesforce";

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

        services.AddRazorPages(c => { });

        services
            .AddSalesforceService()
            .AddRemoteFileService()
            .AddObjectTypeService()
            .AddSingleton<SalesforceLeadService>()
            .AddSingleton<AuthorizationService>()
            .AddDataLoader()
            .AddOnChangeProcessors()
            .AddLeadBuilderService()
            ;

        services.AddSyncService()
            .AddSingleton<IRunJob, AssignWorkOrdersJob>()
            .AddSingleton<IRunJob, ExportProposalsJob>()
            ;

        AddLifetimeService<ActionService>(services);
        AddLifetimeService<GenerateQbFileActionService>(services);
        AddLifetimeService<PatchObjectsService>(services);
        AddLifetimeService<MarketingCloudService>(services);
        // AddLifetimeService<Stream.SubscriberService>(services);

        services.AddSingleton<InstallationMapLoader>();

        services.AddSingleton<MarketingCloudClient>();

        services.AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>();

        services.AddTransient<HybridSalesforceObjectEditor>();

        // TODO: add if we decide to process other runners here or use ActionRunnerService
        // services.AddSingleton<ActionRunnerService>()
        // .AddFlowActionBuilders()
        // .AddRunner<CreateObjectActionRunner>()
        // .AddRunner<CreateObjectUsingFormActionRunner>()
        // .AddRunner<UpdateObjectActionRunner>()
        // .AddRunner<TagObjectActionRunner>()
        // .AddRunner<SetObjectStatusActionRunner>()
        // .AddRunner<LookupObjectActionRunner>()
        // .AddRunner<ConditionalActionRunner>()
        // .AddRunner<SwitchActionRunner>()
        // ;

        // Salesforce only
        services.AddRunner<CreateSalesforceObjectActionRunner>()
            .AddRunner<UpdateSalesforceObjectActionRunner>()
            ;
        
        AddLifetimeService<ActionRunnerFlowService>(services)
            .Configure<ActionRunnerFlowServiceOptions>(options =>
            {
                options.ActionIds =
                [
                    ActionIds.CreateSalesforceObject,
                    ActionIds.UpdateSalesforceObject,
                ];
            })
            ;
    }

    protected override void UseEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.UseEndpoints(endpoints);

        endpoints.MapRazorPages();
    }

    protected override void StartServices(IServiceProvider services)
    {
        base.StartServices(services);

        // hack for now
        MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(typeof(PI.Shared.Salesforce.Models.SalesforceToken));
    }
}