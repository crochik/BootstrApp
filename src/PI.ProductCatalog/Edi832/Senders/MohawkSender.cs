using System;
using System.Collections.Generic;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Edi832
{
    public class MohawkSender : CatalogFormat2_2_02
    {
        public const string DefaultSenderId = "120930102100C";
        public override string Name => "Mohawk";
        public override string SenderId => DefaultSenderId;
        public override Uri Url => new Uri("ftp://mohawk1.mohawkind.com:2850/home/outbox");

        protected override Dictionary<string, ILineParser> Init(CatalogUpdate catalogUpdate, Loop loop)
        {
            var parsers = base.Init(catalogUpdate, loop);
            Hack(parsers);
            return parsers;
        }

        private void Hack(Dictionary<string, ILineParser> parsers)
        {
            if (TryGetParser<ConditionalValueParser>(parsers, "MEA", out var meaParser))
            {
                if (TryGetParser<MeasurementParser>(meaParser.Parsers, "NU", out var swParser))
                {
                    meaParser.Parsers["NU"] = new CustomMeasurementRateParser("Pattern Repeat", "NU", swParser.Setter);
                }
            }
        }

        private class CustomMeasurementRateParser : MeasurementParser
        {
            public CustomMeasurementRateParser(string name, string qualifier, Action<Measurement, CatalogParserContext> setter = null) :
                base(name, qualifier, setter)
            {
            }

            public override LineResult ParseLine(CatalogParserContext context)
            {
                try
                {
                    return base.ParseLine(context);
                }
                catch (System.FormatException ex)
                {
                    return LineResult.Warning($"Unexpected value format: {ex.Message}");
                }
            }

            // protected override LineResult Convert(CatalogParserContext context)
            // {
            //     if (context.Values[2] == null) return LineResult.Warning($"Missing {_name}, skip");

            //     var result = base.Convert(context);

            //     if (Setter == null) return result;

            //     if (!decimal.TryParse(context.Values[3]?.ToString(), out var decValue))
            //     {
            //         return LineResult.Warning($"value is not a number");
            //     }

            //     var measurement = new Measurement
            //     {
            //         Units = decValue,
            //         UOM = context.ParseUOM(context.Values[3]),
            //     };

            //     Setter(measurement, context);

            //     return result;
            // }
        }
    }
}