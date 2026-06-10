using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Services;

public interface IValueMapperService
{
    ValueMapper CreateMaper(FieldMapperConfig field, string cmd, string[] args);
}

public class ValueMapperService : IValueMapperService
{
    private const string MATCH_CONDITION = @"^(?<field>\w+) \s* (?<op>[!=]=) \s* '?(?<value>[^']*)'?$";

    private readonly ILogger<ValueMapperService> _logger;
    private readonly MongoConnection _connection;
    private readonly IEntityIdentityAdapter _identityAdapter;
    private readonly IIntegrationLeadAdapter _integrationLeadAdapter;
    private readonly IEntityMetadataAdapter _entityMetadataAdapter;
    private readonly Dictionary<string, ValueMapper> _cache = new();

    public ValueMapperService(
        ILogger<ValueMapperService> logger,
        MongoConnection connection,
        IEntityIdentityAdapter identityAdapter,
        IIntegrationLeadAdapter integrationLeadAdapter,
        IEntityMetadataAdapter entityMetadataAdapter
    )
    {
        _logger = logger;
        _connection = connection;
        _identityAdapter = identityAdapter;
        _integrationLeadAdapter = integrationLeadAdapter;
        _entityMetadataAdapter = entityMetadataAdapter;
    }

    public ValueMapper CreateMaper(FieldMapperConfig field, string cmd, string[] args)
    {
        if (_cache.TryGetValue(field.Source, out var mapper))
        {
            return mapper;
        }

        if (args.Length != 4)
        {
            _logger.LogError("Unexpected # of arguments in {mapping}", field.Source);
            return null;
        }

        switch (args[0])
        {
            case "ExternalIdentity":
                // "=map(ExternalIdentity, EntityId, Provider=='InspireNet', ExternalId==branchId)"; 
                return ExternalIdentityMapper(field, cmd, args);

            case "IntegrationLead":
                // "=map(IntegrationLead, LeadId, Provider=='InspireNet', ExternalId==projectId)"; 
                return IntegrationLeadMapper(field, cmd, args);

            // case "EntityMetadata":
            //     // "=map(EntityMetadata, EntityId, Key=='ZipCode', Value==srcFieldName)"; 
            //     return EntityMetaDataMapper(field, cmd, args);

            case "CustomObject":
                // "=map(CustomObject, EntityId, ObjectType=='ZeeTerritory', ExternalId==srcFieldName)"; 
                return CustomObjectEntityMapper(field, cmd, args);
        }

        _logger.LogError("Invalid source in {mapping}", field.Source);
        return null;
    }

