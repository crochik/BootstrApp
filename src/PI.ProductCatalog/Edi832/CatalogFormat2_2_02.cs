using System;
using System.Collections.Generic;
using System.Linq;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog;

public abstract class CatalogFormat2_2_02 : AbstractCatalogFormat
{
    // public override ILineParser[] HeaderParsers { get; } = new ILineParser[]
    // {
    //     // new InterchangeStartSegment(),
    //     new FunctionalGroupHeader(),
    // };

    private ILineParser[] MainParsers { get; } =
    [
        // header
        new TransferSetHeader(),
        new BeginningSegment(),
        new Currency(),
        new VendorName(),
        new AccountNumber(), // REF*11

        // body
        new LINParser(), // LIN (loop)

        // footer
        new TransactionTrailer(),
        new FunctionalGroupTrailer(),
        new InterchangeTrailer()
    ];

    protected override Dictionary<string, ILineParser> Init(CatalogUpdate catalogUpdate, Loop loop)
    {
        return loop switch
        {
            Loop.Main => MainParsers.ToDictionary(x => x.Element),
            Loop.Style => LINLoopParsers.ToDictionary(x => x.Element),
            Loop.Price => CTPLoopParsers.ToDictionary(x => x.Element),
            Loop.Subline => GetSublineParsers(catalogUpdate),

            _ => throw new Exception("Invalid lopp")
        };
    }

    private Dictionary<string, ILineParser> GetSublineParsers(CatalogUpdate catalogUpdate)
    {
        switch (catalogUpdate.Pricing)
        {
            case CatalogPricing.LINPricing: // ????
            case CatalogPricing.SLNPricing: // style level with color overrides?
                return SLNLoopParsers.ToDictionary(x => x.Element);

            default:
                break;
        }

        return SLNLoopParsers
            .Where(x => !string.Equals(x.Element, "CTP"))
            .ToDictionary(x => x.Element);
    }

