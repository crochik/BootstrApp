using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using NetCoreForce.Client;
using NetCoreForce.Client.Models;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Salesforce;
using PI.Shared.Salesforce.Models;

namespace PI.Shared.Services;

public class SalesforceService
{
    protected readonly ILogger<SalesforceService> _logger;
    protected readonly MongoConnection _connection;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEntityIdentityAdapter _identityAdapter;
    private readonly IEntityIntegrationAdapter _entityIntegrationAdapter;
    private HttpClient HttpClient => _httpClientFactory.CreateClient("SalesforceClient");

    public NetCoreForceClient SalesforceClient { get; }

    public SalesforceService(
        ILogger<SalesforceService> logger,
        MongoConnection connection,
        NetCoreForceClient salesforceClient,
        IHttpClientFactory httpClientFactory,
        IEntityIdentityAdapter identityAdapter,
        IEntityIntegrationAdapter entityIntegrationAdapter
    )
    {
        SalesforceClient = salesforceClient;

        _logger = logger;
        _connection = connection;
        _httpClientFactory = httpClientFactory;
        _identityAdapter = identityAdapter;
        _entityIntegrationAdapter = entityIntegrationAdapter;
    }

    public async Task<SalesforceUserInfo> GetUserInfoAsync(string accessToken, string loginUrl)
    {
        var parms = new Dictionary<string, string>
        {
            { "access_token", accessToken },
            { "format", "json" },
        };

        var uri = new UriBuilder(loginUrl)
        {
            Path = "services/oauth2/userinfo",
            Query = new QueryBuilder(parms).ToString(),
        }.Uri;

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("{Request} failed: {StatusCode}", uri.ToString(), response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();
        var userInfo = JsonConvert.DeserializeObject<SalesforceUserInfo>(body);

        using var scope = _logger.AddScope(new
        {
            SfUserSub = userInfo?.Sub,
            SfUserId = userInfo?.UserId,
            SfOrgId = userInfo?.OrganizationId,
        });

        if (!(userInfo?.Active ?? false))
        {
            _logger.LogError("User is not active");
            return null;
        }

        _logger.LogInformation("Got user info from Salesforce");

        return userInfo;
    }

    private async Task<(IEntity Entity, EntityIdentity Identity)> GetIdentityAsync(Guid entityId)
    {
        var entity = await _identityAdapter.GetEntityByIdAsync(entityId);
        if (entity?.AccountId == null)
        {
            _logger.LogError("Didn't find account for {EntityId}", entityId);
            return (null, null);
        }

        var integration = await _entityIntegrationAdapter.FindForEntityAsync(entity.AccountId, IntegrationIds.Salesforce);
        var data = integration?.GetData<SalesforceIntegration.Data>();
        if (data == null)
        {
            _logger.LogError("Didn't find integration for {AccountId}", entity.AccountId);
            return (null, null);
        }

        if (data.OverrideEntityId.HasValue)
        {
            if (data.OverrideEntityId.Value != entity.Id)
            {
                entity = await _identityAdapter.GetEntityByIdAsync(data.OverrideEntityId.Value);
            }

            return (entity, entity.FirstIdentity(nameof(ExternalProvider.Salesforce)));
        }

        var entities = await _identityAdapter?.GetEntityTrunkAsync(entityId);

        var first = entities?
            .OrderByDescending(x => x.ObjectType)
            .Select(x => (x, x.FirstIdentity(nameof(ExternalProvider.Salesforce))))
            .Where(x => x.Item2 != null)
            .FirstOrDefault();

        return first.GetValueOrDefault();
    }

    /// <summary>
    /// Get object from salesforce (all fields)
    /// </summary>
    public async Task<ExpandoObject> GetObjectAsync(IEntityContext context, string objectType, string id)
    {
        var (token, error) = await GetTokenAsync(context.AccountId.Value);
        if (token == null) throw new ForbiddenException(error);

        var result = await SalesforceClient.QueryByIdAsync<ExpandoObject>(token, objectType, id);
        return result;
    }

    // private async Task<SalesforceObjectType> GetSalesforceObjectTypeAsync(IEntityContext context, string sfObjectTypeName)
    // {
    //     var objectType = await _connection.Filter<SalesforceObjectType>()
    //         .Eq(x => x.AccountId, context.AccountId.Value)
    //         .Eq(x => x.Name, $"sf_{sfObjectTypeName}")
    //         .Ne(x => x.IsActive, false)
    //         .Ne(x => x.Integrations["Salesforce"], null)
    //         .FirstOrDefaultAsync();
    //
    //     return objectType;
    // }

    /// <summary>
    /// Load object into collection 
    /// </summary>
    public async Task<T> LoadObjectAsync<T>(IEntityContext context, SalesforceObjectType objectType, string sfId, Guid? leadId = null, Guid? entityId = null, Guid? assignedEntityId = null)
        where T : SalesforceCustomObject
    {
        var integration = objectType?.SalesforceIntegration;
        if (integration == null) throw NotFoundException.New("Object Type not found");

        var (token, error) = await GetTokenAsync(context.AccountId.Value);
        if (token == null) throw new ForbiddenException(error);

        // get all fields
        // var fields = integration.FieldMap
        //     .Where(x => !string.IsNullOrWhiteSpace(x.TargetProperty))
        //     .Select(x => x.Source)
        //     .Distinct();
        var result = await SalesforceClient.QueryByIdAsync<dynamic>(token, objectType.SalesforceIntegration.Name, sfId); // , fields

        var src = ((Newtonsoft.Json.Linq.JObject)result).Properties().ToDictionary();
        var dst = new Dictionary<string, object>();
        var properties = new Dictionary<string, object>();
        foreach (var field in integration.FieldMap)
        {
            if (string.IsNullOrWhiteSpace(field.TargetProperty)) continue;
            if (!src.TryGetValue(field.Source, out var value) || value == null) continue;
            dst[field.TargetProperty] = value;

            if (field.TargetProperty.StartsWith("Properties|"))
            {
                properties[field.TargetProperty["Properties|".Length..]] = value;
            }
        }

        var query = _connection.Filter<T>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(x => x.AccountId, objectType.AccountId)
                .Eq(x => x.ExternalId, sfId)
                .Update
                .SetOnInsert(x => x.AccountId, objectType.AccountId)
                .SetOnInsert(x => x.ObjectType, objectType.Name)
                .SetOnInsert(x => x.ObjectTypeId, objectType.Id)
                .SetOnInsert(x => x.ExternalId, sfId)
                .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            ;

        if (dst.TryGetValue(nameof(CustomObject.Name), out var name) && name is string)
        {
            query.SetOnInsert(x => x.Name, name);
        }

        if (dst.TryGetValue(nameof(CustomObject.Description), out var description) && description is string)
        {
            query.SetOnInsert(x => x.Description, description);
        }

        if (objectType.InitialFlowId.HasValue)
        {
            query.SetOnInsert(x => x.FlowId, objectType.InitialFlowId);
        }

        if (objectType.InitialObjectStatusId.HasValue)
        {
            query.SetOnInsert(x => x.ObjectStatusId, objectType.InitialObjectStatusId);
        }

        if (entityId.HasValue)
        {
            // always update
            query.Set(x => x.EntityId, entityId);
        }
        else
        {
            query.SetOnInsert(x => x.EntityId, objectType.EntityId);
        }

        if (typeof(ILeadReference).IsAssignableFrom(typeof(T)) && leadId.HasValue)
        {
            // always update
            query.Set(nameof(ILeadReference.LeadId), leadId.Value);
        }

        if (typeof(IAssignedEntityId).IsAssignableFrom(typeof(T)) && assignedEntityId.HasValue)
        {
            // always update
            query.Set(nameof(IAssignedEntityId.AssignedEntityId), assignedEntityId.Value);
        }

        // always update
        var isActive = !(src.TryGetValue("IsDeleted", out var isDeletedObj) && isDeletedObj is bool and true);
        query.Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.IsActive, isActive)
            .Set(x => x.Properties, properties)
            .Unset(x => x.LoadedOn) // original behavior (to flag that had to be post processed)
            ;

        var record = await query.UpdateAndGetOneAsync(true);

        return record;
    }

