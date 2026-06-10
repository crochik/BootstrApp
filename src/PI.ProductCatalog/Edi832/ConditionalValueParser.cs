using System.Collections.Generic;

namespace PI.ProductCatalog
{
    public class ConditionalValueParser : ILineParser
    {
        public string Element { get; set; }

        public int Index { get; set; } = 1;

        public bool IsCritical => false;

        public Dictionary<string, ILineParser> Parsers { get; set; } = new Dictionary<string, ILineParser>();

        public LineResult ParseLine(CatalogParserContext context)
        {
            if (context.CurrTokens.Length <= Index) throw new ParserException(context, "Conditional Column missing");
            if (!Parsers.TryGetValue(context.CurrTokens[Index], out var parser)) throw new ParserException(context, $"Unexpected Value: {context.CurrTokens[Index]}");

            try
            {
                return parser.ParseLine(context);
            }
            catch (DataElementParserException ex)
            {
                if (parser.IsCritical)
                {
                    throw new ParserException(context, ex.Message);
                }

                throw;
            }
        }
    }
}
