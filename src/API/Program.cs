using Controllers;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Email;
using PI.Shared.OpenAPI;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;
using PI.Shared.Services.DataProtection;
using Serilog;
using Services;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace App;

public class Program : MicroserviceApp
{
    protected override string Name => "API";

    public static async Task<int> Main(string[] args)
    {
        Serilog.Debugging.SelfLog.Enable(Console.Error);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting API...");

            // set min threads
            // ThreadPool.SetMinThreads(100, 100);

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

    protected override void ConfigureKestrel(KestrelServerOptions options)
    {
        base.ConfigureKestrel(options);

        // TODO: ?!?!?!?!!?!?!?!?!?!?
        options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60); // default is 130 (traefik is 90)
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15); // default is 30 

        options.ConfigureEndpointDefaults(listenOptions =>
        {
            // listenOptions.Use(next => new RawConnectionTracker(next).OnConnectAsync);
            listenOptions.Protocols = HttpProtocols.Http1;
        });
        // TODO: ?!?!?!?!!?!?!?!?!?!?
    }

    protected override void Use(IApplicationBuilder app)
    {
        // TODO: ?!?!?!?!!?!?!?!?!?!?
        // app.UseMiddleware<ConnectionStateMiddleware>();
        // TODO: ?!?!?!?!!?!?!?!?!?!?

        base.Use(app);
    }

    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);

        // add request timeouts
        // services.AddRequestTimeouts(options =>
        //     options.DefaultPolicy = new RequestTimeoutPolicy
        //     {
        //         Timeout = TimeSpan.FromSeconds(30),
        //         TimeoutStatusCode = 504
        //     });

        // TODO: ?!?!?!?!!?!?!?!?!?!?
        // services.AddHostedService<ConnectionDiagnosticsService>();
        // services.AddSingleton<ConnectionStateInspector>();
        // TODO: ?!?!?!?!!?!?!?!?!?!?

        services
            .AddSingleton<RemoteFileService>()
            .AddSingleton<IRemoteFileServiceProvider, AwsS3RemoteFileServiceProvider>()
            .AddReportService()
            .AddLeadBuilderService()
            .AddObjectTypeService()
            .AddSingleton<IntegrationAuthService>()
            ;

        services.AddSingleton<IDataProtectionServiceProvider, MicrosoftDataProtectionServiceProvider>();
        
        // run user actions 
        services.AddSingleton<ActionRunnerService>()
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
            ;
        
        // register to handle some user actions via runners
        AddLifetimeService<ActionRunnerFlowService>(services)
            .Configure<ActionRunnerFlowServiceOptions>(options =>
            {
                options.ActionIds =
                [
                    ActionIds.CreateObject,
                    ActionIds.CreateObjectUsingForm,
                    ActionIds.UpdateObject,
                    ActionIds.LookupObject,
                    ActionIds.Switch,
                    ActionIds.Compose,
                    ActionIds.FireEvent,

                    // other candidates
                    // handled by the FlowService right now but with runners
                    // case ConditionalActionRunner:
                    // case TagObjectActionRunner:
                    // case SetObjectStatusActionRunner:
                ];
            });

        // TODO: let the ActionRunnerFlowService handle these instead,
        // these are already all runners... 
        AddLifetimeService<ConditionalActionService>(services);
        AddLifetimeService<SetObjectStatusActionService>(services);
        AddLifetimeService<TagObjectActionService>(services);
        // make these into runners as well?
        // ...
        AddLifetimeService<LoadRelatedObjectActionService>(services);
        AddLifetimeService<StartFlowActionService>(services);
        
        services.AddSingleton<LeadTypeMapper>();

        services
            // .AddTransient<O365Service>()
            .AddTransient<AppointmentSchedulerService>()
            .AddTransient<FlowTreeBuilder>()
            .AddTransient<CalendarViewBuilder>()
            ;

        services.AddSingleton<IUrlService, UrlService>()
            .AddSingleton<ILeadEventService, LeadEventService>()
            .AddSingleton<UserActionService, UserActionWithRunnersService>() // .AddSingleton<UserActionService>()
            .AddSingleton<AuthorizationService>()
            .AddSingleton<RedirectionService>()
            ;

        AddLifetimeService<ICacheService, CacheService>(services);

        AddLifetimeService<FlowService>(services);
        AddLifetimeService<FlowLogService>(services);
        AddLifetimeService<SnapshotService>(services);
        AddLifetimeService<LeadFlowService>(services);
        AddLifetimeService<SchedulerActionsService>(services);
        AddLifetimeService<StatementService>(services);
        AddLifetimeService<TaskSchedulerService>(services);

        IdentityModelEventSource.ShowPII = true; //To show detail of error and see the problem

        // ExcelDataReader
        // https://github.com/crochik/ExcelDataReader
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    protected override void AddPolicies(AuthorizationOptions options)
    {
        base.AddPolicies(options);

        // app specific: scheduler
        options.AddPolicy("scheduler", policy => policy
            .RequireClaim(JwtClaimTypes.ClientId)
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireClaim("client_account_id")
            // .RequireScope("client_app")
            .RequireScope("scheduler")
        );

        options.AddPolicy("schedulerExisting", policy => policy
            .RequireClaim(JwtClaimTypes.ClientId)
            .RequireClaim(JwtClaimTypes.JwtId)
            .RequireClaim("client_account_id")
            // .RequireScope("client_app")
            .RequireScope("scheduler")
            .RequireClaim("pi_lead_id")
        );
    }

    protected override void AddSwaggerGen(SwaggerGenOptions options)
    {
        // force including some models not explicitly used in the API
        // TODO: when we stop using this document to generate the api, we may not need to continue to include
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<Page>>();
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<PI.Shared.Models.Layout.LayoutItem>>();
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<UIElement>>();
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<FieldOptions>>();
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<FormLayout>>();
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<DataViewOptions>>();
        options.DocumentFilter<ExplicitAddClassesDocumentFilter<UnlayerTemplate>>();
        
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
        
        options.SwaggerDoc("scheduler", new OpenApiInfo
        {
            Title = "Scheduler API",
            Description = $"ProgramInterface.com - Scheduler API",
            Version = "0.0.1",
        });

        options.SwaggerDoc("callcenter", new OpenApiInfo
        {
            Title = "Callcenter API",
            Description = $"ProgramInterface.com - Callcenter API",
            Version = "0.0.1",
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
        app.UseSwagger(c => { c.RouteTemplate = Name.ToLowerInvariant() + "/swagger/{documentName}/swagger.json"; });

        app.UseSwaggerUI(c =>
        {
            c.RoutePrefix = "swagger";
            c.SwaggerEndpoint($"/{Name.ToLowerInvariant()}/swagger/{Name}/swagger.json", "Internal");
            c.SwaggerEndpoint($"/{Name.ToLowerInvariant()}/swagger/scheduler/swagger.json", "Scheduler");
            c.SwaggerEndpoint($"/{Name.ToLowerInvariant()}/swagger/callcenter/swagger.json", "Callcenter");
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

    protected override void StartServices(IServiceProvider services)
    {
        base.StartServices(services);

        // hack for now
        MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(typeof(PI.Shared.Salesforce.Models.SalesforceToken));
    }

    // { 
    // handle only JWT
    // instead of "sub", it needs to use the "nameidentifier" claim to get id
    // services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    //     .AddJwtBearer(options =>
    //     {
    //         options.Authority = _config.Authority;
    //         options.RequireHttpsMetadata = options.Authority.StartsWith("https");
    //         options.Audience = _config.APIName;
    //     });

    // is this better/another option?????
    // ...
    // .AddOpenIdConnect("oidc", options =>
    // {
    //     options.Authority = _config.Authority;
    //     options.RequireHttpsMetadata = false;
    //     options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
    //     options.ClientId = "hybrid";
    //     options.ClientSecret = "secret";
    //     options.SaveTokens = true;
    //     options.GetClaimsFromUserInfoEndpoint = true;

    //     options.Scope.Add("api1");
    //     options.Scope.Add("offline_access");

    //     options.Events.OnRedirectToIdentityProvider = (opts) => {
    //         if ( opts.ProtocolMessage.RequestType == OpenIdConnectRequestType.Authentication ) {
    //             opts.ProtocolMessage.AcrValues = "idp:InspireNet tenant:FCI";
    //         }

    //         return Task.CompletedTask;
    //     };
    // });                
}