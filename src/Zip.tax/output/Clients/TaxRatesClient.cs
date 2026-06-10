using System.Net.Http.Json;
using System.Text.Json;
using ZipTax.Models;

namespace ZipTax.Clients;

/// <summary>
/// Client for TaxRates operations
/// </summary>
public partial class TaxRatesClient : ITaxRates
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;

    public TaxRatesClient(HttpClient httpClient, string? baseUrl = null, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? "https://api.zip-tax.com" ?? string.Empty;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        if (timeout.HasValue)
        {
            _httpClient.Timeout = timeout.Value;
        }
    }


    /// <summary>
    /// Get Tax Rates (v6.0)
    /// </summary>
    /// <param name="key">API key for authentication</param>
    /// <param name="address">Street address for geocoding. Provide a full or partial US street address (e.g. "200 Spectrum Center Dr, Irvine, CA 92618").</param>
    /// <param name="lat">Latitude coordinate. Use with `lng` as an alternative to `address`.</param>
    /// <param name="lng">Longitude coordinate. Use with `lat` as an alternative to `address`.</param>
    /// <param name="countryCode">Country code</param>
    /// <param name="format">Response format</param>
    /// <param name="satItemTotal">Single Article Tax item total for Tennessee</param>
    /// <param name="historical">Historical date for rates in YYYY-MM format</param>
    /// <param name="taxabilityCode">Product taxability code (TIC). When provided, the response includes a `productDetail` section with product-specific rate rules. Requires the `product_rates` entitlement.</param>
    /// <param name="addressDetailExtended">When set to `true` or `1`, the `addressDetail` section includes a nested `address` object with individual address components.</param>
    /// <param name="additionalHeaders">Optional additional headers to include in the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<V60Response> GetTaxRatesV60Async(string key, string? address = null, double? lat = null, double? lng = null, GetTaxRatesV60CountryCode? countryCode = null, GetTaxRatesV60Format? format = null, double? satItemTotal = null, string? historical = null, long? taxabilityCode = null, GetTaxRatesV60AddressDetailExtended? addressDetailExtended = null, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default)
    {
        var path = "/request/v60";

        var queryParams = new List<string>();
        queryParams.Add($"key={Uri.EscapeDataString(key.ToString())}");
        if (address != null)
        {
            queryParams.Add($"address={Uri.EscapeDataString(address.ToString()!)}");
        }
        if (lat != null)
        {
            queryParams.Add($"lat={Uri.EscapeDataString(lat.ToString()!)}");
        }
        if (lng != null)
        {
            queryParams.Add($"lng={Uri.EscapeDataString(lng.ToString()!)}");
        }
        if (countryCode != null)
        {
            queryParams.Add($"countryCode={Uri.EscapeDataString(GetTaxRatesV60CountryCodeSerializer.SerializeToString(countryCode.Value))}");
        }
        if (format != null)
        {
            queryParams.Add($"format={Uri.EscapeDataString(GetTaxRatesV60FormatSerializer.SerializeToString(format.Value))}");
        }
        if (satItemTotal != null)
        {
            queryParams.Add($"sat_item_total={Uri.EscapeDataString(satItemTotal.ToString()!)}");
        }
        if (historical != null)
        {
            queryParams.Add($"historical={Uri.EscapeDataString(historical.ToString()!)}");
        }
        if (taxabilityCode != null)
        {
            queryParams.Add($"taxabilityCode={Uri.EscapeDataString(taxabilityCode.ToString()!)}");
        }
        if (addressDetailExtended != null)
        {
            queryParams.Add($"addressDetailExtended={Uri.EscapeDataString(GetTaxRatesV60AddressDetailExtendedSerializer.SerializeToString(addressDetailExtended.Value))}");
        }

        if (queryParams.Any())
        {
            path += "?" + string.Join("&", queryParams);
        }

        var _request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{path}");


        // Add additional custom headers if provided
        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                _request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }


        var _response = await _httpClient.SendAsync(_request, cancellationToken);
        _response.EnsureSuccessStatusCode();

        var _responseJson = await _response.Content.ReadAsStringAsync(cancellationToken);
        using var _document = global::System.Text.Json.JsonDocument.Parse(_responseJson);
        var _result = V60ResponseSerializer.DeserializeFromDocument(_document);
        return _result ?? throw new InvalidOperationException("Response body was null");
    }
}
