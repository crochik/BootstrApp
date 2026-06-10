using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Crochik.Security;
using Crochik.Messaging;
using IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Middleware;
using PI.Shared.OpenAPI;
using Serilog;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using PI.Shared.Filters;
using Microsoft.Extensions.Hosting;
using System.Threading;
using Crochik.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi;
using Newtonsoft.Json.Serialization;
using PI.Shared.Models;

namespace PI.Shared.App;

public abstract class HostedApp
{
    protected string EnvironmentName => Environment.GetEnvironmentVariable("PI_ENVIRONMENT");

    protected IConfiguration Configuration { get; set; }
    public virtual bool UsesAuthentication => false;
    protected abstract string Name { get; }

    protected static string Job => Environment.GetEnvironmentVariable("PI_RUN_JOB");
    public static bool IsWebApi => string.IsNullOrEmpty(Job);

    protected HostedApp(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    protected HostedApp()
    {
    }

    /// <summary>
    /// Configure Configuration
    /// </summary>
    /// <param name="configurationManager"></param>
    protected virtual void InitConfiguration(IConfigurationManager configurationManager)
    {
        configurationManager.AddSystemsManager($"/PI/{EnvironmentName}/Environment/", optional: true);
        configurationManager.AddSystemsManager($"/PI/{EnvironmentName}/{Name}/", optional: true);
        configurationManager.AddJsonFile("/pi/settings/appsettings.json", optional: true);
        configurationManager.AddEnvironmentVariables();

        Configuration = configurationManager.Build();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            UseCookies = false
        });

        AddDataProtection(services);

        if (Configuration.IsRabbitMqServiceConfigured())
        {
            services.ConfigureRabbitMqService(Configuration);
            services.AddSingleton<IMessageBroker, RabbitMessageBroker>();
        }
        else
        {
            services.AddSingleton<IMessageBroker, NoopMessageBroker>();
        }

        AddMapper(services);
        AddServices(services);
    }

    protected void AddDataProtection(IServiceCollection services)
    {
        var config = Configuration.GetDataProtectionConfig();
        if (config == null)
        {
            Log.Error("Missing Data Protection Configuration");
            return;
        }

        if (config.UseAWS)
        {
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

            var prefix = config.KeysPath ?? "/Sched.Onl/DataProtection";
            Log.Information("DataProtection, using AWS Systems Manager: {Prefix}", prefix);
            services.AddDataProtection()
                .PersistKeysToAWSSystemsManager(prefix)
                .SetDefaultKeyLifetime(TimeSpan.FromDays(config.KeysLifeTime))
                .SetApplicationName(config.ApplicationName);

            return;
        }

        var keysPath = config?.KeysPath ?? Directory.GetCurrentDirectory() + "/.dataprotection-keys";
        Log.Warning("DataProtection, using local path: {Path}", keysPath);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetDefaultKeyLifetime(TimeSpan.FromDays(config.KeysLifeTime))
            .SetApplicationName(config.ApplicationName);
    }

    private void AddMapper(IServiceCollection services)
    {
        var config = new MapperConfiguration(cfg => { ConfigureMapper(cfg); });
        config.AssertConfigurationIsValid();

        services.AddSingleton<IMapper>(config.CreateMapper());
    }

    protected virtual void ConfigureMapper(IMapperConfigurationExpression cfg)
    {
        cfg.AddMaps(this.GetType().Assembly);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.FullName.StartsWith("PI."))
            {
                if (this.GetType().Assembly == assembly) continue;
                cfg.AddMaps(assembly);
            }
        }
    }

    protected virtual void AddServices(IServiceCollection services)
    {
        services.AddMongoConnection();
        services.AddMongoAdapters();

        // services.AddSingleton<IAPMService, ElastAPMService>();

        // do not user with new serilog (UseSerilog at the host)
        // services.AddSerilog();
    }
}

public abstract class MicroserviceApp : HostedApp
{
    private AuthenticationConfig _authConfig = null;
    protected AuthenticationConfig AuthConfig => _authConfig ??= AuthenticationConfig.Get(Configuration);

