using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using Entity = PI.Shared.Models.Entity;
using User = PI.Shared.Models.User;

namespace PI.Shared.O365;

public class O365AuthClient
{
    private readonly ILogger<O365AuthClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MongoConnection _connection;
    private readonly IEntityIdentityAdapter _identityAdapter;
    public O365Config Config { get; }

    public O365AuthClient(
        ILogger<O365AuthClient> logger, 
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        MongoConnection connection,
        IEntityIdentityAdapter identityAdapter
        )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _connection = connection;
        _identityAdapter = identityAdapter;

        Config = config.GetSection(O365Config.Section).Get<O365Config>();
    }
    
    public async Task<bool> UpdateTokenAsync(Guid accountId, string tenant)
    {
        if (!Guid.TryParse(tenant, out var tenantId))
        {
            _logger.LogError("Failed to parse {tenant}", tenant);
            return false;
        }

        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.Id, accountId)
            .FirstOrDefaultAsync();

        if (account?.FirstIdentity(ExternalProvider.Microsoft) is not EntityIdentity accountIdentity)
        {
            _logger.LogError("Did not find {accountId} from state", accountId);
            return false;
        }

        if (!Guid.TryParse(accountIdentity.ExternalId, out var accountTenantId) || accountTenantId != tenantId)
        {
            _logger.LogError("Tenant mismatch for {account}: {externalId} vs {tenant}", account.Id, accountIdentity.ExternalId, tenant);
            return false;
        }

        _logger.LogWarning("Successfully got admin consent for {accountId} from {tenant}", accountId, tenant);

        var token = await GetTokenAsync(account, true);
        return token != null;
    }

    private async Task<Token> GetTokenAsync(User user)
    {
        if (user?.FirstIdentity(ExternalProvider.Microsoft) is not EntityIdentity identity)
        {
            _logger.LogError("Did not find {accountId} from state", user.Id);
            return null;
        }

        if (identity?.ExternalIdentity?.Token?.AccessToken != null &&
            DateTime.UtcNow - identity.ExternalIdentity.Token.Expiration > TimeSpan.FromMinutes(10))
        {
            return identity.ExternalIdentity.Token;
        }

        if (identity?.ExternalIdentity?.Token?.RefreshToken != null)
        {
            return await RefreshTokenAsync(identity.ExternalIdentity);
        }

        return null;
    }

    private async Task<Token> GetTokenAsync(Account account, bool force = false)
    {
        if (account?.FirstIdentity(ExternalProvider.Microsoft) is not EntityIdentity accountIdentity)
        {
            _logger.LogError("Did not find {accountId} from state", account.Id);
            return null;
        }

        if (!Guid.TryParse(accountIdentity.ExternalId, out var tenantId))
        {
            _logger.LogError("Failed to parse {tenant}", accountIdentity.ExternalId);
            return null;
        }

        if (accountIdentity?.ExternalIdentity?.Token?.AccessToken == null)
        {
            if (!force) return null;
        }
        else
        {
            if ((accountIdentity.ExternalIdentity.Token.Expiration - DateTime.UtcNow) > TimeSpan.FromMinutes(10))
            {
                return accountIdentity.ExternalIdentity.Token;
            }
        }

        var token = await GetTokenForTenantAsync(tenantId);
        if (token == null)
        {
            _logger.LogError("Failed to get token for {tenant}", tenantId);
            return null;
        }

        accountIdentity.ExternalIdentity ??= new ExternalIdentity();
        // accountIdentity.ExternalIdentity.ExternalId = accountIdentity.ExternalId;
        // accountIdentity.ExternalIdentity.Provider = ExternalProvider.Microsoft;
        accountIdentity.ExternalIdentity.Token = token;

        if (!await _identityAdapter.UpdateTokenAsync(account, accountIdentity))
        {
            _logger.LogError("Failed to store token");
        }

        return token;
    }


    private async Task<Token> GetTokenAsync(string url, HttpContent content)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await client.PostAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new CalendarServiceException(CalendarErrorCode.FailedToRefresh);
        }

        var token = JsonConvert.DeserializeObject<O365Token>(body);
        return new Token
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            Expiration = DateTime.UtcNow.Add(TimeSpan.FromSeconds(token.ExpiresIn))
        };
    }

    public async Task<GraphServiceClient> GetClientForTenantAsync(Guid tenantId)
    {
        var token = await GetTokenForTenantAsync(tenantId);
        if (token.HasExpired)
        {
            // ...
            throw new NotImplementedException();
        }

        return CreateClient(token.AccessToken);
    }
    
    private GraphServiceClient CreateClient(string accessToken)
    {
        var authProvider = new DelegateAuthenticationProvider(
            requestMessage =>
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                return Task.CompletedTask;
            }
        );

        var client = new GraphServiceClient(authProvider)
        {
            BaseUrl = Config.BaseUrl
        };

        return client;
    }
    
    private async Task<Token> GetTokenForTenantAsync(Guid tenantId)
    {
        var url = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        // TODO: CHECK WHAT IS ACTUALLY NEEDED
        // ...

        //     var client = new ConfidentialClientApplication(
        //         _config.ClientId,
        //         String.Format(_config.AuthorityFormat, tenantId),
        //         _config.RedirectUri,
        //         new ClientCredential(_config.ClientSecret),
        //         null,
        //         new TokenCache()
        //     );

        var form = new Dictionary<string, string>
        {
            { "client_id", Config.ClientId },
            { "client_secret", Config.ClientSecret },
            { "scope", "https://graph.microsoft.com/.default" },
            { "grant_type", "client_credentials" },
        };

        return await GetTokenAsync(url, new FormUrlEncodedContent(form));
    }
    
    private async Task<Token> RefreshTokenAsync(ExternalIdentity identity)
    {
        // https://developer.microsoft.com/en-us/graph/docs/concepts/auth_v2_user
        var form = new Dictionary<string, string>
        {
            { "client_id", Config.ClientId },
            { "client_secret", Config.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", identity.Token.RefreshToken }
            // {"redirect_uri", redirect},
            // {"scope", scope},
        };

        identity.Token = await GetTokenAsync(Config.TokenEndpoint, new FormUrlEncodedContent(form));
        await _identityAdapter.UpdateValueAsync(identity);

        return identity.Token;
    }
    
    public async Task<GraphServiceClient> GetClientAsync(ExternalIdentity identity)
    {
        if (identity.Token.HasExpired)
        {
            await RefreshTokenAsync(identity);
        }

        // TODO: add caching?
        // ...

        return CreateClient(identity.Token.AccessToken);
    }

    public GraphServiceClient GetClient(Account account)
    {
        if (account == null || !account.IsActive) throw new NotFoundException(nameof(Account), account.Id);

        var authProvider = new DelegateAuthenticationProvider(
            async requestMessage =>
            {
                var token = await GetTokenAsync(account);
                if (token != null)
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                }
            }
        );

        var client = new GraphServiceClient(authProvider)
        {
            // use beta version :o
            BaseUrl = Config.BaseUrl
        };

        return client;
    }

    public async Task<GraphServiceClient> GetClientAsync(IEntityContext context)
    {
        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.Id, context.AccountId)
            .FirstOrDefaultAsync();

        if (account == null || !account.IsActive) throw new NotFoundException(nameof(Account), context.AccountId);

        switch (context.Role)
        {
            case EntityRoleId.Account:
            case EntityRoleId.Admin:
            {
                var tenantToken = await GetTokenAsync(account);
                return tenantToken == null ? null : CreateClient(tenantToken.AccessToken);
            }

            case EntityRoleId.Manager:
            case EntityRoleId.User:
                break;

            default:
                // throw?
                return null;
        }

        // user 
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, context.UserId)
            .FirstOrDefaultAsync();

        if (user == null || !user.IsActive) return null;
        if (!user.TryGetMicrosoftTenantId(out var tenantId))
        {
            _logger.LogError("Did not find microsoft tenant for {userId}", user.Id);
            return null;
        }

        if (account.TryGetMicrosoftTenantId(out var accountTenantId) && accountTenantId == tenantId)
        {
            // try to use token for account
            var tenantToken = await GetTokenAsync(account);
            if (tenantToken != null) return CreateClient(tenantToken.AccessToken);
        }

        // try to use user token
        var token = await GetTokenAsync(user);
        return token == null ? null : CreateClient(token.AccessToken);
    }
}

public class O365Config
{
    public const string Section = "Office365";

    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RedirectUri { get; set; }
    public string AuthorityFormat { get; set; } = "https://login.microsoftonline.com/{0}/v2.0";
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";
    public string TokenEndpoint { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    public string BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0"; // "https://graph.microsoft.com/beta";
    public string NotificationControllerUrl { get; set; } = "https://api.fci.cloud/o365/v1/Notification";
}