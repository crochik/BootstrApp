using System.Text.Json.Serialization;
using Crochik.Mongo;
using McpServer.Models;
using McpServer.Tools.Attributes;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Client;

namespace McpServer.Tools;

public class AppTools(MongoConnection connection)
{
    [McpTool(
            Name = "init_app",
            Description = "Initialize a new application. ",
            ExamplePrompts =
            [
                "Let's create a new app",
                "I want to write an app"
            ],
            StructuredOutput = true
        )
    ]
    public async Task<Result<CreateAppResult>> InitAppAsync(
        IEntityContext context,
        [McpParameter(Description = "App Name", Required = true)]
        string appName,
        [McpParameter(Description = "App Title", Required = true)]
        string appTitle,
        [McpParameter(Description = "App Description", Required = true)]
        string appDescription
    )
    {
        var entityId = context.OrganizationId ?? context.UserId.Value;

        var app = await GetAppAsync(context, appName);
        if (app != null)
        {
            if (app.EntityId != entityId)
            {
                return Result.Error<CreateAppResult>($"There is already an app named \"{appName}\". Please choose a different name");
            }
        }
        else
        {
            app = await CreateAppAsync(context, appName, appTitle, appDescription, entityId);
        }

        return Result.Success(new CreateAppResult
        {
            Id = app.Id,
            Name = app.Name,
            Title = app.Title,
            Description = app.Description,
            AccountId = app.AccountId,
        });
    }

    private async Task<BootstrApp> CreateAppAsync(IEntityContext context, string appName, string appTitle, string appDescription, Guid entityId)
    {
        var currUser = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, context.UserId.Value)
            .FirstOrDefaultAsync();

        var defaultProfile = new AppProfile
        {
            Id = Guid.NewGuid(),
            AccountId = currUser.AccountId,
            Name = $"{appName}'s User",
            Description = $"Default user profile for {appTitle}",
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            // ClientId = // defined below
        };
        
        // var userId = Guid.NewGuid();
        // var user = new User
        // {
        //     Id = userId,
        //     AccountId = accountId,
        //     EntityId = userId,
        //     // OrganizationId =
        //     CreatedOn = DateTime.UtcNow,
        //     LastActor = context.Actor(),
        //     Name = currUser.Name,
        //     Description = $"Test User for {appTitle}",
        //     UserRoleId = nameof(EntityRoleId.Admin),
        //     AppProfiles =
        //     {
        //         // TODO: add profile to client {{appTitle}}-admin ??? 
        //         // { BootstrAppClientProfileKey, },
        //         { appName, defaultProfile.Id }
        //     },
        //     Email = currUser.Email,
        //     Phone = currUser.Phone,
        //     Identities = currUser.Identities, // TODO: bad idea ...
        //     // FlowId =
        //     // ObjectStatusId =
        // };

        // var account = new Account
        // {
        //     Id = accountId,
        //     AccountId = accountId,
        //     EntityId = accountId,
        //     Name = appName,
        //     Description = $"BootstrApp {appDescription}",
        //     CreatedOn = DateTime.UtcNow,
        //     LastActor = context.Actor(),
        //     Settings = new AccountSettings
        //     {
        //         OwnerId = user.Id, // ???
        //     },
        // };

        // allow auto provision of all
        var defaultProvider = new AuthenticationProvider
        {
            Tenants = new Dictionary<string, TenantConfiguration>
            {
                {
                    "*", new TenantConfiguration
                    {
                        AccountId = currUser.AccountId,
                        AutoProvisionUser = new AutoProvisionUser
                        {
                            UserRole = EntityRoleId.Profile
                        },
                        AppProfiles = new Dictionary<string, AppClientProfile>
                        {
                            { nameof(EntityRoleId.Profile), new AppClientProfile { Id = defaultProfile.Id } }
                        }
                    }
                },
            },
        };

