using System;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Price;

public class AllowanceParser : FixedColsParser
{
    public override string Element => "SAC";

    public override Token[] Tokens => new Token[]
    {
        Token.Const("Allowance", "A"),  

        // F790 = Rebate
        // ZZZZ = Margin            
        Token.ID("Allowance or Charge Code", 4),

        null, //3 
        null, //4

        Token.R("Dollar Amount", 1, 15, false), // N2

        null, //6

        Token.R("Percent", 1, 6, false),
    };

    public Action<Allowance, CatalogParserContext> Setter { get; }

    public AllowanceParser(Action<Models.Allowance, CatalogParserContext> setter)
    {
        Setter = setter;
    }

    protected override LineResult Convert(CatalogParserContext context)
    {
        var result = base.Convert(context);
        if (Setter == null) return result;

        Setter(
            new Models.Allowance
            {
                Type = context.Values[1] switch
                {
                    "F790" => Models.AllowanceType.Rebate,
                    "ZZZZ" => Models.AllowanceType.Margin,
                    _ => Models.AllowanceType.Undefined,
                },
                Amount = context.Values[4] as decimal?,
                Percentage = context.Values[6] as decimal?,
            },
            context
        );

        return result;
    }
}