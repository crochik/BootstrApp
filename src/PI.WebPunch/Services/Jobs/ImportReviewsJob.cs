using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.WebPunch.Services.Jobs;

public class ImportReviewsJob : IRunJob
{
    private readonly ILogger<ImportReviewsJob> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MongoConnection _connection;
    private readonly Configuration _configuration;

    private HttpClient Client => _httpClientFactory.CreateClient("WebPunch");

    public string Name => "WebPunchImportReviews";

    public ImportReviewsJob(
        ILogger<ImportReviewsJob> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        MongoConnection connection
    )
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _connection = connection;
        _configuration = configuration.GetSection("WebPunch").Get<Configuration>();
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Import Reviews");

        var client = new WebPunchClient
        {
            Configuration = _configuration,
            Client = Client,
        };

        var locations = await GetExternalReviewsAsync(context, client);
        await GetFeedbackAsync(context, client, locations);

        return new JobResult
        {
        };
    }

    private async Task GetFeedbackAsync(IEntityContext context, WebPunchClient client, Dictionary<Guid, Guid> locations)
    {
        var page = 1;
        var count = 0;
        do
        {
            var feedback = await client.GetFeedbackAsync(page);
            if (feedback.Feedback.Length < 1) break;

            await saveAsync(feedback);
            count += feedback.Feedback.Length;
            if (count >= feedback.Pagination.Total) break;

            page++;
        } while (true);

        async Task saveAsync(FeedbackResponse feedback)
        {
            await _connection.BulkWriteAsync(feedback.Feedback.Select(map));

            string byName(Feedback f)
            {
                if (string.IsNullOrEmpty(f.Person?.FirstName))
                {
                    return string.IsNullOrEmpty(f.Person?.LastName) ? "" : $" by {f.Person.LastName}";
                }

                return string.IsNullOrEmpty(f.Person?.LastName) ? $" by {f.Person.FirstName}" : $" by {f.Person.FirstName} {f.Person.LastName}";
            }

            string score(Feedback f)
            {
                if (f.Score.HasValue)
                {
                    if (f.Score.Value <= 6) return "\u2639\t ";
                    if (f.Score.Value >= 9) return "\ud83d\ude03\t ";
                    return "\ud83d\ude10 ";
                }

                if (f.FiveStarRating.HasValue) return $"{f.FiveStarRating.Value:n0}\u2605 ";
                return "";
            }

            UpdateOneModel<WebPunchReview> map(Feedback f)
            {
                var feedbackScore = f.Score ?? (f.FiveStarRating.HasValue ? (int)(f.FiveStarRating * 2) : null);
                var rating = f.FiveStarRating ?? (f.Score.HasValue ? (int)Math.Round(f.Score.Value / 2d) : null);
                var role = feedbackScore switch
                {
                    9 or 10 => WebPunchRole.Promoter,
                    7 or 8 => WebPunchRole.Neutral,
                    null => default(WebPunchRole?),
                    _ => WebPunchRole.Detractor
                };

                var update = _connection.Filter<WebPunchReview>()
                    .Eq(x => x.Id, f.Id)
                    .Update
                    .SetOnInsert(x => x.AccountId, context.AccountId)
                    .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                    .SetOnInsert(x => x.IsActive, true)
                    .SetOrUnset(x => x.RespondedOn, f.RespondedAt)
                    .Set(x => x.Provider, "WebPunch")
                    .SetOrUnset(x => x.Score, feedbackScore)
                    .SetOrUnset(x => x.Rating, rating)
                    .SetOrUnset(x => x.Role, role)
                    .Set(x => x.Name, $"{score(f)}{f.SurveyType?.Title} Survey{byName(f)}")
                    .Set(x => x.Description, f.Comments)
                    .Set(x => x.Feedback, f)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .Set(x => x.EntityId, locations.TryGetValue(f.LocationId, out var id) ? id : context.AccountId)
                    ;

                if (!string.IsNullOrEmpty(f.SurveyType?.Title)) update.AddToSet(x => x.Tags, f.SurveyType?.Title);
                
                return update.UpdateOneModel(true);
            }
        }
    }

    private async Task<Dictionary<Guid, Guid>> GetExternalReviewsAsync(IEntityContext context, WebPunchClient client)
    {
        var reviews = await client.GetExternalReviewsAsync(1);

        await saveCompanyAsync();
        var locationDict = await saveLocationsAsync();

        var total = 0;
        var count = 0;
        var page = 1;

        await saveReviews(reviews);

        while (count < total)
        {
            var response = await client.GetExternalReviewsAsync(page);
            if (response.Reviews.Length < 1) break;

            await saveReviews(response);
        }

        return locationDict;

        async Task saveReviews(ExternalReviewsResponse response)
        {
            await _connection.BulkWriteAsync(response.Reviews.Select(map));

            total = response.Pagination.Total;
            count += response.Reviews.Length;
            page++;

            string byName(ExternalReview r)
            {
                return string.IsNullOrEmpty(r.Author) ? "" : $" by {r.Author}";
            }

            string rating(ExternalReview r)
            {
                if (r.Rating.HasValue) return $"{r.Rating.Value:n0}\u2605 ";
                if (r.Recommended.HasValue) return r.Recommended.Value ? "\ud83d\udc4d " : "\ud83d\udc4e ";
                return "";
            }

            UpdateOneModel<WebPunchReview> map(ExternalReview review)
            {
                var score = review.Rating.HasValue ? (int)(review.Rating * 2) : (review.Recommended.HasValue ? (review.Recommended.Value ? 10 : 0) : default(int?));
                var reviewRating = review.Rating ?? (review.Recommended.HasValue ? (review.Recommended.Value ? 5 : 0) : null);
                var role = score switch
                {
                    9 or 10 => WebPunchRole.Promoter,
                    7 or 8 => WebPunchRole.Neutral,
                    null => default(WebPunchRole?),
                    _ => WebPunchRole.Detractor
                };

                return _connection.Filter<WebPunchReview>()
                    .Eq(x => x.Id, review.Id)
                    .Update
                    .SetOnInsert(x => x.AccountId, context.AccountId)
                    .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                    .SetOnInsert(x => x.RespondedOn, review.Date)
                    .SetOnInsert(x => x.IsActive, true)
                    .Set(x => x.Provider, review.Site)
                    .SetOrUnset(x => x.Score, score)
                    .SetOrUnset(x => x.Rating, reviewRating)
                    .SetOrUnset(x => x.Role, role)
                    .Set(x => x.Name, $"{rating(review)}{review.Site} Review{byName(review)}")
                    .Set(x => x.Description, review.Text)
                    .Set(x => x.Feedback, review)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .Set(x => x.EntityId, locationDict.TryGetValue(review.LocationId, out var id) ? id : context.AccountId)
                    .UpdateOneModel(true);
            }
        }

        async Task saveCompanyAsync()
        {
            await _connection.Filter<WebPunchLocation>()
                .Eq(x => x.Id, reviews.Company.Id)
                .Update
                .SetOnInsert(x => x.AccountId, context.AccountId)
                .SetOnInsert(x => x.EntityId, context.AccountId)
                .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                .Set(x => x.Name, reviews.Company.Name)
                .Set(x => x.Location, reviews.Company)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateAndGetOneAsync(true);
        }

        async Task<Dictionary<Guid, Guid>> saveLocationsAsync()
        {
            var orgs = await _connection.Filter<Shared.Models.Entity, Organization>()
                .Eq(x => x.AccountId, context.AccountId)
                .Ne(x => x.IsActive, false)
                .FindAsync();

            var dict = new Dictionary<string, Guid>();
            foreach (var org in orgs)
            {
                var identity = org.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.InspireNet));
                if (identity == null) continue;
                dict[identity.ExternalId] = org.Id;
            }

            await _connection.BulkWriteAsync(reviews.Locations.Select(location =>
                _connection.Filter<WebPunchLocation>()
                    .Eq(x => x.Id, location.Id)
                    .Update
                    .SetOnInsert(x => x.AccountId, context.AccountId)
                    .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                    .Set(x => x.Name, location.Name)
                    .Set(x => x.Location, location)
                    .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                    .Set(x => x.LastActor, context.Actor())
                    .Set(x => x.EntityId, dict.TryGetValue(location.FranchiseNumber ?? "", out var id) ? id : context.AccountId)
                    .UpdateOneModel(true)
            ));

            var locationDict = new Dictionary<Guid, Guid>();
            foreach (var location in reviews.Locations)
            {
                if (!dict.TryGetValue(location.FranchiseNumber ?? "", out var entityId)) continue;
                locationDict[location.Id] = entityId;
            }

            return locationDict;
        }
    }
}

