using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

public enum GetTaxRatesV60Format
{
    [Description("json")]
    Json,
    [Description("xml")]
    Xml
}