    private static ILineParser[] LINLoopParsers =>
    [
        new ConditionalValueParser
        {
            Element = "DTM",
            Parsers =
            {
                {
                    "007", new DateReference(
                        // 007 = Effective 
                        (v, c) => c.Section.EffectiveDate = v
                    )
                },

                {
                    "162", new DateReference(
                        // 162 = Pending
                        (v, c) => c.Section.PendingDate = v
                    )
                },

                {
                    "197", new DateReference(
                        // 197 = Dropped
                        (v, c) => c.Section.DroppedDate = v
                    )
                },

                {
                    "433", new DateReference(
                        // 433 – Error / Remove (Sent in error)                    
                        (v, c) => throw new Exception("not ready to handle")
                    )
                },
            },
        },
        new ConditionalValueParser
        {
            Element = "REF",
            Parsers =
            {
                {
                    "GP", new Style.Reference(30,
                        // Group Number - Buying Group
                        (v, c) => c.Section.BuyingGroup = v.ToString()
                    )
                },

                {
                    "Q1", new Style.Reference(30,
                        // Contract Pricing - Contract Pricing/Quote Number
                        (v, c) => c.Section.Contract = v.ToString()
                    )
                },

                {
                    "19", new Style.Reference3(80,
                        // Division Identifier - Selling Company (Supplier or Mill sending the price catalog)
                        (v, c) => c.Section.SellingCompany = v.ToString()
                    )
                },

                {
                    "DM", new Style.Reference(30,
                        // Associated Product Number - Associated Item Number (SKU)
                        (v, c) =>
                        {
                            c.Section.AssociatedStyleNumbers ??= new List<string>();
                            c.Section.AssociatedStyleNumbers.Add(v.ToString());
                        }
                    )
                },

                {
                    "GG", new Style.Reference(30,
                        // Gauge - Gauge Description
                        (v, c) => c.Section.Gauge = v.ToString()
                    )
                },
            }
        },
        new ConditionalValueParser
        {
            Index = 2,
            Element = "PID",
            Parsers =
            {
                {
                    "TRN", new ProductInfo("TRN", 1, 80,
                        // Style Name
                        (v, c) => c.Section.StyleName = v,
                        isMandatory: false // non-compliant
                    )
                },
                {
                    "MAC", new ProductInfo("MAC", 3, 7,
                        // Material Classification, Product Type Description (see 832 – Price Catalog Appendix A for a listing of valid values)
                        (v, c) => c.Section.MaterialClassification = v,
                        "UNC" // fallback value: unclassified
                    )
                },
                {
                    "PNA", new ProductInfo("PNA", 1, 80,
                        // Product Name Abbreviated
                        (v, c) => c.Section.AbbreviatedProductName = v,
                        isMandatory: false // non-compliant
                    )
                },
                {
                    "CO", new ProductInfo("CO", 1, 80,
                        // Collection
                        (v, c) => c.Section.CollectionName = v,
                        isMandatory: false // non-compliant
                    )
                },
                {
                    "37", new ProductInfo("37", 1, 80,
                        // Primary Component
                        (v, c) => c.Section.PrimaryComponent = v,
                        isMandatory: false // non-compliant
                    )
                },
                {
                    "CM", new ProductInfo("CM", 1, 80,
                        // Composition
                        (v, c) => c.Section.Composition = v,
                        isMandatory: false // non-compliant
                    )
                },
                {
                    "12", new ProductInfo("12", 1, 80,
                        // Type (additional free-form description of product type, e.g. cut loop, cut-pile, No Toe base, etc.)
                        (v, c) => c.Section.ProductType = v,
                        isMandatory: false // non-compliant
                    )
                },
                { "WD", new Style.WarrantyDescription() },
                { "BLM", new Style.BuilderInformationRequired() },
            }
        },
        new ConditionalValueParser
        {
            Index = 2,
            Element = "MEA",
            Parsers =
            {
                {
                    "NU", new MeasurementParser("Pattern Repeat", "NU",
                        (v, c) => c.Section.PatternRepeat = v
                    )
                },
                {
                    "DP", new MeasurementParser("Pattern Drop", "DP",
                        (v, c) => c.Section.PatternDrop = v
                    )
                },
                {
                    "LP", new MeasurementParser("Pattern Length", "LP",
                        (v, c) => c.Section.PatternLength = v
                    )
                },
                {
                    "999", new MeasurementParser("Pattern Width", "999",
                        (v, c) => c.Section.PatternWidth = v
                    )
                },
                {
                    "LN", new MeasurementParser("Standard Length", "LN",
                        (v, c) => c.Section.NominalLength = v
                    )
                },
                {
                    "LM", new MeasurementParser("Actual Length", "LM",
                        (v, c) => c.Section.ActualLength = v
                    )
                },
                {
                    "WD", new MeasurementParser("Standard Width", "WD",
                        (v, c) => c.Section.NominalWidth = v
                    )
                },
                {
                    "WM", new MeasurementParser("Actual Width", "WM",
                        (v, c) => c.Section.ActualWidth = v
                    )
                },
                {
                    "HT", new MeasurementParser("Height", "HT",
                        (v, c) => c.Section.Height = v
                    )
                },
                {
                    "SW", new MeasurementRateParser("Shipping Weight", "SW",
                        (v, c) => c.Section.ShippingWeight = v
                    )
                },
                {
                    "FW", new MeasurementParser("Face Weight", "FW",
                        (v, c) => c.Section.FaceWeight = v
                    )
                },
                {
                    "SU", new MeasurementParser("Selling Unit", "SU",
                        (v, c) => c.Section.SellingUnit = v
                    )
                },
                { "CF", new Style.PackagingParser() },
            }
        },

        // SAC
        new Style.SACParser((v, c) =>
        {
            // TODO: save ...
            // ...
        }),

        new Price.BaseUnitParser(),
        
        new CTPLoopParser(),
        new SLNParser(), // for when there are no prices at the style (LIN level)
        new LINParser(),
        new NumberOfItems()
    ];

    private static ILineParser[] CTPLoopParsers { get; } =
    [
        new ConditionalValueParser
        {
            Element = "DTM",
            Parsers =
            {
                {
                    "015", new DateReference(
                        // 015 = Promotional Start 
                        (v, c) => c.Price.PromotionalStart = v
                    )
                },

                {
                    "016", new DateReference(
                        // 016 = Promotional End
                        (v, c) => c.Price.PromotionalEnd = v
                    )
                },

                {
                    "007", new DateReference(
                        // 007 = Effective 
                        (v, c) => c.Price.EffectiveDate = v
                    )
                },

                {
                    "162", new DateReference(
                        // 162 = Pending
                        (v, c) => c.Price.PendingDate = v
                    )
                },

                {
                    "197", new DateReference(
                        // 197 = Dropped
                        (v, c) => c.Price.DroppedDate = v
                    )
                },

                {
                    "433", new DateReference(
                        // 433 – Error / Remove (Sent in error)                    
                        (v, c) => throw new ParserException("Not ready to handle: DTM*433")
                    )
                },
            },
        },
        new Price.Promotional((v, c) => c.Price.Promotion = v),

        // SAC
        new Price.SACParser((v, c) =>
        {
            // TODO: handle it
            // ...
        }),
        
        new Price.BaseUnitParser(),
        
        // new Price.PackagingParser(),

        new SLNParser(),
        new CTPLoopParser(),
        new LINParser(),
        new NumberOfItems()
    ];

