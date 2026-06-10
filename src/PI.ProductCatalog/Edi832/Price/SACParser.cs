using System;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Price;

public class SACParser : FixedColsParser
{
    public override string Element => "SAC";

    public override Token[] Tokens => new Token[]
    {
        Token.Const("Charge", "C"),

        Token.Const("Recovery Fee", "G090"),

        Token.Const("California", "14"),

        Token.Const("CA Carpet Stewardship Assessment", "CARE"),

        null, // 5

        null, // 6

        null, // 7

        Token.R("Amount of charge to be collected.", 1, 6, false),
        Token.ID("Unit or Basis for Measurement Code Description", 2),

        Token.R("Minimum quantity associated with the quantity.", 1, 15, false, defaultValue: 1),
        
        // 15: SAC15 is a description of the allowance or charge. SAC15 may be used to clarify the code in SAC02 
        
        // 16:  Code specifying the language used in text, from a standard code list maintained by the International Standards Organization (ISO 639)
    };

    public Action<RecoveryFee, CatalogParserContext> Setter { get; }

    public SACParser(Action<Models.RecoveryFee, CatalogParserContext> setter)
    {
        Setter = setter;
    }

    protected override LineResult Convert(CatalogParserContext context)
    {
        var result = base.Convert(context);
        if (Setter == null) return result;

        Setter(
            new Models.RecoveryFee
            {
                Amount = context.Values[7] as decimal?,
                UOM = context.ParseUOM(context.Values[8]),
                MinimumQuantity = context.Values[9] as decimal?,
            },
            context
        );

        return result;
    }
}