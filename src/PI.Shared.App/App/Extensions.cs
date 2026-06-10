using System;
using System.Collections.Specialized;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace Crochik.Logging;

public static class IConfigurationExtensions
{
    public static void UseElasticSearchLogging<T>(this T hostBuilder, string appName) where T : IHostBuilder
    {
        hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var serilog = configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.With<EnvironmentEnricher>()
                    // .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .WriteTo.Console()
                ;

            var config = context.Configuration.GetSection("ELK").Get<ELKLogConfiguration>();

            if (ELKLogConfiguration.IsEnabled)
            {
                Log.Information("ElasticSearch: {Url}", config.Url);

                // this is not really an extension the configuration...
                // https://github.com/serilog/serilog-sinks-elasticsearch/wiki/Configure-the-sink
                // https://github.com/serilog/serilog-sinks-elasticsearch/issues/75
                var options = new ElasticsearchSinkOptions(new Uri(config.Url))
                {
                    MinimumLogEventLevel = LogEventLevel.Debug,
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    OverwriteTemplate = false,
                    NumberOfReplicas = 0,
                    TypeName = null,
                    EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                       EmitEventFailureHandling.RaiseCallback,
                    FailureCallback = (e) => Console.WriteLine($"Unable to submit event {e.MessageTemplate}"),

                    IndexFormat = config.IndexFormat,
                    TemplateName = config.IndexFormat,

                    // IndexFormat = $"pi-{config.IndexFormat}-{appName}-".ToLowerInvariant() + "{0:yyyy.MM.dd}",
                    // TemplateName = "new-pi-logs", 
                    // IndexAliases = [$"pi-{config.IndexFormat}-{appName}".ToLowerInvariant()],
                };
                
                if (!string.IsNullOrEmpty(config.Authorization))
                {
                    options.ModifyConnectionSettings = c => c.GlobalHeaders(
                        new NameValueCollection
                        {
                            { "Authorization", config.Authorization }
                        }
                    );
                }

                serilog.WriteTo.Elasticsearch(options);
            }
            else
            {
                Log.Warning("ElasticSearch: not using for logging");
            }
        });
    }
}