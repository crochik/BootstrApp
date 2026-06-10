using System;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.GeoJSON;

namespace PI.Google.Services;

public class PlacesService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient Client => _httpClientFactory.CreateClient(nameof(PlacesService));
    private readonly GoogleConfiguration _configuration;

    public PlacesService(
        ILogger<PlacesService> logger,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        MongoConnection connection,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration.GetSection("Google").Get<GoogleConfiguration>();
    }

    protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
    {
        MessageBroker.Bind(messageQueue, ActionIds.GetRoute(ActionIds.GooglePlacesSearch));
        mapper.Register<SimpleActionMessage<GenericActionOptions>>();
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            var parts = evt.RoutingKey.Split('.');
            var actionId = Guid.Parse(parts[1]);

            switch (evt.Body)
            {
                case SimpleActionMessage<GenericActionOptions> action:
                {
                    var typedOptions = action.Options.ConvertTo<GooglePlacesSearchActionOptions>();
                    typedOptions.Output = action.Options.Output;
                    await ProcessAsync(action.Event, typedOptions);
                    break;
                }

                case SimpleActionMessage<GooglePlacesSearchActionOptions> action:
                    await ProcessAsync(action.Event, action.Options);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process message {id}", evt.RoutingKey);
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task ProcessAsync(FlowEvent evt, GooglePlacesSearchActionOptions actionOptions)
    {
        var flowRun = await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, evt.AccountId)
            .Eq(x => x.Id, evt.RunId)
            .IncludeFields(
                x => x.AccountId,
                x => x.Id,
                x => x.Objects,
                x => x.ObjectType,
                x => x.InitialEvent,
                x => x.InitialObject
            )
            .FirstOrDefaultAsync();

        IEntityContext accountContext = new AccountContext(evt.AccountId);
        var context = flowRun.BuildHandlebarsContext(evt);

        if (!ExpressionEvaluatorService.TryResolve(accountContext, context, actionOptions.SearchText, out var searchObj) || searchObj is not string searchText)
        {
            Logger.LogError("Couldn't evaluate {SearchText}", actionOptions.SearchText);
            // TODO: fire event error
            // ...
            return;
        }

        var result = await SearchAsync(accountContext, searchText);
        if (!result.IsSuccess)
        {
            Logger.LogError("Couldn't evaluate {SearchText}", actionOptions.SearchText);
            // TODO: fire event error
            // ...
            return;
        }

        var objectTypeName = actionOptions.NewObjectType ?? "google.Place";

        // TODO: check if object exists?
        // ...

        await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, flowRun.AccountId)
            .Eq(x => x.Id, flowRun.Id)
            .Update
            .Set(x => x.Objects[actionOptions.Alias ?? objectTypeName], new ObjectWithType
            {
                ObjectType = objectTypeName,
                Object = JsonObjectConverter.Convert<ExpandoObject>(result.Value),
            })
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();

        var output = actionOptions.Output.FirstOrDefault(x => x.Name == GooglePlacesSearchActionOptions.OnFoundEvent);
        if (output?.EventId.HasValue ?? false)
        {
            var newEvt = new GenericFlowEvent(evt)
            {
                Action = nameof(ActionIds.GooglePlacesSearch),
                Description = output.Description,
                EventTypeId = output.EventId,
            };

            await MessageBroker.DispatchAsync(newEvt);
        }
    }

    /// <summary>
    /// Get place details
    /// if fields is omitted, will return address components and formatted address 
    /// </summary>
    public async Task<Result<PlaceDetailsResponse>> GetPlaceAsync(IEntityContext context, string id, string fields=null)
    {
        var fieldsArg = fields!=null ? $"&fields={fields}" : "&fields=address_components,formatted_address,geometry/location";
        
        var url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={id}{fieldsArg}&key={_configuration.ApiKey}";
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await Client.SendAsync(requestMessage);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return Result.Error<PlaceDetailsResponse>(responseBody);
        var result = JsonConvert.DeserializeObject<PlaceDetailsResponse>(responseBody);

        return string.IsNullOrEmpty(result.ErrorMessage) ? Result.Success(result) : Result.Error<PlaceDetailsResponse>(result.ErrorMessage);
    }
    
    public async Task<Result<AutocompleteResponse>> AutocompleteAsync(IEntityContext context, string address, double? lat = null, double? lon = null, int? radius = null)
    {
        if (!lat.HasValue)
        {
            // TODO: use context to find address to use as center location
            // ...
        }
        
        var encodedInput = HttpUtility.UrlEncode(address);
        var locationArg = lat.HasValue && lon.HasValue ? $"&location={lat},{lon}" : string.Empty;
        var radiusArg = lat.HasValue && lon.HasValue ? $"&radius={radius ?? 10_000}" : string.Empty; 

        var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?input={encodedInput}&types=address&key={_configuration.ApiKey}{locationArg}{radiusArg}"; 
                 
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await Client.SendAsync(requestMessage);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return Result.Error<AutocompleteResponse>(responseBody);
        var result = JsonConvert.DeserializeObject<AutocompleteResponse>(responseBody);

        return string.IsNullOrEmpty(result.ErrorMessage) ? Result.Success(result) : Result.Error<AutocompleteResponse>(result.ErrorMessage);
    }

    public async Task<Result<Place>> SearchAsync(IEntityContext context, string address)
    {
        // TODO: use context to find address to use as center location
        // ...

        Logger.LogInformation("Search: {Address}", address);
        var url = "https://places.googleapis.com/v1/places:searchText";
        var body = new SearchTextRequest
        {
            TextQuery = address,
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.Add("X-Goog-FieldMask", "*");
        requestMessage.Headers.Add("X-Goog-Api-Key", _configuration.ApiKey);

        var json = JsonConvert.SerializeObject(body);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(requestMessage);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode) return Result.Error<Place>(responseBody);

        var result = JsonConvert.DeserializeObject<SearchResponse>(responseBody);
        if (result.Places == null || result.Places?.Length < 1) return Result.Error<Place>("Got success but no result");

        var place = result.Places[0];
        Logger.LogInformation("Found: {Type} {GoogleUrl}", place.PrimaryType, place.GoogleMapsUri);

        return Result.Success(place);
    }
}

