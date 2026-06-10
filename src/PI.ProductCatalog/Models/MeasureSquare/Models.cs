using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace PI.ProductCatalog.Models.MeasureSquare;

public enum MeasurementSystem
{
    Imperial,
    Metric
}

public class Request
{
    /// <summary>
    /// The measure system of calculator, value: Imperial | Metric
    /// </summary>
    [JsonProperty("measureSystem")]
    public MeasurementSystem MeasureSystem { get; set; }

    /// <summary>
    /// The pixel of generated image width, value: 512 ~ 2048, default: 1024
    /// </summary>
    [JsonProperty("imageWidth")]
    public int ImageWidth { get; set; }

    /// <summary>
    /// The xml format of estimate models, include floor product & rooms information
    /// </summary>
    [JsonProperty("modelScript")]
    public string ModelScript { get; set; }
}

public class ProductEstimate
{
    /// <summary>
    /// "ID": "tile",
    /// </summary>
    public string ID { get; set; }

    /// <summary>
    /// "ShapeQty":"285.09",
    /// </summary>
    public string ShapeQty { get; set; }

    /// <summary>
    /// "Usage":"366.30",
    /// </summary>
    public string Usage { get; set; }

    /// <summary>
    /// "TileCount":"129"
    /// </summary>
    public string TileCount { get; set; }
}

/*
{
       "ImageBase64String": "...."
       "MeasureSystem": "Imperial",
       "Layer": {
           "Rooms": [
               {
                   "Name": "room 1",
                   "Type": "Rectangle",
                   "Area": "195.00 SF",
                   "Perimeter": "56\u00270\"",
                   "FinishedEdge": ""
               },
                ...
           ],
           "Stairs": [
               {
                   "Name": "stair 1",
                   "Width": "3\u00270\"",
                   "Riser": "7\"",
                   "Tread": "11\"",
                   "Steps": 7
               }
           ]
       },
       "ProductEstimates": [
           {
               "ID": "carpet",
               "Type": "CarpetRoll",
               "ShapeQty": "30.81",
               "Usage": "33.67",
               "TotalSalesAmount": 0,
               "TileCount": null,
               "BoxCount": 0,
               "BoxedUsage": "33.67"
           },
            ...
       ]
   } */
/// <summary>
/// 
/// </summary>
public class Response
{
    /// <summary>
    /// The measure system of calculator, value: Imperial | Metric
    /// </summary>
    public MeasurementSystem MeasureSystem { get; set; }

    public string ImageBase64String { get; set; }
    public ProductEstimate[] ProductEstimates { get; set; }

    [JsonIgnore] public byte[] ImageBytes => Convert.FromBase64String(ImageBase64String);
}

public enum Direction
{
    [XmlEnum("Auto")] Auto,
    [XmlEnum("Horizontal")] Horizontal,
    [XmlEnum("Vertical")] Vertical
}

[XmlRoot("M2Script")]
public class M2Script
{
    // Auto | Horizontal | Vertical
    [XmlAttribute] public Direction Direction { get; set; } = Direction.Auto;

    /// <summary>
    /// roll goods
    /// * CutMargin default is 3"
    /// </summary>
    [XmlAttribute]
    public string CutMargin { get; set; } = "3\"";

    /// <summary>
    /// roll goods
    /// MaxTSeamCount default is 2
    /// </summary>
    [XmlAttribute]
    public int MaxTSeamCount { get; set; } = 1;

    /// <summary>
    /// tile products
    /// default is 0.25"
    /// </summary>
    [XmlAttribute]
    public string GroutWidth { get; set; } = "0.25\"";

    // This allows multiple different tags to exist in the same list in order
    [XmlElement("FloorProduct", typeof(FloorProduct))]
    [XmlElement("RectRoom", typeof(RectRoom))]
    [XmlElement("PolygonRoom", typeof(PolygonRoom))]
    [XmlElement("Stairway", typeof(Stairway))]
    public List<object> Items { get; set; } = [];

    public string ToXml()
    {
        var serializer = new XmlSerializer(typeof(M2Script));
        using var stringWriter = new StringWriter();

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true
        };
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);

        var ns = new XmlSerializerNamespaces([XmlQualifiedName.Empty]);
        serializer.Serialize(xmlWriter, this, ns);
        var xml = stringWriter.ToString();

        // may need to replace encoded " 
        // xml = xml.Replace("&quot;", "\"");

        return xml;
    }
}

public enum FloorProductType
{
    [XmlEnum("Carpet")] Carpet,
    [XmlEnum("Tile")] Tile,
    [XmlEnum("Hardwood")] Hardwood,
    [XmlEnum("Laminate")] Laminate,
    [XmlEnum("Vinyl")] Vinyl,
    [XmlEnum("CarpetTile")] CarpetTile,
    [XmlEnum("VinylTile")] VinylTile
}

public enum TileCalcMethod
{
    [XmlEnum("WasteAddon")] WasteAddon,
    [XmlEnum("HalfReuse")] HalfReuse,
    [XmlEnum("CutAndFit")] CutAndFit,
}

public enum LayoutDirection
{
    [XmlEnum("0")]
    DEGREES_0,
    [XmlEnum("45")]
    DEGREES_45,
    [XmlEnum("90")]
    DEGREES_90,
    [XmlEnum("135")]
    DEGREES_135
}

