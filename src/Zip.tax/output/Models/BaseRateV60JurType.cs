using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// Jurisdiction tax type
/// </summary>
public enum BaseRateV60JurType
{
    [Description("US_STATE_SALES_TAX")]
    UsStateSalesTax,
    [Description("US_STATE_USE_TAX")]
    UsStateUseTax,
    [Description("US_COUNTY_SALES_TAX")]
    UsCountySalesTax,
    [Description("US_COUNTY_USE_TAX")]
    UsCountyUseTax,
    [Description("US_CITY_SALES_TAX")]
    UsCitySalesTax,
    [Description("US_CITY_USE_TAX")]
    UsCityUseTax,
    [Description("US_DISTRICT_SALES_TAX")]
    UsDistrictSalesTax,
    [Description("US_DISTRICT_USE_TAX")]
    UsDistrictUseTax
}
