using System.Collections.Generic;
using System.Linq;

namespace PI.ProductCatalog
{
    public abstract class FixedColsParser : ILineParser
    {
        public abstract string Element { get; }
        public abstract Token[] Tokens { get; }

        public virtual bool IsCritical => false;

        public virtual LineResult ParseLine(CatalogParserContext context)
        {
            context.Values = new object[Tokens.Length];

            IEnumerable<string> warnings = null;
            
            for (var c = 0; c < Tokens.Length; c++)
            {
                var token = Tokens[c];
                if (token == null) continue;

                var value = c + 1 < context.CurrTokens.Length ? context.CurrTokens[c + 1] : null;
                var parsed = token.Parse(c, value);
                if (parsed.Failed)
                {
                    return LineResult.Warning(parsed.Message);
                }

                if (parsed.Message!=null)
                {
                    warnings = (warnings ?? Enumerable.Empty<string>()).Append(parsed.Message);
                }

                context.Values[c] = parsed.Value;
            }

            var conversion = Convert(context);
            if (conversion.Message != null)
            {
                warnings = (warnings ?? Enumerable.Empty<string>()).Append(conversion.Message);
            }

            return warnings != null ? LineResult.Warning(string.Join("; ", warnings)) : conversion;
        }

        protected virtual LineResult Convert(CatalogParserContext context)
        {
            for (var c = 0; c < Tokens.Length && c < context.Values.Length; c++)
            {
                var token = Tokens[c];
                var value = context.Values[c];
                if (token?.Setter == null || value == null) continue;

                token.Setter(value, context);
            }

            return LineResult.Success;
        }
    }
}
