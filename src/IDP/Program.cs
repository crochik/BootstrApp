using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.App;
using Newtonsoft.Json;
using Stores;
using Services;
using Crochik.Security;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using PI.Shared.Services;
using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PI.Shared.Filters;
using PI.Shared.O365;

namespace IDP;

public class Program : MicroserviceApp
{
    protected override string Name => "IDP";

    public static async Task<int> Main(string[] args)
    {
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
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    protected override void InitConfiguration(IConfigurationManager configurationManager)
    {
        configurationManager.AddJsonFile("/pi/settings/appsettings.json", optional: true);
        Configuration = configurationManager.Build();
    }

    protected override void AddServices(IServiceCollection services)
    {
        // base.AddServices(services);

        // var health = services.AddHealthChecks();
        // AddHealthCheckServices(health);

        services.AddMongoConnection();
        services.AddMongoAdapters();

        // IDP
        AddIdentityServer(services);
        services.AddAuthentication(Configuration);

        services
            .AddSingleton<LoginService>()
            .AddSingleton<IIdentityProvider, MicrosoftIdentityProvider>()
            .AddSingleton<IIdentityProvider, SalesforceIdentityProvider>()
            .AddSingleton<IIdentityProvider, GoogleIdentityProvider>()
            .AddSingleton<IIdentityProvider, GitHubIdentityProvider>()
            .AddSingleton<GenericIdentityProvider>()
            .AddSingleton<ProfileService>()
            ;

        services
            .AddObjectTypeService()
            .AddSalesforceService()
            ;

        services.AddTransient<O365AuthClient>();
        services.AddSingleton<UserActionService>()
            .AddSingleton<AuthorizationService>()
            .AddSingleton<MagicCodeService>()
            .AddSingleton<PasswordlessService>()
            ;

#if DEBUG
        services.AddSwaggerGen(AddSwaggerGen);
        services.AddSwaggerGenNewtonsoftSupport();
#endif

        services.AddControllersWithViews(options => { options.Filters.Add<ExceptionFilter>(); })
            .AddNewtonsoftJson(options => options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore
            );

        // only for controllers to check bearer (pi) tokens
        services.AddAuthorization(AddPolicies);
    }

    private void AddIdentityServer(IServiceCollection services)
    {
        // services.Configure<CookiePolicyOptions>(options =>
        // {
        //     options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
        //     options.Secure = CookieSecurePolicy.SameAsRequest;
        // });

        var provider = services.AddIdentityServer(opts =>
        {
            // opts.UserInteraction.LoginUrl = "/account/login"
            opts.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.None;
            opts.Cors.CorsPaths.Add("/passwordless/start");
        });

        provider.AddClientStore<ClientStore>()
            .AddResourceStore<ResourceStore>()
            .AddPersistedGrantStore<PersistedGrantStore>()
            .AddCorsPolicyService<CorsPolicyService>()
            .AddProfileService<ProfileService>()
            .AddExtensionGrantValidator<PasswordlessGrantValidator>()
            .AddExtensionGrantValidator<MagicCodeGrantValidator>()
            ;

        var dataprotection = Configuration.GetDataProtectionConfig();
        if (dataprotection.UseAWS)
        {
            var rsakey = AWSSystemManagerHelper.GetParameter(Configuration, dataprotection.DeveloperSigningCredential);
            var serializer = new RSAKeySerializer();
            var credentials = serializer.GetSigningCredentials(null, rsakey);
            provider.AddSigningCredential(credentials);
        }
        else
        {
            // var keyFilename = Path.Combine(dataprotection.KeysPath, $"{dataprotection.DeveloperSigningCredential}.rsa");
            var keyFilename = $"{dataprotection.DeveloperSigningCredential}.rsa";
            System.Console.WriteLine($"Using certificate from local path: {keyFilename}");
            provider.AddDeveloperSigningCredential(true, keyFilename);
        }
    }

    protected override void Use(IApplicationBuilder app)
    {
        // app.UseAPM(Configuration);

        var forwardedHeaderOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto // ForwardedHeaders.All
        };

        forwardedHeaderOptions.KnownNetworks.Clear();
        forwardedHeaderOptions.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeaderOptions);

        app.UseStaticFiles();

        // no need for this as we defer to the IdentityServer that will defer to the CorPolicyService
        // app.UseCors(builder =>
        // {
        //     builder
        //         // .WithOrigins(cors.Origins)
        //         .AllowAnyOrigin()
        //         .AllowAnyMethod()
        //         .AllowAnyHeader();
        // });

        app.UseRouting();

        app.UseIdentityServer();

        app.UseSerilogRequestLogging(opts => opts.EnrichDiagnosticContext = EnrichDiagnosticContext);

        // add authentication/authorization so controllers can use PI bearer tokens
        app.UseAuthentication();
        app.UseAuthorization();
        // app.UseMiddleware<ActorMiddleware>();

#if DEBUG
        UseSwagger(app);
#endif

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapDefaultControllerRoute(); // razor pages
            
            // endpoints.MapHealthChecks("/health", HealthCheckOptions);
            endpoints.MapGet("/health", () => Results.Ok());
        });
    }

    protected override void StartServices(IServiceProvider services)
    {
        MongoCacheTicketStore.Get().Connection = services.GetRequiredService<MongoConnection>();
    }
}

// This method gets called by the runtime. Use this method to add services to the container.
// public void ConfigureServices(IServiceCollection services)
// {
//     var connectionString = "mongodb://localhost:27017/IDP";

//     services.AddMapper();

//     // services.AddDataProtection()
//     //     .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory()));

//     services.AddSingleton<EncryptedStringSerializer>();
//     services.AddSingleton<MongoConnection>((provider) => new MongoConnection(connectionString));
//     services.AddSingleton<DataInitializer>();

//     // re-define authentication (it was already initialized by AddIdentity)
//     services.AddAuthentication(Configuration.GetSection("Authentication"));
//     services.AddSingleton<IUserService, UserService>();

// services.AddIdentityServer()
//     // .AddProfileService<ProfileService>()
//     .AddConfigurationStore(options =>
//     {
//         options.CollectionNamePrefix = "idp";
//         options.ConnectionString = connectionString;
//     })
//     .AddOperationalStore(options =>
//     {
//         options.CollectionNamePrefix = "idp";
//         options.ConnectionString = connectionString;
//     })
//     // needed in order to generate jwt tokens
//     // TODO: add path 
//     .AddDeveloperSigningCredential()
//     ;

//     services.AddMvc();
// }