    public async Task<Result<SalesforceToken>> GetTokenAsync(IEntityContext context, GetTokenOptions options)
    {
        if (options.UseIntegration)
        {
            return await GetTokenAsync(context.AccountId.Value, options);
        }

        return context.Role switch
        {
            EntityRoleId.User or EntityRoleId.Manager or EntityRoleId.Admin => await GetTokenAsync(context.AccountId.Value, context.EntityId.Value, options),
            _ => Result.Error<SalesforceToken>("Invalid Context"),
        };
    }

    [Obsolete("use version with options")]
    public Task<(SalesforceToken Token, string Error)> GetTokenAsync(IEntityContext context, bool forceRefresh = false)
        => GetTokenAsync(context.AccountId.Value, forceRefresh);

    [Obsolete("use version with options")]
    protected async Task<(SalesforceToken Token, string Error)> GetTokenAsync(Guid accountId, bool forceRefresh = false)
    {
        var result = await GetTokenAsync(accountId, new GetTokenOptions { ForceRefresh = forceRefresh });
        if (!result.IsSuccess) return (null, result.Status);

        return (result.Value, null);
    }

    private async Task<Result<SalesforceToken>> GetTokenAsync(Guid accountId, GetTokenOptions options)
    {
        var account = await _connection.Filter<Entity, Account>()
            .Eq(x => x.AccountId, accountId)
            .FirstOrDefaultAsync();

        var sfIntegration = account?.Integrations?.FirstOrDefault(x => x.IntegrationId == IntegrationIds.Salesforce);
        if (sfIntegration == null)
        {
            return Result.Error<SalesforceToken>("Missing integration");
        }

        var data = sfIntegration?.GetData<SalesforceIntegration.Data>();

        var result = data.OverrideEntityId.HasValue ? await GetTokenAsync(accountId, data.OverrideEntityId.Value, options) : await GetTokenAsync(account, options);

        return result;
    }

