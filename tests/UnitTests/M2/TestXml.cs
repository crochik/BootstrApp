using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using PI.ProductCatalog.Models.MeasureSquare;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.M2;

public class TestXml
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TestXml(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task Test1()
    {
        var model = new M2Script
        {
            Direction = Direction.Auto,
            CutMargin = "0'3\"",
            MaxTSeamCount = 1,
            GroutWidth = "0.25\"",
            Items =
            [
                new FloorProduct
                {
                    Type = FloorProductType.Carpet,
                    ID = "carpet",
                    Width = "12'0\"",
                    Length = "150'0\"",
                    HoriRepeat = "0'0\"",
                    VertRepeat = "0'0\"",
                    HoriDrop = "0'0\"",
                    VertDrop = "0'0\"",
                },
                new RectRoom
                {
                    RoomName = "room 1",
                    Width = "13'0\"",
                    Length = "15'0\"",
                },
                new Stairway
                {
                    CoveringStyle = CoveringStyle.Waterfall,
                    StairName = "stair 1",
                    Units =
                    [
                        new StairUnit
                        {
                            UnitStyle = UnitStyle.Regular,
                            StepCount = 0,
                            StairWidth = "3'0\"",
                            TreadWidth = "0'11\"",
                            RiseHeight = "0'7\"",
                        },
                        new StairUnit
                        {
                            UnitStyle = UnitStyle.RightAngleTurnArc,
                        },
                        new StairUnit
                        {
                            UnitStyle = UnitStyle.Regular,
                            StepCount = 0,
                            StairWidth = "3'0\"",
                            TreadWidth = "0'11\"",
                            RiseHeight = "0'7\"",
                        }
                    ],
                },
                new FloorProduct
                {
                    Type = FloorProductType.Tile,
                    ID = "tile",
                    Width = "2'0\"",
                    Length = "2'0\"",
                    TileCalcMethod = TileCalcMethod.WasteAddon,
                    WasteAddon = "0%",
                },
                new RectRoom
                {
                    RoomName = "room 2",
                    Width = "15'0\"",
                    Length = "18'0\"",
                },
                new PolygonRoom
                {
                    RoomName = "room 3",
                    Points = "0,2438.40|2438.40,2438.40|2438.40,6096.00|6096.00,6096.00|6096.00,8534.40|0,8534.40"
                },
            ]
        };

        var xml = model.ToXml();
//
//         xml = @"
// <M2Script Direction=""Auto"" CutMargin=""0'3&quot;""><FloorProduct Type=""Carpet"" ID=""carpet"" Width=""12'0&quot;"" Length=""150'0&quot;"" HoriRepeat=""0'0&quot;"" VertRepeat=""0'0&quot;"" HoriDrop=""0'0&quot;"" VertDrop=""0'0&quot;"" /><RectRoom RoomName=""room 1"" Width=""10'0&quot;"" Length=""20'0&quot;""/></M2Script>
// ";

        _testOutputHelper.WriteLine(xml);

        var client = new HttpClient();
        client.BaseAddress = new Uri("https://calculator.measuresquare.com/");
        // client.BaseAddress = new Uri("https://calculatorapi.measuresquare.com/");

        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", "Rmxvb3IgQ292ZXJpbmdzIEludGVybmF0aW9uYWw6UGFzc3dvcmQ=");
        var response = await client.PostAsJsonAsync("public/calculator", new Request
        {
            MeasureSystem = MeasurementSystem.Imperial,
            ImageWidth = 1024,
            ModelScript = xml,
        }, options);

        var body = await response.Content.ReadAsStringAsync();
        _testOutputHelper.WriteLine($"STATUS: {response.StatusCode}");

        if (response.IsSuccessStatusCode)
        {
            var result = JsonConvert.DeserializeObject<Response>(body);
            _testOutputHelper.WriteLine(JsonConvert.SerializeObject(result.ProductEstimates, Formatting.Indented));

            var imageBytes = Convert.FromBase64String(result.ImageBase64String);
            await File.WriteAllBytesAsync("imperial.png", imageBytes);
        }

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}