public class Configuration
{
    public string UserName { get; set; }
    public string Password { get; set; }
}

public class ExternalReviewsResponse
{
    public ExternalReview[] Reviews { get; set; }
    public Company Company { get; set; }
    public Location[] Locations { get; set; }
    public Pagination Pagination { get; set; }
}

public class FeedbackResponse
{
    public Feedback[] Feedback { get; set; }
    public Pagination Pagination { get; set; }
}

public class WebPunchClient
{
    public Configuration Configuration { get; init; }
    public HttpClient Client { get; init; }

    public string GetUrl(string path, int page = 1) => $"https://app.webpunch12.com/api/{path}?page={page}&per=300";

    public async Task<ExternalReviewsResponse> GetExternalReviewsAsync(int page = 1)
    {
        Client.SetBasicAuthentication(Configuration.UserName, Configuration.Password);
        return await Client.GetAsync<ExternalReviewsResponse>(GetUrl("v1/external_reviews", page));
    }

    public async Task<FeedbackResponse> GetFeedbackAsync(int page = 1)
    {
        Client.SetBasicAuthentication(Configuration.UserName, Configuration.Password);
        return await Client.GetAsync<FeedbackResponse>(GetUrl("v2/feedback", page));
    }
}

[BsonCollection("webPunch.Location")]
public class WebPunchLocation : EntityOwnedModel
{
    public Entity Location { get; set; }
}

