using System;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using PI.Shared.Integrations.Delivery;
using PI.Shared.Integrations.DependencyInjection;
using PI.Shared.Models;
using PI.Shared.Services;
using Serilog;
using Webhooks.Models;

namespace PI.Webhooks;

public class Program : MicroserviceApp
{
    protected override string Name => "Webhooks";

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
            // generic webhook subscriptions into the webhooks.Subscription collection.
            .AddIntegrationServices<WebhookSubscription>(Configuration)
            ;

        // Generic REST Hook delivery: listen to object events, deliver, retry.
        AddLifetimeService<WebhookEventListener>(services);
        AddLifetimeService<WebhookDeliveryWorker>(services);
        AddLifetimeService<WebhookOutboxReconciler>(services);
    }

    protected override void AddPolicies(AuthorizationOptions options)
    {
        base.AddPolicies(options);

        options.AddPolicy("webhooks", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Root))
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireScope("webhooks")
        );
    }
}
