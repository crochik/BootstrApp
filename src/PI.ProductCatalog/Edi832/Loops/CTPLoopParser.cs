using System.Collections.Generic;

namespace PI.ProductCatalog
{
    public class CTPLoopParser : FixedColsParser
    {
        public override string Element => "CTP";

        public override Token[] Tokens => new Token[]
        {
            null, // 1
            
            // LPR = List Price
            // PRP = Promotional Price
            // ICL = Price Through Quantity
            // PAQ = Price Break Quantity
            // PBQ = Price Beginning Quantity            
            Token.ID("Price Identifier Code", 3, defaultValue: "LPR"), // default if missing (non-standard)

            Token.R("Unit Price", 1, 17, defaultValue: 0M), // default to zero
            Token.R("Quantity", 1, 15, false),
            Token.ID("Unit of Measurement Code",2),

            null, //6
            null, //7
            null, //8

            // ST = Standard Roll Length
            // CT = Cut
            // RC = Roll at Cut
            // PL = Pallet            
            Token.ID("Basis of Unit Price Code", 2, isMandatory: false),

            // Location ID used to identify specific pricing for different locations within the receiving system (Ship to Level pricing)
            Token.AN("Condition Value", 4, isMandatory: false)
        };

        protected override LineResult Convert(CatalogParserContext context)
        {
            context.Loop = Loop.Price;

            if (context.Values[2] == null) return LineResult.Warning("Missing required Unit Price, skip");
            if (context.Values[4] == null) return LineResult.Warning("Missing required UOM, skip");

            context.Price = new Models.LINCTP
            {
                Criteria = context.Values[1] switch
                {
                    "LPR" => Models.PriceCriteria.List,
                    "PRP" => Models.PriceCriteria.Promotional,
                    "ICL" => Models.PriceCriteria.ThroughQuantity,
                    "PAQ" => Models.PriceCriteria.BreakQuantity,
                    "PBQ" => Models.PriceCriteria.BeginningQuantity,
                    _ => throw new ParserException(context, $"Unexpected Price Criteria: '{context.Values[1]}'")
                },
                UnitCost = (decimal)context.Values[2],
                MinimumQuantity = context.Values[3] as decimal?,
                UOM = context.ParseUOM(context.Values[4]),
                PackageCondition = context.Values[8] switch
                {
                    "ST" => Models.PackagePriceCondition.StandardRollLength,
                    "CT" => Models.PackagePriceCondition.Cut,
                    "RC" => Models.PackagePriceCondition.RollAtCut,
                    "PL" => Models.PackagePriceCondition.Pallet,
                    _ => default(Models.PackagePriceCondition?),
                },
                LocationId = context.Values[9]?.ToString(),
            };

            var result = base.Convert(context);

            context.Section.CTPs ??= new List<Models.LINCTP>();
            context.Section.CTPs.Add(context.Price);

            return result;
        }
    }
}
