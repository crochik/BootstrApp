using System;
using System.Threading.Tasks;
using Adapters;
using Crochik.Mongo;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Models;
using MongoDB.Bson.Serialization;
using PI.Shared.App;
using PI.Shared.Data.Mongo;
using PI.Shared.Services;
using Serilog;
using Services;

namespace PI.Singer;

public class Program : MicroserviceApp
{
    protected override string Name => "Singer";

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
            .AddFileStorage(Configuration)
            .AddLeadBuilderService()
            .AddSalesforceService()
            .AddObjectTypeService()
            .AddSyncService()
            .AddSingleton<IRunJob, SingerJob>()
            .AddTransient<ISingerConfigAdapter, SingerConfigAdapter>()
            .AddSingleton<ExtractService>()
            .AddSingleton<ITransferService, DirectLoaderService>()
            .AddSingleton<LoaderService>()
            ;

        AddLifetimeService<SingerService>(services);
    }

    protected override void StartServices(IServiceProvider services)
    {
        // configure mongo to use data protector
        services.GetRequiredService<IDataProtectionProvider>().ConfigureMongo();

        base.StartServices(services);
    }
}

public static class AppExtensions
{
    public static void ConfigureMongo(this IDataProtectionProvider provider)
    {
        var _protector = provider.CreateProtector(typeof(MapperInitializer).FullName);
        var _encryptStringSerializer = new EncryptedStringSerializer(_protector);

        BsonClassMap.RegisterClassMap<SalesforceTapConfig>(cm =>
        {
            cm.AutoMap();
            cm.SetDiscriminatorIsRequired(true);
            cm.MapMember(c => c.RefreshToken).SetSerializer(_encryptStringSerializer);
            cm.MapMember(c => c.ClientSecret).SetSerializer(_encryptStringSerializer);
        });
    }
}