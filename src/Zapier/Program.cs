using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Integrations.ActionRunners;
using PI.Shared.Integrations.Delivery;
using PI.Shared.Integrations.DependencyInjection;
using PI.Shared.Models;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;
using Serilog;
using Zapier.Models;
using Zapier.Services;

namespace PI.Zapier;

public class Program : MicroserviceApp
{
    protected override string Name => "Zapier";

    
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
            // Catalog, subscriptions and the durable signed-delivery pipeline, persisting
            // Zapier subscriptions into the zapier.Subscription collection.
            .AddIntegrationServices<ZapierSubscription>(Configuration)
            ;

        
        // AddLifetimeService<WebhookEventListener>(services);
        services.AddSingleton<ActionRunnerService>()
            .AddRunner<FireWebhookActionRunner>()
            ;
        
        AddLifetimeService<ActionRunnerFlowService>(services)
            .Configure<ActionRunnerFlowServiceOptions>(options =>
            {
                options.ActionIds =
                [
                    ActionIds.FireWebhook,
                ];
            });
        
        // Generic REST Hook delivery: listen to object events, deliver, retry.
        AddLifetimeService<WebhookDeliveryWorkerService>(services);
        AddLifetimeService<WebhookOutboxReconcilerService>(services);

        // HttpCallOut flow action (unrelated to Zapier subscriptions).
        AddLifetimeService<HttpCallOutService>(services);
    }

    protected override IDictionary<string, string> SwaggerScopes => new Dictionary<string, string>
    {
        { "zapier", "Zapier" },
    };

    protected override void AddPolicies(AuthorizationOptions options)
    {
        base.AddPolicies(options);

        options.AddPolicy("zapier", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Root))
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireScope("zapier")
        );
    }
}