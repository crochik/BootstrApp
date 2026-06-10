using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Qvinci.Models;

namespace Qvinci;

public class Client
{
    private const string BASEURL = "https://api.qvinci.com/v1/Reporting/";
    private readonly ILogger<Client> _logger;
    private readonly IOptions<Config> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient _client => _httpClientFactory.CreateClient("Qvinci");

    private string API_KEY => _options.Value.ApiKey;

    public Client(
        ILogger<Client> logger,
        IOptions<Config> options,
        IHttpClientFactory httpClientFactory
    )
    {
        _logger = logger;
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    private static string BuildUrl(QvinciLocation location, QvinciReport report)
    {
        var url = report switch
        {
            QvinciReport.Aging => "Aging/?VerticalAnalysisType=None&AP=false&AR=true",
            QvinciReport.AP => "Aging/?VerticalAnalysisType=None&AP=true&AR=false",
            QvinciReport.BalanceSheet_LastYear => "BalanceSheet/?DateFrequency=Monthly&UseAccountMapping=true&VerticalAnalysisType=None&IncludeComputedColumns=true&OrderBy=&RelativeDateRange=LastCalendarYear&UseCustomDateRange=false",
            QvinciReport.BalanceSheet_YTD => "BalanceSheet/?DateFrequency=Monthly&UseAccountMapping=true&VerticalAnalysisType=None&IncludeComputedColumns=true&OrderBy=&RelativeDateRange=ThisCalendarYearToDate&UseCustomDateRange=false",
            QvinciReport.PNL_LastYear => "ProfitAndLoss/?DateFrequency=Monthly&UseAccountMapping=true&VerticalAnalysisType=None&IncludeComputedColumns=true&OrderBy=&RelativeDateRange=LastCalendarYear&UseCustomDateRange=false",
            QvinciReport.PNL_YTD => "ProfitAndLoss/?DateFrequency=Monthly&UseAccountMapping=true&VerticalAnalysisType=None&IncludeComputedColumns=true&OrderBy=&RelativeDateRange=ThisCalendarYearToDate&UseCustomDateRange=false",
            _ => throw new Exception("Invalid Report")
        };

        return $"{BASEURL}{url}&CompanyId={location.CompanyId}&Locations=[{location.Id}]";
    }

    internal async Task<List<QvinciLocation>> GetLocationsAsync()
    {
        var locations = new List<QvinciLocation>();
        foreach (int companyid in _options.Value.Companies)
        {
            var companuyLocations = await GetLocationsAsync(companyid);
            locations.AddRange(companuyLocations);
        }

        return locations;
    }

    public async Task<ReportFile> GetAsync(QvinciLocation location, QvinciReport report)
    {
        var url = BuildUrl(location, report);
        await Task.Delay(500);
        return await GetAsync<ReportFile>(url, "X-apiToken", API_KEY);
    }

    public async Task<T> GetAsync<T>(string url, string authHeader, string authValue)
        where T : class
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Add(authHeader, authValue);
        // requestMessage.ContentType = "application/xml; charset=utf-8";

        var response = await _client.SendAsync(requestMessage);
        var body = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                _logger.LogInformation("Got no body");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize response");
            }
        }

        _logger.LogError("Failed GET {url} => {status} {reason}: {body}", url, response.StatusCode, response.ReasonPhrase, body);
        throw new HttpClientRequestException(response.ReasonPhrase)
        {
            Url = url,
            StatusCode = (int)response.StatusCode,
            Status = response.ReasonPhrase,
            Body = body,
        };
    }

    public async Task<List<QvinciLocation>> GetLocationsAsync(int companyid)
    {
        _logger.LogInformation("Get Locations for {companyId}", companyid);

        var take = 100;
        var locations = new List<QvinciLocation>();
        while (true)
        {
            _logger.LogInformation("Get Page of {page} from {skip}", take, locations.Count);

            var url = $"https://api.qvinci.com/v1/location/search/?CompanyId={companyid}&take={take}&skip={locations.Count}";

            await Task.Delay(500);

            var result = await GetAsync<LocationSearch>(url, "X-apiToken", API_KEY);
            if (result == null) break;

            foreach (var location in result.Items)
            {
                location.CompanyId = companyid;
            }

            locations.AddRange(result.Items);
            if (locations.Count >= result.TotalCount) break;
        }

        return locations;
    }
}

public class Config
{
    public string ApiKey { get; set; }
    public int[] Companies { get; set; }
}