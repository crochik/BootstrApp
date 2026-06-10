using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using Serilog;
using Ingress.DependencyInjection;

namespace Ingress;

/// <summary>
/// Inbound webhook receiver. A single dynamic endpoint (<c>/ingress/{uuid}</c>) accepts
/// deliveries from external providers and runs them through the configuration-driven engine.
/// Follows the standard <see cref="MicroserviceApp"/> pattern (logging, config, Swagger,
/// lifetime) while keeping its own validate → handshake → parse → handle → respond pipeline.
/// </summary>
public class Program : MicroserviceApp
{
    protected override string Name => "Ingress";

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
                // Pure web service — no background consumers. The job mode is supported for
                // pattern parity but simply runs the host with no hosted services.
                var builder = new Program().RunJob(args);
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

        services.AddWebhookEngine();

        // Register custom IWebhookHandler implementations here, e.g.:
        //   services.AddWebhookHandler<MyOrderCreatedHandler>();
    }

    protected override void Use(IApplicationBuilder app)
    {
        // Buffer the request body so the controller can read the exact raw bytes even when
        // MVC has already consumed the stream for form model binding. Required for signature
        // schemes (HMAC, Twilio, ECDSA) that sign the raw body.
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next();
        });

        base.Use(app);
    }
}
