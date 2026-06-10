namespace Webhook.Service.Formats;

/// <summary>Resolves an <see cref="IPayloadParser"/> by format name.</summary>
public sealed class PayloadParserRegistry
{
    private readonly IReadOnlyDictionary<string, IPayloadParser> _parsers;

    public PayloadParserRegistry(IEnumerable<IPayloadParser> parsers)
    {
        _parsers = parsers.ToDictionary(p => p.Format, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the parser for <paramref name="format"/>, falling back to <c>raw</c>.</summary>
    public IPayloadParser Resolve(string format)
    {
        if (_parsers.TryGetValue(format, out var parser))
        {
            return parser;
        }

        return _parsers["raw"];
    }
}