public class GoogleConfiguration
{
    public string ApiKey { get; set; }
}

public class SearchTextRequest
{
    [JsonProperty("textQuery")] public string TextQuery { get; set; }
}

public class AddressComponent
{
    [JsonProperty("shortText")] public string Name { get; set; }

    [JsonProperty("longText")] public string Description { get; set; }

    public string[] Types { get; set; }
}

public class Location
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}

public class Place
{
    public string Id { get; set; }

    [JsonProperty("name")] public string MapsName { get; set; }

    public string[] Types { get; set; }

    [JsonProperty("formattedAddress")] public string Description { get; set; }

    public AddressComponent[] AddressComponents { get; set; }

    [JsonProperty("location")] public Location Coordinates { get; set; }

    [BsonElement] public Point Location => Coordinates == null ? null : new Point { Coordinates = new[] { Coordinates.Longitude, Coordinates.Latitude } };

    public string GoogleMapsUri { get; set; }

    public string PrimaryType { get; set; }

    [JsonProperty("shortFormattedAddress")]
    public string Name { get; set; }

    [BsonElement] public string Neighborhood => AddressComponents?.FirstOrDefault(x => x.Types?.Contains("neighborhood") ?? false)?.Name;

    [BsonElement] public string County => AddressComponents?.FirstOrDefault(x => x.Types?.Contains("administrative_area_level_2") ?? false)?.Name;

    [BsonElement] public string State => AddressComponents?.FirstOrDefault(x => x.Types?.Contains("administrative_area_level_1") ?? false)?.Name;

    [BsonElement] public string City => AddressComponents?.FirstOrDefault(x => x.Types?.Contains("locality") ?? false)?.Name;

    [BsonElement] public string Route => AddressComponents?.FirstOrDefault(x => x.Types?.Contains("route") ?? false)?.Name;
}

public class SearchResponse
{
    public Place[] Places { get; set; }
}

public class AutocompleteResponse
{
    public Prediction[] Predictions { get; set; }
    public string Status { get; set; }
    public string ErrorMessage { get; set; }
}

public class Prediction
{
    public string Description { get; set; }

    [JsonProperty("matched_substrings")] public MatchedSubstring[] MatchedSubstrings { get; set; }

    [JsonProperty("place_id")] public string PlaceId { get; set; }

    public string Reference { get; set; }

    [JsonProperty("structured_formatting")]
    public PredictionStructuredFormatting StructuredFormatting { get; set; }

