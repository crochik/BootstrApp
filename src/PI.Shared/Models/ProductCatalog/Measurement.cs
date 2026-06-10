using System;

namespace PI.ProductCatalog.Models;

public class Measurement
{
    public decimal Units { get; init; }
    public UnitOfMeasurement UOM { get; init; }

    public static UnitOfMeasurement Parse(string code)
        => code switch
        {
            "1V" => UnitOfMeasurement.Slab,
            "BD" => UnitOfMeasurement.Bundle,
            "BG" => UnitOfMeasurement.Bag,
            "BX" => UnitOfMeasurement.Box,
            "CA" => UnitOfMeasurement.Case,
            "CG" => UnitOfMeasurement.Card,
            "CT" => UnitOfMeasurement.Carton,
            "PA" => UnitOfMeasurement.Pail,
            "PC" => UnitOfMeasurement.Piece,
            "PK" => UnitOfMeasurement.Package,
            "PL" => UnitOfMeasurement.Pallet,
            "RL" => UnitOfMeasurement.Roll,
            "TC" => UnitOfMeasurement.Truckload,
            "EA" => UnitOfMeasurement.Each,
            "ST" => UnitOfMeasurement.Set,
            "SH" => UnitOfMeasurement.Sheet,
            "TL" => UnitOfMeasurement.Truckload,

            // Linear
            "IN" => UnitOfMeasurement.Inches,
            "FT" => UnitOfMeasurement.FeetAndInches, // Foot (inches to right of decimal)
            "EZ" => UnitOfMeasurement.Feet, // Foot (fraction to right of decimal) 
            "MR" => UnitOfMeasurement.Meter, //  Meters 
            "CM" => UnitOfMeasurement.Centimeters, // Centimeters 

            "LF" => UnitOfMeasurement.Feet, // ******* HAVE TO CHECK DOCS TO SEE IF it should be Feet or FeetAndInches

            // Area
            "SF" => UnitOfMeasurement.SqFt,
            "SY" => UnitOfMeasurement.SqYd,

            // Weight
            // G39
            "L" => UnitOfMeasurement.Pounds, // G39
            "O" => UnitOfMeasurement.Ounces, // G39

            // Volume
            // G39
            "GA" => UnitOfMeasurement.Gallon,
            "OZ" => UnitOfMeasurement.Ounces, // ???
            "PT" => UnitOfMeasurement.Pint,
            "FO" => UnitOfMeasurement.FluidOunces,
            "QT" => UnitOfMeasurement.Quart,

            // Packaging 
            // G39
            "BAG" => UnitOfMeasurement.Bag,
            "BOT" => UnitOfMeasurement.Bottle,
            "BOX" => UnitOfMeasurement.Box,
            "BXT" => UnitOfMeasurement.Bucket,
            "CAN" => UnitOfMeasurement.Can,
            "CAS" => UnitOfMeasurement.Case,
            "CNT" => UnitOfMeasurement.Container,
            "CTN" => UnitOfMeasurement.Carton,
            "PAL" => UnitOfMeasurement.Pail,
            "PCS" => UnitOfMeasurement.Piece,
            "PKG" => UnitOfMeasurement.Package,
            "ROL" => UnitOfMeasurement.Roll,
            "SPL" => UnitOfMeasurement.Spool,

            // SHAW uses NA for whatever reason
            _ => UnitOfMeasurement.Unknown,
        };

    // public static Measurement New(decimal units, string code)
    //     => new Measurement
    //     {
    //         Units = units,
    //         UOM = Parse(code),
    //     };

    public bool IsWhole() => (Units - decimal.Floor(Units)) != 0;

    public override string ToString() => (Units == 0) ? null : UOM.Format(Units);

    public override bool Equals(object obj)
    {
        return obj is Measurement other &&
               Units == other.Units &&
               UOM == other.UOM;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Units, UOM);
    }
    
    public Measurement Convert(UnitOfMeasurement uom)
    {
        if (uom == UOM) return this;

        decimal? units = uom switch
        {
            UnitOfMeasurement.Feet => UOM switch
            {
                UnitOfMeasurement.Inches => Units / 12L,
                _ => null,
            },
            UnitOfMeasurement.SqFt => UOM switch
            {
                UnitOfMeasurement.SqYd => Units * 9,
                _ => null,
            },
            UnitOfMeasurement.SqYd => UOM switch
            {
                UnitOfMeasurement.SqFt => Units / 9L,         
                _ => null,
            },
            _ => null,
        };

        return units != null
            ? new Measurement
            {
                Units = units.Value,
                UOM = uom,
            }
            : null;
    }
}