using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Controllers;
using Crochik.Mongo;
using IdentityModel.Client;
using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Results;
using Intuit.Ipp.Core;
using Intuit.Ipp.Core.Configuration;
using Intuit.Ipp.Data;
using Intuit.Ipp.DataService;
using Intuit.Ipp.Exception;
using Intuit.Ipp.QueryFilter;
using Intuit.Ipp.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using PI.ProductCatalog.Models;
using PI.QuickBooks.Models;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models;
using PI.Shared.Services;
using PI.Shared.Services.DataProtection;
using Account = Intuit.Ipp.Data.Account;
using EmailAddress = Intuit.Ipp.Data.EmailAddress;
using IEntity = Intuit.Ipp.Data.IEntity;
using Result = PI.Shared.Models.Result;
using Task = System.Threading.Tasks.Task;

namespace PI.QuickBooks.Services;

public class QuickBooksService
{
    private readonly ILogger<QuickBooksService> _logger;
    private readonly MongoConnection _connection;
    private readonly DataProtectionService _dataProtectionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ObjectTypeService _objectTypeService;
    private readonly Configuration _configuration;
    private readonly string _baseUrl;
    private HttpClient Client => _httpClientFactory.CreateClient(nameof(QuickBooksService));

    public class Configuration
    {
        public const string DevelopmentAuthority = "https://developer.api.intuit.com";
        public const string SandboxDiscoveryDocumentPath = "/.well-known/openid_sandbox_configuration";
        public const string ProductionDiscoveryDocumentPath = "/.well-known/openid_configuration";

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Scope { get; set; } = "com.intuit.quickbooks.accounting com.intuit.quickbooks.project-management.project openid profile email phone address";

        public bool UseDevelopmentAuth { get; set; } = false;
    }

    private OidcClient GetClient(bool useSandbox)
    {
        var options = new OidcClientOptions
        {
            Authority = "https://developer.api.intuit.com",
            ClientId = _configuration.ClientId,
            ClientSecret = _configuration.ClientSecret,
            RedirectUri = $"{_baseUrl}/quickbooks/v1/Integration/redirect",
            Scope = _configuration.Scope,
            // ProviderInformation = providerInfo,
            Policy = new Policy
            {
                Discovery = new DiscoveryPolicy
                {
                    DiscoveryDocumentPath = useSandbox ? Configuration.SandboxDiscoveryDocumentPath : Configuration.ProductionDiscoveryDocumentPath,
                    // Authority = "https://oauth.platform.intuit.com/op/v1",
                    ValidateEndpoints = false,
                    ValidateIssuerName = false,
                }
            }
        };

        return new OidcClient(options);
    }

    public QuickBooksService(
        ILogger<QuickBooksService> logger,
        MongoConnection connection,
        IConfiguration configuration,
        DataProtectionService dataProtectionService,
        IHttpClientFactory httpClientFactory,
        ObjectTypeService objectTypeService
    )
    {
        _logger = logger;
        _connection = connection;
        _dataProtectionService = dataProtectionService;
        _httpClientFactory = httpClientFactory;
        _objectTypeService = objectTypeService;

        _configuration = configuration.GetSection(nameof(QuickBooksService)).Get<Configuration>();
        _baseUrl = configuration.GetValue("BaseUrl", default(string));

        if (_baseUrl == null || _configuration?.ClientId == null || _configuration?.ClientSecret == null)
        {
            _logger.LogCritical("Missing configuration");
            throw new Exception("Missing configuration");
        }
    }

    public async Task<Result<string>> StartLoginAsync(IEntityContext context)
    {
        var state = await GetClient(_configuration.UseDevelopmentAuth)
            .PrepareLoginAsync();

        await _connection.InsertAsync(new QuickbooksSingleUseTicket
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            EntityId = context.OrganizationId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            Name = "Add Quickbooks integration",
            ExpiresOn = DateTime.UtcNow.AddMinutes(10),
            IsActive = true,

            State = state.State,
            StartUrl = state.StartUrl,
            CodeVerifier = state.CodeVerifier,
            RedirectUri = state.RedirectUri,
            UseSandbox = _configuration.UseDevelopmentAuth,
        });

