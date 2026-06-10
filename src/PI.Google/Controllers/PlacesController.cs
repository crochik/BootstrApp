using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.Google.Services;
using PI.Shared.ContractResolvers;
using PI.Shared.Controllers;

namespace PI.Google.Controllers;

[Route("/google/v1/[controller]")]
public class PlacesController : APIController
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new AlwaysUseUnderlyingPropertyNameContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };
    
    private readonly PlacesService _service;

    public PlacesController(PlacesService service)
    {
        _service = service;
    }

    [Authorize("default")]
    [HttpGet("Autocomplete")]
    public async Task<IActionResult> AutocompleteAsync([FromQuery] string search, [FromQuery] string center=null)
    {
        // TODO: parse center 
        // ...
        
        var result = await _service.AutocompleteAsync(Context, search);

        return result.IsSuccess ? Content(JsonConvert.SerializeObject(result.Value, JsonSerializerSettings), "application/json") : BadRequest(result.Status); 
    }

    [Authorize("default")]
    [HttpGet("/google/v1/[controller]({id})")]
    public async Task<IActionResult> GetPlaceAsync([FromRoute] string id)
    {
        // TODO: parse center 
        // ...
        
        var result = await _service.GetPlaceAsync(Context, id);

        return result.IsSuccess ? Content(JsonConvert.SerializeObject(result.Value, JsonSerializerSettings), "application/json") : BadRequest(result.Status); 
    }

    [Authorize("admin")]
    [HttpPost]
    public async Task<IActionResult> GetAsync([FromBody] Request request)
    {
        var result = await _service.SearchAsync(Context, string.Join(',', request.Get()));

        return result.IsSuccess ? Content(JsonConvert.SerializeObject(result.Value, JsonSerializerSettings), "application/json") : BadRequest(result.Status); 
    }
}

public class Request
{
    public string Address { get; set; }
    public string City { get; set; }
    public string PostalCode { get; set; }
    public string State { get; set; }
    public string Country { get; set; }

    public IEnumerable<string> Get()
    {
        if (!string.IsNullOrEmpty(Address)) yield return Address;
        if (!string.IsNullOrEmpty(City)) yield return City;
        if (!string.IsNullOrEmpty(PostalCode)) yield return PostalCode;
        if (!string.IsNullOrEmpty(State)) yield return State;
        if (!string.IsNullOrEmpty(Country)) yield return Country;
    }
}