    public Term[] Terms { get; set; }
    public string[] Types { get; set; }
    
    public class MatchedSubstring
    {
        public int Length { get; set; }
        public int Offset { get; set; }
    }

    public class PredictionStructuredFormatting
    {
        [JsonProperty("main_text")] public string MainText { get; set; }

        [JsonProperty("main_text_matched_substrings")]
        public MatchedSubstring[] MainTextMatchedSubstrings { get; set; }

        [JsonProperty("secondary_text")] public string SecondaryText { get; set; }
    }

    public class Term
    {
        public int Offset { get; set; }
        public string Value { get; set; }
    }    
}

public class PlaceDetailsResponse
{
    [JsonProperty("html_attributions")] public object[] HtmlAttributions { get; set; }

    [JsonProperty("result")] public PlaceDetails Result { get; set; }

    [JsonProperty("status")] public string Status { get; set; }

    [JsonProperty("error_message")] public string ErrorMessage { get; set; }
}

public enum PlaceDetailsBasicFields
{
    address_components,
    adr_address,
    business_status,
    formatted_address,
    geometry,
    icon,
    icon_mask_base_uri,
    icon_background_color,
    name, 
    photo,
    place_id,
    plus_code,
    type,
    url,
    utc_offset,
    vicinity,
    wheelchair_accessible_entrance,
}

public class PlaceDetails
{
    [JsonProperty("address_components")] public AddressComponent[] AddressComponents { get; set; }

    [JsonProperty("adr_address")] public string Address { get; set; }

    [JsonProperty("formatted_address")] public string FormattedAddress { get; set; }

    [JsonProperty("formatted_phone_number")]
    public string FormattedPhoneNumber { get; set; }

    [JsonProperty("geometry")] public LocationGeometry Geometry { get; set; }

    [JsonProperty("icon")] public string Icon { get; set; }

    [JsonProperty("icon_background_color")]
    public string IconBackgroundColor { get; set; }

    [JsonProperty("icon_mask_base_uri")] public string IconMaskBaseUri { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("photos")] public Photo[] Photos { get; set; }

    [JsonProperty("place_id")] public string PlaceId { get; set; }

    [JsonProperty("rating")] public double Rating { get; set; }

    [JsonProperty("reference")] public string Reference { get; set; }

    [JsonProperty("reviews")] public Review[] Reviews { get; set; }

    [JsonProperty("types")] public string[] Types { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("utc_offset")] public int UtcOffset { get; set; }

    [JsonProperty("vicinity")] public string Vicinity { get; set; }

    [JsonProperty("website")] public string Website { get; set; }
    
    public class AddressComponent
    {
        [JsonProperty("long_name")] public string LongName { get; set; }

        [JsonProperty("short_name")] public string ShortName { get; set; }

        [JsonProperty("types")] public string[] Types { get; set; }
    }
    
    public class LocationGeometry
    {
        [JsonProperty("location")] public Location Location { get; set; }

        [JsonProperty("viewport")] public Viewport Viewport { get; set; }
    }

    public class Location
    {
        [JsonProperty("lat")] public double Lat { get; set; }

        [JsonProperty("lng")] public double Lng { get; set; }
    }

    public class Viewport
    {
        [JsonProperty("northeast")] public Location Northeast { get; set; }

        [JsonProperty("southwest")] public Location Southwest { get; set; }
    }

    public class Photo
    {
        [JsonProperty("height")] public int Height { get; set; }

        [JsonProperty("html_attributions")] public string[] HtmlAttributions { get; set; }

        [JsonProperty("photo_reference")] public string PhotoReference { get; set; }

        [JsonProperty("width")] public int Width { get; set; }
    }

    public class Review
    {
        [JsonProperty("author_name")] public string AuthorName { get; set; }

        [JsonProperty("author_url")] public string AuthorUrl { get; set; }

        [JsonProperty("language")] public string Language { get; set; }

        [JsonProperty("profile_photo_url")] public string ProfilePhotoUrl { get; set; }

        [JsonProperty("rating")] public int Rating { get; set; }

        [JsonProperty("relative_time_description")]
        public string RelativeTimeDescription { get; set; }

        [JsonProperty("text")] public string Text { get; set; }

        [JsonProperty("time")] public int Time { get; set; }
    }
}




