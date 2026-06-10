using System.Collections.Generic;

namespace PI.ProductCatalog.Style
{
    public class PackagingParser : FixedColsParser
    {
        public override string Element => "MEA";

        public override Token[] Tokens => new Token[]
        {
            null,
            Token.Const("Measurement Qualifier", "CF"),
            Token.R("Whole Number of Base Units Contained in MEA,04", 1,20),
            // CT = Carton
            // PL = Pallet
            // TL = Truckload            
            Token.ID("Unit of Measurement Code", 2)
        };

        protected override LineResult Convert(CatalogParserContext context)
        {
            var result = base.Convert(context);

            context.Section.Packaging ??= new List<Models.UOMRate>();
            context.Section.Packaging.Add(new Models.UOMRate
            {
                UOM = context.ParseUOM(context.Values[3]),
                Measurement = new Models.Measurement
                {
                    Units = (decimal)context.Values[2],
                    UOM = Models.UnitOfMeasurement.Each, // TODO: this should actually be based on BaseUnit (g39)
                }
            });

            return result;
        }
    }
}
