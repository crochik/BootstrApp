using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Typeform.Services;

public class TypeformClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const string AuthorizationEndpoint = "https://api.typeform.com/oauth/authorize";
    private const string TokenEndpoint = "https://api.typeform.com/oauth/token";
    private const string UserInformationEndpoint = "https://api.typeform.com/me";

    private string ClientId => _configuration.ClientId;
    private string ClientSecret => _configuration.ClientSecret;
    private string RedirectUrl => _configuration.RedirectUrl;

    private static string[] Scopes =
    {
        "accounts:read",
        "forms:read",
        "responses:read",
        "webhooks:read",
        "webhooks:write",
        "offline"
    };

    private HttpClient Client
    {
        get
        {
            var client = _httpClientFactory.CreateClient(nameof(TypeformClient));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }

    private readonly Configuration _configuration;

    public TypeformClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;

        _configuration = configuration.GetSection("Typeform").Get<Configuration>();
    }

    public string GetUrl(string state)
    {
        return $"{AuthorizationEndpoint}?client_id={ClientId}&state={state}&redirect_uri={RedirectUrl}&scope={string.Join(" ", Scopes)}";
    }

    public async Task<Token> GetTokenAsync(string code)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "client_secret", ClientSecret },
            { "grant_type", "authorization_code" },
            { "redirect_uri", RedirectUrl },
            { "code", code }
        };

        var response = await Client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // boyd = {
            //     "code": "bad_request",
            //     "description": "Bad Request: bad request: {\"code\":\"INVALID_AUTHORIZATION\",\"description\":\"Invalid authorization: Code.RedirectURI differs from Request argument\"}\n",
            //     "help": "https://developers.typeform.com/get-started/authentication/"
            // }

            throw new BadRequestException("Invalid Authorization Code");
        }

        return JsonConvert.DeserializeObject<Token>(body);
    }

    public async Task<User> GetUserAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await Client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException($"An error occurred when retrieving user information ({response.StatusCode})");
        }

        var body = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<User>(body);
    }

    public class Configuration
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUrl { get; set; }
    }

    public class User
    {
        public string Alias { get; set; }
        public string Email { get; set; }
        public string Language { get; set; }

        [JsonProperty("user_id")] public string UserId { get; set; }

        [JsonProperty("tracking_id")] public int TrackingId { get; set; }
    }
}

public class Token
{
    [JsonProperty("access_token")] public string AccessToken { get; set; }

    [JsonProperty("token_type")] public string TokenType { get; set; }

    [JsonProperty("refresh_token")] public string RefreshToken { get; set; }

    [JsonProperty("state")] public string State { get; set; }

    [JsonProperty("expires_in")] public int ExpiresIn { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [BsonElement] public DateTime ExpiresOn => CreatedOn.AddSeconds(ExpiresIn);
}

[BsonDiscriminator("typeform")]
public class TypeformIntegrationConfiguration : IntegrationConfiguration
{
    public TypeformIntegrationConfiguration()
    {
        IntegrationId = IntegrationIds.TypeForm;
    }

    public Token Token { get; set; }

    public string UserId { get; set; }
    public string Alias { get; set; }
    public string Email { get; set; }

    public string PersonalAccessToken { get; set; }
}