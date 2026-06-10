using System;
using System.Collections.Generic;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Edi832
{
    public class ShawSender : CatalogFormat2_2_02
    {
        public const string DefaultSenderId =  "045840055";
        public override string SenderId => DefaultSenderId;
        public override string Name => "Shaw";
        public override Uri Url => new("sftp://shawedi.shawfloors.com:22/Outbox");
        public override bool UseStyleAndColorName => true;

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
                if (TryGetParser<MeasurementRateParser>(meaParser.Parsers, "SW", out var swParser))
                {
                    meaParser.Parsers["SW"] = new CustomMeasurementRateParser("Shipping Weight", "SW", swParser.Setter);
                }
            }
        }

        private class CustomMeasurementRateParser : MeasurementRateParser
        {
            public CustomMeasurementRateParser(string name, string qualifier, Action<UOMRate, CatalogParserContext> setter = null) :
                base(name, qualifier, setter)
            {
            }

            protected override LineResult Convert(CatalogParserContext context)
            {
                return context.Values[3] switch
                {
                    "SY" => LineResult.Warning("Invalid UOM for Shipping Weight: SY"),
                    _ => base.Convert(context),
                };
            }
        }
    }
}