[BsonDiscriminator]
[BsonKnownTypes(typeof(Company), typeof(Location))]
public class Entity
{
    [JsonProperty("uuid")] public Guid Id { get; set; }

    public string Name { get; set; }
}

public class Company : Entity
{
}

public class Location : Entity
{
    // "address": "1 N Moore St",
    public string Address { get; set; }

    // "city": "Tucson",
    public string City { get; set; }

    // "province": "Colorado",
    [JsonProperty("province")] public string State { get; set; }

    // "zip": "10013",
    [JsonProperty("zip")] public string PostalCode { get; set; }

    // "country": "United States",
    public string Country { get; set; }

    // "franchise_number": null,
    [JsonProperty("franchise_number")] public string FranchiseNumber { get; set; }

    // "business_owner_name": "Elnora Fadel PhD",
    [JsonProperty("business_owner_name")] public string BusinessOwnerName { get; set; }

    // "aggregate": {
    //     "count": 2,
    //     "mean_five_star_rating": 4.5
    // }
    [JsonProperty("aggregate")] public AggregateRating AggregateRating { get; set; }
}

public class AggregateRating
{
    public int Count { get; set; }

    [JsonProperty("mean_five_star_rating")]
    public decimal Mean5StarRating { get; set; }
}

public enum WebPunchRole
{
    Neutral,
    Promoter,
    Detractor,
}

[BsonCollection("webPunch.Feedback")]
public class WebPunchReview : EntityOwnedModel, ITaggable
{
    public DateTime? RespondedOn { get; set; }
    public string Provider { get; set; }

    /// <summary>
    /// Score (0-10?)
    /// </summary>
    public int? Score { get; set; }

    /// <summary>
    /// 5star rating
    /// </summary>
    public decimal? Rating { get; set; }

    public WebPunchFeedback Feedback { get; set; }

    public Dictionary<string, string> Refs { get; set; }

    public WebPunchRole? Role { get; set; }
    
    public string[] Tags { get; set; }
    
    public bool IsActive { get; set; }
}

[BsonDiscriminator]
[BsonKnownTypes(typeof(ExternalReview), typeof(Feedback))]
public class WebPunchFeedback
{
    // UUID of the review.
    // "uuid": "8635b095-1f3d-4a85-9d50-e106a68e9d8c",
    [JsonProperty("uuid")] public Guid Id { get; set; }
}

[BsonDiscriminator("external")]
public class ExternalReview : WebPunchFeedback
{
    // Title of the review. If review site does not have the title, then
    // this will be the beginning of review text.
    // "title": "2 Lorem ipsum dolor sit...",
    public string Title { get; set; }

