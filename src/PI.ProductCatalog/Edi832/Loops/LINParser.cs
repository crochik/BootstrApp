namespace PI.ProductCatalog;

public class LINParser : FixedColsParser
{
    public override string Element => "LIN";

    public override Token[] Tokens { get; } =
    [
        null,

        Token.Const("Pricing Group Qualifier", "GS", isMandatory:false),
        Token.AN("Pricing Group", 1, 48, isMandatory: false,
            setter: (value, context) => context.Section.PricingGroup = value?.ToString()
        ),

        Token.Const("Manufacturer Qualifier", "MF"),
        Token.AN("Manufactuer Name", 1, 48,
            setter: (value, context) => context.Section.Manufacturer = value?.ToString()
        ),

        Token.Const("Style Number Qualifier", "ST"),
        Token.AN("Style Number", 1, 48,
            setter: (value, context) => context.Section.StyleNumber = value?.ToString(),
            defaultValue: "[MISSING]" // fallback value
        ),

        Token.Const("Backing Qualifier", "BK", isMandatory: false),
        Token.AN("Backing",1,48, isMandatory: false,
            setter: (value, context) => context.Section.Backing = value?.ToString()
        ),

        Token.Const("Size Code Qualifier", "SZ", isMandatory: false),
        Token.AN("Size Code", 1, 48, isMandatory: false,
            setter: (value, context) => context.Section.SizeCode = value?.ToString()
        ),

        Token.Const("Manufacturer Style Number Qualifier", "MS", isMandatory: false),
        Token.AN("Manufacturer Style Number",1,48, isMandatory: false,
            setter: (value, context) => context.Section.ManufacturerStyleNumber = value?.ToString()
        ),

        Token.Const("Manufacturer Style Name Qualifier", "MN", isMandatory: false),
        Token.AN("Manufacturer Style Name",1,48, isMandatory: false,
            setter: (value, context) => context.Section.ManufacturerStyleName = value?.ToString()
        ),

        Token.Const("Unique Product Number Qualifier", "UX"),
        Token.AN("Unique Product Number", 1, 48,
            setter: (value, context) => context.Section.UniqueStyleCode = value?.ToString()
        )
    ];

    public override LineResult ParseLine(CatalogParserContext context)
    {
        context.Loop = Loop.Style;
        return base.ParseLine(context);
    }

    protected override LineResult Convert(CatalogParserContext context)
    {
        context.Section = new Models.LIN();
        return base.Convert(context);
    }
}