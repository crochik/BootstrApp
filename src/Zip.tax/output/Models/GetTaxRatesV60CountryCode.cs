using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

public enum GetTaxRatesV60CountryCode
{
    [Description("USA")]
    Usa,
    [Description("CAN")]
    Can
}
