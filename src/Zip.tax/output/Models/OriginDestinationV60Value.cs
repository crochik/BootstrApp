using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// O = Origin-based, D = Destination-based
/// </summary>
public enum OriginDestinationV60Value
{
    [Description("O")]
    O,
    [Description("D")]
    D
}
