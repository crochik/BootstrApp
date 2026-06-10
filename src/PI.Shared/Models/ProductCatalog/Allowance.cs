using System;

namespace PI.ProductCatalog.Models;

public class Allowance
{
    public decimal? Percentage { get; set; }
    public decimal? Amount { get; set; }
    public AllowanceType Type { get; set; }

    public override bool Equals(object obj)
        => (obj is Allowance other) &&
           Type == other.Type &&
           Percentage.GetValueOrDefault(-1) == other.Percentage.GetValueOrDefault(-1) &&
           Amount.GetValueOrDefault(-1) == other.Amount.GetValueOrDefault(-1);

    public override int GetHashCode() => HashCode.Combine(Type, Percentage, Amount);
}