using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Google.Services;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models.GeoJSON;

namespace Controllers.App.Api;

[Authorize("rest")]
[Route("/google/api/[controller]")]
public class AppPlacesController : APIController
{
    private readonly PlacesService _service;

    public AppPlacesController(PlacesService service)
    {
        _service = service;
    }

    [HttpGet("Autocomplete")]
    public async Task<AutocompleteResponse> AutocompleteAsync([FromQuery] string search, [FromQuery] string center = null)
    {
        double? lat = null;
        double? lon = null;
        if (!string.IsNullOrEmpty(center))
        {
            var parts = center.Split(',');
            if (parts.Length == 2)
            {
                if (double.TryParse(parts[0], out var coord0) && double.TryParse(parts[1], out var coord1))
                {
                    lon = coord0;
                    lat = coord1;
                }
            }
        }

        var result = await _service.AutocompleteAsync(Context, search, lat: lat, lon: lon);

        if (result.IsError) throw new BadRequestException(result.Status);

        return new AutocompleteResponse
        {
            Predictions = result.Value.Predictions?.Select(x => new PredictionResponse
            {
                Description = x.Description,
                MainText = x.StructuredFormatting?.MainText,
                SecondaryText = x.StructuredFormatting?.SecondaryText,
                PlaceId = x.PlaceId,
            }).ToArray(),
        };
    }

    [HttpGet("/google/api/[controller]({id})")]
    public async Task<PlacesResponse> GetPlaceAsync([FromRoute] string id)
    {
        // TODO: parse center 
        // ...

        var result = await _service.GetPlaceAsync(Context, id);
        if (result.IsError) throw new BadRequestException(result.Status);

        var resp = new PlacesResponse
        {
            FormattedAddress = result.Value.Result.FormattedAddress,
            Components = new Dictionary<string, string>(),
            Coordinates = result.Value.Result.Geometry?.Location != null
                ? new Point
                {
                    Coordinates =
                    [
                        (decimal)result.Value.Result.Geometry.Location.Lng,
                        (decimal)result.Value.Result.Geometry.Location.Lat
                    ]
                }
                : null,
        };

        foreach (var comp in result.Value.Result.AddressComponents)
        {
            var type = comp.Types.First(x => x != "political");
            var name = comp.LongName ?? comp.ShortName;
            resp.Components[type] = name;
            switch (type)
            {
                case "street_number": resp.StreetNumber = name; break;
                case "route": resp.Street = name; break;
                case "neighborhood": resp.Neighborhood = name; break;
                case "locality": resp.City = name; break;
                case "administrative_area_level_2": resp.County = name; break;
                case "administrative_area_level_1": resp.State = name; break;
                case "country": resp.Country = name; break;
                case "postal_code": resp.PostalCode = name; break;
                case "postal_code_suffix": resp.PostalCodeSuffix = name; break;
            }
        }

        return resp;
    }

    public class AutocompleteResponse
    {
        public PredictionResponse[] Predictions { get; set; }
    }

    public class PredictionResponse
    {
        public string Description { get; set; }
        public string PlaceId { get; set; }
        public string MainText { get; set; }
        public string SecondaryText { get; set; }
    }

    public class PlacesResponse
    {
        public string FormattedAddress { get; set; }

        public string StreetNumber { get; set; }

        // route
        public string Street { get; set; }
        public string Neighborhood { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string County { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string PostalCodeSuffix { get; set; }
        public Point Coordinates { get; set; }
        public Dictionary<string, string> Components { get; set; }
    }
}