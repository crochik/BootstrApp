using System;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using PI.Shared.Services;
using Serilog;

namespace Qvinci;

public class Program : MicroserviceApp
{
    protected override string Name => "Qvinci";

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

        services.AddSingleton<Client>();
        services.Configure<Config>(Configuration.GetSection("Qvinci"));

        services.AddSyncService()
            .AddSingleton<IRunJob, QvinciExtractJob>();
    }

    protected override void AddPolicies(AuthorizationOptions options)
    {
        base.AddPolicies(options);

        // partner
        options.AddPolicy("partner", policy => policy
            .RequireClaim(JwtClaimTypes.ClientId)
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireClaim("client_account_id")
            .RequireScope("partner")
        );
    }
}