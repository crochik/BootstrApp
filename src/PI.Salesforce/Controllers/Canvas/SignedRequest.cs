using System.Collections.Generic;
using Newtonsoft.Json;

namespace PI.Shared.Salesforce.Models.Canvas;

public class SignedRequest
{
    public string Algorithm { get; set; }
    public int IssuedAt { get; set; }
    public string UserId { get; set; }
    public Client Client { get; set; }
    public Context Context { get; set; }
}

public class Client
{
    [JsonProperty("instanceId")]
    public string InstanceId { get; set; }
    [JsonProperty("targetOrigin")]
    public string TargetOrigin { get; set; }
    [JsonProperty("instanceUrl")]
    public string InstanceUrl { get; set; }
    [JsonProperty("oauthToken")]
    public string OauthToken { get; set; }
}

public class Context
{
    public User User { get; set; }
    public Links Links { get; set; }
    public Application Application { get; set; }
    public Organization Organization { get; set; }
    public Environment Environment { get; set; }
}

public class User
{
    public string Language { get; set; }
    public string ProfilePhotoUrl { get; set; }
    public string UserId { get; set; }
    public bool IsDefaultNetwork { get; set; }
    public string UserType { get; set; }
    public string ProfileId { get; set; }
    public string Email { get; set; }
    public object NetworkId { get; set; }
    public object SiteUrl { get; set; }
    public string TimeZone { get; set; }
    public string UserName { get; set; }
    public string Locale { get; set; }
    public string FullName { get; set; }
    public bool AccessibilityModeEnabled { get; set; }
    public string ProfileThumbnailUrl { get; set; }
    public object RoleId { get; set; }
    public object SiteUrlPrefix { get; set; }
    public string CurrencyISOCode { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class Links
{
    public string LoginUrl { get; set; }
    public string EnterpriseUrl { get; set; }
    public string MetadataUrl { get; set; }
    public string PartnerUrl { get; set; }
    public string RestUrl { get; set; }
    public string SobjectUrl { get; set; }
    public string SearchUrl { get; set; }
    public string QueryUrl { get; set; }
    public string RecentItemsUrl { get; set; }
    public string ChatterFeedsUrl { get; set; }
    public string ChatterGroupsUrl { get; set; }
    public string ChatterUsersUrl { get; set; }
    public string ChatterFeedItemsUrl { get; set; }
    public string UserUrl { get; set; }
}

public class Application
{
    public string Namespace { get; set; }
    public string Mame { get; set; }
    public string CanvasUrl { get; set; }
    public string ApplicationId { get; set; }
    public string Version { get; set; }
    public string AuthType { get; set; }
    public string ReferenceId { get; set; }
    public List<string> Options { get; set; }
    public string DeveloperName { get; set; }
}

public class Organization
{
    public string OrganizationId { get; set; }
    public string Name { get; set; }
    public bool MulticurrencyEnabled { get; set; }
    public string NamespacePrefix { get; set; }
    public string CurrencyIsoCode { get; set; }
}

public class Environment
{
    public string LocationUrl { get; set; }
    public object DisplayLocation { get; set; }
    public string UiTheme { get; set; }
    public Dimensions Dimensions { get; set; }
    public Dictionary<string, string> Parameters { get; set; }
    public Version Version { get; set; }
    public Record Record { get; set; }
}

public class Dimensions
{
    public string Width { get; set; }
    public string MaxHeight { get; set; }
    public string MaxWidth { get; set; }
    public string Height { get; set; }
    public string ClientWidth { get; set; }
    public string ClientHeight { get; set; }
}

public class Version
{
    public string Season { get; set; }
    public string Api { get; set; }
}

public class Record
{
    public RecordAttributes Attributes { get; set; }
    public string Id { get; set; }
}

public class RecordAttributes
{
    public string Type { get; set; }
    public string Url { get; set; }
}