using System.Collections.Generic;

namespace PI.ProductCatalog
{
    public class SLNParser : FixedColsParser
    {
        public override string Element => "SLN";

        public override Token[] Tokens => new Token[]
        {
            Token.AN("Assigned Identification", 1, 20,
                isMandatory: false, // non-spec: since we don't have much use for it and Nourison forgets it
                setter: (v,c) => c.Item.UniqueColorCode = v?.ToString()
            ),

            null,

            Token.Const("Information Only", "O"),

            null,null,null,null,null, //4-8

            Token.Const("Vendor’s SKU Number", "SK"),
            Token.AN("SKU",1,48,
                setter: (v,c) => c.Item.SKU = v?.ToString()
            ),

            Token.Const("UPC", "UP", false),
            Token.AN("UPC Code", 1, 48, isMandatory: false,
                setter: (v,c) => c.Item.UPCCode = v?.ToString()
            ),

            Token.Const("MFG SKU", "MG", false),
            Token.AN("Manufacturers Part Number", 1, 48, isMandatory: false,
                setter: (v,c) => c.Item.ManufacturerSKU = v?.ToString()
            ),

            Token.Const("Style Number", "ST", false),
            Token.AN("Style Number", 1, 48, isMandatory: false,
                setter: (v,c) => {
                    if (v==null) return;

                    var styleNumber = v.ToString();
                    if ( c.Item.StyleNumber!=null && !string.Equals(c.Item.StyleNumber, styleNumber))
                    {
                        throw new DataElementParserException($"Trying to change stylenumber from {c.Item.StyleNumber} to {styleNumber}", "Style Number", 15);
                    }

                    c.Item.StyleNumber = styleNumber;
                }
            ),

            Token.Const("Backing", "BK", false),
            Token.AN("Backing", 1, 48, isMandatory: false,
                setter: (v,c) => c.Item.Backing = v?.ToString()
            ),

            Token.Const("Vendor Alphanumeric Size Code", "SZ", false),
            Token.AN("Size Code", 1, 48, isMandatory: false,
                setter: (v,c) => c.Item.SizeCode = v?.ToString()
            ),

            Token.Const("Manufacturers Style Number", "MS", false),
            Token.AN("Manufacturers Style Number", 1, 48, isMandatory: false,
                setter: (v,c) => c.Item.ManufacturerStyleNumber = v?.ToString()
            ),

            Token.Const("Manufacturers Style Name", "MN", false),
            Token.AN("Manufacturers Style Name", 1, 48, isMandatory: false,
                setter: (v,c) => c.Item.ManufacturerStyleName = v?.ToString()
            ),
        };

        public override LineResult ParseLine(CatalogParserContext context)
        {
            context.Loop = Loop.Subline;
            return base.ParseLine(context);
        }

        protected override LineResult Convert(CatalogParserContext context)
        {
            context.Item = new Models.SLN
            {
                CTPs = new List<Models.SLNCTP>(),

                // // default values from style
                // StyleNumber = context.Section.StyleNumber,
                // StyleName = context.Section.StyleName,
                // ManufacturerStyleNumber = context.Section.ManufacturerStyleNumber,
                // ManufacturerStyleName = context.Section.ManufacturerStyleName,
            };

            // // seed prices with style price
            // foreach (var price in context.Section.Prices)
            // {
            //     context.Item.Prices.Add(new Models.ItemPrice
            //     {
            //         Criteria = price.Criteria,
            //         Unit = price.Unit,
            //         PackageCondition = price.PackageCondition, // ???
            //         UnitPrice = price.UnitPrice,
            //         LocationId = price.LocationId,
            //     });
            // }

            var result = base.Convert(context);

            context.Section.Items ??= new List<Models.SLN>();
            context.Section.Items.Add(context.Item);

            return result;
        }
    }
}