        var appClient = new AppClient
        {
            ClientId = appName,
            AccountId = currUser.AccountId,
            ClientName = appName,
            Description = $"Client for {appTitle}",
            AppProfiles = new Dictionary<string, AppClientProfile>
            {
                {
                    nameof(EntityRoleId.Profile), new AppClientProfile
                    {
                        Id = defaultProfile.Id,
                    }
                }
            },
            // RedirectUris =
            // AllowedCorsOrigins =
            // PostLogoutRedirectUris
            AllowedGrantTypes =
            [
                new ClientGrantType { GrantType = "authorization_code" },
            ],
            RequirePkce = true,
            AllowPlainTextPkce = true,
            AllowAccessTokensViaBrowser = true,
            AllowOfflineAccess = true, // ???
            AuthenticationProviders = new Dictionary<string, AuthenticationProvider>
            {
                { "Microsoft", defaultProvider },
                { "Google", defaultProvider },
                { "Salesforce",  defaultProvider},  
                { "GitHub",  defaultProvider},  
            },
            AllowedScopes =
            [
                new ClientScope { Scope = "openid" },
                new ClientScope { Scope = "profile" },
                new ClientScope { Scope = "rest" },
                new ClientScope { Scope = "email" },
            ]
        };

        defaultProfile.ClientId = appClient.ClientId;

        var app = new BootstrApp
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            Name = appName,
            Description = appDescription,
            Title = appTitle,
            AccountId = currUser.AccountId,
            ClientId = appClient.ClientId,
            ProfileId = defaultProfile.Id,
        };

        // await connection.InsertAsync(user);
        // await connection.InsertAsync(account);
        await connection.InsertAsync(defaultProfile);
        await connection.InsertAsync(appClient);
        await connection.InsertAsync(app);
        
        // add profile for the user so he can login
        currUser = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, currUser.AccountId)
            .Eq(x => x.Id, currUser.Id)
            .Update
            .Set(x => x.AppProfiles[appName], defaultProfile.Id)
            .UpdateAndGetOneAsync();
        
        // TODO: fire events

        // TODO: import standard objects ? 
        // ...
        return app;
    }

    [McpTool(
            Name = "add_oidc_client_redirect",
            Description = "Add Redirection Url to allowed list for the oidc client. ",
            ExamplePrompts =
            [
                "Register redirection url for oidc client",
                "Configure login client to allow redirecting to url"
            ]
        )
    ]
    public async Task<string> ConfigureOidcClient(
        IEntityContext context,
        [McpParameter(Description = "App Name", Required = true)]
        string appName,
        [McpParameter(Description = "Redirect Url", Required = true)]
        string redirectUrl
    )
    {
        var app = await GetAppAsync(context, appName);
        if (app == null) throw new McpToolException($"{appName} not found or hasn't been initialized");

        var client = await connection.Filter<AppClient>()
            .Eq(x => x.AccountId, app.AccountId)
            .Eq(x => x.ClientId, app.ClientId)
            .FirstOrDefaultAsync();

        if (client == null) throw new McpToolException($"oidc client for {appName} not found");

        if (client.RedirectUris?.Any(x => x.RedirectUri == redirectUrl) ?? false)
        {
            return $"Client already allows {redirectUrl} as a valid redirect";
        }

        client = await connection.Filter<AppClient>()
            .Eq(x => x.AccountId, app.AccountId)
            .Eq(x => x.ClientId, client.ClientId)
            .Update
            .AddToSet(x => x.RedirectUris, new ClientRedirectUri { RedirectUri = redirectUrl })
            .UpdateAndGetOneAsync();

        if (client == null) throw new McpToolException($"oidc client for {appName} not found");

        return $"{redirectUrl} added to allow list for oidc client";
    }

    private async Task<BootstrApp?> GetAppAsync(IEntityContext context, string appName)
    {
        return await connection.Filter<BootstrApp>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Name, appName)
            .FirstOrDefaultAsync();
    }

    public class CreateAppResult
    {
        [JsonPropertyName("id")] public required Guid Id { get; set; }
        [JsonPropertyName("name")] public required string Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("summary")] public required string Title { get; set; }
        [JsonPropertyName("accountId")] public required Guid AccountId { get; set; }
    }
}

[BsonCollection("bootstrsapp.App")]
public class BootstrApp : EntityOwnedModel
{
    public const string ObjectTypeFullName = "bootstrsapp.App";

    public required string Title { get; set; }
    public Guid ProfileId { get; set; }
    public string ClientId { get; set; }

    public string ObjectsNamespace => $"app.{Name}";
}