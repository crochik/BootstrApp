using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PI.Shared.App;
using PI.Shared.Services;
using Serilog;
using Services;

namespace PI.Stripe;

public class Program : MicroserviceApp
{
    protected override string Name => "Stripe";

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

        AddLifetimeService<StripeService>(services);

        // BsonClassMap.RegisterClassMap<Form.Models.MenuItem>(cm =>
        // {
        //     cm.AutoMap();
        //     cm.SetDiscriminatorIsRequired(true);
        // });
    }
}