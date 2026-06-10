using System;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog;

public class MeasurementParser : FixedColsParser
{
    private readonly string _name;
    private readonly string _qualifier;
    public Action<Models.Measurement, CatalogParserContext> Setter { get; }

    public override string Element => "MEA";

    public override Token[] Tokens =>
    [
        null,
        Token.Const("Measurement Qualifier", _qualifier), //  1, 3
        Token.R(_name, 1,20),
        Token.ID("Unit of Measurement Code", 2)
    ];

    public MeasurementParser(string name, string qualifier, Action<Measurement, CatalogParserContext> setter = null)
    {
        _name = name;
        _qualifier = qualifier;
        Setter = setter;
    }

    protected override LineResult Convert(CatalogParserContext context)
    {
        var result = base.Convert(context);

        if (Setter == null) return result;

        if (context.Values[2] == null) return LineResult.Warning($"Missing {_name}, skip");

        var measurement = new Measurement
        {
            Units = (decimal)context.Values[2],
            UOM = context.ParseUOM(context.Values[3]),
        };

        Setter(measurement, context);

        return result;
    }
}

public class MeasurementRateParser : FixedColsParser
{
    protected readonly string _name;
    protected readonly string _qualifier;
    public Action<Models.UOMRate, CatalogParserContext> Setter { get; }

    public override string Element => "MEA";

    public override Token[] Tokens => new Token[]
    {
        null,
        Token.Const("Measurement Qualifier", _qualifier), //  1, 3
        Token.R(_name, 1,20),
        Token.ID("Unit of Measurement Code", 2)
    };

    public MeasurementRateParser(string name, string qualifier, Action<UOMRate, CatalogParserContext> setter = null)
    {
        _name = name;
        _qualifier = qualifier;
        Setter = setter;
    }

    protected override LineResult Convert(CatalogParserContext context)
    {
        var result = base.Convert(context);

        if (Setter == null) return result;

        // var rate = new UOMRate
        // {
        //     Measurement = new Measurement
        //     {
        //         Units = (decimal)context.Values[2],
        //     }
        // };

        var units = (decimal)context.Values[2];
        var rate = (string)context.Values[3] switch
        {
            // Pounds per Piece
            "3G" => new UOMRate
            {
                UOM = UnitOfMeasurement.Piece,
                Measurement = new Measurement
                {
                    Units = units,
                    UOM = UnitOfMeasurement.Pounds,
                }
            },
            // Kilograms per Piece
            "3I" => new UOMRate
            {
                UOM = UnitOfMeasurement.Piece,
                Measurement = new Measurement
                {
                    Units = units,
                    UOM = UnitOfMeasurement.Kilograms,
                }
            },
            // Ounces per Square Foot
            "37" => new UOMRate
            {
                UOM = UnitOfMeasurement.SqFt,
                Measurement = new Measurement
                {
                    Units = units,
                    UOM = UnitOfMeasurement.Ounces,
                }
            },
            // Ounces per Square Yard
            "ON" => new UOMRate
            {
                UOM = UnitOfMeasurement.SqYd,
                Measurement = new Measurement
                {
                    Units = units,
                    UOM = UnitOfMeasurement.Ounces,
                }
            },
            // Pounds per Square Foot
            "FP" => new UOMRate
            {
                UOM = UnitOfMeasurement.SqFt,
                Measurement = new Measurement
                {
                    Units = units,
                    UOM = UnitOfMeasurement.Pounds,
                }
            },
            _ => null,
        };
            
        if (rate==null) return LineResult.Warning($"Unknown UOM: {context.Values[3]}");

        Setter(rate, context);

        return result;
    }
}