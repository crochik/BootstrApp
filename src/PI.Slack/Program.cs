using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.App;
using Serilog;
using Services;

namespace PI.Slack;

public class Program : MicroserviceApp
{
    protected override string Name => "Slack";

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

        services.AddSingleton<SlackClient>();

        AddLifetimeService<SlackIntegrationService>(services);
    }
}