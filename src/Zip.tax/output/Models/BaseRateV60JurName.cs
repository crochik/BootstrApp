using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// Jurisdiction level name
/// </summary>
public enum BaseRateV60JurName
{
    [Description("US_STATE")]
    UsState,
    [Description("US_COUNTY")]
    UsCounty,
    [Description("US_CITY")]
    UsCity,
    [Description("US_DISTRICT")]
    UsDistrict
}
