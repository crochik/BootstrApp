using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Models.Slack;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.Shared.Models;

namespace Services;

public class SlackClient
{
    private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    private HttpClient Client => _httpClientFactory.CreateClient();
    private readonly ILogger<SlackClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SlackClient(
        ILogger<SlackClient> logger,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Result<string>> SendMessageAsync(string url, SlackMessage message)
    {
        var json = JsonConvert.SerializeObject(message, _settings);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await Client.PostAsync(url, httpContent);
        var body = await response.Content.ReadAsStringAsync();

        string errorMessage = null;
        if (response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Successfully posted message to Slack");
            return Result.Success(body);
        }

        errorMessage = $"Failed to Post: {response.ReasonPhrase}";
        _logger.LogError("{statusCode}: {status} {body}", response.StatusCode, response.ReasonPhrase, body);
        return Result<string>.Error(errorMessage);
    }
}