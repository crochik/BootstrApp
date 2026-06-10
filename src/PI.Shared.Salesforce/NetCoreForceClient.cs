using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Logging;
using IdentityModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetCoreForce.Client;
using NetCoreForce.Client.Models;
using PI.Shared.Salesforce.Models;

namespace PI.Shared.Salesforce;

public class SalesforceConfig
{
    public string TokenRequestEndpointUrl { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    public static SalesforceConfig Get(IConfiguration configuration)
        => configuration.GetSection(nameof(NetCoreForceClient)).Get<SalesforceConfig>();
}

public class NetCoreForceClient
{
    private readonly SalesforceConfig _configuration;

    public string TokenRequestEndpointUrl => _configuration.TokenRequestEndpointUrl;
    public string ClientId => _configuration.ClientId;
    private string ClientSecret => _configuration.ClientSecret;
    private readonly ILogger<NetCoreForceClient> _logger;

    public NetCoreForceClient(
        ILogger<NetCoreForceClient> logger,
        IConfiguration configuration
    )
    {
        _logger = logger;
        _configuration = SalesforceConfig.Get(configuration);
    }

    /// <summary>
    /// Generic POST call to Salesforce (for services, e.g. .../services/apexrest/PI/Lead) 
    /// </summary>
    public async Task<T> PostAsync<T>(SalesforceToken token, string path, object body)
    {
        var client = GetClient(token);
        var uri = new Uri(UriFormatter.BaseUri(client.InstanceUrl), path);
        var jsonClient = new JsonClient(token.AccessToken);

        return await jsonClient.HttpPostAsync<T>(body, uri);
    }

    public async Task<SalesforceToken> RefreshTokenAsync(string refreshToken, bool autoRetry = true)
    {
        using var auth = new AuthenticationClient();

        try
        {
            await auth.TokenRefreshAsync(refreshToken, ClientId, ClientSecret, TokenRequestEndpointUrl);

            var url = $"{auth.AccessInfo.InstanceUrl}/services/oauth2/introspect";
            var introspect = await auth.IntrospectTokenAsync(auth.AccessInfo.AccessToken, ClientId, ClientSecret, url);

            var issuedAt = introspect.IssuedAt.ToDateTimeFromEpoch();
            var notBefore = introspect.NotBefore.ToDateTimeFromEpoch();
            var expiration = introspect.Expiration.ToDateTimeFromEpoch();

            return new SalesforceToken
            {
                InstanceUrl = auth.AccessInfo.InstanceUrl,
                ApiVersion = auth.ApiVersion,
                AccessToken = auth.AccessInfo.AccessToken,
                RefreshToken = auth.AccessInfo.RefreshToken,
                TokenType = auth.AccessInfo.TokenType,
                ExpiresIn = (int)(expiration - issuedAt).TotalSeconds,
                Expiration = expiration,
            };
        }
        catch (ForceAuthException ex)
        {
            if (autoRetry)
            {
                _logger.LogError(ex, "Failed to refresh token: retry in one second");
                await Task.Delay(1000);
                return await RefreshTokenAsync(refreshToken, false);
            }

            _logger.LogError(ex, "Failed to refresh token after retry");
            return null;
        }
    }

    public async Task<SalesforceToken> LoginAsync(string endpointUrl, string user, string password)
    {
        var auth = new AuthenticationClient();
        try
        {
            // await auth.UsernamePasswordAsync(ClientId, ClientSecret, user, password, endpointUrl ?? TokenRequestEndpointUrl);
            await auth.UsernamePasswordAsync(ClientId, ClientSecret, user, password);
        }
        catch (ForceAuthException ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            return null;
        }

        return new SalesforceToken
        {
            InstanceUrl = auth.AccessInfo.InstanceUrl,
            ApiVersion = auth.ApiVersion,
            AccessToken = auth.AccessInfo.AccessToken,
            RefreshToken = auth.AccessInfo.RefreshToken,
            TokenType = auth.AccessInfo.TokenType,
            // ExpiresIn 
        };
    }

    public async Task<SObjectDescribeFull> DescribeAsync(SalesforceToken token, string objectType)
    {
        // return await GetClient(token).DescribeAsync<ObjectMetaData>(objectType);   
        return await GetClient(token).GetObjectDescribe(objectType);
    }

    public async Task<T> QueryByIdAsync<T>(SalesforceToken token, string objectType, string id, IEnumerable<string> fields = null)
    {
        using var scope = _logger.AddScope(new
        {
            ObjectType = objectType,
            Id = id,
        });

        var start = DateTime.UtcNow;

        try
        {
            return await GetClient(token).GetObjectById<T>(objectType, id, fields?.ToList());
        }
        finally
        {
            _logger.LogInformation("Got object in {ms}", (DateTime.UtcNow - start).TotalMilliseconds);
        }
    }

    public async Task<List<T>> QueryAsync<T>(SalesforceToken token, string query)
    {
        // var result = await GetClient(token).QueryAllAsync<T>(query);
        // return result.Records;
        var cursor = GetClient(token).QueryAsync<T>(query);
        var list = new List<T>();
        await foreach (var item in cursor)
        {
            list.Add(item);
        }

        return list;
    }

    public Task<List<T>> QueryAllAsync<T>(SalesforceToken token, string query)
    {
        // var result = await GetClient(token).QueryAllAsync<T>(query);
        // return result.Records;
        return GetClient(token).Query<T>(query);
    }

    public async Task<(string id, string error)> CreateAsync(SalesforceToken token, string objectType, object sfObject)
    {
        var resp = await GetClient(token).CreateRecord(objectType, sfObject);
        if (!resp.Success)
        {
            _logger.LogError("Failed to create {objectType} without exception", objectType);
            return (null, $"Failed to create {objectType} without exception");
        }

        return (resp.Id, null);
    }

    public async Task UpdateAsync(SalesforceToken token, string objectType, string objectId, object sfObject)
    {
        await GetClient(token).UpdateRecord(objectType, objectId, sfObject);
    }

    private static ForceClient GetClient(SalesforceToken token) => new(token.InstanceUrl, token.ApiVersion, token.AccessToken);
}