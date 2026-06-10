using System;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using PI.Shared.Models;
using PI.Shared.Services;
using Serilog;
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
            // .AddSingleton<PI.Shared.Services.AuthorizationService>()
            // .AddSingleton<PI.Shared.Services.AppointmentSchedulerService>()
            .AddObjectTypeService()
            ;
        
        AddLifetimeService<WebhookService>(services);        
    }

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