    private async Task<Result<SalesforceToken>> GetTokenAsync(Guid accountId, Guid entityId, GetTokenOptions options)
    {
        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, accountId)
            .Eq(x => x.Id, entityId)
            .FirstOrDefaultAsync();

        return await GetTokenAsync(entity, options);
    }

    private async Task<Result<SalesforceToken>> GetTokenAsync(Entity entity, GetTokenOptions options)
    {
        var identity = entity.Identities
            .FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce) && x.ExternalIdentity?.Token != null);

        if (identity == null)
        {
            return Result.Error<SalesforceToken>("Salesforce Identity not found");
        }

        if (identity.ExternalIdentity?.Token == null)
        {
            return Result.Error<SalesforceToken>("Salesforce Credentials not found");
        }

        if (!options.ForceRefresh && identity.ExternalIdentity.Token is SalesforceToken token && (token.Expiration - DateTime.UtcNow).TotalMinutes > 10)
        {
            _logger.LogInformation("Reusing previous token: {Expiration}", token.Expiration);
            return Result.Success(token);
        }

        if (string.IsNullOrWhiteSpace(identity?.ExternalIdentity?.Token?.RefreshToken))
        {
            return Result.Error<SalesforceToken>("Missing RefreshToken");
        }

        // TODO: use instance url when exists?
        // ...

        _logger.LogInformation("Try to refresh token for {EntityId} {ExternalId} {ClientId} {Url}", entity.Id, identity.ExternalIdentity?.ExternalId, SalesforceClient.ClientId, SalesforceClient.TokenRequestEndpointUrl);
        var refreshToken = identity?.ExternalIdentity?.Token.RefreshToken;
        token = await SalesforceClient.RefreshTokenAsync(refreshToken);
        if (token == null)
        {
            return Result.Error<SalesforceToken>("Failed to refresh token");
        }

        _logger.LogInformation(
            "Update token for {EntityId}: {Provider} {ExternalId}",
            entity.Id,
            identity.ExternalIdentity.Provider,
            identity.ExternalIdentity.ExternalId
        );

        identity.ExternalIdentity.Token = token;
        await _identityAdapter.UpdateTokenAsync(entity, identity);

        return Result.Success(token);
    }

    public async Task<T> QueryByIdAsync<T>(IEntityContext context, string objectType, string id, IEnumerable<string> fields = null)
    {
        var (token, error) = await GetTokenAsync(context);
        if (!string.IsNullOrWhiteSpace(error)) throw new ForbiddenException(error);
        return await SalesforceClient.QueryByIdAsync<T>(token, objectType, id, fields);
    }

    public async Task<SObjectDescribeFull> DescribeAsync(IEntityContext context, string objectType)
    {
        var (token, error) = await GetTokenAsync(context);
        if (!string.IsNullOrWhiteSpace(error)) throw new ForbiddenException(error);
        return await SalesforceClient.DescribeAsync(token, objectType);
    }

    public async Task<List<T>> QueryAllAsync<T>(IEntityContext context, string query)
    {
        var (token, error) = await GetTokenAsync(context);
        if (!string.IsNullOrWhiteSpace(error)) throw new ForbiddenException(error);
        return await SalesforceClient.QueryAllAsync<T>(token, query);
    }

    /// <summary>
    /// Patch object 
    /// </summary>
    public async Task<IResult> UpdateObjectAsync(IEntityContext context, string objectType, string id, Dictionary<string, object> update, GetTokenOptions options = null)
    {
        var token = await GetTokenAsync(context, options ?? GetTokenOptions.Default);
        if (!token.IsSuccess) return token;

        try
        {
            await SalesforceClient.UpdateAsync(token.Value, objectType, id, update);
            return Result.Success(update, "Updated");
        }
        catch (ForceApiException ex)
        {
            return Result.Error(ex.Message);
        }
    }
}