        return Result.Success(state.StartUrl);
    }

    private async Task<Result<string>> UnprotectAsync(IEntityContext context, string protectedString)
    {
        if (string.IsNullOrEmpty(protectedString))
        {
            return Result.Error<string>("Missing Token");
        }

        try
        {
            var unprotected = await _dataProtectionService.UnprotectAsync(
                context,
                new MicrosoftDataProtectionConfig
                {
                    Purpose = QuickBooksIntegrationConfiguration.ProtectionPurpose,
                },
                protectedString
            );

            return Result.Success(unprotected);
        }
        catch (Exception ex)
        {
            return Result.Error<string>(ex.Message);
        }
    }

    private async Task<IntegrationToken> ProtectAsync(IEntityContext context, IntegrationToken token)
    {
        var config = new MicrosoftDataProtectionConfig
        {
            Purpose = QuickBooksIntegrationConfiguration.ProtectionPurpose,
        };

        var accessToken = await _dataProtectionService.ProtectAsync(context, config, token.AccessToken);
        var refreshToken = await _dataProtectionService.ProtectAsync(context, config, token.RefreshToken);

        token.AccessToken = accessToken;
        token.RefreshToken = refreshToken;

        return token;
    }

    private Task<Result<string>> UnprotectAccessTokenAsync(IEntityContext context, QuickBooksIntegrationConfiguration integration)
        => UnprotectAsync(context, integration?.Token?.AccessToken);

    private Task<Result<string>> UnprotectRefreshTokenAsync(IEntityContext context, QuickBooksIntegrationConfiguration integration)
        => UnprotectAsync(context, integration?.Token?.RefreshToken);

    public async Task<Result<string>> GetAccessTokenAsync(IEntityContext context, QuickBooksIntegrationConfiguration integration)
    {
        if (integration.Token.ExpiresOn < DateTime.UtcNow.AddMinutes(10))
        {
            integration = await RefreshTokenAsync(context, integration);
        }

        return await UnprotectAccessTokenAsync(context, integration);
    }

    public async Task<ServiceContext> GetServiceContextAsync(IEntityContext context)
    {
        var integration = await GetIntegrationAsync(context);

        if (integration == null) throw new BadRequestException("Integration not configured");

        var accessToken = await GetAccessTokenAsync(context, integration);
        if (!accessToken.IsSuccess)
        {
            throw new BadRequestException(accessToken.Status);
        }

        var oauthValidator = new OAuth2RequestValidator(accessToken.Value);
        var serviceContext = new ServiceContext(integration.CompanyId, IntuitServicesType.QBO, oauthValidator);
        if (integration.UseSandbox)
        {
            serviceContext.IppConfiguration.BaseUrl.Qbo = integration.BaseUrl;
        }
        
        // hack to prevent version conflict with serilog file sync!
        // https://github.com/intuit/quickbooks-v3-dotnet-sdk/issues/315
        // serviceContext.IppConfiguration.AdvancedLogger = new AdvancedLogger { 
        //     RequestAdvancedLog = new RequestAdvancedLog
        //     {
        //         CustomLogger = null, // Serilog.Log.Logger
        //     } 
        // };
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForConsole = false;
        serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForDebug = true;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForFile = false;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForTrace = false;
        
        serviceContext.IppConfiguration.Logger.RequestLog.EnableRequestResponseLogging = true;
        // serviceContext.IppConfiguration.Logger.CustomLogger = null; // Ensure no custom serilog is injected
        
        
        // serviceContext.IppConfiguration.MinorVersion.Qbo = integration.MinorVersion;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.CustomLogger = _logger;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForFile = true;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForConsole = true;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForTrace = true;
        // serviceContext.IppConfiguration.AdvancedLogger.RequestAdvancedLog.EnableSerilogRequestResponseLoggingForDebug = true;

        return serviceContext;
    }

    public async Task<QuickBooksIntegrationConfiguration> GetIntegrationAsync(IEntityContext context)
    {
        var integration = await _connection.Filter<QuickBooksIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.IntegrationId, IntegrationIds.QuickBooks)
            .FirstOrDefaultAsync();
        return integration;
    }

    private async Task<QuickBooksIntegrationConfiguration> RefreshTokenAsync(IEntityContext context, QuickBooksIntegrationConfiguration integration)
    {
        var refreshToken = await UnprotectRefreshTokenAsync(context, integration);
        if (!refreshToken.IsSuccess)
        {
            throw new BadRequestException(refreshToken.Status);
        }

        var result = await GetClient(integration.UseSandbox)
            .RefreshTokenAsync(refreshToken.Value);

        if (result.IsError)
        {
            throw new ForbiddenException(result.Error);
        }

        var token = await ProtectAsync(context, new IntegrationToken
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresOn = result.AccessTokenExpiration.UtcDateTime,
        });

        return await _connection.Filter<QuickBooksIntegrationConfiguration>()
            .Eq(x => x.AccountId, integration.AccountId)
            .Eq(x => x.Id, integration.Id)
            .Update
            .Set(x => x.Token, token)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();
    }

    public async Task<Result<QuickBooksIntegrationConfiguration>> LoginAsync(string state, string queryString)
    {
        var ticket = await _connection.Filter<QuickbooksSingleUseTicket>()
            .Eq(x => x.State, state)
            .Gt(x => x.ExpiresOn, DateTime.UtcNow)
            .Eq(x => x.IsActive, true)
            .Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (ticket == null) return Result.Error<QuickBooksIntegrationConfiguration>("Invalid state");

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, ticket.AccountId)
            .Eq(x => x.Id, ticket.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (organization == null) return Result.Error<QuickBooksIntegrationConfiguration>("Invalid Entity");

        var context = organization.Context;

        var login = await GetClient(ticket.UseSandbox).ProcessResponseAsync(
            queryString,
            new AuthorizeState
            {
                State = ticket.State,
                CodeVerifier = ticket.CodeVerifier,
                RedirectUri = ticket.RedirectUri,
                StartUrl = ticket.StartUrl,
            }
        );

        var companyId = login.User.Claims.FirstOrDefault(x => x.Type == "realmid").Value;

        // do not double encrypt (password field will take care of it because we are using the objecttype service)
        // var token = await ProtectAsync(context, new IntegrationToken
        // {
        //     AccessToken = login.AccessToken,
        //     RefreshToken = login.RefreshToken,
        //     ExpiresOn = login.AccessTokenExpiration.UtcDateTime,
        // });

        var token = new IntegrationToken
        {
            AccessToken = login.AccessToken,
            RefreshToken = login.RefreshToken,
            ExpiresOn = login.AccessTokenExpiration.UtcDateTime,
        };

        var integration = new QuickBooksIntegrationConfiguration
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            AccountId = ticket.AccountId,
            EntityId = ticket.EntityId,
            Name = $"{companyId} @ QuickBooks",
            Description = ticket.UseSandbox ? "QuickBooks integration (Sandbox)" : "QuickBooks integration",
            LastActor = context.Actor(),
            IntegrationId = IntegrationIds.QuickBooks,
            CompanyId = companyId,
            Token = token,
            UseSandbox = ticket.UseSandbox,
        };

        var result = await UpsertIntegrationAsync(context, integration);
        if (!result.IsSuccess) return result;
        integration = result.Value;

        var userInfo = await GetClient(integration.UseSandbox)
            .GetUserInfoAsync(login.AccessToken);

        await AddIdentityAsync(context, organization, integration, userInfo);

        return Result.Success(integration);
    }

    /// <summary>
    /// Upsert integration on the context of the Owner (Account/Organization)
    /// </summary>
    private async Task<Result<QuickBooksIntegrationConfiguration>> UpsertIntegrationAsync(IEntityContext context, QuickBooksIntegrationConfiguration integration)
    {
        // update on the context of the owner (e.g. Account/Org)
        var ownerContext = context.GetOwnerEntityContext();
        var objectType = await _objectTypeService.GetAsync(ownerContext, QuickBooksIntegrationConfiguration.IntegrationObjectTypeName);
        if (objectType == null) throw NotFoundException.New(QuickBooksIntegrationConfiguration.IntegrationObjectTypeName);
        var result = await _objectTypeService.AddObjectAsync(
            ownerContext,
            objectType,
            integration,
            new ObjectTypeService.AddObjectOptions
            {
                IsUpsert = true
            }
        );

        if (!result.IsSuccess) return result.ConvertTo<QuickBooksIntegrationConfiguration>();

        integration = await _connection.Filter<QuickBooksIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, result.Value.ObjectId)
            .FirstOrDefaultAsync();

        if (integration == null) return Result.Error<QuickBooksIntegrationConfiguration>("Couldn't find integration after saving it");

        return Result.Success(integration);
    }

    private async Task<bool> AddIdentityAsync(IEntityContext context, Organization entity, QuickBooksIntegrationConfiguration integration, UserInfoResult userInfo)
    {
        if (entity.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Quickbooks)) != null)
        {
            _logger.LogInformation("{OrganizationId} already has a Quickbooks identity", entity.Id);
            return false;
        }

        _logger.LogInformation("Add Quickbooks identity to {OrganizationId}: {CompanyId}", entity.Id, integration.CompanyId);

        // add identity to organization
        var update = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, entity.AccountId)
            .Eq(x => x.Id, entity.Id)
            .Update
            .AddToSet(x => x.Identities, new EntityIdentity
            {
                Id = Guid.NewGuid(),
                IdentityProviderId = nameof(ExternalProvider.Quickbooks),
                ExternalId = integration.CompanyId,
                Data = userInfo.Claims.ToDictionary(x => x.Type, x => (object)x.Value),
            })
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();

        if (update.ModifiedCount != 1)
        {
            _logger.LogError("Failed to add Quickbooks identity");
            return false;
        }

        await _objectTypeService.FireObjectUpdatedAsync(context, entity, new Dictionary<string, object>
        {
            { nameof(Entity.Identities), "Quickbooks" }
        }, e =>
        {
            e.Description = "Added CompanyCam Identity";
            e.TryAddMetaValue(nameof(QuickBooksIntegrationConfiguration.CompanyId), integration.CompanyId);
        });

        return true;
    }

    private Customer BuildCustomer(Lead lead)
    {
        return new Customer
        {
            DisplayName = lead.GetDisplayName(),
            GivenName = lead.FirstName,
            FamilyName = lead.LastName,
            AcctNum = lead.Id.ToString(),
            Job = false,
            PrimaryEmailAddr = new EmailAddress
            {
                Address = lead.NormalizedEmail,
            },
            PrimaryPhone = new TelephoneNumber
            {
                FreeFormNumber = lead.NormalizedPhoneNumber,
            },
            BillAddr = new PhysicalAddress
            {
                Line1 = lead.Address,
                City = lead.City,
                CountrySubDivisionCode = lead.State,
                PostalCode = lead.PostalCode,
                Country = lead.Country,
            },
        };
    }

    private Customer BuildProject(string customerId, SfWorkOrder project, Lead lead)
    {
        var customer = new Customer
        {
            DisplayName = project.GetDisplayName(),
            // FullyQualifiedName = ... // is it calculated automatically? 
            GivenName = lead.FirstName,
            FamilyName = lead.LastName,
            CompanyName = lead.Name,
            // PrintOnCheckName = lead.Name,
            AcctNum = project.ExternalId,
            Notes = project.Notes,
            ShipAddr = new PhysicalAddress
            {
                Line1 = project.Street,
                City = project.City,
                CountrySubDivisionCode = project.State,
                PostalCode = project.PostalCode,
                Country = project.Country,
            },
            BillAddr = new PhysicalAddress
            {
                Line1 = lead.Address,
                City = lead.City,
                CountrySubDivisionCode = lead.State,
                PostalCode = lead.PostalCode,
                Country = lead.Country,
            },
            // sub account
            Job = true,
            JobSpecified = true,
            // project seems to be ignored :(
            // IsProject = true,
            // IsProjectSpecified = true,
            ParentRef = new ReferenceType
            {
                Value = customerId, //  project.LeadId.ToString(),
            }
        };

        return customer;
    }

    private async Task<QbEntity> GetLocalAsync(IEntityContext context, string objectType, object id, string entityType = null)
    {
        var query = _connection.Filter<QbEntity>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.OrganizationId)
            .Eq(x => x.Refs[objectType], id);

        if (entityType != null) query.Eq(x => x.EntityType, entityType);

        return await query.FirstOrDefaultAsync();
    }

    public async Task<QbEntity> SaveAsync<T>(IEntityContext context, T entity, string refObjectType, object refObjectId, string fullyQualifiedName, string externalId)
    {
        var qbEntity = await _connection.Filter<QbEntity>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.EntityId)
            .Eq(x => x.EntityType, typeof(T).Name)
            .Eq(x => x.ExternalId, externalId)
            .Update
            .SetOnInsert(x => x.AccountId, context.AccountId)
            .SetOnInsert(x => x.EntityId, context.OrganizationId)
            .SetOnInsert(x => x.FullyQualifiedName, fullyQualifiedName)
            .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            .SetOnInsert(x => x.Id, Guid.NewGuid())
            .SetOnInsert(x => x.EntityType, typeof(T).Name)
            .SetOnInsert(x => x.ExternalId, externalId)
            .Set(x => x.Properties, JsonObjectConverter.Convert<ExpandoObject>(entity))
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.Refs[refObjectType], refObjectId)
            .UpdateAndGetOneAsync(true);

        return qbEntity;
    }

    public Task<IList<Item>> GetAllItemsAsync(IEntityContext context) => GetAllAsync<Item>(context);

    public async Task<IList<T>> FindAsync<T>(IEntityContext context, string fieldName, string fieldValue) where T : IEntity, new()
    {
        var entityName = typeof(T).Name.ToLower(CultureInfo.InvariantCulture);

        var serviceContext = await GetServiceContextAsync(context);
        var sql = $"SELECT * FROM {entityName} WHERE {fieldName} = '{fieldValue}'";
        var querySvc = new QueryService<T>(serviceContext);
        var list = querySvc.ExecuteIdsQuery(sql);

        _logger.LogInformation("Find {EntityName}, {FieldName}={FieldValue}, {Matches}", entityName, fieldName, fieldValue, list.Count);
        return list;
    }

    public async Task<IList<T>> GetAllAsync<T>(IEntityContext context) where T : IEntity, new()
    {
        // var serviceContext = await GetServiceContextAsync(context);
        // var ds = new DataService(serviceContext);
        var ds = await GetDataServiceAsync(context);

        var result = new List<T>();
        var pageSize = 500;
        for (var c = 1; c < 100_000; c += pageSize)
        {
            var items = ds.FindAll(new T(), c, pageSize);
            result.AddRange(items);

            _logger.LogInformation("Got Page: {Object}: {Count}", typeof(T).Name, items.Count);
            if (items.Count < pageSize)
            {
                _logger.LogInformation("Got All: {Object}: {Count}", typeof(T).Name, result.Count);
                return result;
            }
        }

        _logger.LogError("Got more than 100 000 items");
        return result;

        // sql ??= $"SELECT * FROM {typeof(T).Name}";
        // var querySvc = new QueryService<T>(serviceContext);
        // var items = querySvc.ExecuteIdsQuery(sql);
    }

    public async Task<Result<T>> ExportAsync<T>(IEntityContext context, T item, LocalCache qb = null)
        where T : IEntity
    {
        try
        {
            var ds = await GetDataServiceAsync(context, qb);

            item = ds.Add(item);

            return Result.Success(item);
        }
        catch (IdsException ex)
        {
            _logger.LogError(ex, "Error adding {Object}: {Obj}", typeof(T).Name, JsonConvert.SerializeObject(item));
            return Result.Error<T>(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding {Object}: {Obj}", typeof(T).Name, JsonConvert.SerializeObject(item));
            return Result.Error<T>(ex.Message);
        }
    }

    private async Task<DataService> GetDataServiceAsync(IEntityContext context, LocalCache qb = null)
    {
        if (qb?.DataService != null) return qb.DataService;
        var serviceContext = qb?.ServiceContext ?? await GetServiceContextAsync(context);
        var ds = new DataService(serviceContext);
        if (qb != null)
        {
            qb.ServiceContext = serviceContext;
            qb.DataService = ds;
        }

        return ds;
    }

    public async Task<Result<QbEntity>> GetOrCreateVendorAsync(IEntityContext context, LocalCache qb, Guid catalogFeedId)
    {
        var vendor = await LoadVendorAsync(context, qb, catalogFeedId);
        if (vendor != null) return Result.Success(vendor);

        var catalogFeed = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, catalogFeedId)
            // .Ne(x=>x.IsActive, false)
            .FirstOrDefaultAsync();

        // try to find in quickbooks
        var qbVendors = await FindAsync<Vendor>(context, nameof(Vendor.DisplayName), catalogFeed.GetDisplayName());
        if (qbVendors.Count == 1)
        {
            // add to local
            var qbVendor = qbVendors.Single();
            var existing = await SaveAsync(context, qbVendor, nameof(CatalogFeed), catalogFeed.Id, qbVendor.DisplayName, qbVendor.Id);
            return Result.Success(existing);
        }

        var created = await ExportVendorAsync(context, catalogFeed);
        if (created.IsSuccess)
        {
            qb.CatalogFeedIds ??= new Dictionary<Guid, string>();
            qb.CatalogFeedIds[catalogFeedId] = created.Value.ExternalId;
        }

        return created;
    }

    public Task<QbEntity> LoadVendorAsync(IEntityContext context, LocalCache qb, CatalogFeed feed)
        => LoadVendorAsync(context, qb, feed.Id);

    public async Task<QbEntity> LoadVendorAsync(IEntityContext context, LocalCache qb, Guid catalogFeedId)
    {
        var vendor = await _connection.Filter<QbEntity>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.OrganizationId)
            .Eq(x => x.EntityType, nameof(Vendor))
            .Eq(x => x.Refs[nameof(CatalogFeed)], catalogFeedId)
            .IncludeField(x => x.ExternalId)
            .FirstOrDefaultAsync();

        if (vendor == null) return null;

        qb.CatalogFeedIds ??= new Dictionary<Guid, string>();
        qb.CatalogFeedIds[catalogFeedId] = vendor.ExternalId;

        return vendor;
    }

    public async Task<Result<QbEntity>> GetOrCreateAsync(IEntityContext context, LocalCache qb, CatalogItem item)
    {
        // try to find in Quickbooks
        var existing = await FindAsync<Item>(context, nameof(Item.Name), item.GetFullyQualifiedName());
        if (existing.Count == 1)
        {
            var qbItem = existing.First();
            _logger.LogInformation("Found {DisplayName} with {Id} for {ItemId}", qbItem.FullyQualifiedName, qbItem.Id, item.Id);

            var qbEntity = await SaveAsync(context, qbItem, nameof(CatalogItem), item.Id, qbItem.FullyQualifiedName ?? qbItem.Name, qbItem.Id);
            return Result.Success(qbEntity);
        }

        return await ExportAsync(context, qb, item);
    }

    private async Task<Result<QbEntity>> ExportAsync(IEntityContext context, LocalCache qb, CatalogItem item)
    {
        var buildResult = await BuildItemAsync(context, qb, item);
        if (!buildResult.IsSuccess)
        {
            _logger.LogError("Failed to Build {Item}: {Status}", item.Id, buildResult.Status);
            return Result.Error<QbEntity>($"Failed to Build Item \"{item.Name}\" ({item.SKU}), {item.Id}: {buildResult.Status}");
        }

        var qbItem = buildResult.Value;
        try
        {
            var ds = await GetDataServiceAsync(context, qb);
            qbItem = ds.Add(qbItem);
        }
        catch (IdsException ex)
        {
            _logger.LogInformation(ex, "Error adding Item: {Obj}", JsonConvert.SerializeObject(qbItem));
            return Result.Error<QbEntity>($"Reuse existing Item {item.SKU} (\"{item.Name}\")");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Item: {Obj}", JsonConvert.SerializeObject(qbItem));
            return Result.Error<QbEntity>(ex.Message);
        }

        var qbEntity = await SaveAsync(context, qbItem, nameof(CatalogItem), item.Id, qbItem.FullyQualifiedName ?? qbItem.Name, qbItem.Id);
        if (qbEntity == null)
        {
            // should never happen but... 
            return Result.Error<QbEntity>($"Failed to save {qbItem.FullyQualifiedName} to local database");
        }

        return Result.Success(qbEntity);
    }

    private async Task<Result<QbEntity>> ImportItemAsync(IEntityContext context, Item item, Guid catalogItemId)
    {
        var existing = await FindAsync<Item>(context, nameof(Item.Name), item.Name);
        if (existing.Count != 1)
        {
            return Result.Error<QbEntity>($"Couldn't find Item by Name: {item.Name}");
        }

        var qbItem = existing.First();

        // TODO: check that is a good match
        // ...

        // TODO: pass argument to decide whether to persist or just return info
        // ...

        return Result.Success(new QbEntity
        {
            ExternalId = item.Id,
            AccountId = context.AccountId.Value,
            EntityId = context.OrganizationId.Value,
            FullyQualifiedName = qbItem.FullyQualifiedName ?? qbItem.Name,
            CreatedOn = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            EntityType = nameof(Item),
            Properties = JsonObjectConverter.Convert<ExpandoObject>(qbItem),
            LastActor = context.Actor(),
            LastModifiedOn = DateTime.UtcNow,
            Refs = new Dictionary<string, object>
            {
                { nameof(CatalogItem), catalogItemId }
            }
        });
    }

    private async Task<Result<QbEntity>> ExportAsync(IEntityContext context, LocalCache qb, CatalogItem item, Item qbItem = null)
    {
        var buildResult = await BuildItemAsync(context, qb, item, qbItem);
        if (!buildResult.IsSuccess) return buildResult.ConvertTo<QbEntity>();

        try
        {
            var ds = await GetDataServiceAsync(context, qb);
            qbItem = ds.Add(buildResult.Value);

            var qbEntity = await SaveAsync(context, qbItem, nameof(CatalogItem), item.Id, qbItem.FullyQualifiedName ?? qbItem.Name, qbItem.Id);
            if (qbEntity == null) return Result.Error<QbEntity>($"Failed to save {buildResult.Value.FullyQualifiedName} to local database");

            qb.ItemIds ??= new Dictionary<Guid, string>();
            qb.ItemIds[item.Id] = qbItem.Id;

            return Result.Success(qbEntity);
        }
        catch (IdsException ex)
        {
            _logger.LogInformation(ex, "Error adding Item: {Obj}", JsonConvert.SerializeObject(buildResult.Value));
            return Result.Error<QbEntity>($"Reuse existing Item {item.SKU} (\"{item.Name}\")");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding Item: {Obj}", JsonConvert.SerializeObject(buildResult.Value));
            return Result.Error<QbEntity>(ex.Message);
        }
    }

    public async Task<Result<Item>> BuildItemAsync(IEntityContext context, LocalCache qb, CatalogItem item, Item qbItem = null)
    {
        qbItem ??= new Item
        {
            Sku = item.SKU,
            Name = item.SKU,
            // ItemCategoryType = 
        };

        qbItem.Description = item.Description ?? item.Name;

        if (!qb.TryGetVendorId(item.CatalogFeedId, out var vendorId))
        {
            var vendor = await GetOrCreateVendorAsync(context, qb, item.CatalogFeedId);
            if (!vendor.IsSuccess)
            {
                return Result.Error<Item>($"Unable to create vendor for {item.CatalogFeedId}");
            }

            vendorId = vendor.Value.ExternalId;
        }

        if (vendorId != null)
        {
            qbItem.PrefVendorRef = new ReferenceType { Value = vendorId };
        }

        if (qb.TryGetItemType(item, out var type))
        {
            qbItem.Type = type;
            qbItem.TypeSpecified = true;
        }

        if (qb.TryGetIncomeAccountId(item, out var incomeAccountId))
        {
            qbItem.IncomeAccountRef = new ReferenceType { Value = incomeAccountId };
        }

        if (qb.TryGetExpenseAccountId(item, out var expenseAccountId))
        {
            qbItem.ExpenseAccountRef = new ReferenceType { Value = expenseAccountId };
        }

        if (item.StandardCost?.UnitCost != null)
        {
            qbItem.PurchaseCost = item.StandardCost.UnitCost;
            qbItem.PurchaseCostSpecified = true;
        }

        if (item.StandardPrice.HasValue)
        {
            qbItem.UnitPrice = item.StandardPrice.Value;
            qbItem.UnitPriceSpecified = true;
        }

        return Result.Success(qbItem);
    }

    public async Task<Result<QbEntity>> GetOrCreateAsync(IEntityContext context, Lead lead)
    {
        // var existing = await GetLocalAsync(context, nameof(Lead), lead.Id, nameof(Customer));
        // if (existing != null) return Result.Success(existing);

        // try to find in quickbooks
        var qbCustomers = await FindAsync<Customer>(context, nameof(Customer.DisplayName), lead.GetDisplayName());
        if (qbCustomers.Count == 1)
        {
            // add to local
            var qbCustomer = qbCustomers.Single();
            var existing = await SaveAsync(context, qbCustomer, nameof(Lead), lead.Id, qbCustomer.DisplayName, qbCustomer.Id);
            return Result.Success(existing);
        }

        // create new customer 
        var customer = BuildCustomer(lead);
        var result = await ExportAsync(context, customer);
        if (!result.IsSuccess) return result.ConvertTo<QbEntity>();

        var created = await SaveAsync(context, result.Value, nameof(Lead), lead.Id, result.Value.DisplayName, result.Value.Id);

        return Result.Success(created);
    }

    public async Task<Result<QbEntity>> GetOrCreateAsync(IEntityContext context, QbEntity customer, SfWorkOrder project, Lead lead)
    {
        // var existing = await GetLocalAsync(context, "sf_WorkOrder", project.ExteranlId, nameof(Customer));
        // if (existing != null) return Result.Success(existing);

        // try to find in quickbooks
        var qbCustomers = await FindAsync<Customer>(context, nameof(Customer.FullyQualifiedName), project.GetFullyQualifiedName(lead.GetDisplayName()));
        if (qbCustomers.Count == 1)
        {
            // add to local
            var qbCustomer = qbCustomers.Single();
            var existing = await SaveAsync(context, qbCustomer, "sf_WorkOrder", project.ExternalId, qbCustomer.FullyQualifiedName ?? qbCustomer.DisplayName, qbCustomer.Id);
            return Result.Success(existing);
        }

        var result = await ExportAsync(context, BuildProject(customer.ExternalId, project, lead));
        if (!result.IsSuccess) return result.ConvertTo<QbEntity>();

        var created = await SaveAsync(context, result.Value, "sf_WorkOrder", project.ExternalId, result.Value.FullyQualifiedName ?? result.Value.DisplayName, result.Value.Id);

        return Result.Success(created);
    }

    /// <summary>
    /// Load accounts from database into cache
    /// They have to be "synced" first
    /// </summary>
    public async Task LoadAccountsAsync(IEntityContext context, LocalCache qb)
    {
        var accounts = await _connection.Filter<QbEntity>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.OrganizationId)
            .Eq(x => x.EntityType, nameof(Account))
            .FindAsync();

        var dict = new Dictionary<string, string>();
        foreach (var account in accounts)
        {
            dict[account.FullyQualifiedName] = account.ExternalId;
            if (
                account.Properties != null &&
                account.Properties.TryGetValue("AcctNum", out var acctNum) &&
                acctNum is string acctNumStr
            )
            {
                dict[acctNumStr] = account.ExternalId;
            }
        }

        qb.AccountIds = dict;
    }

    /// <summary>
    /// Sync Accounts between QBO and the PI Organization
    /// </summary>
    public async Task<List<string>> SyncAccountsAsync(IEntityContext context, LocalCache localCache = null, bool updateExisting = false)
    {
        var result = new List<string>();
        var chartOfAccounts = GetChartOfAccounts(context).ToArray();
        if (localCache?.AccountIds != null)
        {
            var missing = chartOfAccounts.Where(x => !localCache.AccountIds.ContainsKey(x[2])).ToArray();
            if (missing.IsEmpty() && !updateExisting)
            {
                result.Add("All accounts already in local cache");
                return result;
            }
        }

        var accounts = await ImportAccountsAsync(context);
        var existing = new Dictionary<string, Account>();
        foreach (var account in accounts)
        {
            if (!string.IsNullOrEmpty(account.AcctNum)) existing.TryAdd(account.AcctNum, account);
            if (!string.IsNullOrEmpty(account.FullyQualifiedName)) existing.TryAdd(account.FullyQualifiedName, account);
        }

        var upsert = new List<WriteModel<QbEntity>>();
        foreach (var item in chartOfAccounts)
        {
            if (!updateExisting && localCache?.AccountIds != null && localCache.AccountIds.ContainsKey(item[2]))
            {
                result.Add($"{item[0]} #{item[2]}: Already synced");
                continue;
            }

            var parts = item[0].Split(":");
            var parentName = string.Join(':', parts[..^1]);
            if (!existing.TryGetValue(parentName, out var parentAccount) && parts.Length > 1)
            {
                _logger.LogError("Couldn't find {ParentAccount}, don't add {Account}", parentName, parts[^1]);
                result.Add($"{item[0]} #{item[2]}: Couldn't find {parentName}");
                continue;
            }

            if (existing.TryGetValue(item[2], out var existingAccount))
            {
                _logger.LogInformation("{QboAccount} ({QboAccountId}) already exists in QBO, skip", item[0], existingAccount.Id);

                if (existingAccount.ParentRef == null && parentAccount != null)
                {
                    _logger.LogInformation("Update parent of {Account} to {Parent}", item[2], parentName);

                    result.Add($"{item[0]} #{item[2]}: Quickbooks account is missing the parent account {parentName}");

                    if (!updateExisting)
                    {
                        existing[item[0]] = existingAccount;
                        continue;
                    }

                    // existing[existingAccount.FullyQualifiedName] = existingAccount;

                    existingAccount.ParentRef = new ReferenceType
                    {
                        Value = parentAccount?.Id,
                    };
                    existingAccount.SubAccount = true;
                    existingAccount.SubAccountSpecified = true;
                    existingAccount.Description = "Parent set by PI";
                }
                else if (existingAccount.ParentRef != null && parentAccount != null && existingAccount.ParentRef.Value != parentAccount.Id)
                {
                    existing[item[0]] = existingAccount;
                    result.Add($"{item[0]} #{item[2]}: Parent Mismatch {parentName}, {existingAccount.ParentRef.Value} => {parentAccount.Id}");
                    continue;
                }
                else
                {
                    result.Add($"{item[0]} #{item[2]}: Already exists in Quickbooks");
                    existing[item[0]] = existingAccount;
                    upsert.Add(BuildUpsertQuery(context, existingAccount).UpdateOneModel(true));
                    continue;
                }
            }

            if (existingAccount == null && existing.TryGetValue(item[0], out existingAccount))
            {
                // found match by name, but no account number
                if (!string.IsNullOrEmpty(existingAccount.AcctNum))
                {
                    result.Add($"{item[0]} #{item[2]}: Account mismatch {existingAccount.AcctNum} => {item[2]}");
                    _logger.LogError("Account mismatch: {Account} {ExistingAccountNum} => {ExpectedAccountNum}", item[0], existingAccount.AcctNum, item[2]);
                    continue;
                }

                result.Add($"{item[0]} #{item[2]}: Missing Account Number {item[2]}");
                if (!updateExisting)
                {
                    continue;
                }

                // existing[existingAccount.FullyQualifiedName] = existingAccount;

                _logger.LogInformation("Found existing {Account} without account number, try to update", item[0]);
                existingAccount.AcctNum = item[2];
                existingAccount.Description = "Account Number updated by PI";
            }

            var account = existingAccount ?? new Account
            {
                Name = parts[^1],
                Description = "Account created by PI",
                FullyQualifiedName = item[0],
                AccountType = item[1] switch
                {
                    "OCLIAB" => AccountTypeEnum.OtherCurrentLiability,
                    "INC" => AccountTypeEnum.Income,
                    "COGS" => AccountTypeEnum.CostofGoodsSold,
                    _ => throw new Exception($"{item[1]} unrecognized as account type"),
                },
                AccountTypeSpecified = true,
                AcctNum = item[2],
                SubAccount = parts.Length > 1,
                SubAccountSpecified = true,
                ParentRef = parts.Length > 1
                    ? new ReferenceType
                    {
                        Value = parentAccount?.Id,
                    }
                    : null,
            };

            var added = await ExportAsync(context, account);
            if (!added.IsSuccess)
            {
                _logger.LogError("Failed to Add/Update {AccountNumber} - {Account}", account.AcctNum, account.Name);
                result.Add($"{item[0]} #{item[2]}: Error upserting Account in Quickbooks");
                continue;
            }

            if (localCache?.AccountIds != null)
            {
                localCache.AccountIds[added.Value.FullyQualifiedName] = added.Value.Id;
            }

            if (existingAccount == null)
            {
                result.Add($"{item[0]} #{item[2]}: Account created in Quickbooks");
            }

            // update local cache
            existing[item[0]] = added.Value;
            upsert.Add(BuildUpsertQuery(context, added.Value).UpdateOneModel(true));
        }

        if (upsert.Count > 0)
        {
            _logger.LogInformation("Exported {AccountsCount}", upsert.Count);
            await _connection.BulkWriteAsync(upsert);
        }

        return result;
    }

    /// <summary>
    /// Get raw list of accounts expected to be in QBO for this account
    /// TODO: implement so each account can have a different list
    /// ...
    /// </summary>
    private IEnumerable<string[]> GetChartOfAccounts(IEntityContext context)
    {
        // "NAME", "ACCNTTYPE", "ACCNUM", "EXTRA" 
        yield return new[] { "Sales Tax Payable", "OCLIAB", "2150", "SALESTAX" };
        yield return new[] { "Sales", "INC", "4000", "" };
        yield return new[] { "Sales:Area Rug Sales", "INC", "4010", "" };
        yield return new[] { "Sales:Carpet Sales", "INC", "4020", "" };
        yield return new[] { "Sales:Ceramic and Stone Sales", "INC", "4030", "" };
        yield return new[] { "Sales:Hardwood Sales", "INC", "4040", "" };
        yield return new[] { "Sales:Laminate Sales", "INC", "4050", "" };
        yield return new[] { "Sales:Vinyl Sales", "INC", "4060", "" };
        yield return new[] { "Sales:Sales - Other", "INC", "4070", "" };
        yield return new[] { "Sales:Sales Discounts", "INC", "4080", "" };
        yield return new[] { "Sales:Installation Labor", "INC", "4090", "" };

        yield return new[] { "Cost of Goods Sold", "COGS", "5000", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs", "COGS", "5100", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Area Rugs", "COGS", "5110", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Carpet Material", "COGS", "5120", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Ceramic and Stone Material", "COGS", "5130", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Hardwood Material", "COGS", "5140", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Laminate Material", "COGS", "5150", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Vinyl Material", "COGS", "5160", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Material Costs - Other", "COGS", "5170", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Sales Tax on Material", "COGS", "5180", "" };
        yield return new[] { "Cost of Goods Sold:Material Costs:Early Payment Discount", "COGS", "5190", "" };
        yield return new[] { "Cost of Goods Sold:Installation Costs", "COGS", "5200", "" };
    }

    private async Task<IList<Account>> ImportAccountsAsync(IEntityContext context)
    {
        var list = await GetAllAsync<Account>(context);
        await UpsertAccountsAsync(context, list);
        return list;
    }

    private async Task UpsertAccountsAsync(IEntityContext context, IEnumerable<Account> list)
    {
        await _connection.BulkWriteAsync(getUpdates());

        IEnumerable<WriteModel<QbEntity>> getUpdates()
        {
            foreach (var account in list)
            {
                yield return BuildUpsertQuery(context, account)
                    .UpdateOneModel(true);
            }
        }
    }

    private UpdateQuery<QbEntity> BuildUpsertQuery(IEntityContext context, Account account)
    {
        var updateQuery = _connection.Filter<QbEntity>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.EntityId, context.OrganizationId)
                .Eq(x => x.FullyQualifiedName, account.FullyQualifiedName)
                .Eq(x => x.EntityType, nameof(Account))
                .Update
                .SetOnInsert(x => x.AccountId, context.AccountId)
                .SetOnInsert(x => x.EntityId, context.OrganizationId)
                .SetOnInsert(x => x.FullyQualifiedName, account.FullyQualifiedName)
                .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                .SetOnInsert(x => x.Id, Guid.NewGuid())
                .SetOnInsert(x => x.EntityType, nameof(Account))
                // .Set(x => x.Refs["QbAccountNumber"], account.AcctNum)
                .Set(x => x.ExternalId, account.Id)
                .Set(x => x.Properties, JsonObjectConverter.Convert<ExpandoObject>(account))
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            ;

        return updateQuery;
    }

    public async Task<CompanyInfo> GetCompanyInfoAsync(IEntityContext context)
    {
        var serviceContext = await GetServiceContextAsync(context);
        var querySvc = new QueryService<CompanyInfo>(serviceContext);
        var companyInfo = querySvc.ExecuteIdsQuery("SELECT * FROM CompanyInfo").FirstOrDefault();

        return companyInfo;
    }

    public async Task<List<QbEntity>> ExportVendorsAsync(IEntityContext context)
    {
        var feeds = await _connection.Filter<CatalogFeed>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Ne(x => x.IsActive, false)
            .FindAsync();

        var result = new List<QbEntity>();

        foreach (var feed in feeds)
        {
            var vendor = await ExportVendorAsync(context, feed);
            if (vendor.IsSuccess)
            {
                result.Add(vendor.Value);
            }
        }

        return result;
    }

    private async Task<Result<QbEntity>> ExportVendorAsync(IEntityContext context, CatalogFeed feed)
    {
        var vendor = await ExportAsync(context, new Vendor
        {
            DisplayName = feed.GetDisplayName(),
            CompanyName = feed.Description ?? feed.Name,
            AcctNum = feed.Id.ToString(),
        });

        if (!vendor.IsSuccess) return vendor.ConvertTo<QbEntity>();

        // var update = _connection.Filter<QbEntity>()
        //     .Eq(x => x.AccountId, context.AccountId)
        //     .Eq(x => x.EntityId, context.OrganizationId.Value)
        //     .Eq(x => x.FullyQualifiedName, vendor.Value.DisplayName)
        //     .Eq(x => x.EntityType, nameof(Vendor))
        //     .Update
        //     .SetOnInsert(x => x.AccountId, context.AccountId)
        //     .SetOnInsert(x => x.EntityId, context.OrganizationId.Value)
        //     .SetOnInsert(x => x.FullyQualifiedName, vendor.Value.DisplayName)
        //     .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
        //     .SetOnInsert(x => x.Id, Guid.NewGuid())
        //     .SetOnInsert(x => x.EntityType, nameof(Vendor))
        //     .Set(x => x.ExternalId, vendor.Value.Id)
        //     .Set(x => x.Properties, JsonObjectConverter.Convert<ExpandoObject>(vendor.Value))
        //     .Set(x => x.LastActor, context.Actor())
        //     .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //     .Set(x => x.Refs[nameof(CatalogFeed)], feed.Id);
        //
        // var entity = await update.UpdateAndGetOneAsync(true);
        // if (entity == null)
        // {
        //     return Result.Error<QbEntity>("Exported but couldn't add to local database");
        // }
        //
        // return Result.Success(entity);

        var qbEntity = await SaveAsync(context, vendor.Value, nameof(CatalogFeed), feed.Id, vendor.Value.DisplayName, vendor.Value.Id);
        return Result.Success(qbEntity);
    }

    public async Task<List<QbEntity>> ExportAllItemsAsync(IEntityContext context, CatalogFeed feed, bool forceUpdate)
    {
        var qb = new LocalCache();
        await LoadAccountsAsync(context, qb);

        var catalogItems = await _connection.Filter<CatalogItem>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.CatalogFeedId, feed.Id)
            .Ne(x => x.IsActive, false)
            .FindAsync();

        var cachedItems = (await _connection.Filter<QbEntity>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.OrganizationId.Value)
                .Eq(x => x.EntityType, nameof(Item))
                .In($"{nameof(QbEntity.Refs)}.{nameof(CatalogItem)}", catalogItems.Select(x => x.Id))
                .FindAsync())
            .ToDictionary(x => ((ObjectId)x.Refs[nameof(CatalogItem)]).ToGuid());

        var list = new List<QbEntity>();
        foreach (var item in catalogItems)
        {
            if (cachedItems.TryGetValue(item.Id, out var qbEntity))
            {
                _logger.LogInformation("{SKU} already cached.", item.SKU);
                if (!forceUpdate) continue;
            }

            // try to find match
            var qbItem = forceUpdate ? await GetItemAsync(context, item, qbEntity?.ExternalId) : null;
            if (qbItem != null)
            {
                if (qbItem.Sku != item.SKU)
                {
                    if (qbItem.Sku == null)
                    {
                        _logger.LogInformation("{ExternalId}: Missing {SKU}, set", qbItem.Id, item.SKU);
                        qbItem.Sku = item.SKU;
                    }
                    else
                    {
                        _logger.LogError("SKU Mismatch: {ExternalId}: {SKU} vs {CurrentSKU}", qbItem.Id, item.SKU, qbItem.Sku);
                        continue;
                    }
                }

                if (qbItem.Name != item.SKU)
                {
                    qbItem.Name = item.SKU;
                }
            }

            var result = await ExportAsync(context, qb, item, qbItem);
            if (result.IsSuccess)
            {
                _logger.LogInformation("{SKU} added", item.SKU);
                list.Add(result.Value);
                continue;
            }

            _logger.LogError("failed to add {SKU}: {Status}", item.SKU, result.Status);
        }

        return list;
    }

    private async Task<Item> GetItemAsync(IEntityContext context, CatalogItem item, string externalId)
    {
        var qbItem = default(Item);
        var sql = externalId != null ? $"SELECT * FROM Item WHERE Id = '{externalId}'" : $"SELECT * FROM Item WHERE Name='{item.Name}'";

        // update existing item
        var serviceContext = await GetServiceContextAsync(context);

        var querySvc = new QueryService<Item>(serviceContext);
        var found = querySvc.ExecuteIdsQuery(sql);

        if (found.Count == 0 && externalId == null)
        {
            // try to look by SKU
            sql = $"SELECT * FROM Item WHERE Sku='{item.SKU}'";
            found = querySvc.ExecuteIdsQuery(sql);
        }

        return found.Count == 1 ? found[0] : null;
    }

    public async Task<Result<Attachable>> AttachAsync(IEntityContext context, LocalCache qb, string sourceUrl, string fileName, IEnumerable<AttachableRef> refs)
    {
        var attachment = new Attachable
        {
            FileName = fileName,
            ContentType = "application/pdf",
            Category = AttachableCategoryEnum.Document.ToString(),
            AttachableRef = refs.ToArray(),
        };

        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
            var response = await Client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get document contents from {Url}", sourceUrl);
                return Result.Error<Attachable>("Failed to get document contents");
            }

            // Upload the attachment
            var ds = await GetDataServiceAsync(context, qb);
            var stream = await response.Content.ReadAsStreamAsync();
            Attachable result = ds.Upload(attachment, stream);
            return Result.Success(result);
        }
        catch (IdsException ex)
        {
            _logger.LogError(ex, "Failed to upload Attachment");
            return Result.Error<Attachable>(ex.Message);
        }
    }
}

public static class SfWorkOrderExtensions
{
    public static string GetDisplayName(this SfWorkOrder workOrder) => $"Project #{workOrder.ProjectNumber}";

    public static string GetFullyQualifiedName(this SfWorkOrder workOrder, string customerDisplayName)
        => string.IsNullOrEmpty(customerDisplayName) ? workOrder.GetDisplayName() : $"{customerDisplayName}:{workOrder.GetDisplayName()}";
}

public static class LeadExtensions
{
    public static string GetDisplayName(this Lead lead) => lead.Name;
}

public static class CatalogFeedExtensions
{
    public static string GetDisplayName(this CatalogFeed catalogFeed) => catalogFeed.Name;
}

public static class CatalogItemExtensions
{
    public static string GetFullyQualifiedName(this CatalogItem item) => item.SKU;
}