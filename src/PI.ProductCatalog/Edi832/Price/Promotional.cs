using System;

namespace PI.ProductCatalog.Price
{
    /// <summary>
    /// Allow multiple!?!?!?!?!!??
    /// </summary>
    public class Promotional : FixedColsParser
    {
        public Action<string, CatalogParserContext> Setter { get; }

        public Promotional(Action<string, CatalogParserContext> setter)
        {
            Setter = setter;
        }

        public override string Element => "G43";

        public override Token[] Tokens => new Token[]
        {
            Token.Const("Market Area Code Qualifier", "003"),
            null,
            Token.AN("FOB Description", 1,80),
        };

        protected override LineResult Convert(CatalogParserContext context)
        {
            var result = base.Convert(context);
            if (Setter == null) return result;

            if (context.Values.Length > 2)
            {
                Setter(context.Values[2]?.ToString(), context);
            }

            return result;
        }
    }
}