[BsonKnownTypes(typeof(ObjectTypeSalesforceIntegration))]
[DiscriminatorWithFallback]
public class ObjectTypeIntegration
{
}

public class SalesforceFieldMap
{
    public string Source { get; set; }
    public string TargetProperty { get; set; }
    public string SalesforceType { get; set; }
}

[BsonDiscriminator("salesforce")]
public class ObjectTypeSalesforceIntegration : ObjectTypeIntegration
{
    public string Name { get; set; }
    public SalesforceFieldMap[] FieldMap { get; set; }
    public bool HasSubtypes { get; set; }
    public bool IsSubType { get; set; }
    public bool AutoLoad { get; set; }

    /// <summary>
    /// Whether the go-salesforce/import should fire individual "loaded" events when importing a batch of objects 
    /// </summary>
    public bool FireEvents { get; set; }

    public int MaxPerLoad { get; set; }

    public int PageSize { get; set; }

    // LastSync                *SalesforceSyncTask `bson:"LastSync,omitempty"`
    // CurrentSync             *SalesforceSyncTask `bson:"CurrentSync,omitempty"`
    // SystemModstamp          *time.Time          `bson:"SystemModstamp,omitempty"`
    // LastId                  string              `bson:"LastId,omitempty"`
    // LatestDeleteDateCovered *time.Time          `bson:"LatestDeleteDateCovered,omitempty"`
}

public class SalesforceObjectType : ObjectType
{
    public Dictionary<string, ObjectTypeIntegration> Integrations { get; set; }

    [BsonIgnore]
    public ObjectTypeSalesforceIntegration SalesforceIntegration
    {
        get
        {
            if (Integrations == null) return null;
            if (!Integrations.TryGetValue("Salesforce", out var integration)) return null;
            if (integration is not ObjectTypeSalesforceIntegration salesforceIntegration) return null;
            return salesforceIntegration;
        }
    }
}

public interface ISalesforceCustomObject
{
}

/// <summary>
/// salesforce object has link to lead
/// </summary>
public interface ILeadReference : ISalesforceCustomObject
{
    Guid? LeadId { get; set; }
}

/// <summary>
/// salesforce object has an assigned entity id (user)
/// </summary>
public interface IAssignedEntityId : ISalesforceCustomObject
{
    Guid? AssignedEntityId { get; set; }
}

public class SalesforceCustomObject : CustomObject, ISalesforceCustomObject
{
    public DateTime? LoadedOn { get; set; }
}

public class GetTokenOptions
{
    public static GetTokenOptions Default = new();

    public bool UseIntegration { get; init; } = true;
    public bool ForceRefresh { get; init; } = false;
}