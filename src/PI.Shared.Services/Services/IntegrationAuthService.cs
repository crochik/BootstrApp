using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.SingleUseTickets;
using PI.Shared.Services.DataProtection;

namespace PI.Shared.Services;

public class IntegrationAuthService
{
    private readonly ILogger<IntegrationAuthService> _logger;
    private readonly MongoConnection _connection;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DataProtectionService _dataProtectionService;
    private readonly ObjectTypeService _objectTypeService;
    private HttpClient Client => _httpClientFactory.CreateClient(nameof(IntegrationAuthService));
    private readonly string _baseUrl;

    public IntegrationAuthService(
        ILogger<IntegrationAuthService> logger,
        MongoConnection connection,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        DataProtectionService dataProtectionService,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _httpClientFactory = httpClientFactory;
        _dataProtectionService = dataProtectionService;
        _objectTypeService = objectTypeService;
        _baseUrl = configuration.GetValue<string>("BaseUrl");
    }

    private static Guid? GetIntegrationId(string integration)
    {
        return integration switch
        {
            "GitHub" => IntegrationIds.GitHub,
            _ => default(Guid?)
        };
    }

    public Task<Result<string>> GetLoginUrlAsync(IEntityContext context, string integration)
        => GetLoginUrlAsync(context, GetIntegrationId(integration).Value);

    public async Task<Result<string>> GetLoginUrlAsync(IEntityContext context, Guid integrationId)
    {
        var integration = IntegrationIds.GetName(integrationId);
        var ticket = await _connection.InsertAsync(new IntegrationSingleUseTicket
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.OrganizationId ?? context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Name = $"Add {integration} integration",
            ExpiresOn = DateTime.UtcNow.AddMinutes(10),
            IsActive = true,
            IntegrationId = integrationId,
        });

        var options = await GetOAuthOptionsAsync(integrationId);
        if (!options.IsSuccess) return options.ConvertTo<string>();

        var redirectUri = WebUtility.UrlEncode($"{_baseUrl}/api/v1/integration/{integration}/redirect");
        var url = $"{options.Value.AuthorizationEndpoint}?client_id={options.Value.ClientId}&redirect_uri={redirectUri}&state={ticket.Id}&prompt=select_account";

