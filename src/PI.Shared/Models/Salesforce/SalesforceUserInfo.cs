using System;
using Newtonsoft.Json;

namespace PI.Shared.Salesforce.Models;

public class SalesforceUserInfo
{
    // "sub": "https://login.salesforce.com/id/00D41000002kXaPEAU/0053o000009iDJBAA2",
    public string Sub { get; set; }

    // "user_id": "0053o000009iDJBAA2",
    [JsonProperty("user_id")] public string UserId { get; set; }

    // "organization_id": "00D41000002kXaPEAU",
    [JsonProperty("organization_id")] public string OrganizationId { get; set; }

    // "preferred_username": "felipe@fci.com",
    [JsonProperty("preferred_username")] public string PreferredUserName { get; set; }

    //"nickname": "User16660351087143708553",
    public string Nickname { get; set; }

    //"name": "Felipe Manager",
    public string Name { get; set; }

    //"email": "felipe@crochik.com",
    public string Email { get; set; }

    // "email_verified": true,
    [JsonProperty("email_verified")] public bool EmailVerified { get; set; }

    // "given_name": "Felipe",
    [JsonProperty("given_name")] public string GivenName { get; set; }

    // "family_name": "Manager",
    [JsonProperty("family_name")] public string FamilyName { get; set; }

    // "zoneinfo": "America/New_York",
    public string Zoneinfo { get; set; }
    public string Profile { get; set; }
    public string Picture { get; set; }

    // Photos
    // ... 

    // address
    // ...

    // urls 
    public UserInfoUrls Urls { get; set; }

    // "active": true,
    public bool Active { get; set; }

    // "user_type": "STANDARD",
    [JsonProperty("user_type")] public string UserType { get; set; }

    // "language": "en_US",
    public string Language { get; set; }

    // "locale": "en_US",
    public string Locale { get; set; }

    // "utcOffset": -18000000,
    // ...

    public class UserInfoUrls
    {
        // "enterprise": "https://fcifloors.my.salesforce.com/services/Soap/c/{version}/00D41000002kXaP",
        public Uri Enterprise { get; set; }

        // "metadata": "https://fcifloors.my.salesforce.com/services/Soap/m/{version}/00D41000002kXaP",
        public Uri Metadata { get; set; }

        // "partner": "https://fcifloors.my.salesforce.com/services/Soap/u/{version}/00D41000002kXaP",
        public Uri Partner { get; set; }

        // "rest": "https://fcifloors.my.salesforce.com/services/data/v{version}/",
        public Uri Rest { get; set; }

        // "sobjects": "https://fcifloors.my.salesforce.com/services/data/v{version}/sobjects/",
        [JsonProperty("sobjects")] public Uri SObjects { get; set; }

        // "search": "https://fcifloors.my.salesforce.com/services/data/v{version}/search/",
        public Uri Search { get; set; }

        // "query": "https://fcifloors.my.salesforce.com/services/data/v{version}/query/",
        public Uri Query { get; set; }

        // "recent": "https://fcifloors.my.salesforce.com/services/data/v{version}/recent/",
        public Uri Recent { get; set; }

        // "tooling_soap": "https://fcifloors.my.salesforce.com/services/Soap/T/{version}/00D41000002kXaP",
        [JsonProperty("tooling_soap")] public Uri ToolingSoap { get; set; }

        // "tooling_rest": "https://fcifloors.my.salesforce.com/services/data/v{version}/tooling/",
        [JsonProperty("tooling_rest")] public Uri ToolingRest { get; set; }

        // "profile": "https://fcifloors.my.salesforce.com/0053o000009iDJBAA2",
        public Uri Profile { get; set; }

        // "feeds": "https://fcifloors.my.salesforce.com/services/data/v{version}/chatter/feeds",
        public Uri Feeds { get; set; }

        // "groups": "https://fcifloors.my.salesforce.com/services/data/v{version}/chatter/groups",
        public Uri Groups { get; set; }

        // "users": "https://fcifloors.my.salesforce.com/services/data/v{version}/chatter/users",
        public Uri Users { get; set; }

        // "feed_items": "https://fcifloors.my.salesforce.com/services/data/v{version}/chatter/feed-items",
        [JsonProperty("feed_items")] public Uri FeedItems { get; set; }

        // "feed_elements": "https://fcifloors.my.salesforce.com/services/data/v{version}/chatter/feed-elements",
        [JsonProperty("feed_elements")] public Uri FeedElements { get; set; }

        // "custom_domain": "https://fcifloors.my.salesforce.com"
        [JsonProperty("custom_domain")] public Uri CustomDomain { get; set; }
    }
}