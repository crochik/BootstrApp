using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PI.ProductCatalog.Models;

public enum UnitOfMeasurement
{
    Unknown,
    Each,
    Inches,

    [Description("Feet.Inches")]
    FeetAndInches,

    Feet,
    Meter,
    Centimeters,
    // PoundsPerPc,
    // KilogramsPerPc,
    // OuncesPerSqFt,
    // OuncesPerSqYd,
    // PondsPerSqFt,
    SqFt,
    Pounds,
    Ounces,
    Quart,

    [Description("Fluid Ounces")]
    FluidOunces,

    Pint,
    Gallon,
    Bag,
    Bottle,
    Box,
    Bucket,
    Can,
    Case,
    Container,
    Carton,
    Pail,
    Piece,
    Package,
    Roll,
    Spool,
    Pallet,
    Truckload,
    SqYd,
    Sheet,
    Set,
    Slab,
    Bundle,
    Card,
    Kilograms
}

public class UOMRate
{
    public UnitOfMeasurement UOM { get; set; }
    public Measurement Measurement { get; set; }

    public override bool Equals(object obj)
        => (obj is UOMRate other) &&
           UOM == other.UOM &&
           (Measurement == null ? other.Measurement == null : Measurement.Equals(other.Measurement));

    public override int GetHashCode() => HashCode.Combine(UOM, Measurement);

    public override string ToString()
    {
        return $"{Measurement} / {UOM.GetAbbreviation()}";
    }
}

public static class UnitOfMeasurementExtensions
{
    public static string GetAbbreviation(this UnitOfMeasurement uom) => uom switch
    {
        UnitOfMeasurement.Feet => "ft.",
        UnitOfMeasurement.Inches => "in.",

        UnitOfMeasurement.SqFt => "sq.ft.",
        UnitOfMeasurement.SqYd => "sq.yd.",

        // UnitOfMeasurement.PoundsPerPc => "lb. / pc.",
        // UnitOfMeasurement.PondsPerSqFt => "lb. / sq.ft.",

        UnitOfMeasurement.Piece => "pc.",
        UnitOfMeasurement.Each => "pc.",

        UnitOfMeasurement.Pounds => "lb.",

        _ => uom.ToString(),
    };

    public enum MeasurementOf
    {
        Unspecified,
        Length,
        Area,
    };

    public static MeasurementOf GetMeasurementOf(this UnitOfMeasurement src)
        => src switch
        {
            UnitOfMeasurement.Inches => MeasurementOf.Length,
            UnitOfMeasurement.Feet => MeasurementOf.Length,
            UnitOfMeasurement.FeetAndInches => MeasurementOf.Length,
            UnitOfMeasurement.Meter => MeasurementOf.Length,

            UnitOfMeasurement.SqFt => MeasurementOf.Area,
            UnitOfMeasurement.SqYd => MeasurementOf.Area,

            _ => MeasurementOf.Unspecified,
        };

    public static bool CanConvertTo(this UnitOfMeasurement src, UnitOfMeasurement other)
        => src.GetMeasurementOf() == other.GetMeasurementOf();

    public static (decimal F, decimal D) GetConversionFactorTo(this UnitOfMeasurement src, UnitOfMeasurement dst)
    {
        if (src == dst) return (1, 1);

        return src switch
        {
            UnitOfMeasurement.Feet => dst switch
            {
                UnitOfMeasurement.Inches => (12, 1),
                _ => (0, 1),
            },

            UnitOfMeasurement.Inches => dst switch
            {
                UnitOfMeasurement.Feet => (1, 12),
                _ => (0, 1),
            },
            
            UnitOfMeasurement.SqYd => dst switch
            {
                UnitOfMeasurement.SqFt => (9, 1),
                _ => (0,1)
            },

            UnitOfMeasurement.SqFt => dst switch
            {
                UnitOfMeasurement.SqYd => (1, 9),
                _ => (0,1)
            },

            _ => (0, 1),
        };
    }

    private static string FeetInchesToString(decimal units)
    {
        var ft = (int)units;
        var inch = (units - ft) * 100;

        if (ft == 0)
        {
            return $"{inch:0.##} {UnitOfMeasurement.Inches.GetAbbreviation()}";
        }

        if (inch > 0)
        {
            if (ft == 1)
            {
                return $"{ft * 12 + inch:0.##} {UnitOfMeasurement.Inches.GetAbbreviation()}";
            }

            return $"{ft} {UnitOfMeasurement.Feet.GetAbbreviation()} {inch:0.##} {UnitOfMeasurement.Inches.GetAbbreviation()}";
        }

        return $"{ft} {UnitOfMeasurement.Feet.GetAbbreviation()}";
    }

    public static string Format(this UnitOfMeasurement uom, decimal units)
    {
        return uom switch
        {
            UnitOfMeasurement.FeetAndInches => FeetInchesToString(units),
            _ => $"{units:0.####} {uom.GetAbbreviation()}",
        };
    }

    public static bool ConvertTo(this Measurement measurement, UnitOfMeasurement dst, out Measurement convertedValue)
    {
        convertedValue = null;

        if (measurement == null) return false;

        if (measurement.UOM == UnitOfMeasurement.FeetAndInches)
        {
            var ft = (int)measurement.Units;
            var inch = (measurement.Units - ft) * 100;
            measurement = new Measurement
            {
                Units = ft * 12 + inch,
                UOM = UnitOfMeasurement.Inches
            };
        }

        if (dst.CanConvertTo(measurement.UOM))
        {
            var factor = measurement.UOM.GetConversionFactorTo(dst);
            if (factor.F == 0) return false;
            
            convertedValue = new Measurement
            {
                Units = measurement.Units * factor.F / factor.D,
                UOM = dst
            };

            return true;
        }

        return false;
    }

    public static IEnumerable<UOMRate> SortByUOM(this IEnumerable<UOMRate> src, params UnitOfMeasurement[] preference)
        => src.SortByUOM(preference.ToSortDict());

    public static Dictionary<UnitOfMeasurement, int> ToSortDict(this IEnumerable<UnitOfMeasurement> preference)
        => new(preference.Select((x, i) => new KeyValuePair<UnitOfMeasurement, int>(x, i)));

    public static IEnumerable<UOMRate> SortByUOM(this IEnumerable<UOMRate> src, Dictionary<UnitOfMeasurement, int> dict)
        => src.OrderBy(x => dict.TryGetValue(x.UOM, out var index) ? index : int.MaxValue);
}