    private static ILineParser[] SLNLoopParsers { get; } =
    [
        new ConditionalValueParser
        {
            Index = 2,
            Element = "PID",
            Parsers =
            {
                {
                    "73", new ProductInfo("73", 1, 80,
                        // Color Name
                        (v, c) => c.Item.ColorName = v
                    )
                },

                {
                    "35", new ProductInfo("35", 1, 80,
                        // Color Number
                        (v, c) => c.Item.ColorNumber = v
                    )
                },

                {
                    "PNA", new ProductInfo("PNA", 1, 80,
                        // Abbreviated Product Name
                        (v, c) => c.Item.AbbreviatedProductName = v
                    )
                },

                {
                    "08", new ProductInfo("08", 1, 80,
                        // Associated Sku
                        (v, c) =>
                        {
                            c.Item.PID08 ??= new List<string>();
                            c.Item.PID08.Add(v);
                        }
                    )
                },
            }
        },

        new Subline.ColorLevelPricing(),

        new ConditionalValueParser
        {
            Element = "DTM",
            Parsers =
            {
                {
                    "015", new DateReference(
                        // 015 = Promotional Start 
                        (v, c) => c.Item.PromotionalStart = v
                    )
                },

                {
                    "016", new DateReference(
                        // 016 = Promotional End
                        (v, c) => c.Item.PromotionalEnd = v
                    )
                },

                {
                    "007", new DateReference(
                        // 007 = Effective 
                        (v, c) => c.Item.EffectiveDate = v
                    )
                },

                {
                    "162", new DateReference(
                        // 162 = Pending
                        (v, c) => c.Item.PendingDate = v
                    )
                },

                {
                    "197", new DateReference(
                        // 197 = Dropped
                        (v, c) => c.Item.DroppedDate = v
                    )
                },

                {
                    "433", new DateReference(
                        // 433 – Error / Remove (Sent in error)                    
                        (v, c) => throw new Exception("not ready to handle")
                    )
                },
            },
        },

        new ConditionalValueParser
        {
            Index = 2,
            Element = "MEA",
            Parsers =
            {
                {
                    "NU", new MeasurementParser("Pattern Repeat", "NU",
                        (v, c) => c.Item.PatternRepeat = v
                    )
                },
                {
                    "DP", new MeasurementParser("Pattern Drop", "DP",
                        (v, c) => c.Item.PatternDrop = v
                    )
                },
                {
                    "LP", new MeasurementParser("Pattern Length", "LP",
                        (v, c) => c.Item.PatternLength = v
                    )
                },
                {
                    "999", new MeasurementParser("Pattern Width", "999",
                        (v, c) => c.Item.PatternWidth = v
                    )
                },
                {
                    "LN", new MeasurementParser("Standard Length", "LN",
                        (v, c) => c.Item.NominalLength = v
                    )
                },
                {
                    "LM", new MeasurementParser("Actual Length", "LM",
                        (v, c) => c.Item.ActualLength = v
                    )
                },
                {
                    "WD", new MeasurementParser("Standard Width", "WD",
                        (v, c) => c.Item.NominalWidth = v
                    )
                },
                {
                    "WM", new MeasurementParser("Actual Width", "WM",
                        (v, c) => c.Item.ActualWidth = v
                    )
                },
                {
                    "HT", new MeasurementParser("Height", "HT",
                        (v, c) => c.Item.Height = v
                    )
                },
                {
                    "SW", new MeasurementRateParser("Shipping Weight", "SW",
                        (v, c) => c.Item.ShippingWeight = v
                    )
                },
                {
                    "FW", new MeasurementParser("Face Weight", "FW",
                        (v, c) => c.Item.FaceWeight = v
                    )
                },
                {
                    "SU", new MeasurementParser("Selling Unit", "SU",
                        (v, c) => c.Item.SellingUnit = v
                    )
                },
            }
        },

        new ConditionalValueParser
        {
            Index = 1,
            Element = "MTX",
            Parsers =
            {
                {
                    "POB", new Subline.Message("Primary Observation (URL for picture)", "POB",
                        (v, c) => c.Item.AddImageUrl(v)
                    )
                },
                {
                    "SOB",
                    new Subline.Message("Secondary Observation (URL for second picture)", "SOB",
                        (v, c) => c.Item.AddImageUrl(v)
                    )
                },
                {
                    "PDS", new Subline.Message("Product Specification", "PDS",
                        (v, c) => c.Item.ProductSpecification = v
                    )
                },
            }
        },

        // BACK UP
        new SLNParser(),
        // new Style.PriceLoop(), // how to differentiate this from the color levle pricing ?????
        new LINParser(),
        new NumberOfItems()
    ];
}