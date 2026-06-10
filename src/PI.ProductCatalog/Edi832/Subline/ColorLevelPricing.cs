using System.Collections.Generic;

namespace PI.ProductCatalog.Subline
{
    /// <summary>
    /// identitical to CTP (Style.PriceLoop), since the other may be the start of a new loop may need to check for required elements
    /// </summary>
    public class ColorLevelPricing : FixedColsParser
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
            Token.ID("Price Identifier Code", 3, defaultValue: "LPR"), // default to List Price (non-standard)
 
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
            var result = base.Convert(context);

            context.ItemPrice = new Models.SLNCTP
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
                
                // TODO: this seems to be completely wrong in version 3+
                // very different meaning... 
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

            context.Item.CTPs ??= new List<Models.SLNCTP>();

            var existing = context.Item.CTPs.FindIndex(
                x => x.Criteria == context.ItemPrice.Criteria &&
                (string.Equals(context.ItemPrice.LocationId, x.LocationId) || context.ItemPrice.LocationId == x.LocationId) &&
                x.UOM == context.ItemPrice.UOM &&
                x.PackageCondition == context.ItemPrice.PackageCondition
            );

            if (existing >= 0) context.Item.CTPs.RemoveAt(existing);

            context.Item.CTPs.Add(context.ItemPrice);

            return result;
        }
    }
}
