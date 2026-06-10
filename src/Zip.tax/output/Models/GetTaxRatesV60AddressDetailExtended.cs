using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

public enum GetTaxRatesV60AddressDetailExtended
{
    [Description("true")]
    True,
    [Description("false")]
    False,
    [Description("1")]
    _1,
    [Description("0")]
    _0
}
