using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class AdditionalContext
{
    public Guid? LeadId { get; init; }
    public Guid? AppointmentId { get; init; }
    public string SfServiceAppointmentId { get; init; }
    public string SfWorkOrderId { get; init; }
    public string SfAccountId { get; init; }
    public string SfLeadId { get; init; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
}

public class ProcessObjectChange
{
    public Guid AccountId { get; init; }
    public string ExternalId { get; init; }
    public DateTime? Timestamp { get; init; }

    public AdditionalContext AdditionalContext { get; init; }
}

public class ImportObject
{
    public IEntityContext Context { get; init; }
    public SalesforceObjectType ObjectType { get; init; }
    public SalesforceCustomObject Source { get; init; }

    public AdditionalContext AdditionalContext { get; init; }
}

public abstract class LoadObjectOnChangeProcessor<T> : IObjectChangeProcessor
    where T : SalesforceCustomObject
{
    protected const string SystemModstamp = "SystemModstamp";

    protected readonly ILogger<LoadObjectOnChangeProcessor<T>> _logger;
    protected readonly MongoConnection _connection;
    protected readonly ObjectTypeService _objectTypeService;
    protected readonly SalesforceService _salesforceService;

    protected LoadObjectOnChangeProcessor(
        ILogger<LoadObjectOnChangeProcessor<T>> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        SalesforceService salesforceService
    )
    {
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _salesforceService = salesforceService;
    }

    public abstract string ObjectType { get; }

    protected abstract Task<IFlowObject> ImportObjectAsync(ImportObject options);

    public Task<IFlowObject> ImportObjectAsync(IEntityContext context, SalesforceObjectType objectType, SalesforceCustomObject source)
        => ImportObjectAsync(new ImportObject
        {
            Context = context,
            ObjectType = objectType,
            Source = source,
        });

    public async Task<(SalesforceCustomObject Source, IFlowObject Imported)> ImportObjectAsync(IEntityContext context, SalesforceObjectType objectType, string externalId)
    {
        var source = await _connection.Filter<T>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, externalId)
            .FirstOrDefaultAsync();

        if (source == null)
        {
            _logger.LogError("{ObjectType} not found with {ExternalId}", objectType.Name, externalId);
            return (null, null);
        }

        _logger.LogInformation("{ObjectType} with {ExternalId}: {Id}", objectType.Name, externalId, source.Id);

        var imported = await ImportObjectAsync(new ImportObject
        {
            Context = context,
            ObjectType = objectType,
            Source = source,
        });

        return (source, imported);
    }

    public virtual async Task<(SalesforceCustomObject, IFlowObject)> ProcessChangeAsync(Guid accountId, string externalId, DateTime? timestamp)
        => await ProcessChangeAsync(new ProcessObjectChange
        {
            AccountId = accountId, ExternalId = externalId, Timestamp = timestamp,
        });

    public virtual async Task<(SalesforceCustomObject, IFlowObject)> ProcessChangeAsync(ProcessObjectChange change)
    {
        using var scope = _logger.AddScope(new
        {
            change.AccountId,
            change.ExternalId,
            change.Timestamp
        });

        var context = new AccountContext(change.AccountId);
        var objectType = await _objectTypeService.GetAsync<SalesforceObjectType>(context, ObjectType);
        var sfObject = await _connection.Filter<T>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, change.AccountId)
            .Eq(x => x.ExternalId, change.ExternalId)
            .FirstOrDefaultAsync();

        var created = sfObject == null;

        if (sfObject == null)
        {
            _logger.LogInformation("Object not found locally, create it");
            sfObject = await CreateAsync(context, objectType, change.ExternalId, change.AdditionalContext);
        }
        else if (sfObject.TryGetProperty<DateTime>(SystemModstamp, out var sourceTimestamp) && change.Timestamp.HasValue && change.Timestamp <= sourceTimestamp)
        {
            _logger.LogInformation("Outdated notification: {NotificationTimeStamp} <= {SourceTimeStamp}, ignore", change.Timestamp, sourceTimestamp);
            sfObject = null;
        }
        else
        {
            _logger.LogInformation("Update existing {ObjectId}", sfObject.Id);
            sfObject = await UpdateAsync(context, objectType, sfObject, change.AdditionalContext);
        }

        if (sfObject == null)
        {
            _logger.LogInformation("Change ignored");
            return (null, null);
        }

        _logger.LogInformation("Object loaded: {ObjectId}", sfObject.Id);
        var result = await ImportObjectAsync(
            new ImportObject
            {
                Context = context,
                ObjectType = objectType,
                Source = sfObject,
                AdditionalContext = change.AdditionalContext,
            }
        );

        // fire event
        if (created)
        {
            _logger.LogInformation("Fire Object Created Event: {ExternalId}", sfObject.ExternalId);
            await _objectTypeService.FireCreateEventAsync(context, sfObject);
        }
        else
        {
            _logger.LogInformation("Fire Object Updated Event: {ExternalId}", sfObject.ExternalId);
            await _objectTypeService.FireObjectUpdatedAsync(context, sfObject, new Dictionary<string, object> { { nameof(SfWorkOrderObject.Properties), "*" } });
        }

        return (sfObject, result);
    }

    /// <summary>
    /// Update copy of object, salesforce.* 
    /// </summary>
    protected async Task<T> UpdateAsync(AccountContext context, SalesforceObjectType objectType, T current, AdditionalContext additionalContext)
    {
        return await _salesforceService.LoadObjectAsync<T>(context, objectType, current.ExternalId, entityId: additionalContext?.OrganizationId, leadId: additionalContext?.LeadId, assignedEntityId: additionalContext?.UserId);
    }

    /// <summary>
    /// Add copy of object, salesforce.* 
    /// </summary>
    protected async Task<T> CreateAsync(IEntityContext context, SalesforceObjectType objectType, string externalId, AdditionalContext additionalContext)
    {
        return await _salesforceService.LoadObjectAsync<T>(context, objectType, externalId, entityId: additionalContext?.OrganizationId, leadId: additionalContext?.LeadId, assignedEntityId: additionalContext?.UserId);
    }
}