public enum StartPoint
{
    [XmlEnum("135")]
    Center,
    [XmlEnum("Top-Left")]
    TopLeft,
    [XmlEnum("Top-Right")]
    TopRight,
    [XmlEnum("Bottom-Left")]
    BottomLeft,
    [XmlEnum("Bottom-Right")]
    BottomRight
}

public class FloorProduct
{
    /// <summary>
    /// Carpet | Tile | Hardwood | Laminate | Vinyl | CarpetTile | VinylTile
    /// </summary>
    [XmlAttribute]
    public FloorProductType Type { get; set; }

    [XmlAttribute] public string ID { get; set; }
    [XmlAttribute] public string Width { get; set; }
    [XmlAttribute] public string Length { get; set; }

    // -----  Carpet | Vinyl
    /// <summary>
    /// default is 0'0"
    /// </summary>
    [XmlAttribute]
    public string HoriRepeat { get; set; } = "0'0\"";

    /// <summary>
    /// default is 0'0"
    /// </summary>
    [XmlAttribute]
    public string VertRepeat { get; set; } = "0'0\"";

    /// <summary>
    /// default is 0'0"
    /// </summary>
    [XmlAttribute]
    public string HoriDrop { get; set; } = "0'0\"";

    /// <summary>
    /// default is 0'0"
    /// </summary>
    [XmlAttribute]
    public string VertDrop { get; set; } = "0'0\"";

    // ----- Tile | VinylTile | CarpetTile | Hardwood | Laminate:
    /// <summary>
    /// WasteAddon default is 0%
    /// </summary>
    [XmlAttribute]
    public string WasteAddon { get; set; } = "0%";

    // ------ Tile | VinylTile | CarpetTile:
    /// <summary>
    /// WasteAddon | HalfReuse | CutAndFit, default is WasteAddon
    /// </summary>
    [XmlAttribute]
    public TileCalcMethod TileCalcMethod { get; set; } = TileCalcMethod.WasteAddon;

    /// <summary>
    /// * GroutWidth default is 0.25"
    /// </summary>
    [XmlAttribute]
    public string GroutWidth { get; set; } = "0.25\"";

    /// <summary>
    /// LayoutDirection 0 | 45 | 90 | 135, default is 0
    /// </summary>
    [XmlAttribute]
    public LayoutDirection LayoutDirection { get; set; } = LayoutDirection.DEGREES_0;

    /// <summary>
    /// StartPoint Center | Top-Left | Top-Right | Bottom-Left | Bottom-Right, default is Center
    /// </summary>
    [XmlAttribute]
    public StartPoint StartPoint { get; set; } = StartPoint.Center;
}

public class RectRoom
{
    [XmlAttribute] public string RoomName { get; set; }

    /// <summary>
    /// 13'0"
    /// </summary>
    [XmlAttribute]
    public string Width { get; set; }

    /// <summary>
    /// 13'0"
    /// </summary>
    [XmlAttribute]
    public string Length { get; set; }
}

public class PolygonRoom
{
    [XmlAttribute("RoomName")] // Note the lowercase 'n' to match your XML
    public string RoomName { get; set; }

    /// <summary>
    /// Points= "0,2438.40|2438.40,2438.40|2438.40,6096.00|6096.00,6096.00|6096.00,8534.40|0,8534.40"
    /// Anticlockwise, each point split by |, can display room in any shape(include L-shape)
    /// 1(ft) = 304.8, 1(in) = 25.4
    /// </summary>
    [XmlAttribute]
    public string Points { get; set; }
}

public enum CoveringStyle
{
    [XmlEnum("Waterfall")]
    Waterfall,
    [XmlEnum("TreadAndRise")]
    TreadAndRise,
    [XmlEnum("TreadOnly")]
    TreadOnly,
    [XmlEnum("RiseOnly")]
    RiseOnly,
    [XmlEnum("Fullwrap")]
    Fullwrap
}
public class Stairway
{
    /// <summary>
    /// Waterfall | TreadAndRise | TreadOnly | RiseOnly | Fullwrap
    /// </summary>
    [XmlAttribute]
    public CoveringStyle CoveringStyle { get; set; } 

    [XmlAttribute] public string StairName { get; set; }

    [XmlElement("StairUnit")] public List<StairUnit> Units { get; set; } = new List<StairUnit>();
}

public enum UnitStyle
{
    [XmlEnum("Regular")]
    Regular,
    [XmlEnum("RightAngleLanding")]
    RightAngleLanding,
    [XmlEnum("StraightLanding")]
    StraightLanding,
    [XmlEnum("RightAngleTurn")]
    RightAngleTurn,
    [XmlEnum("RightAngleLandingArc")]
    RightAngleLandingArc,
    [XmlEnum("StraightLandingArc")]
    StraightLandingArc,
    [XmlEnum("RightAngleTurnArc")]
    RightAngleTurnArc
    
}
public class StairUnit
{
    /// <summary>
    /// Regular | RightAngleLanding | StraightLanding | RightAngleTurn |
    /// RightAngleLandingArc | StraightLandingArc | RightAngleTurnArc
    /// </summary>
    [XmlAttribute]
    public UnitStyle UnitStyle { get; set; }

    [XmlAttribute] public int StepCount { get; set; }
    [XmlAttribute] public string StairWidth { get; set; }
    [XmlAttribute] public string TreadWidth { get; set; }
    [XmlAttribute] public string RiseHeight { get; set; }

    // Use ShouldSerialize to prevent empty values from cluttering the XML
    public bool ShouldSerializeStepCount() => StepCount > 0;
}