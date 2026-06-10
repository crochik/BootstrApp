using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Crochik.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using PI.U2.Middleware;
using Serilog;

namespace PI.U2;

public class Program
{
    private string Name => "U2";
    
    private IConfigurationRoot Configuration { get; set; }
    
    // public static void Main(string[] args)
    // {
    //     WebHostBuilder(args).Build().Run();
    // }
    
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

    private async Task RunWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        InitConfiguration(builder.Configuration);
        builder.Host.UseElasticSearchLogging(Name);
        ConfigureServices(builder.Services);

        var app = builder.Build();

        var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        Configure(app, app.Environment, appLifetime);

        await app.RunAsync();
    }

    private void InitConfiguration(IConfigurationManager configurationManager)
    {
        configurationManager.AddJsonFile("/pi/settings/appsettings.json", optional: true);
        Configuration = configurationManager.Build();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            UseCookies = false
        });

        services.AddDataProtection(Configuration);
        services.AddMongoConnection();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    // only called for the webhostbuilder
    public virtual void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime lifetime
    )
    {
        lifetime.ApplicationStarted.Register(() => { Log.Logger.Information("Application started..."); });

        lifetime.ApplicationStopping.Register(() =>
        {
            Log.Logger.Information("Application stopping...");
            Thread.Sleep(10000);
        });

        lifetime.ApplicationStopped.Register(() =>
        {
            Log.Logger.Information("Application stopped");
            Log.CloseAndFlush();
        });

        Use(app);
    }

    private void Use(IApplicationBuilder app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto // ForwardedHeaders.All
        });

        app.UseMiddleware<RedirectMiddleware>();
    }
}