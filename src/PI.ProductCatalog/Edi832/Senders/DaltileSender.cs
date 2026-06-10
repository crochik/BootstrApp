using System;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Edi832
{
    public class DaltileSender : CatalogFormat2_2_02
    {
        public override string SenderId => "2143981411";
        public override string Name => "Daltile";
        public override Uri Url => new Uri("ftp://daltileb2b.daltile.com/Outbox");

        public override UnitOfMeasurement ParseUOM(object value)
        {
            return value switch
            {
                "FT2" => UnitOfMeasurement.SqFt,    // made up UOM
                null => UnitOfMeasurement.Inches,   // when missing assume IN (bad!)
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
        // if (TryGetParser<LINParser>(parsers, "LIN", out var linParser))
        // {
        //     linParser.Tokens[10].MaxLength = null; // Size Code

        //     linParser.Tokens[14].MaxLength = null;
        //     linParser.Tokens[15].IsMandatory = false;
        //     linParser.Tokens[16].IsMandatory = false;
        // }

        // if (TryGetParser<SLNParser>(parsers, "SLN", out var slnParser))
        // {
        //     parsers["SLN"] = new CustomSublineLoop();
        // }

        // if (TryGetParser<ConditionalValueParser>(parsers, "MEA", out var meaParser))
        // {
        //     if (TryGetParser<MeasurementParser>(meaParser.Parsers, "LN", out var lnParser))
        //     {
        //         meaParser.Parsers["LN"] = new CustomMeaParser("Standard Length", "LN", lnParser.Setter);
        //     }

        //     if (TryGetParser<MeasurementParser>(meaParser.Parsers, "WD", out var wdParser))
        //     {
        //         meaParser.Parsers["WD"] = new CustomMeaParser("Standard Width", "WD", wdParser.Setter);
        //     }

        //     if (TryGetParser<MeasurementParser>(meaParser.Parsers, "SU", out var suParser))
        //     {
        //         meaParser.Parsers["SU"] = new CustomMeaParser("Selling Unit", "SU", suParser.Setter);
        //     }
        // }

        // if (TryGetParser<ConditionalValueParser>(parsers, "PID", out var pidParser))
        // {
        // //     if (TryGetParser<ProductInfo>(pidParser.Parsers, "73", out var cnParser))
        // //     {
        // //         pidParser.Parsers["73"] = new CustomProductInfo("73", 1, 80, cnParser.Setter);
        // //     }

        // //     if (TryGetParser<ProductInfo>(pidParser.Parsers, "35", out var cn2Parser))
        // //     {
        // //         pidParser.Parsers["35"] = new CustomProductInfo("35", 1, 80, cn2Parser.Setter);
        // //     }

        //     // if (TryGetParser<ProductInfo>(pidParser.Parsers, "TRN", out var trnParser))
        //     // {
        //     //     pidParser.Parsers["TRN"] = new CustomProductInfo("TRN", 1, 80, trnParser.Setter);
        //     // }
        // }

        // if (TryGetParser<CTPLoopParser>(parsers, "CTP", out var ctpParser))
        // {
        //     parsers["CTP"] = new CustomStylePriceLoop();
        // }

        // if (TryGetParser<Subline.ColorLevelPricing>(parsers, "CTP", out var ctp2Parser))
        // {
        //     parsers["CTP"] = new CustomColorLevelPricing();
        // }
        // }

        // public class CustomSublineLoop : SLNParser
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             tokens[23].MaxLength = null;
        //             return tokens;
        //         }
        //     }
        // }

        // private class CustomColorLevelPricing : Subline.ColorLevelPricing
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             tokens[1].IsMandatory = false; // "Price Identifier Code"
        //             tokens[2].IsMandatory = false; // "Unit Price"
        //             tokens[4].MaxLength = null; // "Unit of Measurement Code" (support FT2)
        //             return tokens;
        //         }
        //     }

        //     protected override LineResult Convert(CatalogParserContext context)
        //     {
        //         context.Values[1] ??= "LPR";
        //         context.Values[2] ??= 0M;

        //         if (string.Equals(context.Values[4], "FT2")) context.Values[4] = "SF";

        //         return base.Convert(context);
        //     }
        // }

        // private class CustomStylePriceLoop : CTPLoopParser
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             tokens[1].IsMandatory = false; // "Price Identifier Code"
        //             tokens[2].IsMandatory = false; // "Unit Price"
        //             // tokens[4].MaxLength = null; // "Unit of Measurement Code" (support FT2)
        //             return tokens;
        //         }
        //     }

        //     protected override LineResult Convert(CatalogParserContext context)
        //     {
        //         context.Values[1] ??= "LPR";
        //         context.Values[2] ??= 0M;

        //         if (string.Equals(context.Values[4], "FT2")) context.Values[4] = "SF";

        //         return base.Convert(context);
        //     }
        // }

        // private class CustomProductInfo : ProductInfo
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             // tokens[4].IsMandatory = false;
        //             tokens[4].MaxLength = null;
        //             return tokens;
        //         }
        //     }

        //     public CustomProductInfo(string charCode, int min, int max, Action<string, CatalogParserContext> setter = null) :
        //         base(charCode, min, max, setter)
        //     {
        //     }
        // }

        // private class CustomMeaParser : MeasurementParser
        // {
        //     public override Token[] Tokens
        //     {
        //         get
        //         {
        //             var tokens = base.Tokens;
        //             tokens[2].IsMandatory = false; // Qtty
        //             // tokens[3].IsMandatory = false; // Unit of Measurement Code
        //             // tokens[3].MaxLength = null; // Unit of Measurement Code (invalid UOM: FT2)
        //             return tokens;
        //         }
        //     }

        //     public CustomMeaParser(string name, string qualifier, Action<Measurement, CatalogParserContext> setter = null) :
        //         base(name, qualifier, setter)
        //     {
        //     }

        //     protected override LineResult Convert(CatalogParserContext context)
        //     {
        //         if (context.Values[2] == null) return LineResult.Warning("Missing required Quantity");

        //         context.Values[3] = context.Values[3] switch
        //         {
        //             "FT2" => "SF", // made up UOM
        //             _ => context.Values[3] ?? "IN", // fallback to inches
        //         };

        //         return base.Convert(context);
        //     }
        // }
    }
}