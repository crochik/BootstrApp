// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net.Http;
// using System.Threading.Tasks;
// using IdentityModel.Client;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using PI.IDP.API.Api;
// using PI.IDP.API.Client;
// using PI.IDP.API.Model;
//
// namespace PI.Shared.IDP;
//
// public class IDPClient
// {
//     private readonly ILogger<IDPClient> _logger;
//     private readonly Config _config;
//
//     private Token _token;
//
//     private readonly Configuration _globalConfig;
//     private readonly HttpClient _httpClient;
//
//     private Configuration apiConfig(string accessToken = null) => new Configuration
//     {
//         UserAgent = "PI Scheduler API",
//         BasePath = _config?.BaseUrl,
//         AccessToken = accessToken
//     };
//
//     public IDPClient(
//         ILogger<IDPClient> logger,
//         IConfiguration config,
//         IHttpClientFactory clientFactory  
//     )
//     {
//         _logger = logger;
//         _config = config.GetSection(nameof(IDPClient)).Get<Config>();
//         _globalConfig = apiConfig();
//         _httpClient = clientFactory.CreateClient("IDP");
//     }
//
//     public async Task<string> ImpersonateAsync(Guid entityId, string accessToken)
//     {
//         var api = new TokenApi(apiConfig(accessToken));
//         var result = await api.ImpersonateAsync(new ImpersonateRequest(entityId));
//
//         return result?.AccessToken;
//     }
//
//     public async Task<DeriveResult> DeriveTokenAsync(Guid leadId, Guid? integrationId = null, string externalId = null)
//     {
//         await GetAccessTokenAsync();
//
//         var request = new DeriveToken
//         {
//             ClientId = _config.Scheduler.ClientId,
//             Scope = _config.Scheduler.Scope.Split(" ").ToList(),
//             Audience = _config.Scheduler.Audience.Split(" ").ToList(),
//             Claims = new List<ClaimPair> {
//                 new ClaimPair {Name = "lead_id", Value = leadId.ToString() }
//             }
//         };
//
//         if (integrationId.HasValue)
//         {
//             request.Claims.Add(new ClaimPair { Name = "integration_id", Value = integrationId.ToString() });
//             if (externalId != null) request.Claims.Add(new ClaimPair { Name = "external_id", Value = externalId });
//         }
//
//         var api = new TokenApi(_globalConfig);
//         var resp = await api.DeriveAsync(request);
//
//         return resp;
//     }
//
//     public async Task<string> GetAppointmentUrlAsync(Guid leadId, Guid appointmentTypeId, Guid? entityId = null, Guid? integrationId = null, string externalId = null)
//     {
//         var token = await DeriveTokenAsync(leadId, integrationId, externalId);
//
//         var schedulerUrl = _config.Scheduler.Url;
//         var url = schedulerUrl;
//         url += $"?apptType={appointmentTypeId}";
//         if (entityId.HasValue)
//         {
//             url += $"&entity={entityId.Value}";
//         }
//         url += $"&auth={token.AccessToken}";
//
//         return url;
//     }
//
//     private async Task<string> GetAccessTokenAsync()
//     {
//         if (_token == null || _token.IsExpired)
//         {
//             _logger.LogInformation("Renew PI token");
//             await RenewTokenAsync();
//
//             _globalConfig.AccessToken = _token.AccessToken;
//             // PI.IDP.API.Client.Configuration.Default.AccessToken = _token.AccessToken;
//         }
//         else
//         {
//             _logger.LogDebug("Use Cached PI Token");
//         }
//
//         return _token.AccessToken;
//     }
//
//     private async Task RenewTokenAsync()
//     {
//         var client = new TokenClient(_httpClient, new TokenClientOptions
//         {
//             Address = $"{_config.BaseUrl}/connect/token",
//             ClientId = _config.ClientId,
//             ClientSecret = _config.ClientSecret,
//             
//         });
//         
//         // var client = new TokenClient($"{_config.BaseUrl}/connect/token", _config.ClientId, _config.ClientSecret);
//         var resp = await client.RequestClientCredentialsTokenAsync(_config.Scope);
//         if (resp.IsError)
//         {
//             _logger.LogError("Failed to renew PI token: {error}", resp.Error);
//             throw new Exception($"Failed to renew PI token: {resp.Error}");
//         }
//         _token = new Token
//         {
//             AccessToken = resp.AccessToken,
//             ExpiresIn = resp.ExpiresIn
//         };
//
//         _logger.LogInformation("Renewd PI Access Token");
//     }
//
//     class Token
//     {
//         public string AccessToken { get; set; }
//         public int ExpiresIn { get; set; }
//         public DateTime AcquiredOn { get; set; } = DateTime.UtcNow;
//         public DateTime ExpiresOn
//         {
//             get
//             {
//                 return AcquiredOn.AddSeconds(ExpiresIn);
//             }
//         }
//         public bool IsExpired
//         {
//             get
//             {
//                 return (ExpiresOn - DateTime.UtcNow).TotalMinutes < 5;
//             }
//         }
//     }
//
//     class Config
//     {
//         public class SchedlerConfig
//         {
//             public string Url { get; set; }
//             public string Scope { get; set; }
//             public string Audience { get; set; }
//             public string ClientId { get; set; }
//         }
//
//         public string BaseUrl { get; set; }
//         public string ClientId { get; set; }
//         public string ClientSecret { get; set; }
//         public string Scope { get; set; }
//
//         public SchedlerConfig Scheduler { get; set; }
//     }
// }