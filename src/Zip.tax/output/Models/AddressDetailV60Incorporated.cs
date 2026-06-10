using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// Whether the location is within an incorporated area
/// </summary>
public enum AddressDetailV60Incorporated
{
    [Description("true")]
    True,
    [Description("false")]
    False
}
