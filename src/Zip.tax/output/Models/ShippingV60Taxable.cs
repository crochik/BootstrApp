using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// Whether shipping is taxable
/// </summary>
public enum ShippingV60Taxable
{
    [Description("Y")]
    Y,
    [Description("N")]
    N
}