    /// <summary>
    /// Map entity using CustomObjects
    /// Only supports leads
    /// Only supports leads that by default are assigned to the account  (lead.EntityId) 
    /// </summary>
    private ValueMapper CustomObjectEntityMapper(FieldMapperConfig field, string cmd, string[] args)
    {
        // "=map(CustomObject, EntityId, ObjectType=='ZeeTerritory', ExternalId==srcFieldName)"; 
        // var (keyValue, srcPropName) = ParseInput(args, "CustomObject", "EntityId", "ObjectType", "ExternalId");

        if (args.Length != 4 ||
            !string.Equals(args[0], "CustomObject") ||
            !string.Equals(args[1], "EntityId"))
        {
            _logger.LogInformation("Failed to map {field}={map}: expected [CustomObject,EntityId]", field.Name, field.Source);
            return null;
        }

        var objectTypeField = Parse(args[2]);
        if (!string.Equals(objectTypeField?.Key, "ObjectType") || string.IsNullOrWhiteSpace(objectTypeField?.Value))
        {
            _logger.LogInformation("Failed to map {field}={map}: expected ObjectType='...'", field.Name, field.Source);
            return null;
        }

        var externalIdField = Parse(args[3]);
        if (!string.Equals(externalIdField?.Key, "ExternalId") || string.IsNullOrWhiteSpace(externalIdField?.Value))
        {
            _logger.LogInformation("Failed to map {field}={map}: expected ExternalId='...'", field.Name, field.Source);
            return null;
        }

        var objectType = objectTypeField.Value.Value;
        var srcPropName = externalIdField.Value.Value;

        ValueMapper fieldMapper = (config, body, lead) =>
        {
            // ugly hack but this code is on its way out 
            var externalId = objectType switch
            {
                "ZeeTerritory" => Lead.GetPostalCodeForLookup(lead[srcPropName]),
                _ => lead[srcPropName],
            };

            if (string.IsNullOrEmpty(externalId)) return null;

            // TODO: use IIndexedProperties so it will also work for other objects instead?
            // ... 
            var entityId = (lead as Lead)?.EntityId;
            if (!entityId.HasValue) return null;

            var match = _connection.Filter<CustomObject>()
                .Eq(x => x.AccountId, entityId)
                .Eq(x => x.ObjectType, objectType)
                .Eq(x => x.ExternalId, externalId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync().Result;

            if (match != null)
            {
                var value = match.EntityId;
                _logger.LogInformation("'{mapping}' produced '{target}' for '{source}'", field.Source, value, externalId);
                return value;
            }

            return null;
        };

        _logger.LogInformation("Created Value Mapper for {mapping}", field.Source);
        _cache[field.Source] = fieldMapper;

        return fieldMapper;
    }

    /// <summary>
    /// Map entity
    /// Only supports leads 
    /// </summary>
    [Obsolete]
    private ValueMapper EntityMetaDataMapper(FieldMapperConfig field, string cmd, string[] args)
    {
        // "=map(EntityMetaData, EntityId, Key=='ZipCode', Value==srcFieldName)"; 
        var (keyValue, srcPropName) = ParseInput(args, "EntityMetadata", "EntityId", "Key", "Value");
        if (keyValue == null || srcPropName == null)
        {
            _logger.LogInformation("Failed to map {field}: {map}", field.Name, field.Source);
            return null;
        }

        ValueMapper fieldMapper = (config, body, lead) =>
        {
            var source = lead[srcPropName];
            if (source == null) return null;

            var entityId = (lead as Lead)?.EntityId;
            if (!entityId.HasValue) return null;

            var matches = _entityMetadataAdapter.FindAsync(entityId.Value, keyValue, source).Result;
            var list = matches != null ? matches.ToArray() : null;
            if (list?.Length == 1)
            {
                var value = list[0].EntityId;
                _logger.LogInformation("'{mapping}' produced '{target}' for '{source}'", field.Source, value, source);
                return value;
            }

            return null;
        };

        _logger.LogInformation("Created Value Mapper for {mapping}", field.Source);
        _cache[field.Source] = fieldMapper;

        return fieldMapper;
    }

    private ValueMapper IntegrationLeadMapper(FieldMapperConfig field, string cmd, string[] args)
    {
        // "=map(IntegrationLead, LeadId, Provider=='InspireNet', ExternalId==selectionId)"; 
        var (provider, sourceField) = ParseInput(args, "IntegrationLead", "LeadId", "Provider", "ExternalId");
        if (provider == null || sourceField == null)
        {
            _logger.LogInformation("Failed to map {field}: {map}", field.Name, field.Source);
            return null;
        }

        ValueMapper fieldMapper = (config, body, lead) =>
        {
            var source = lead[sourceField];
            if (source == null) return null;

            var integrationLead = _integrationLeadAdapter.FindAsync(provider, source).Result;
            var value = integrationLead?.LeadId;

            _logger.LogInformation("'{mapping}' produced '{target}' for '{source}'", field.Source, value, source);
            return value;
        };

        _logger.LogInformation("Created Value Mapper for {mapping}", field.Source);
        _cache[field.Source] = fieldMapper;

        return fieldMapper;
    }

    private ValueMapper ExternalIdentityMapper(FieldMapperConfig field, string cmd, string[] args)
    {
        // "=map(ExternalIdentity, EntityId, Provider=='InspireNet', ExternalId==branchId)"; 
        var (provider, sourceField) = ParseInput(args, "ExternalIdentity", "EntityId", "Provider", "ExternalId");
        if (provider == null || sourceField == null)
        {
            _logger.LogInformation("Failed to map {field}: {map}", field.Name, field.Source);
            return null;
        }

        ValueMapper fieldMapper = (config, body, lead) =>
        {
            var source = lead[sourceField];
            if (source == null) return null;

            // TODO: should constraint to account 
            // ...
            var (entity, identity) = _identityAdapter.FindAsync(RootContext.Context, provider, source).Result;
            var value = entity?.Id;

            _logger.LogInformation("'{mapping}' produced '{target}' for '{source}'", field.Source, value, source);
            return value;
        };

        _logger.LogInformation("Created Value Mapper for {mapping}", field.Source);
        _cache[field.Source] = fieldMapper;

        return fieldMapper;
    }

    private (string, string) ParseInput(string[] args, string table, string field, string where1, string where2)
    {
        if (args.Length != 4 ||
            !string.Equals(args[0], table) ||
            !string.Equals(args[1], field))
        {
            return (null, null);
        }

        return (ParseKeyValue(args[2], where1), ParseKeyValue(args[3], where2));
    }

    private string ParseKeyValue(string value, string expectedKey)
    {
        var parsed = Parse(value);
        return string.Equals(parsed?.Key, expectedKey) ? parsed?.Value : null;
    }

    private KeyValuePair<string, string>? Parse(string value)
    {
        var matches = Regex.Matches(value, MATCH_CONDITION, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        if (matches.Count != 1) return null;
        return new KeyValuePair<string, string>(matches[0].Groups[1].Captures[0].Value, matches[0].Groups[3].Captures[0].Value);
    }
}