    // Text of the review.
    // "text": "2 Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
    public string Text { get; set; }

    // Date when review was posted.
    // "date": "2019-01-15",
    public DateTime Date { get; set; }

    // URL of the page where the review is posted.
    // "url": "http://example.com/1",
    public string Url { get; set; }

    // Rating from 0 to 5 if applicable. For example, this might be null
    // for Facebook reviews.
    // "rating": 4.0,
    public decimal? Rating { get; set; }

    // Recommendation (true or false) if applicable. For example, this will
    // not be null for Facebook reviews.
    // "recommended": null,
    public bool? Recommended { get; set; }

    // Review author name (as shown on the review site)
    // "author": "author2",
    public string Author { get; set; }

    // Which review site this review is posted on.
    // "site": "FakeReviewSite1",
    public string Site { get; set; }

    // UUID of a location for which the review is posted.
    // "location_uuid": "7d817391-9dd3-47c5-9cf9-a0602277eeb1"
    [JsonProperty("location_uuid")] public Guid LocationId { get; set; }
}

[BsonDiscriminator("feedback")]
public class Feedback : WebPunchFeedback
{
    // "responded_at": "2024-02-16T19:45:48Z",
    [JsonProperty("responded_at")] public DateTime? RespondedAt { get; set; }

    // "comments": null,
    public string Comments { get; set; }

    // "score": 10,
    public int? Score { get; set; }

    // "five_star_rating": 5.0,
    public decimal? FiveStarRating { get; set; }

    // "location_uuid": "42b19780-a230-4b19-8ce5-b54cd85bbfc8",
    [JsonProperty("location_uuid")] public Guid LocationId { get; set; }

    // "name_usage_approval": false,
    [JsonProperty("name_usage_approval")] public bool? NameUsageApproval { get; set; }

    // "resolution_status": "open",
    [JsonProperty("resolution_status")] public string ResolutionStatus { get; set; }

    // "resolved_in": null,
    [JsonProperty("resolved_in")] public object ResolvedIn { get; set; }

    // "loop_closed_by": null,
    [JsonProperty("loop_closed_by")] public string LoopClosedBy { get; set; }

    // "in_progress_at": null,
    [JsonProperty("in_progress_at")] public DateTime? InProgressAt { get; set; }

    // "closed_at": null,
    [JsonProperty("closed_at")] public DateTime? ClosedAt { get; set; }

    // "resolution_deadline_at": null,
    [JsonProperty("resolution_deadline_at")]
    public DateTime? ResolutionDeadlineAt { get; set; }

    // "job_number": "08pUJ000000HlybYAC"
    [JsonProperty("job_number")] public string JobNumber { get; set; }

    [JsonProperty("survey_type")] public SurveyType SurveyType { get; set; }

    public Person Person { get; set; }

    public Sale Sale { get; set; }
}

public class Sale
{
    //     "importer_uid": "00QUJ000004GwfF2AS"
    [JsonProperty("importer_uid")] public string ImporterUid { get; set; }
}

public class Person
{
    //     "first_name": "Jingwei",
    [JsonProperty("first_name")] public string FirstName { get; set; }

    //     "last_name": "Li",
    [JsonProperty("last_name")] public string LastName { get; set; }

    //     "city": "Columbia",
    [JsonProperty("city")] public string City { get; set; }

    //     "province": "SC",
    [JsonProperty("province")] public string State { get; set; }

    //     "phone_number": "8033546878",
    [JsonProperty("phone_number")] public string PhoneNumber { get; set; }

    //     "cell_phone_number": null,
    [JsonProperty("cell_phone_number")] public string CellPhoneNumber { get; set; }

    //     "email": "jingwei.li@live.com",
    [JsonProperty("email")] public string Email { get; set; }

    //     "importer_uid": "00QUJ000004GwfF2AS"
    [JsonProperty("importer_uid")] public string ImporterUid { get; set; }
}

public class SurveyType
{
    public string Title { get; set; }
    public string Slug { get; set; }
}

public class Pagination
{
    // "page_number": 1,
    [JsonProperty("page_number")] public int PageNumber { get; set; }

    // "total_available": 2,
    [JsonProperty("total_available")] public int Total { get; set; }

    // "results_per_page": 100
    [JsonProperty("results_per_page")] public int ResultsPerPage { get; set; }
}