using System;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Services;
using Serilog;
using Services;

namespace PI.Convertros;

public class Program : MicroserviceApp
{
    protected override string Name => nameof(IntegrationIds.Convertros);
    
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
            .AddSingleton<ConvertrosService>()
            .AddSingleton<ILeadConversionIntegrationService, ConvertrosService>()
            .AddSingleton<AuthorizationService>()
            .AddSingleton<AppointmentSchedulerService>()
            ;

        AddLifetimeService<LeadConversionIntegrationMonitorService>(services);
    }

    protected override void AddPolicies(AuthorizationOptions options)
    {
        base.AddPolicies(options);

        // partner 
        options.AddPolicy("integration", policy => policy
            .RequireClaim(JwtClaimTypes.ClientId, Name)
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireClaim("client_account_id")
            .RequireScope("partner")
        );

        options.AddPolicy("integration-lead", policy => policy
            .RequireClaim(JwtClaimTypes.ClientId, Name)
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireClaim("client_account_id")
            .RequireClaim("pi_lead_id")
            .RequireScope("partner")
        );
    }
}

