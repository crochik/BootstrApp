namespace PI.ProductCatalog
{
    public class LineResult
    {
        public static readonly LineResult Success = new();

        public string Message { get; }

        public LineResult(string message)
        {
            Message = message;
        }

        public LineResult()
        {
        }

        public static LineResult Warning(string message) => new(message);
    }

    public interface ILineParser
    {
        string Element { get; }
        bool IsCritical { get; }

        LineResult ParseLine(CatalogParserContext context);
    }
}
