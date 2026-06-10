using System;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Edi832
{
    public class NourisonSender : CatalogFormat2_2_02
    {
        public const string DefaultSenderId =  "037269487";
        public override string SenderId => DefaultSenderId;
        public override string Name => "Nourison";
        public override Uri Url => new("ftp://b2b.nourison.net/OUTBOX");
        public override bool UseStyleAndColorName => true;

        public override UnitOfMeasurement ParseUOM(object value)
        {
            return value switch
            {
                "FT" => UnitOfMeasurement.Feet,    // they don't actually mean feet.inches :(
                _ => base.ParseUOM(value)
            };
        }

        // protected override Dictionary<string, ILineParser> Init(CatalogUpdate catalogUpdate, Loop loop)
        // {
        //     var parsers = base.Init(catalogUpdate, loop);
        //     Hack(parsers);
        //     return parsers;
        // }

        // private void Hack(Dictionary<string, ILineParser> parsers)
        // {
        //     if (TryGetParser<FixedColsParser>(parsers, "SLN", out var slnParser))
        //     {
        //         parsers["SLN"] = new CustomSLNParser();
        //     }

        //     // if (TryGetParser<ConditionalValueParser>(parsers, "PID", out var pidParser))
        //     // {
        //     //     // // Color Name (missing value)
        //     //     // if (TryGetParser<ProductInfo>(pidParser.Parsers, "73", out var cnParser))
        //     //     // {
        //     //     //     pidParser.Parsers["73"] = new CustomProductInfo("73", 1, 80, cnParser.Setter);
        //     //     // }

        //     //     // Primary Component (missing value)
        //     //     if (TryGetParser<ProductInfo>(pidParser.Parsers, "37", out var primaryComponentParser))
        //     //     {
        //     //         pidParser.Parsers["37"] = new CustomProductInfo("37", 1, 80, primaryComponentParser.Setter);
        //     //     }
        //     // }
        // }

        // /// <summary>
        // /// Missing required field 'Value'
        // /// PID*F*37
        // /// </summary>
        // private class CustomProductInfo : ProductInfo
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             tokens[4].IsMandatory = false;
        //             return tokens;
        //         }
        //     }

        //     public CustomProductInfo(string charCode, int min, int max, Action<string, CatalogParserContext> setter = null) :
        //         base(charCode, min, max, setter)
        //     {
        //     }
        // }

        /// <summary>
        /// Missing mandatory element: 'Assigned Identification' on position 0'
        /// SLN***O******SK*2--295 BRK  045069NR*UP*099446109361***ST*-295*BK*NR*SZ*045069
        /// </summary>
        // private class CustomSLNParser : SLNParser
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             tokens[0].IsMandatory = false;
        //             return tokens;
        //         }
        //     }
        // }
    }
}