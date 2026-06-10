using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PI.CompanyCam.Services;
using PI.Shared.App;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Serilog;

namespace PI.CompanyCam;

public class Program : MicroserviceApp
{
    protected override string Name => "CompanyCam";

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

        services.AddSingleton<CompanyCamService>();
        
        AddLifetimeService<ActionService>(services);
    }
}