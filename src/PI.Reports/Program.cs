using System;
using Microsoft.Extensions.DependencyInjection;
using DevExpress.AspNetCore;
using PI.Shared.App;
using PI.Shared.Services;
using DevExpress.AspNetCore.Reporting;
using Microsoft.AspNetCore.Builder;
using Reports.Services;
using System.IO;
using System.Threading.Tasks;
using DevExpress.DashboardAspNetCore;
using Microsoft.Extensions.Hosting;
using PI.ProductCatalog.Postgres;
using PI.Shared.Middleware;
using PI.Shared.Services.DataProtection;
using Reports.Dashboards;
using Reports.Services.Jobs;
using Serilog;

namespace Reports;

public class Program : MicroserviceApp
{
    protected override string Name => "Reports";
    
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
        
        services.AddSingleton<PostgresConnection>();

        services
            .AddReportService()
            .AddObjectTypeService()
            .AddSingleton<RemoteFileService>()
                .AddSingleton<IRemoteFileServiceProvider, AwsS3RemoteFileServiceProvider>()
                .AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>()
            .AddSyncService()
                .AddSingleton<IRunJob, ExportToPostgresJob>()
                .AddSingleton<IRunJob, TestJob>()
            ;

        AddLifetimeService<ExtractDataToFileActionService>(services);
        
        services.AddDevExpressControls();

        ConfigureReporting(services);
        ConfigureBusinessIntelligence(services);
    }
    
    private void ConfigureBusinessIntelligence(IServiceCollection services)
    {
        services.AddSingleton<IDashboardService, DashboardService>();
        // services.AddSingleton<SampleDashboardService>();
        services.AddScoped(serviceProvider =>
        {
            var dashboardService = serviceProvider.GetRequiredService<IDashboardService>();
            return dashboardService.GetConfigurator();
        });
    }

    private void ConfigureReporting(IServiceCollection services)
    {
        services.AddSingleton<BridgeService>();

        // devexpress
        services.AddScoped<DevExpress.XtraReports.Web.Extensions.ReportStorageWebExtension, ReportStorageWebExtension>();
        services.AddScoped<DevExpress.XtraReports.Web.WebDocumentViewer.IWebDocumentViewerReportResolver, WebDocumentViewerReportResolver>();
        services.AddScoped<DevExpress.XtraReports.Web.ReportDesigner.Services.PreviewReportCustomizationService, PreviewReportCustomizationService>();
        services.AddScoped<DevExpress.XtraReports.Web.WebDocumentViewer.IWebDocumentViewerAuthorizationService, DocumentViewerAuthorizationService>();
        services.AddScoped<DevExpress.XtraReports.Web.WebDocumentViewer.WebDocumentViewerOperationLogger, DocumentViewerAuthorizationService>();
        services.AddScoped<DevExpress.XtraReports.Web.WebDocumentViewer.IExportingAuthorizationService, DocumentViewerAuthorizationService>();

        // UseAsyncEngine
        // IReportProviderAsync

        
        services.ConfigureReportingServices(configurator =>
        {
            // configurator.UseAsyncEngine();

            configurator.ConfigureReportDesigner(designerConfigurator =>
            {
                designerConfigurator.RegisterDataSourceWizardConfigFileConnectionStringsProvider();
                // designerConfigurator.RegisterObjectDataSourceConstructorFilterService<ObjectDataSourceConstructorFilterService>();
                // designerConfigurator.RegisterObjectDataSourceWizardTypeProvider<ObjectDataSourceWizardTypeProvider>();
            });

            configurator.ConfigureWebDocumentViewer(viewerConfigurator =>
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                viewerConfigurator.UseFileDocumentStorage(
                    Path.Combine(baseDirectory, "ViewerStorages/Documents"),
                    DevExpress.XtraReports.Web.WebDocumentViewer.StorageSynchronizationMode.InterThread
                );

                viewerConfigurator.UseFileExportedDocumentStorage(
                    Path.Combine(baseDirectory, "ViewerStorages/ExportedDocuments"),
                    DevExpress.XtraReports.Web.WebDocumentViewer.StorageSynchronizationMode.InterThread
                );

                viewerConfigurator.UseFileReportStorage(
                    Path.Combine(baseDirectory, "ViewerStorages/Reports"),
                    DevExpress.XtraReports.Web.WebDocumentViewer.StorageSynchronizationMode.InterThread
                );

                // viewerConfigurator.UseCachedReportSourceBuilder();
            });
        });

        // DevExpress.XtraReports.Web.ClientControls.LoggerService.Initialize(new CustomReportingLoggerService(loggerFactory.CreateLogger("DXReporting")));

        // services.AddCors(options => {
        //     options.AddPolicy("AllowCorsPolicy", builder => {
        //         // Allow all ports on local host.
        //         builder.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost");
        //         builder.WithHeaders("Content-Type");
        //     });
        // });              
    }

    protected override void Use(IApplicationBuilder app)
    {
        base.Use(app);

        // devexpress
        DevExpress.XtraReports.Configuration.Settings.Default.UserDesignerOptions.DataBindingMode = DevExpress.XtraReports.UI.DataBindingMode.Expressions;
        app.UseDevExpressControls();
     
        app.UseEndpoints(builder =>
        {
            builder.MapDashboardRoute("bi/v1/dashboard", "MainDashboard");
        });
    }

    protected override void UseAuth(IApplicationBuilder app)
    {
        app.UseMiddleware<ParameterAuthenticationMiddleware>();
        
        base.UseAuth(app);
    }
}