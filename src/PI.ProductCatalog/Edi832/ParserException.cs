using System;

namespace PI.ProductCatalog
{
    public class ParserException : Exception
    {
        public int LineNumber { get; }
        public string Line { get; }

        public ParserException(CatalogParserContext context, string message) : base(message)
        {
            LineNumber = context.LineNumber;
            Line = context.Line;
        }

        public ParserException(string message) : base(message)
        {
        }
    }
}
