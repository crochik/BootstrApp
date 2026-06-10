using PI.DocuSeal.Models;
using PI.DocuSeal.Providers;
using PI.DocuSeal.Services;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Providers;
using Serilog;
using Services.ActionRunners;

namespace PI.DocuSeal;

public class Program : MicroserviceApp
{
    protected override string Name => "DocuSeal";

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
            Console.Error.WriteLine(ex.Message);
            Console.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);

        services
            .AddObjectTypeService()
            ;

        services.AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>();

        // Configure RazorLight
        // services.AddSingleton<IRazorLightEngine>(provider => new RazorLightEngineBuilder()
        //         .UseFileSystemProject(Path.Combine(Directory.GetCurrentDirectory(), "Templates"))
        //         .UseMemoryCachingProvider()
        //         .Build()
        // );
        // services.AddScoped<ITemplateProvider, RazorLightTemplateProvider>();

        services.AddScoped<ITemplateProvider, HandlebarsTemplateProvider>();
        services.AddScoped<HandlebarsTemplateProvider>();

        // Configure DocuSeal
        services.Configure<DocuSealConfiguration>(Configuration.GetSection(DocuSealConfiguration.SectionName));
        services.AddSingleton<DocuSealService>();

        // web hook
        services.Configure<DocuSealWebhookConfiguration>(Configuration.GetSection(DocuSealWebhookConfiguration.SectionName));
        services.AddScoped<DocuSealWebhookService>();

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
        
        // DocuSeal only
        services.AddRunner<CreateDucuSealSubmissionActionRunner>();
        
        AddLifetimeService<ActionRunnerFlowService>(services)
            .Configure<ActionRunnerFlowServiceOptions>(options =>
            {
                options.ActionIds =
                [
                    ActionIds.CreateDocuSealSubmission,
                ];
            });
    }
}