using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PI.ProductCatalog.Services
{
    public class CatalogParser
    {
        private CatalogParserContext _context;
        private readonly ILogger _logger;
        private readonly Loader _loader;

        public CatalogParser(ILogger<CatalogParser> logger, Loader loader)
        {
            _logger = logger;
            _loader = loader;
        }

        public async Task ParseAsync(CatalogParserContext context)
        {
            if (_context != null) throw new Exception("Parser instance can't be reused");

            _context = context;
            _loader.Init(_context);

            FileStream fileStream = null;
            try
            {
                fileStream = new FileStream(_context.Path, FileMode.Open);
                using var reader = new StreamReader(fileStream);

                await ParseHeaderAsync(reader);

                _context.Loop = Loop.Main;
                while (await LoadLineAsync(reader))
                {
                    await ProcessLineAsync();
                }

                await FinishAsync();
            }
            finally
            {
                fileStream?.Close();
            }
        }

        private async Task<bool> LoadLineAsync(StreamReader reader)
        {
            _context.LineNumber++;
            _context.Line = await reader.ReadLineAsync();
            _context.CurrTokens = _context.Line?.Split("*").Select(x => string.IsNullOrEmpty(x) ? null : x).ToArray();

            return _context.Line != null;
        }

        private async Task ParseHeaderAsync(StreamReader reader)
        {
            var header = new InterchangeStartSegment();
            if (!(await LoadLineAsync(reader))) throw new ParserException("EOF");
            header.ParseLine(_context);

            if (!(await LoadLineAsync(reader))) throw new ParserException("EOF");
            new FunctionalGroupHeader().ParseLine(_context);
        }

        private async Task ProcessLineAsync()
        {
            _logger?.LogInformation("Processing {LineNumber}: {Line}", _context.LineNumber, _context.Line);
            
            if (string.IsNullOrWhiteSpace(_context.Line)) return;

            _context.CurrTokens = _context.Line.Split("*").Select(x => string.IsNullOrEmpty(x) ? null : x).ToArray();

            if (!_context.Parsers.TryGetValue(_context.CurrTokens[0], out var parser))
            {
                throw new ParserException(_context, $"Unexpected Element: '{_context.CurrTokens[0]}'");
            }

            try
            {
                var result = parser.ParseLine(_context);
                if (result.Message != null)
                {
                    await _loader.LogWarningAsync(result.Message);
                }

                if (_context.Pop(out var style))
                {
                    await _loader.QueueAsync(style);
                }
            }
            catch (DataElementParserException ex)
            {
                await _loader.LogErrorAsync(ex, parser.IsCritical);

                if (parser.IsCritical)
                {
                    throw new ParserException(ex.Message);
                }
            }
            catch (Exception ex)
            {
                await _loader.LogErrorAsync(ex);
                throw;
            }
        }

        private async Task FinishAsync()
        {
            await _loader.FinishAsync();
        }
    }
}
