using System;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog
{
    public class BeginningSegment : FixedColsParser
    {
        public override string Element => "BCT";

        public override Token[] Tokens => new Token[]{
            Token.Const("Catalog Purpose Code = Price Sheet", "PS"),
            null,
            
            // 0 = No Color Level pricing
            // 1 = Color Level pricing (multiple LIN)
            // 2 = Color Level pricing (CTP/DTM in the SLN)            
            Token.AN("Catalog Version Number (Pricing)", 1, 15, false,
                setter: (value, context) => context.CatalogUpdate.Pricing = (CatalogPricing)Enum.Parse(typeof(CatalogPricing), value as string)
            ), // 0,1,2

            null,
            null,
            null,
            null,
            null,
            null,

            // 03 = Delete
            // 04 = Change
            Token.AN("Transaction Set Purpose codes", 2, isMandatory: false,
                setter: (value, context) => context.CatalogUpdate.OperationType = value switch 
                {
                    "03" => CatalogOperationType.Delete,
                    "04" => CatalogOperationType.Update,
                    _ => default(CatalogOperationType?)
                }
            ),
        };
    }
}
