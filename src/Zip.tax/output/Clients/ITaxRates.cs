using ZipTax.Models;

namespace ZipTax.Clients;

/// <summary>
/// Client for TaxRates operations
/// </summary>
public interface ITaxRates
{

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
    Task<V60Response> GetTaxRatesV60Async(string key, string? address = null, double? lat = null, double? lng = null, GetTaxRatesV60CountryCode? countryCode = null, GetTaxRatesV60Format? format = null, double? satItemTotal = null, string? historical = null, long? taxabilityCode = null, GetTaxRatesV60AddressDetailExtended? addressDetailExtended = null, Dictionary<string, string>? additionalHeaders = null, CancellationToken cancellationToken = default);
}
