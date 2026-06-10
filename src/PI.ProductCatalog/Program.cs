using System;
using System.Threading.Tasks;
using AutoMapper;
using Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Services;
using PI.ProductCatalog.Services.ActionRunners;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.FileTransferProviders;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;
using PI.Shared.Services.DataProtection;
using Serilog;
using Services;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PI.ProductCatalog;

public class Program : MicroserviceApp
{
    protected override string Name => "ProductCatalog";

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
            .AddReportService()
            .AddSyncService()
            ;

        // remote file service
        services.AddSingleton<RemoteFileService>()
            .AddSingleton<IRemoteFileServiceProvider, AwsS3RemoteFileServiceProvider>()
            .AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>()
            ;
        
        // old file storage?
        services.AddFileStorage(Configuration);

        // jobs
        services.AddSingleton<IRunJob, Breadcrumbs2Job>()
            .AddSingleton<IRunJob, B2BSyncJob>()
            .AddSingleton<IRunJob, UpgradeItems2Job>()
            .AddSingleton<IRunJob, KeyDatesJob>()
            ;
        
        services.AddScoped<ICatalogFormat, EmserSender>()
            .AddScoped<ICatalogFormat, ShawSender>()
            .AddScoped<ICatalogFormat, DaltileSender>()
            .AddScoped<ICatalogFormat, NourisonSender>()
            .AddScoped<ICatalogFormat, MohawkSender>()
            ;

        services.AddSingleton<CatalogService>();
        services.AddSingleton<IDataService, MongoDataService>();
        services.AddScoped<CatalogParser>();
        services.AddScoped<Loader, Loader>();

        // services.AddSingleton<UserActionService>();
        
        // run user actions using runner
        services.AddSingleton<UserActionService, UserActionWithRunnersService>()
            .AddSingleton<ActionRunnerService>()
            .AddFlowActionBuilders()
            .AddRunner<CreateObjectActionRunner>()
            .AddRunner<CreateObjectUsingFormActionRunner>()
            .AddRunner<FireEventActionRunner>()
            .AddRunner<UpdateObjectActionRunner>()
            .AddRunner<LookupObjectActionRunner>()
            .AddRunner<SwitchActionRunner>()
            .AddRunner<ComposeActionRunner>()
            // implemented both ways
            .AddRunner<ConditionalActionRunner>()
            .AddRunner<TagObjectActionRunner>()
            .AddRunner<SetObjectStatusActionRunner>()
            // exclusive product catalog
            .AddRunner<CalculateSeamsActionRunner>()
            ;
        
        AddLifetimeService<ActionRunnerFlowService>(services)
            .Configure<ActionRunnerFlowServiceOptions>(options =>
            {
                options.ActionIds =
                [
                    ActionIds.CalculateSeams,
                ];
            })
            ;            
        
        // ftp, ftps, sftp, ...
        services.AddSingleton<IFileTransferProvider, FTPFileTransferProvider>()
            .AddSingleton<IFileTransferProvider, SFTPFileTransferProvider>()
            ;

        services.AddTransient<CSVFileImporter>();

        services.AddSingleton<TaxService>();
        services.AddSingleton<EstimateService>();
        services.AddSingleton<MeasureSquareService>();

        // form interceptors
        services.AddSingleton<IFormInterceptor, RoomSelectionFormInterceptor>()
            .AddSingleton<RoomSelectionFormInterceptor>();

        AddLifetimeService<MonitorService>(services);
        AddLifetimeService<StoredProcedureService>(services);

        // ExcelDataReader
        // https://github.com/crochik/ExcelDataReader
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    protected override void ConfigureMapper(IMapperConfigurationExpression cfg)
    {
        base.ConfigureMapper(cfg);

        // should it be global but may break so many things :(
        cfg.AllowNullCollections = true;
    }

    protected override void AddSwaggerGen(SwaggerGenOptions options)
    {
        base.AddSwaggerGen(options);
        
        options.DocInclusionPredicate((docName, apiDesc) =>
        {
            // 1. If the attribute [ApiExplorerSettings(GroupName = "...")] is present
            if (apiDesc.GroupName != null)
            {
                // Only include if it matches the current document being generated
                return apiDesc.GroupName.Equals(docName, StringComparison.OrdinalIgnoreCase);
            }

            // 2. If no GroupName is set, default it to your main "API" document
            return docName.Equals(Name, StringComparison.OrdinalIgnoreCase);
        });
        
        options.SwaggerDoc("rest", new OpenApiInfo
        {
            Title = "External API",
            Description = $"ProgramInterface.com - External API",
            Version = "0.0.1",
        });
    }

    protected override void UseSwagger(IApplicationBuilder app)
    {
        app.UseSwagger(c =>
        {
            c.RouteTemplate = Name.ToLowerInvariant() + "/swagger/{documentName}/swagger.json";
            // c.SerializeAsV2 = true;
        });

        app.UseSwaggerUI(c =>
        {
            c.RoutePrefix = $"{Name.ToLowerInvariant()}/swagger";
            c.SwaggerEndpoint($"/{Name.ToLowerInvariant()}/swagger/{Name}/swagger.json", Name);
            c.SwaggerEndpoint($"/{Name.ToLowerInvariant()}/swagger/rest/swagger.json", "External");

            c.DisplayOperationId();
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableFilter();
            c.ShowExtensions();
            c.ShowCommonExtensions();
            c.EnableValidator();
        });
    }
}