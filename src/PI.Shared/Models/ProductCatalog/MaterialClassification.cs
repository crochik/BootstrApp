using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.ProductCatalog.Models
{
    public enum Surface
    {
        Unclassified,
        Any,
        Floor,
        Wall,
        Ceiling,
        Countertop,
        Pool,
    }

    public enum MaterialType
    {
        Unclassified,
        Miscellaneous,
        Accessories,

        [Description("Wall Base")]
        WallBase,
        Carpet,

        [Description("Ceramic Tile")]
        CeramicTile,
        Ceilings,
        Displays,
        Fixtures,
        Glass,
        Installation,
        Laminates,
        Linoleum,
        Metal,
        Pad,

        [Description("Rubber Mats")]
        RubberMats,

        [Description("Area Rugs")]
        AreaRugs,

        [Description("Natural Stones")]
        NaturalStones,
        Education,
        Tools,
        Vinyl,

        [Description("Wall Coverings")]
        WallCoverings,
        Wood,

        // non b2b standard
        Labor,
        Discount,
        Custom1,
        Custom2,
        Custom3,
        Custom4
    }

    public enum MaterialApplication
    {
        Unclassified,
        Miscellaneous,
        Commercial,
        Residential
    }

    public enum MaterialSubType
    {
        // all ?
        Unclassified,
        Miscellaneous,
        Sample,

        // not b2b standard
        Accessories,
        Custom1,
        Custom2,
        Custom3,
        Custom4,

        [MaterialType(MaterialType.Accessories, "Maintenance and Cleaning")]
        MaintenanceAndCleaning,

        [MaterialType(MaterialType.WallBase)]
        Rubber,
        [MaterialType(MaterialType.WallBase)]
        Filler,
        [MaterialType(MaterialType.WallBase)]
        Vinyl,
        [MaterialType(MaterialType.WallBase)]
        Moldings,
        [MaterialType(MaterialType.WallBase)]
        Cap,

        [MaterialType(MaterialType.Carpet)]
        Indoor,
        [MaterialType(MaterialType.Carpet)]
        Outdoor,
        [MaterialType(MaterialType.Carpet, "Tile")]
        CarpetTile,
        [MaterialType(MaterialType.Carpet)]
        Tufted,
        [MaterialType(MaterialType.Carpet, "Needle Punch")]
        NeedlePunch,
        [MaterialType(MaterialType.Carpet)]
        Woven,
        [MaterialType(MaterialType.Carpet)]
        Flocked,
        [MaterialType(MaterialType.Carpet)]
        Berber,
        [MaterialType(MaterialType.Carpet)]
        Patterned,
        [MaterialType(MaterialType.Carpet, "Patterned Berber")]
        PatternedBerber,

        [MaterialType(MaterialType.CeramicTile, "Floor Tile")]
        FloorTile,
        [MaterialType(MaterialType.CeramicTile, "Wall Tile")]
        WallTile,
        [MaterialType(MaterialType.CeramicTile, "Pool Tile")]
        PoolTile,
        [MaterialType(MaterialType.CeramicTile, "Bicutture Floor Tile")]
        BicuttureFloorTile,
        [MaterialType(MaterialType.CeramicTile, "Monocutture Floor Tile")]
        MonocuttureFloorTile,
        [MaterialType(MaterialType.CeramicTile, "Porcelane Floor Tile")]
        PorcelaneFloorTile,
        [MaterialType(MaterialType.CeramicTile, "Quarry Floor Tile")]
        QuarryFloorTile,
        [MaterialType(MaterialType.CeramicTile, "Mosaic Floor Tile")]
        MosaicFloorTile,
        [MaterialType(MaterialType.CeramicTile, "TerraCotta Floor Tile")]
        TerraCottaFloorTile,
        [MaterialType(MaterialType.CeramicTile, "Border Floor Tile")]
        BorderFloorTile,
        [MaterialType(MaterialType.CeramicTile, "Field Wall Tile")]
        FieldWallTile,
        [MaterialType(MaterialType.CeramicTile, "Decorative Wall Tile")]
        DecorativeWallTile,
        [MaterialType(MaterialType.CeramicTile, "Listello Wall Tile")]
        ListelloWallTile,
        [MaterialType(MaterialType.CeramicTile, "Bullnose Or Cap")]
        BullnoseOrCap,
        [MaterialType(MaterialType.CeramicTile, "Surface Trim")]
        SurfaceTrim,
        [MaterialType(MaterialType.CeramicTile)]
        Mud,
        [MaterialType(MaterialType.CeramicTile, "Chair Rail")]
        ChairRail,

        [MaterialType(MaterialType.Ceilings, "Tile")]
        CeilingsTile,
        [MaterialType(MaterialType.Ceilings, "Grid")]
        CeilingsGrid,

        [MaterialType(MaterialType.Fixtures, "Bathroom Fixtures")]
        BathroomFixtures,
        [MaterialType(MaterialType.Fixtures, "Kitchen Fixtures")]
        KitchenFixtures,

        [MaterialType(MaterialType.Glass, "Glass Mosaic")]
        GlassMosaic,
        [MaterialType(MaterialType.Glass, "Glass Tile")]
        GlassTile,

        [MaterialType(MaterialType.Installation)]
        Adhesives,
        [MaterialType(MaterialType.Installation)]
        Underlayments,
        [MaterialType(MaterialType.Installation)]
        Grouts,
        [MaterialType(MaterialType.Installation)]
        Sealant,
        [MaterialType(MaterialType.Installation, "Tack Strip")]
        TackStrip,
        [MaterialType(MaterialType.Installation)]
        Tools,
        [MaterialType(MaterialType.Installation)]
        Reducers,
        [MaterialType(MaterialType.Installation)]
        TMold,
        [MaterialType(MaterialType.Installation)]
        Transitions,
        [MaterialType(MaterialType.Installation)]
        EdgeGuard,

        [MaterialType(MaterialType.Laminates, "Floors")]
        LaminatedFloors,
        [MaterialType(MaterialType.Laminates, "Countertop")]
        LaminatedCountertop,
        [MaterialType(MaterialType.Laminates, "Moldings")]
        LaminatedMoldings,
        [MaterialType(MaterialType.Laminates, "Acoustic Moldings")]
        LaminatedAcousticMoldings,
        [MaterialType(MaterialType.Laminates, "Baseboard Moldings")]
        LaminatedBaseboardMoldings,
        [MaterialType(MaterialType.Laminates, "End Cap Moldings")]
        LaminatedEndCapMoldings,
        [MaterialType(MaterialType.Laminates, "Quarter Round Moldings")]
        LaminatedQuarterRoundMoldings,
        [MaterialType(MaterialType.Laminates, "Reducer Moldings")]
        LaminatedReducerMoldings,
        [MaterialType(MaterialType.Laminates, "Stair Casing Moldings")]
        LaminatedStairCasingMoldings,
        [MaterialType(MaterialType.Laminates, "Step Down Moldings")]
        LaminatedStepDownMoldings,
        [MaterialType(MaterialType.Laminates, "Stair Nosing Moldings")]
        LaminatedStairNosingMoldings,
        [MaterialType(MaterialType.Laminates, "Threshold Moldings")]
        LaminatedThresholdMoldings,
        [MaterialType(MaterialType.Laminates, "T-Moldings")]
        LaminatedTMoldings,
        [MaterialType(MaterialType.Laminates, "Transition Strip Moldings")]
        LaminatedTransitionStripMoldings,
        [MaterialType(MaterialType.Laminates, "Vent Moldings")]
        LaminatedVentMoldings,

        [MaterialType(MaterialType.Linoleum, "Tile")]
        LinoleumTile,
        [MaterialType(MaterialType.Linoleum, "Sheet")]
        LinoleumSheet,

        [MaterialType(MaterialType.Metal, "Tile")]
        MetalTile,

        [MaterialType(MaterialType.AreaRugs, "Area Rugs")]
        AreaRugs,

        [MaterialType(MaterialType.NaturalStones, "Slabs")]
        NaturalStonesSlabs,
        [MaterialType(MaterialType.NaturalStones, "Tiles")]
        NaturalStonesTiles,
        [MaterialType(MaterialType.NaturalStones, "Granite Slabs")]
        NaturalStonesGraniteSlabs,
        [MaterialType(MaterialType.NaturalStones, "Limestone Slabs")]
        NaturalStonesLimestoneSlabs,
        [MaterialType(MaterialType.NaturalStones, "Marble Slabs")]
        NaturalStonesMarbleSlabs,
        [MaterialType(MaterialType.NaturalStones, "Engineered Slabs")]
        NaturalStonesEngineeredSlabs,
        [MaterialType(MaterialType.NaturalStones, "Slate Slabs")]
        NaturalStonesSlateSlabs,
        [MaterialType(MaterialType.NaturalStones, "Stones Mosaics")]
        NaturalStonesMosaics,

        [MaterialType(MaterialType.Education, "Literature")]
        EducationLiterature,
        [MaterialType(MaterialType.Education, "Multi Media")]
        EducationMultiMedia,

        [MaterialType(MaterialType.Tools, "Rental")]
        RentalTools,
        [MaterialType(MaterialType.Tools, "Inventory")]
        InventoryTools,

        [MaterialType(MaterialType.Vinyl, "Sheet")]
        VinylSheet,
        [MaterialType(MaterialType.Vinyl, "Tile")]
        VinylTile,
        [MaterialType(MaterialType.Vinyl, "Treads")]
        VinylTreads,
        [MaterialType(MaterialType.Vinyl, "Nosing")]
        VinylNosing,
        [MaterialType(MaterialType.Vinyl, "Strip")]
        VinylStrip,
        [MaterialType(MaterialType.Vinyl, "Stringer")]
        VinylStringer,

        [MaterialType(MaterialType.WallCoverings, "Wall Paper")]
        Wallpaper,
        [MaterialType(MaterialType.WallCoverings)]
        Paint,

        [MaterialType(MaterialType.Wood, "Moldings")]
        WoodMoldings,
        [MaterialType(MaterialType.Wood, "Unfinished Engineered Wood")]
        UnfinishedEngineeredWood,
        [MaterialType(MaterialType.Wood, "Prefinished Engineered Wood")]
        PrefinishedEngineeredWood,
        [MaterialType(MaterialType.Wood, "Unfinished Solid Wood")]
        UnfinishedSolidWood,
        [MaterialType(MaterialType.Wood, "Prefinished Solid Wood")]
        PrefinishedSolidWood,
        [MaterialType(MaterialType.Wood, "Acoustic Wood Moldings")]
        AcousticWoodMoldings,
        [MaterialType(MaterialType.Wood, "Baseboard Moldings")]
        BaseboardWoodMoldings,
        [MaterialType(MaterialType.Wood, "End Cap Moldings")]
        EndCapWoodMoldings,
        [MaterialType(MaterialType.Wood, "Quarter Round Moldings")]
        QuarterRoundWoodMoldings,
        [MaterialType(MaterialType.Wood, "Reducer Moldings")]
        ReducerWoodMoldings,
        [MaterialType(MaterialType.Wood, "Stair Casing Moldings")]
        StairCasingWoodMoldings,
        [MaterialType(MaterialType.Wood, "Step Down Moldings")]
        StepDownWoodMoldings,
        [MaterialType(MaterialType.Wood, "Stair Nosing Moldings")]
        StairNosingWoodMoldings,
        [MaterialType(MaterialType.Wood, "Threshold Moldings")]
        ThresholdWoodMoldings,
        [MaterialType(MaterialType.Wood, "T-Moldings")]
        TWoodMoldings,
        [MaterialType(MaterialType.Wood, "Transition Strip Moldings")]
        TransitionStripWoodMoldings,
        [MaterialType(MaterialType.Wood, "Vent Moldings")]
        VentWoodMoldings
    }

    public class MaterialClassification
    {
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public MaterialType Type { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public MaterialSubType? SubType { get; set; }

        public MaterialApplication? Application { get; private set; }

        private Surface[] _surfaces = null;

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public Surface[] Surfaces
        {
            get => _surfaces ?? GetDefaultSurfaces();
            set => _surfaces = value;
        }

        [BsonElement]
        public bool IsRollGoods => Type switch
        {
            MaterialType.Carpet => SubType switch
            {
                MaterialSubType.CarpetTile => false,
                _ => true,
            },
            MaterialType.Vinyl => SubType switch
            {
                MaterialSubType.VinylSheet => true,
                _ => false,
            },
            MaterialType.Linoleum => SubType switch
            {
                MaterialSubType.LinoleumSheet => true,
                _ => false,
            },
            _ => false,
        };

        public override bool Equals(object obj)
            => (obj is MaterialClassification other) &&
                Type == other.Type &&
                SubType.GetValueOrDefault(MaterialSubType.Unclassified) == other.SubType.GetValueOrDefault(MaterialSubType.Unclassified) &&
                Application.GetValueOrDefault(MaterialApplication.Unclassified) == other.Application.GetValueOrDefault(MaterialApplication.Unclassified) &&
                Surfaces.IsEqualTo(other.Surfaces);

        public override int GetHashCode() => HashCode.Combine(Type, SubType, Application);

        public override string ToString()
        {
            var builder = new StringBuilder(Type.ToString());
            if (SubType.HasValue) builder.Append($"/{SubType}");
            if (Application.HasValue) builder.Append($" - {Application}");
            return builder.ToString();
        }

        private Surface[] GetDefaultSurfaces()
        {
            var surfaces = CalculateDefaultSurfaces().ToArray();
            return surfaces.Length > 0 ? surfaces : null;
        }

        private IEnumerable<Surface> CalculateDefaultSurfaces()
        {
            switch (Type)
            {
                case MaterialType.CeramicTile:
                    switch (SubType)
                    {
                        case MaterialSubType.PoolTile:
                            yield return Surface.Pool;
                            break;

                        case MaterialSubType.FloorTile:
                        case MaterialSubType.BicuttureFloorTile:
                        case MaterialSubType.MonocuttureFloorTile:
                        case MaterialSubType.PorcelaneFloorTile:
                        case MaterialSubType.QuarryFloorTile:
                        case MaterialSubType.MosaicFloorTile:
                        case MaterialSubType.TerraCottaFloorTile:
                        case MaterialSubType.BorderFloorTile:
                            yield return Surface.Floor;
                            break;

                        case MaterialSubType.WallTile:
                        case MaterialSubType.DecorativeWallTile:
                        case MaterialSubType.FieldWallTile:
                        case MaterialSubType.ListelloWallTile:
                            // case MaterialSubType.BullnoseOrCap:
                            // case MaterialSubType.Mud:
                            // case MaterialSubType.ChairRail:
                            yield return Surface.Wall;
                            break;
                    }
                    break;

                case MaterialType.Laminates:
                    switch (SubType)
                    {
                        case MaterialSubType.LaminatedCountertop:
                            yield return Surface.Countertop;
                            break;
                        default:
                            yield return Surface.Floor;
                            break;
                    }
                    break;

                // case MaterialType.Fixtures:
                case MaterialType.WallCoverings:
                    yield return Surface.Wall;
                    // if (SubType == MaterialSubType.Paint) yield return Surface.Ceiling;
                    break;

                case MaterialType.AreaRugs:
                case MaterialType.Carpet:
                case MaterialType.Wood:
                case MaterialType.Pad:
                case MaterialType.RubberMats:
                case MaterialType.Linoleum:
                case MaterialType.Vinyl:
                    yield return Surface.Floor;
                    break;

                case MaterialType.Ceilings:
                    yield return Surface.Ceiling;
                    break;

                case MaterialType.Glass:
                case MaterialType.Metal:
                case MaterialType.NaturalStones:
                case MaterialType.WallBase:
                case MaterialType.Miscellaneous:
                    // mixed/unknown?
                    break;

                case MaterialType.Accessories:
                case MaterialType.Displays:
                case MaterialType.Education:
                case MaterialType.Installation:
                case MaterialType.Tools:
                default:
                    // none
                    break;
            }
        }

        public static MaterialClassification Parse(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length < 3) return null;

            return First3(code) switch
            {
                "ACC" => Accessories(code),
                "BAS" => WallBase(code),
                "CAR" => Carpet(code),
                "CER" => CeramicTile(code),
                "CEI" => Ceilings(code),
                "DIS" => new MaterialClassification { Type = MaterialType.Displays },
                "FIX" => Fixtures(code),
                "GLS" => Glass(code),
                "INS" => Installation(code),
                "LAM" => Laminates(code),
                "LIN" => Linoleum(code),
                "MET" => Metal(code),
                "PAD" => new MaterialClassification { Type = MaterialType.Pad },
                "RUB" => new MaterialClassification { Type = MaterialType.RubberMats },
                "RUG" => AreaRugs(code),
                "STO" => NaturalStones(code),
                "TRA" => Education(code),
                "TOO" => Tools(code),
                "UNC" => new MaterialClassification(),
                "VIN" => Vinyl(code),
                "WAL" => WallCoverings(code),
                "WOO" => Wood(code),

                _ => new MaterialClassification(),
            };
        }

        public static MaterialClassification New(MaterialType type, MaterialSubType? subType = null)
            => new MaterialClassification
            {
                Type = type,
                SubType = subType
            };

        private static string First3(string code) => code != null && code.Length >= 3 ? code.Substring(0, 3) : null;
        private static string Next3(string code) => code != null && code.Length >= 6 ? code.Substring(3, 3) : null;
        private static MaterialApplication? GetMaterialApplication(string code) => code != null && code.Length == 7 ? code[6] switch
        {
            'C' => MaterialApplication.Commercial,
            'R' => MaterialApplication.Residential,
            _ => default(MaterialApplication?)
        } : default(MaterialApplication?);

        private static MaterialClassification Unclassified(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Unclassified,
            };

        private static MaterialClassification Accessories(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Accessories,
                SubType = Next3(code) switch
                {
                    "MAI" => MaterialSubType.MaintenanceAndCleaning,
                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification WallBase(string code)
            => new MaterialClassification
            {
                Type = MaterialType.WallBase,
                SubType = Next3(code) switch
                {
                    "RUB" => MaterialSubType.Rubber,
                    "VIN" => MaterialSubType.Vinyl,
                    "FIL" => MaterialSubType.Filler,
                    "CAP" => MaterialSubType.Cap,
                    "MOL" => MaterialSubType.Moldings,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Carpet(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Carpet,
                SubType = Next3(code) switch
                {
                    "IND" => MaterialSubType.Indoor, // Wall2Wall
                    "OUT" => MaterialSubType.Outdoor, // Turf
                    "TIL" => MaterialSubType.CarpetTile,
                    "TUF" => MaterialSubType.Tufted,
                    "NEE" => MaterialSubType.NeedlePunch,
                    "WOV" => MaterialSubType.Woven,
                    "FLO" => MaterialSubType.Flocked,
                    "BER" => MaterialSubType.Berber,
                    "PAT" => MaterialSubType.Patterned,
                    "PBR" => MaterialSubType.PatternedBerber,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification CeramicTile(string code)
            => new MaterialClassification
            {
                Type = MaterialType.CeramicTile,
                SubType = Next3(code) switch
                {
                    "FLO" => MaterialSubType.FloorTile,
                    "WAL" => MaterialSubType.WallTile,
                    "POO" => MaterialSubType.PoolTile,

                    "FBC" => MaterialSubType.BicuttureFloorTile,
                    "FMC" => MaterialSubType.MonocuttureFloorTile,
                    "FPO" => MaterialSubType.PorcelaneFloorTile,
                    "FQR" => MaterialSubType.QuarryFloorTile,
                    "FMO" => MaterialSubType.MosaicFloorTile,
                    "FTC" => MaterialSubType.TerraCottaFloorTile,
                    "FBO" => MaterialSubType.BorderFloorTile,

                    "WFL" => MaterialSubType.FieldWallTile,
                    "WDE" => MaterialSubType.DecorativeWallTile,
                    "WLI" => MaterialSubType.ListelloWallTile,

                    "TBN" => MaterialSubType.BullnoseOrCap,
                    "TSU" => MaterialSubType.SurfaceTrim,
                    "TMU" => MaterialSubType.Mud,
                    "TCH" => MaterialSubType.ChairRail,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Ceilings(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Ceilings,
                SubType = Next3(code) switch
                {
                    "TIL" => MaterialSubType.CeilingsTile,
                    "GRI" => MaterialSubType.CeilingsGrid,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Fixtures(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Fixtures,
                SubType = Next3(code) switch
                {
                    "BAT" => MaterialSubType.BathroomFixtures,
                    "KIT" => MaterialSubType.KitchenFixtures,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Glass(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Glass,
                SubType = Next3(code) switch
                {
                    "TIL" => MaterialSubType.GlassTile,
                    "MOS" => MaterialSubType.GlassMosaic,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Installation(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Installation,
                SubType = Next3(code) switch
                {
                    "ADH" => MaterialSubType.Adhesives,
                    "UND" => MaterialSubType.Underlayments,
                    "GRO" => MaterialSubType.Grouts,
                    "SEA" => MaterialSubType.Sealant,
                    "TAC" => MaterialSubType.TackStrip,
                    "TOO" => MaterialSubType.Tools,
                    "RED" => MaterialSubType.Reducers,
                    "MTM" => MaterialSubType.TMold,
                    "MTS" => MaterialSubType.Transitions,
                    "EDG" => MaterialSubType.EdgeGuard,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Laminates(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Laminates,
                SubType = Next3(code) switch
                {
                    "FLO" => MaterialSubType.LaminatedFloors,
                    "COU" => MaterialSubType.LaminatedCountertop,
                    "MOL" => MaterialSubType.LaminatedMoldings,
                    "MAC" => MaterialSubType.LaminatedAcousticMoldings,
                    "MBB" => MaterialSubType.LaminatedBaseboardMoldings,
                    "MEC" => MaterialSubType.LaminatedEndCapMoldings,
                    "MQR" => MaterialSubType.LaminatedQuarterRoundMoldings,
                    "MRE" => MaterialSubType.LaminatedReducerMoldings,
                    "MSC" => MaterialSubType.LaminatedStairCasingMoldings,
                    "MSD" => MaterialSubType.LaminatedStepDownMoldings,
                    "MSN" => MaterialSubType.LaminatedStairNosingMoldings,
                    "MTH" => MaterialSubType.LaminatedThresholdMoldings,
                    "MTM" => MaterialSubType.LaminatedTMoldings,
                    "MTS" => MaterialSubType.LaminatedTransitionStripMoldings,
                    "MVE" => MaterialSubType.LaminatedVentMoldings,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Linoleum(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Linoleum,
                SubType = Next3(code) switch
                {
                    "SHE" => MaterialSubType.LinoleumSheet,
                    "TIL" => MaterialSubType.LinoleumTile,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Metal(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Metal,
                SubType = Next3(code) switch
                {
                    "TIL" => MaterialSubType.MetalTile,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification AreaRugs(string code)
            => new MaterialClassification
            {
                Type = MaterialType.AreaRugs,
                SubType = Next3(code) switch
                {
                    "RUG" => MaterialSubType.AreaRugs,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification NaturalStones(string code)
            => new MaterialClassification
            {
                Type = MaterialType.NaturalStones,
                SubType = Next3(code) switch
                {
                    "SLA" => MaterialSubType.NaturalStonesSlabs,
                    "TIL" => MaterialSubType.NaturalStonesTiles,
                    "SGR" => MaterialSubType.NaturalStonesGraniteSlabs,
                    "SLI" => MaterialSubType.NaturalStonesLimestoneSlabs,
                    "SMA" => MaterialSubType.NaturalStonesMarbleSlabs,
                    "SEN" => MaterialSubType.NaturalStonesEngineeredSlabs,
                    "SSL" => MaterialSubType.NaturalStonesSlateSlabs,
                    "MOS" => MaterialSubType.NaturalStonesMosaics,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Education(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Education,
                SubType = Next3(code) switch
                {
                    "LIT" => MaterialSubType.EducationLiterature,
                    "VID" => MaterialSubType.EducationMultiMedia,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Tools(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Tools,
                SubType = Next3(code) switch
                {
                    "INV" => MaterialSubType.InventoryTools,
                    "REN" => MaterialSubType.RentalTools,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Vinyl(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Vinyl,
                SubType = Next3(code) switch
                {
                    "SHE" => MaterialSubType.VinylSheet,
                    "TIL" => MaterialSubType.VinylTile,
                    "TRE" => MaterialSubType.VinylTreads,
                    "NOS" => MaterialSubType.VinylNosing,
                    "STR" => MaterialSubType.VinylStrip,
                    "STG" => MaterialSubType.VinylStringer,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification WallCoverings(string code)
            => new MaterialClassification
            {
                Type = MaterialType.WallCoverings,
                SubType = Next3(code) switch
                {
                    "PAP" => MaterialSubType.Wallpaper,
                    "PAI" => MaterialSubType.Paint,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };

        private static MaterialClassification Wood(string code)
            => new MaterialClassification
            {
                Type = MaterialType.Wood,
                SubType = Next3(code) switch
                {
                    "MOL" => MaterialSubType.WoodMoldings,
                    "ENU" => MaterialSubType.UnfinishedEngineeredWood,
                    "ENP" => MaterialSubType.PrefinishedEngineeredWood,
                    "SOU" => MaterialSubType.UnfinishedSolidWood,
                    "SOP" => MaterialSubType.PrefinishedSolidWood,

                    "MAC" => MaterialSubType.AcousticWoodMoldings,
                    "MBB" => MaterialSubType.BaseboardWoodMoldings,
                    "MEC" => MaterialSubType.EndCapWoodMoldings,
                    "MQR" => MaterialSubType.QuarterRoundWoodMoldings,
                    "MRE" => MaterialSubType.ReducerWoodMoldings,
                    "MSC" => MaterialSubType.StairCasingWoodMoldings,
                    "MSD" => MaterialSubType.StepDownWoodMoldings,
                    "MSN" => MaterialSubType.StairNosingWoodMoldings,
                    "MTH" => MaterialSubType.ThresholdWoodMoldings,
                    "MTM" => MaterialSubType.TWoodMoldings,
                    "MTS" => MaterialSubType.TransitionStripWoodMoldings,
                    "MVE" => MaterialSubType.VentWoodMoldings,

                    "MIS" => MaterialSubType.Miscellaneous,
                    "SAM" => MaterialSubType.Sample,
                    _ => default(MaterialSubType?)
                },
                Application = GetMaterialApplication(code)
            };
    }

    public static class MaterialTypeExtensions
    {
        private static Dictionary<MaterialType, (string Name, MaterialTypeAttribute Attrib)[]> _subTypes;
        public static (string Name, MaterialTypeAttribute Attrib)[] GetSubTypes(this MaterialType type)
        {
            if (_subTypes == null)
            {
                _subTypes = typeof(MaterialSubType)
                    .GetFields()
                    .Where(x => !string.Equals(x.Name, "value__"))
                    .Select(x => (Model: x, Attrib: x.GetCustomAttribute<MaterialTypeAttribute>()))
                    .GroupBy(x => x.Attrib?.Material ?? MaterialType.Unclassified)
                    .ToDictionary(x => x.Key, x => x.Select(y => (y.Model.Name, y.Attrib)).ToArray());
            }

            return _subTypes.TryGetValue(type, out var list) ? list : null;
        }

        private static Dictionary<MaterialType, string> _types;
        public static string GetDescription(this MaterialType? type)
        {
            if (!type.HasValue) return nameof(MaterialType.Unclassified);

            if (_types == null)
            {
                _types = typeof(MaterialType)
                    .GetFields()
                    .Where(x => !string.Equals(x.Name, "value__"))
                    .ToDictionary(x => Enum.Parse<MaterialType>(x.Name), x => x.GetCustomAttribute<DescriptionAttribute>()?.Description ?? x.Name);
            }

            return _types.TryGetValue(type.Value, out var description) ? description : "[ERROR]";
        }

        public static string GetDescription(this MaterialType type) => GetDescription((MaterialType?)type);
        public static string GetDescription(this MaterialSubType subType) => GetDescription((MaterialSubType?)subType);

        public static string GetDescription(this MaterialSubType? subType)
        {
            if (!subType.HasValue) return null;

            var field = typeof(MaterialSubType).GetField(subType.ToString());
            if (field != null)
            {
                return field.GetCustomAttribute<MaterialTypeAttribute>()?.Description ?? subType.ToString();
            }

            return subType.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class MaterialTypeAttribute : Attribute
    {
        public MaterialType Material { get; set; }
        public string Description { get; set; }

        public MaterialTypeAttribute(MaterialType material, string description = null)
        {
            Material = material;
            Description = description;
        }
    }
}