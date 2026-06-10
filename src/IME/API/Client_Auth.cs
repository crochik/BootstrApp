using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IME.API;

public partial class Client
{
    private readonly IOptions<Config> _options;
    public AuthenticationToken Token { get; private set; }

    public Client(
        ILogger<Client> logger,
        IOptions<Config> options,
        IHttpClientFactory clientFactory        
        ) 
    {
        BaseUrl = options.Value.Url;
        
        _httpClient = clientFactory.CreateClient();
        _settings = new System.Lazy<Newtonsoft.Json.JsonSerializerSettings>(CreateSerializerSettings);
        _options = options;
    }

    partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url)
    {
        if (!string.IsNullOrEmpty(Token?.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token.AccessToken);
        }
    }

    partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, System.Text.StringBuilder urlBuilder)
    {
        if (!string.IsNullOrEmpty(Token?.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token.AccessToken);
        }
    }

    /// <summary>
    /// Authenticate client
    /// </summary>
    public async Task LoginAsync()
    {
        // TODO: keep token and only login the first time or if it is about to expire
        // ...

        Token = await LoginAsync(new IME.API.MicUserLoginRequest
        {
            UserName = _options.Value.UserName,
            Password = _options.Value.Password,
        });
    }
}

public class Config
{
    public string Url { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}