        return Result.Success(url);
    }

    public async Task<Result<OAuthOptions>> GetOAuthOptionsAsync(Guid integrationId)
    {
        await Task.CompletedTask;

        var options = new OAuthOptions
        {
            ClientId = "Iv23liOaIKdSmOqPmhyC",
            ClientSecret = "2cbb72e642b3ba589b289cccabf61831cee781b3",
            TokenEndpoint = "https://github.com/login/oauth/access_token",
            AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
            UserInformationEndpoint = "https://api.github.com/user",
        };

        return Result.Success(options);
    }

    public Task<Result<IntegrationConfiguration>> RedirectFromLoginAsync(string integration, IQueryCollection query)
        => RedirectFromLoginAsync(GetIntegrationId(integration).Value, query);

    public async Task<Result<IntegrationConfiguration>> RedirectFromLoginAsync(Guid integrationId, IQueryCollection query)
    {
        var state = (!query.TryGetValue("state", out var stateStr) || stateStr.Count != 1) ? null : stateStr[0];

        // default implementation 
        // ... 

        if (!query.TryGetValue("code", out var codes) || codes.Count != 1) return Result.Error<IntegrationConfiguration>("Missing code");
        var code = codes[0];

        var integration = await _connection.Filter<Integration>()
            .Eq(x => x.Id, integrationId)
            .FirstOrDefaultAsync();

        // oauth? 
        var options = await GetOAuthOptionsAsync(integrationId);
        if (!options.IsSuccess) return options.ConvertTo<IntegrationConfiguration>();

        var now = DateTime.UtcNow;

        var rawToken = await Client.GetAccessTokenAsync(options.Value, code);

        var externalId = default(string);
        var data = default(IDictionary<string, object>);
        if (options.Value.UserInformationEndpoint != null)
        {
            // TODO: may need some more flexibility here 
            // ... 

            // get user from integration 

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, options.Value.UserInformationEndpoint);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            requestMessage.Headers.Add("Authorization", $"Bearer {rawToken.AccessToken}");
            requestMessage.Headers.Add("User-Agent", "ProgramInterface.com");

            data = await Client.SendAsync<ExpandoObject>(requestMessage);

            //  TODO: need some mapping
            // ... 

            if (!data.TryGetStrParam("login", out externalId))
            {
                // ...
            }
        }

        var entity = default(Entity);
        if (state != null && Guid.TryParse(state, out var singleUseTicketId))
        {
            var ticket = await _connection.Filter<IntegrationSingleUseTicket>()
                .Eq(x => x.Id, singleUseTicketId)
                .Gt(x => x.ExpiresOn, DateTime.UtcNow)
                .Eq(x => x.IsActive, true)
                .Eq(x => x.IntegrationId, integrationId)
                .Update
                .Set(x => x.IsActive, false)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            if (ticket == null) return Result.Error<IntegrationConfiguration>("Bad state");

            entity = await _connection.Filter<Entity>()
                .Eq(x => x.AccountId, ticket.AccountId)
                .Eq(x => x.Id, ticket.EntityId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }
        else if (!string.IsNullOrEmpty(externalId))
        {
            // try to find local entity for that user
            entity = await _connection.Filter<Entity>()
                .ElemMatchBuilder(x => x.Identities, q => q
                    .Eq(x => x.IdentityProviderId, integration.Name)
                    .Eq(x => x.ExternalId, externalId)
                )
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }

        if (entity == null) return Result.Error<IntegrationConfiguration>("Invalid Entity");

        var context = entity.Context;

        var token = await ProtectAsync(context, integrationId, rawToken);
        token.ExpiresOn = now.AddSeconds(rawToken.ExpiresIn); // ???

        var id = Guid.NewGuid();
        var integrationConfiguration = await _connection.Filter<IntegrationConfigurationWithToken>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.EntityId, entity.Id)
            .Eq(x => x.IntegrationId, integrationId)
            .Update
            .SetOnInsert(x => x.AccountId, entity.AccountId)
            .SetOnInsert(x => x.EntityId, entity.Id)
            .SetOnInsert(x => x.Id, id)
            .SetOnInsert(x => x.IntegrationId, integrationId)
            .SetOnInsert(x => x.Name, integration.Name)
            .SetOnInsert(x => x.CreatedOn, now)
            // .SetOnInsert(x => x.Description, integration.Name)
            .Set(x => x.LastModifiedOn, now)
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.Token, token)
            .UpdateAndGetOneAsync(true);

        // TODO: fire event?
        // can't right now because it doesn't extend IFlowObject
        // ...

        if (integration.Id == id)
        {
            await AddIdentityAsync(context, entity, integration.Name, externalId, data);
        }
        
        // TODO: update token for identity?
        // ...

        return Result.Success<IntegrationConfiguration>(integrationConfiguration);
    }

    private async Task<bool> AddIdentityAsync(IEntityContext context, Entity entity, string integrationName, string externalId, IDictionary<string, object> data)
    {
        if (entity.Identities?.FirstOrDefault(x => x.IdentityProviderId == integrationName) != null)
        {
            _logger.LogInformation("{OrganizationId} already has a {integration} identity", integrationName, entity.Id);
            return false;
        }


        // add identity to organization
        var update = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.Id, entity.Id)
            .Update
            .AddToSet(x => x.Identities, new EntityIdentity
            {
                Id = Guid.NewGuid(),
                IdentityProviderId = nameof(ExternalProvider.CompanyCam),
                ExternalId = externalId,
                Data = data?.ToDictionary(),
            })
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();

        if (update.ModifiedCount != 1)
        {
            _logger.LogError("Failed to add {integration} identity", integrationName);
            return false;
        }

        await _objectTypeService.FireObjectUpdatedAsync(context, entity, new Dictionary<string, object>
        {
            { nameof(Entity.Identities), integrationName }
        }, e =>
        {
            e.Description = $"Added {integrationName} Identity";
            e.TryAddMetaValue(nameof(EntityIdentity.IdentityProviderId), integrationName);
            e.TryAddMetaValue(nameof(EntityIdentity.ExternalId), externalId);
        });

        return true;
    }
    
    private async Task<Result<string>> UnprotectAsync(IEntityContext context, Guid integrationId, string protectedString)
    {
        if (string.IsNullOrEmpty(protectedString))
        {
            return Result.Error<string>("Missing Token");
        }

        try
        {
            var config = GetDateProtectionConfig(integrationId);
            var unprotected = await _dataProtectionService.UnprotectAsync(context, config, protectedString);
            return Result.Success(unprotected);
        }
        catch (Exception ex)
        {
            return Result.Error<string>(ex.Message);
        }
    }

    private async Task<IntegrationToken> ProtectAsync(IEntityContext context, Guid integrationId, Token token)
    {
        var config = GetDateProtectionConfig(integrationId);
        var accessToken = await _dataProtectionService.ProtectAsync(context, config, token.AccessToken);
        var refreshToken = await _dataProtectionService.ProtectAsync(context, config, token.RefreshToken);

        return new IntegrationToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
        };
    }

    private async Task<IntegrationToken> ProtectAsync(IEntityContext context, Guid integrationId, IntegrationToken token)
    {
        var config = GetDateProtectionConfig(integrationId);
        var accessToken = await _dataProtectionService.ProtectAsync(context, config, token.AccessToken);
        var refreshToken = await _dataProtectionService.ProtectAsync(context, config, token.RefreshToken);

        token.AccessToken = accessToken;
        token.RefreshToken = refreshToken;

        return token;
    }

    public async Task<Result<string>> GetAccessTokenAsync(IEntityContext context, IntegrationConfigurationWithToken integration)
    {
        // TODO: check if it is still valid 
        // ....

        if (integration.Token.ExpiresOn < DateTime.UtcNow)
        {
            var refreshToken = await GetRefreshTokenAsync(context, integration);
            if (!refreshToken.IsSuccess)
            {
                _logger.LogError("Failed to get RefreshToken: {Reason}", refreshToken.Status);
                return refreshToken;
            }

            var authOptions = await GetOAuthOptionsAsync(integration.IntegrationId);
            if (!authOptions.IsSuccess)
            {
                _logger.LogError("Failed to get Options: {Reason}", authOptions.Status);
                return authOptions.ConvertTo<string>();
            }

            var token = await Client.RefreshTokenAsync(authOptions.Value, refreshToken.Value);
            
            // TODO: save 
            // ...
            
            return Result.Success(token.AccessToken);
        }
        
        return await UnprotectAsync(context, integration.IntegrationId, integration?.Token?.AccessToken);
    }

    private Task<Result<string>> GetRefreshTokenAsync(IEntityContext context, IntegrationConfigurationWithToken integration)
        => UnprotectAsync(context, integration.IntegrationId, integration?.Token?.RefreshToken);

    private DataProtectionConfig GetDateProtectionConfig(Guid integrationId)
    {
        // var integration = await _connection.Filter<IntegrationConfigurationWithToken>()
        //     .Eq(x => x.AccountId, context.AccountId)
        //     .Eq(x => x.EntityId, context.GetOwnerEntityId())
        //     .Eq(x => x.IntegrationId, integrationId)
        //     .FirstOrDefaultAsync();
        //
        // if (integration == null) return Result.Error<string>("Integration not configured");
        //
        
        var purpose = "EntityIntegration.Configuration";
        if (IntegrationIds.All.TryGetValue(integrationId, out var name))
        {
            purpose = $"EntityIntegration.{name}";
        }
        else
        {
            // TODO: load integration and get it from there 
            // var integration = await _connection.Filter<Integration>()
            //     .Eq(x => x.Id, integrationId)
            //     .FirstOrDefaultAsync();
            // ...
        }
        
        var config = new MicrosoftDataProtectionConfig
        {
            Purpose = purpose,
        };

        return config;
    }

    public async Task<Result<IntegrationConfigurationWithToken>> GetIntegrationConfigurationAsync(IEntityContext context, Guid integrationId)
    {
        var entityId = context.Role switch
        {
            EntityRoleId.Admin => context.AccountId,
            EntityRoleId.Manager => context.OrganizationId,
            _ => default,
        };

        if (!entityId.HasValue) return Result.Error<IntegrationConfigurationWithToken>("Invalid user");

        var config = await _connection.Filter<IntegrationConfigurationWithToken>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, entityId.Value)
            .Eq(x => x.IntegrationId, integrationId)
            .FirstOrDefaultAsync();

        if (config == null) return Result.Error<IntegrationConfigurationWithToken>("Integration not configured");

        return Result.Success(config);
    }
}