    /// <summary>
    /// If false, will clear naming convention so models will be generated with their C# property names
    /// If true, will use .net defaults
    /// </summary>
    protected virtual bool CamelCasePropertiesForApiModels => Configuration.GetValue("CamelCasePropertiesForApiModels", true);

    public override bool UsesAuthentication => !string.IsNullOrEmpty(AuthConfig?.Authority);

    protected virtual HealthCheckOptions HealthCheckOptions => new()
    {
        ResponseWriter = WriteResponse
    };

    protected async Task RunWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.WebHost.ConfigureKestrel(ConfigureKestrel);
        
        InitConfiguration(builder.Configuration);
        builder.Host.UseElasticSearchLogging(Name);
        ConfigureServices(builder.Services);

        var app = builder.Build();

        var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        Configure(app, app.Environment, appLifetime);

        await app.RunAsync();
    }

    protected virtual void ConfigureKestrel(KestrelServerOptions options)
    {
        // do nothing 
    } 

    protected HostApplicationBuilder RunJob(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        InitConfiguration(builder.Configuration);

        builder.Services.AddSerilog((configuration) =>
            configuration
                    .ReadFrom.Configuration(Configuration)
                    .Enrich.FromLogContext()
                    // .Enrich.With<EnvironmentEnricher>()
                    // .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .WriteTo.Console());
        
        ConfigureServices(builder.Services);

        return builder;
    }
    
    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);

        if (IsWebApi)
        {
            AddWebServices(services);
        }
    }

    protected void AddWebServices(IServiceCollection services)
    {
        // var health = services.AddHealthChecks();
        // AddHealthCheckServices(health);

        if (UsesAuthentication)
        {
            // handle JWT and reference tokens                
            // http://docs.identityserver.io/en/release/topics/apis.html
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddIdentityServerAuthentication(options =>
                {
                    // any way to hardcode discovery?
                    // ...
                    options.Authority = AuthConfig.Authority;
                    options.RequireHttpsMetadata = options.Authority.StartsWith("https");
                    options.ApiName = AuthConfig.APIName;
                });
        }

        AddSwagger(services);

        services.AddControllers(options =>
            {
                options.OutputFormatters.Add(new CsvOutputFormatter());
                options.Filters.Add<ExceptionFilter>();
                options.Filters.Add<DynamicJsonSerializationFilter>();
                options.Conventions.Add(new DefaultGroupNameConvention(Name));
            })
            .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                    options.SerializerSettings.Converters.Add(new Converters.ObjectIdConverter());

                    if (!CamelCasePropertiesForApiModels)
                    {
                        // do not change model property names (keep case, instead of camel)
                        ((DefaultContractResolver)options.SerializerSettings.ContractResolver)!.NamingStrategy = null;
                    }
                }
            );

        if (UsesAuthentication)
        {
            services.AddAuthorization(AddPolicies);
        }
    }

    protected virtual void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(AddSwaggerGen);
        // use newtonsoft json
        services.AddSwaggerGenNewtonsoftSupport();
    }

    protected void AddHealthCheckServices(IHealthChecksBuilder builder)
    {
        // disable rabbitmq 2025-05-03 to test if it is causing the crashes
        // if (Configuration.IsRabbitMqServiceConfigured())
        // {
        //     System.Console.WriteLine("Add RabbitMq monitoring...");
        //     var rabbitMqConfig = RabbitMessageBroker.Options.Get(Configuration);
        //     builder.AddRabbitMQ(new Uri(rabbitMqConfig.Url));
        // }

        // Disable elastic search as it is restarting the server
        /*
        var elkUrl = Configuration["ELK:Url"];
        if (!string.IsNullOrEmpty(elkUrl))
        {
            System.Console.WriteLine($"Add ElasticSearch monitoring...");
            builder.AddElasticsearch(opts =>
            {
                var user = Configuration["ELK:User"];
                var password = Configuration["ELK:Password"];
                if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
                {
                    opts.UseBasicAuthentication(user, password);
                }

                opts.UseServer(elkUrl);
            });
        }
        */
    }

    protected virtual void AddSwaggerGen(SwaggerGenOptions options)
    {
        options.SwaggerDoc(Name, new OpenApiInfo
        {
            Title = Name,
            Description = $"ProgramInterface.com - {Name}",
            Version = "0.0.1",
        });

        if (UsesAuthentication)
        {
            options.UseAllOfForInheritance();
            options.UseAllOfToExtendReferenceSchemas();
            // options.UseOneOfForPolymorphism();
            options.EnableAnnotations(enableAnnotationsForInheritance: true, enableAnnotationsForPolymorphism: false);

            options.OperationFilter<SecurityRequirementsOperationFilter>();
            options.OperationFilter<OperationIdFilter>();
            // options.OperationFilter<GenericApiModelFilter>();
            options.OperationFilter<StringBodyFilter>();

            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    Implicit = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{AuthConfig.Authority}/connect/authorize"), // ?acr_values=idp:InspireNet
                        TokenUrl = new Uri($"{AuthConfig.Authority}/connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "api", "ProgramInterface.com" },
                            { "rest", "REST API" }
                        }
                    }
                }
            });
        }

        options.CustomSchemaIds(SwaggerSchemaIdSelector);

        // Set the comments path for the Swagger JSON and UI.
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
    }

    private Dictionary<Type, string> resolved = new()
    {
        { typeof(KeyValuePair<string, object>), "KVP" },
    };

    private HashSet<string> used = new HashSet<string>();

    private string SwaggerSchemaIdSelector(Type type)
    {
        if (resolved.TryGetValue(type, out var name)) return name;

        name = getName();

        if (used.Contains(name))
        {
            throw new Exception($"There is already a type mapped to {name}");
        }

        Log.Information("Mapped {Type} to {Name}", type.FullName, name);
        resolved[type] = name;
        used.Add(name);
        return name;

        string getName()
        {
            var parts = type.FullName.Split('.');
            var lastPart = parts[^1];
            var index = lastPart.IndexOf('+');

            if (type.IsEnum)
            {
                if (index > 0)
                {
                    var candidate = lastPart[(index + 1)..];
                    if (!used.Contains(candidate)) return candidate;

                    candidate = lastPart.Replace("+", "");
                    if (!used.Contains(candidate)) return candidate;
                }

                return lastPart;
            }

            if (index > 0)
            {
                lastPart = lastPart.Replace("+", "");
            }

            if (lastPart.EndsWith("DAO", StringComparison.OrdinalIgnoreCase))
            {
                lastPart = lastPart.Substring(0, name.Length - 3);
            }

            if (type.IsGenericType)
            {
                return string.Join("_", type.GenericTypeArguments.Select(x => x.Name).Prepend(type.Name[..type.Name.IndexOf('`')]));
            }

            if (lastPart.Contains(' '))
            {
                throw new Exception($"Error {type.FullName}: {lastPart}");
            }

            for (var c = parts.Length - 1; c >= 0; c--)
            {
                var candidate = c == parts.Length - 1 ? "" : string.Join('_', parts, c, parts.Length - 1 - c);
                candidate += lastPart;
                if (!used.Contains(candidate)) return candidate;
            }

            throw new Exception($"Couldn't figure out unique name for {type.FullName}");
        }
    }

    protected virtual void AddPolicies(AuthorizationOptions options)
    {
        // TODO: add required scope based on app ".Name" 
        // ...

        // any authenticated user (/*/v1 or /*/api)
        options.AddPolicy("anyUserOrApi", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.User), nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Root), nameof(EntityRoleId.Profile))
            .RequireScope("api", "rest")
        );

        // any authenticated user
        options.AddPolicy("default", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.User), nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Root), nameof(EntityRoleId.Profile))
            .RequireScope("api")
        );

        // same as "default" but will also allow scope "rest" (instead of api)
        options.AddPolicy("rest", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.User), nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Root), nameof(EntityRoleId.Profile))
            .RequireAssertion(context => context.User.HasClaim(c => c is { Type: "scope", Value: "rest" or "api" }))
        );

        // by role
        options.AddPolicy("manager", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.Manager))
            .RequireScope("api")
        );
        options.AddPolicy("managerplus", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.Manager), nameof(EntityRoleId.Admin), nameof(EntityRoleId.Root))
            .RequireScope("api")
        );
        options.AddPolicy("admin", policy => policy
            .RequireClaim(JwtClaimTypes.Subject)
            .RequireRole(nameof(EntityRoleId.Admin))
            .RequireScope("api")
        );

        // options.AddPolicy("root", policy => policy
        //     .RequireClaim(JwtClaimTypes.Subject)
        //     .RequireRole(nameof(EntityRoleId.Root))
        // );

        // me..me..mme,
        options.AddPolicy("root", policy => policy
                .RequireClaim(JwtClaimTypes.Subject)
                .RequireRole(nameof(EntityRoleId.Admin))
                .RequireClaim(JwtClaimTypes.Email, "felipe@programinterface.onmicrosoft.com")
            // .RequireClaim("pi_account_id", 
            //     "9b0a4f86-c0b1-46a4-8a3b-e3444bc6228c" // ...@schedonl.onmicrosoft.com (staging)
            // )
        );

        // partner (wescheduleit, oi, dw/pbi, ...)
        options.AddPolicy("partner", policy => policy
            .RequireClaim(JwtClaimTypes.ClientId)
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireClaim("client_account_id")
            .RequireScope("partner")
        );
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    // only called for the webhostbuilder
    public virtual void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime lifetime
    )
    {
        bool isDevelopment = env.EnvironmentName.Equals("Development");
        if (isDevelopment)
        {
            app.UseDeveloperExceptionPage();
        }

        lifetime.ApplicationStarted.Register(() =>
        {
            if (isDevelopment)
            {
                Log.Logger.Information("Initializing services");
                StartServices(app.ApplicationServices);
            }
            else
            {
                Log.Logger.Information("Application has started: wait 30s before starting services");
                Task.Run(async () =>
                {
                    await Task.Delay(30000);

                    Log.Logger.Information("Initializing services");
                    StartServices(app.ApplicationServices);
                });
            }
        });

        lifetime.ApplicationStopping.Register(() =>
        {
            Log.Logger.Information("Application stopping...");
            StopServices(app.ApplicationServices);

            Thread.Sleep(10000);
        });

        lifetime.ApplicationStopped.Register(() =>
        {
            Log.Logger.Information("Application stopped");
            Log.CloseAndFlush();
        });

        Use(app);
    }

    protected List<Type> _lifetimeServiceTypes = [];

    protected MicroserviceApp()
    {
    }

    protected IServiceCollection AddLifetimeService<T>(IServiceCollection services) where T : class, ILifetimeService
    {
        services.AddSingleton<T>();
        _lifetimeServiceTypes.Add(typeof(T));
        return services;
    }

    protected IServiceCollection AddLifetimeService<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService, ILifetimeService
    {
        services.AddSingleton<TService, TImplementation>();
        _lifetimeServiceTypes.Add(typeof(TService));
        return services;
    }

    protected virtual void StartServices(IServiceProvider services)
    {
        foreach (var serviceType in _lifetimeServiceTypes)
        {
            var service = (ILifetimeService)services.GetRequiredService(serviceType);
            service.Start();
        }
    }

    protected virtual void StopServices(IServiceProvider services)
    {
        foreach (var serviceType in _lifetimeServiceTypes)
        {
            var service = (ILifetimeService)services.GetRequiredService(serviceType);
            service.Stop();
        }
    }

    protected virtual void Use(IApplicationBuilder app)
    {
        // app.UseAPM(Configuration);

        var forwardedHeaderOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto // ForwardedHeaders.All
        };
        // clear so it will allow any forward to be processed
        forwardedHeaderOptions.KnownNetworks.Clear();
        forwardedHeaderOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeaderOptions);

        // don't think we are using static files for any app
        // app.UseStaticFiles();

        app.UseRouting();

        // THIS IS PROBABLY NOT DOING ANYTHING AS THERE ISN'T A ADDCORS ANYWHERE ?!?!?!??!
        // NOT THAT WOULD MATTER AS IT IS, SINCE WOULD ALLOW ANYTHING ANYWAY :)
        // INVESTIGATE....
        app.UseCors(builder =>
        {
            builder
                // .WithOrigins(cors.Origins)
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });

        UseAuth(app);
        
        app.UseMiddleware<ActorMiddleware>();

        // if (Configuration.IsApmEnabled())
        // {
        //     Console.WriteLine("Add APM Middleware and MongoDb subscriber");
        //     Elastic.Apm.Agent.Subscribe(new Elastic.Apm.MongoDb.MongoDbDiagnosticsSubscriber());
        //     app.UseMiddleware<APMMiddleware>();
        // }

        app.UseMiddleware<RequestContextMiddleware>();
        app.UseSerilogRequestLogging(opts => opts.EnrichDiagnosticContext = EnrichDiagnosticContext);

        UseSwagger(app);

        app.UseEndpoints(UseEndpoints);
    }

    protected virtual void UseAuth(IApplicationBuilder app)
    {
        if (UsesAuthentication)
        {
            app.UseAuthentication();
        }

        app.UseAuthorization();
    }

    protected virtual void EnrichDiagnosticContext(IDiagnosticContext diagnostics, HttpContext http)
    {
        if (!http.Request.Headers.TryGetValue("X-Forwarded-For", out var ip))
        {
            ip = http.Connection.RemoteIpAddress.ToString();
        }

        diagnostics.Set("remoteIp", ip);
    }

    protected virtual void UseEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        
        // endpoints.MapHealthChecks("/health", HealthCheckOptions);
        endpoints.MapGet("/health", () => Results.Ok());
    }

    protected virtual void UseSwagger(IApplicationBuilder app)
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

            // c.DefaultModelExpandDepth(2);
            // c.DefaultModelRendering(ModelRendering.Model);
            // c.DefaultModelsExpandDepth(-1);
            c.DisplayOperationId();
            c.DisplayRequestDuration();
            // c.DocExpansion(DocExpansion.None);
            c.EnableDeepLinking();
            c.EnableFilter();
            // c.MaxDisplayedTags(5);
            c.ShowExtensions();
            c.ShowCommonExtensions();
            c.EnableValidator();
            // c.SupportedSubmitMethods(SubmitMethod.Get, SubmitMethod.Head);
            // c.UseRequestInterceptor("(request) => { return request; }");
            // c.UseResponseInterceptor("(response) => { return response; }");
        });
    }

    private Task WriteResponse(HttpContext httpContext, HealthReport result)
    {
        // TODO: add back to log health to apm
        // foreach (var entry in result.Entries)
        // {
        //     using var apm = _apm.StartTransaction("HealthCheck", entry.Key, entry.Key);
        //     if (entry.Value.Exception != null)
        //     {
        //         apm.Context = new
        //         {
        //             Status = entry.Value.Status.ToString(),
        //             Description = entry.Value.Description,
        //             Exception = entry.Value.Exception.ToString()
        //         };
        //         continue;
        //     }

        //     apm.Context = new
        //     {
        //         Status = entry.Value.Status.ToString(),
        //         Description = entry.Value.Description
        //     };
        // }

        httpContext.Response.ContentType = "application/json";

        var json = new JObject(
            new JProperty("status", result.Status.ToString()),
            new JProperty("results", new JObject(result.Entries.Select(pair =>
                new JProperty(pair.Key, new JObject(
                    new JProperty("status", pair.Value.Status.ToString()),
                    new JProperty("description", pair.Value.Description),
                    new JProperty("data", new JObject(pair.Value.Data.Select(p => new JProperty(p.Key, p.Value))))))))));

        return httpContext.Response.WriteAsync(json.ToString(Formatting.Indented));
    }

    public class AuthenticationConfig
    {
        public string Authority { get; set; }
        public string APIName { get; set; }
        public string[] CorsOrigins { get; set; }

        public static AuthenticationConfig Get(IConfiguration configuration) => configuration.GetSection("PI").Get<AuthenticationConfig>();
    }
}
