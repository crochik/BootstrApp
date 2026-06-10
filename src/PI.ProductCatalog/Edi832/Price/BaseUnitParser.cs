namespace PI.ProductCatalog.Price;

public class BaseUnitParser : FixedColsParser
{
    public override string Element => "G39";

    public override Token[] Tokens =>
    [
        null,
        Token.Const("Mutually Defined", "ZZ"),
        Token.Const("ACTUAL Base Unit Measurements", "BU"),

        null,  // 4

        Token.R("Unit Weight", 1, 8, false),
        Token.Const("Per Base Unit", "U"),  
        // L = Pound
        // O = Ounces
        Token.ID("Weight Unit Code", 1),

        Token.R("Vertical Dimension Value", 1, 8, false),
        Token.Const("Feet Inches (format ast FT)", "HT"),

        Token.R("Width: Smallest of 2 Horizontal Values", 1, 8, false),
        Token.Const("Feet Inches (format as FT)", "WD"),

        Token.R("Length: Largest of 2 Horizontal Values", 1, 8, false),
        Token.Const("Linear Feet Inches (format as FT)", "LF"),

        Token.R("Volumetric Measure", 1, 8, false), 
        // GA = Gallon 
        // OZ = Ounces
        // PT = Pint 
        // FO = Fluid Ounces
        // QT = Quart            
        Token.ID("Unit or Basis for Measurement Code", 2),

        null, // 16
        null, // 17

        Token.R("Coverage per Base Unit", 1, 8, false),
        Token.Const("Square Feet", "SF"),

        null, null, null, null, null, null, null, null, // 20-27

        // BAG = Bag 
        // BOT = Bottle
        // BOX = Box 
        // BXT = Bucket
        // CAN = Can 
        // CAS = Case
        // CNT = Container 
        // CTN = Carton
        // PAL = Pail 
        // PCS = Piece
        // PKG = Package 
        // ROL = Roll
        // SPL = Spool
        Token.ID("Base Unit", 2)
    ];

    protected override LineResult Convert(CatalogParserContext context)
    {
        var result = base.Convert(context);

        Models.BaseUnit bu = null;

        if (context.Values[4] != null && context.Values[6] != null)
        {
            bu ??= new Models.BaseUnit();

            bu.Weight = new Models.Measurement
            {
                Units = (decimal)context.Values[4],
                UOM = context.ParseUOM(context.Values[6]),
            };
        }

        if (context.Values[7] != null)
        {
            bu ??= new Models.BaseUnit();
            bu.Height = new Models.Measurement
            {
                Units = (decimal)context.Values[7],
                UOM = Models.UnitOfMeasurement.FeetAndInches
            };
        }

        if (context.Values[9] != null)
        {
            bu ??= new Models.BaseUnit();
            bu.Width = new Models.Measurement
            {
                Units = (decimal)context.Values[9],
                UOM = Models.UnitOfMeasurement.FeetAndInches
            };
        }

        if (context.Values[11] != null)
        {
            bu ??= new Models.BaseUnit();
            bu.Length = new Models.Measurement
            {
                Units = (decimal)context.Values[11],
                UOM = Models.UnitOfMeasurement.FeetAndInches
            };
        }

        if (context.Values[13] != null || context.Values[14] != null)
        {
            bu ??= new Models.BaseUnit();
            bu.Volume = new Models.Measurement
            {
                Units = (decimal)context.Values[13],
                UOM = context.ParseUOM(context.Values[14]),
            };
        }

        if (context.Values[17] != null)
        {
            bu ??= new Models.BaseUnit();
            bu.Coverage = new Models.Measurement
            {
                Units = (decimal)context.Values[17],
                UOM = Models.UnitOfMeasurement.SqFt
            };
        }

        if (context.Values[27] != null)
        {
            bu ??= new Models.BaseUnit();
            bu.UOM = context.ParseUOM(context.Values[27]);
        }

        if (bu != null)
        {
            if (context.Section.BaseUnit != null)
            {
                throw new ParserException(context, "There is already a Base Unit defined");
            }

            context.Section.BaseUnit = bu;
        }

        return result;
    }
}