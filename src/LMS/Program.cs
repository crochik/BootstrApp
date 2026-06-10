using System;
using System.Threading.Tasks;
using LMS.ActionRunners;
using LMS.Handlers;
using LMS.Services;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.App;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;
using PI.Shared.Services.DataProtection;
using Serilog;

namespace LMS;

public class Program : MicroserviceApp
{
    protected override string Name => "LMS";

    public static async Task<int> Main(string[] args)
    {
        Serilog.Debugging.SelfLog.Enable(Console.Error);
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting...");
            await new Program().RunWebApplication(args);
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
            .AddSingleton<AuthorizationService>()
            .AddSingleton<AppointmentSchedulerService>()
            .AddObjectTypeService()
            .AddLeadBuilderService()
            ;
        
        services.AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>();
        
        services.AddSingleton<NewLeadService>();

        services.AddSingleton<IResponseWriter, HttpStatusResponseWriter>();
        
        // handlers
        // TODO: refactor so we don't need them anymore
        // and can seed the flow with the leadType object and fire events for lmsTransaction 
        // ....
        services.AddSingleton<INewLeadHandler, SaveRequestHandler>();
        services.AddSingleton<INewLeadHandler, LoadLeadTypeHandler>();
        services.AddSingleton<INewLeadHandler, Version2InterceptorHandler>();
        
        // action runners
        services.AddSingleton<ActionRunnerService>()
            .AddFlowActionBuilders()
            .AddRunner<CreateObjectActionRunner>()
            .AddRunner<CreateObjectUsingFormActionRunner>()
            .AddRunner<FireEventActionRunner>()
            .AddRunner<UpdateObjectActionRunner>()
            .AddRunner<TagObjectActionRunner>()
            .AddRunner<SetObjectStatusActionRunner>()
            .AddRunner<LookupObjectActionRunner>()
            .AddRunner<ConditionalActionRunner>()
            .AddRunner<SwitchActionRunner>()
            // LMS only
            .AddRunner<DuplicatedLeadCheckActionRunner>()
            .AddRunner<LeadTypeServiceUsageActionRunner>()
            .AddRunner<LeadTypeTimeOfDayActionRunner>()
            .AddRunner<TrustedFormCertActionRunner>()
            ;
    }
}

