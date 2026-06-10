using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// Whether services are taxable
/// </summary>
public enum ServiceV60Taxable
{
    [Description("Y")]
    Y,
    [Description("N")]
    N
}
