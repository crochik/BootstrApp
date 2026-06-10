using System;

namespace PI.ProductCatalog.Models;

public class RecoveryFee
{
    public decimal? Amount { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public UnitOfMeasurement? UOM { get; set; }

    public override bool Equals(object obj)
        => (obj is RecoveryFee other) &&
           MinimumQuantity.GetValueOrDefault(-1) == other.MinimumQuantity.GetValueOrDefault(-1) &&
           Amount.GetValueOrDefault(-1) == other.Amount.GetValueOrDefault(-1) &&
           UOM.GetValueOrDefault(UnitOfMeasurement.Unknown) == other.UOM.GetValueOrDefault(UnitOfMeasurement.Unknown);

    public override int GetHashCode() => HashCode.Combine(Amount, MinimumQuantity, UOM);
    
}