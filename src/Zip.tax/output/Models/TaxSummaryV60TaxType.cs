using System.Text.Json.Serialization;
using System.ComponentModel;

namespace ZipTax.Models;

/// <summary>
/// Tax type category
/// </summary>
public enum TaxSummaryV60TaxType
{
    [Description("SALES_TAX")]
    SalesTax,
    [Description("USE_TAX")]
    UseTax
}
