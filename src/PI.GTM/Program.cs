using System;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace PI.GTM;

public class Program
{
    public string Name = "GTM";
    public IConfiguration Configuration { get; set; }
    
    // public static void Main(string[] args)
    // {
    //     WebHostBuilder(args).Build().Run();
    // }
    //
    // private static IHostBuilder WebHostBuilder(string[] args) =>
    //     Host.CreateDefaultBuilder(args)
    //         .ConfigureWebHostDefaults(webBuilder =>
    //         {
    //             webBuilder.ConfigureAppConfiguration((hostingContext, config) =>
    //             {
    //                 config.AddJsonFile("/pi/settings/appsettings.json", optional: true);
    //             });
    //             webBuilder.UseStartup<Program>();
    //             webBuilder.UselasticSearchLogging();
    //         });

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
    
    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName).
            ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false
            });
            
        services
            .AddDataProtection(Configuration)
            ;

        var config = new MapperConfiguration(cfg =>
        {
        });
        var mapper = config.CreateMapper();

        services.AddSingleton<IMessageBroker, RabbitMessageBroker>();
        // services.AddSingleton<IAPMService, ElastAPMService>();
        services.AddSingleton<IMapper>(mapper);

        // services.AddSingleton<MonitorService>();

        throw new NotImplementedException();
        // services.AddSqlAdapters();

        services.AddMvc();

        // services.AddSwaggerGen(c =>
        // {
        //     c.SwaggerDoc("v1", new Info { Title = "ProgramInterface", Version = "v1" });

        //     // c.OperationFilter<SecurityRequirementsOperationFilter>();
        //     // c.OperationFilter<OperationIdFilter>();

        //     // var scheme = new OAuth2Scheme
        //     // {
        //     //     Type = "oauth2",
        //     //     AuthorizationUrl = $"{_config.Authority}/connect/authorize", // ?acr_values=idp:InspireNet
        //     //     Flow = "implicit",
        //     //     TokenUrl = $"{_config.Authority}/connect/token",
        //     //     Scopes = new Dictionary<string, string> {
        //     //         { _config.APIName, "ProgramInterface API" }
        //     //     }
        //     // };

        //     // c.AddSecurityDefinition("oauth2", scheme);
        //     // c.CustomSchemaIds(x => x.FullName);
        // });            
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app,
        IWebHostEnvironment env, IHostApplicationLifetime appLifetime)
    {
        // app.UseAPM(Configuration);

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // app.UseHsts();
            // app.UseHttpsRedirection();
        }

        throw new NotImplementedException("Have to upgrade app");
        
        app.UseMvc();

        // app.UseSwagger();
        // app.UseSwaggerUI(c =>
        // {
        //     c.SwaggerEndpoint("/swagger/v1/swagger.json", "ProgramInterface");
        // });

        // var service = app.ApplicationServices.GetRequiredService<MonitorService>();
        // service.CreateMeetingAsync(
        //     // Guid.Parse("D1C75FA6-4F6D-4A99-863F-95DA09EB4AA4"),
        //     Guid.Parse("0A43AF65-31A0-4D0A-9EC9-B9628A37CA7D"),
        //     new Messages.Lead.AppointmentEvent
        //     {
        //         Appointment = new Shared.Models.Appointment
        //         {
        //             Id = Guid.NewGuid(),
        //             Start = DateTime.UtcNow.AddHours(1),
        //             End = DateTime.UtcNow.AddHours(2)
        //         }
        //     }).Wait();

        // app.ApplicationServices.GetRequiredService<MonitorService>().Start();
    }
}