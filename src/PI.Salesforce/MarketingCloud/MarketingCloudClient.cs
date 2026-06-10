using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.Shared.Exceptions;

namespace Services;

public class MarketingCloudClient
{
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    private HttpClient _client
    {
        get
        {
            var client = _httpClientFactory.CreateClient(nameof(MarketingCloudClient));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }

    public MarketingCloudClient(ILogger<MarketingCloudClient> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<MarketingCloudToken> GetTokenAsync(string subdomain, string clientId, string clientSecret, string scope=null)
    {
        var request = new ClientCredentialsRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scope = scope,
        };
        
        var url = $"https://{subdomain}.auth.marketingcloudapis.com/v2/token";
        var json = JsonConvert.SerializeObject(request, JsonSerializerSettings);
        var content = new StringContent(json, null, "application/json");
        var response = await _client.PostAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException("Invalid Authorization Code");
        }

        return JsonConvert.DeserializeObject<MarketingCloudToken>(body);
    }

    public async Task<UpsertDataExtensionResponse> UpsertDataEventAsync(MarketingCloudToken token, string extensionExternalId, string primaryKeyField, string primaryKeyValue, UpsertDataExtensionRequest request)
    {
        var url = $"{token.RestUrl}/hub/v1/dataevents/key:{extensionExternalId}/rows/{primaryKeyField}:{primaryKeyValue}";
        var json = JsonConvert.SerializeObject(request, JsonSerializerSettings);
        
        var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.SetToken("Bearer", token.AccessToken);
        req.Content = new StringContent(json, null, "application/json");

        var response = await _client.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("{URL}: Request Failed: {Request} {Response}", url, json, body);
            
            // {
            //     "message": "Unable to save rows for data extension ID dcf7a0f9-ff23-ef11-b859-48df37dc1382",
            //     "errorcode": 10006,
            //     "documentation": "",
            //     "additionalErrors": [
            //     {
            //         "message": "Email: The value for column [Email] is not a valid email address. Parse error [InvalidEmailAddress]",
            //         "errorcode": 10000,
            //         "documentation": ""
            //     },
            //     {
            //         "message": "Phone: The value for column [Phone] is not a valid phone number. Parse error [ExactTarget.Core.Validation.ValidationResult[]]",
            //         "errorcode": 10000,
            //         "documentation": ""
            //     }
            //     ]
            // }
            
            throw new BadRequestException("Upsert Failed");
        }

        return JsonConvert.DeserializeObject<UpsertDataExtensionResponse>(body, JsonSerializerSettings);
    }

    public class ClientCredentialsRequest
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }
        
        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }
        
        [JsonProperty("grant_type")]
        public string GrantType => "client_credentials";
        
        [JsonProperty("scope")]
        public string Scope { get; set; }
    }

    public class UpsertDataExtensionRequest
    {
        public IDictionary<string, object> Values { get; set; }
    }
    
    public class UpsertDataExtensionResponse
    {
        public Dictionary<string, object> Keys { get; set; }
        public Dictionary<string, object> Values { get; set; }
    }
    
}