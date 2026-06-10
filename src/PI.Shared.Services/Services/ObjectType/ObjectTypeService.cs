using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using CsvHelper;
using Messages.Flow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Constants;
using PI.Shared.ContractResolvers;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Json;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Requests;
using PI.Shared.Services.DataProtection;
using Result = PI.Shared.Models.Result;

namespace PI.Shared.Services;

public partial class ObjectTypeService
{
    /// <summary>
    /// Json serialization to allow converting between the "database representation" (ExpandoObject) and C# Types
    /// </summary>
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new AllPropertiesWithUnderlyingNameContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Converters =
        [
            new FlagsEnumConverter(),
            new DefaultEnumJsonConverter(),
            new Decimal128Converter(), // TODO: probably needs something special  
        ]
    };

    /// <summary>
    /// Json serialization to allow converting between the "database representation" (ExpandoObject) and "API" representation
    /// </summary>
    private static readonly JsonSerializerSettings ToJsonResultSettings = new()
    {
        ContractResolver = new AlwaysUseUnderlyingPropertyNameContractResolver(), // should it also use AllPropertiesWithUnderlyingNameContractResolver?
        NullValueHandling = NullValueHandling.Ignore,
        Converters =
        [
            new FlagsEnumConverter(),
            new DefaultEnumJsonConverter(),
            new Decimal128Converter(), // force decimal128 to be serialized as numbers
            new ObjectIdConverter(), // will serialize objectids as UUIDs strings
        ]
    };

    public class AddObjectResult
    {
        public ExpandoObject Object { get; init; }
        public Guid ObjectId { get; init; }
        public bool Existing { get; init; }
        public bool Skipped { get; init; }
        public GenericFlowEvent FiredEvent { get; init; }

        public IDictionary<string, object> UpdatedFields { get; set; }
    }

    public class AddObjectOptions : GetFormOptions
    {
        public Func<IDictionary<string, object>, Result<IDictionary<string, object>>> OnBeforeSerializing { get; init; }
        public bool IsUpsert { get; init; }
        public bool IsImporting { get; init; }
        public bool SkipObjectTypeValidation { get; init; }

        public bool AllowInitialValueOverride { get; init; }
        
        /// <summary>
        /// Process event before is dispatched
        /// </summary>
        public Action<GenericFlowEvent> PrepareEvent { get; init; }

        public AddObjectOptions()
        {
        }

        public AddObjectOptions(GetFormOptions options) : base(options)
        {
        }

        public AddObjectOptions(GetObjectOptions options) : base(options)
        {
        }
    }

    public class UpdateObjectResult
    {
        public ExpandoObject Object { get; init; }
        public bool Skipped { get; init; }
        public IDictionary<string, object> UpdatedFields { get; init; }
        public GenericFlowEvent FiredEvent { get; init; }
    }

    public class UpdateObjectOptions : GetFormOptions
    {
        public bool SkipObjectTypeValidation { get; init; }

        /// <summary>
        /// Patch, only update fields that got a non-null value
        /// </summary>
        public bool PartialUpdate { get; init; }

        public UpdateObjectOptions()
        {
        }

        public UpdateObjectOptions(GetFormOptions options) : base(options)
        {
        }

        public UpdateObjectOptions(GetObjectOptions options) : base(options)
        {
        }
    }

    public class GetValuesFromInputOptions
    {
        public IDictionary<string, object> Input { get; init; }
        public bool ExcludeNulls { get; init; }
        public GetFormOptions GetFormOptions { get; init; }

        public bool AllowInitialValueOverride { get; init; }
        
        public GetValuesFromInputOptions WithInput(IDictionary<string, object> input) => new()
        {
            Input = input,
            ExcludeNulls = ExcludeNulls,
            GetFormOptions = GetFormOptions,
            AllowInitialValueOverride = AllowInitialValueOverride,
        };
    }

    /// <summary>
    /// Convert a "class" into what would be the Database representation of it
    /// tries to simulate what the "mongo driver" would do with the "our settings" 
    /// ** far from perfect
    /// </summary>
    public static ExpandoObject AsSerialized(object obj) => JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(obj, JsonSerializerSettings));

    private readonly ILogger<ObjectTypeService> _logger;
    private readonly MongoConnection _connection;
    private readonly IMessageBroker _messageBroker;
    private readonly DataProtectionService _dataProtectionService;
    private readonly IEntityIdentityAdapter _identityAdapter;

    /// <summary>
    /// Get object type by id
    /// </summary>
    public async Task<ObjectType> GetAsync(IEntityContext context, Guid id, GetObjectOptions opts = null)
    {
        var result = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, id)
            // .SortAsc(x => x.AccountId)
            .FirstOrDefaultAsync();

        await LoadBaseObjectAsync(_connection, context, result, opts);

        return result;
    }

    public Query<ObjectType> Query(IEntityContext context, string fullName, bool includeSuperNamespaces = false)
        => Query<ObjectType>(_connection, context, fullName, includeSuperNamespaces);

    public Query<T> Query<T>(IEntityContext context, string fullName, bool includeSuperNamespaces = false)
        where T : ObjectType
        => Query<T>(_connection, context, fullName, includeSuperNamespaces);

    public static Query<ObjectType> Query(MongoConnection connection, IEntityContext context, string fullName, bool includeSuperNamespaces = false)
        => Query<ObjectType>(connection, context, fullName, includeSuperNamespaces);

    private static Query<T> Query<T>(MongoConnection connection, IEntityContext context, string fullName, bool includeSuperNamespaces = false)
        where T : ObjectType
    {
        var query = connection.Filter<T>()
                .Eq(x => x.AccountId, context.AccountId)
            ;

        var parts = fullName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            // no namespace
            return query.Eq(x => x.Name, fullName)
                .Eq(x => x.Namespace, null);
        }

        query.Eq(x => x.Name, parts[^1]);

        if (!includeSuperNamespaces) // || parts.Length == 2
        {
            // only exact namespace
            return query.Eq(x => x.Namespace, string.Join('.', parts, 0, parts.Length - 1));
        }

        return query
                .In(x => x.Namespace, getNamespaces())
                .SortDesc(x => x.Namespace) // so the most specific (longer namespace) will win, null will be last
            ;

        IEnumerable<string> getNamespaces()
        {
            yield return null;

            var ns = "";
            for (var c = 0; c < parts.Length - 1; c++)
            {
                ns = c == 0 ? parts[c] : $"{ns}.{parts[c]}";
                yield return ns;
            }
        }
    }

    /// <summary>
    /// Get object type by name 
    /// </summary>
    public static Task<ObjectType> GetAsync(MongoConnection connection, IEntityContext context, string objectType, GetObjectOptions opts = null)
        => GetAsync<ObjectType>(connection, context, objectType, opts);

    public Task<T> GetAsync<T>(IEntityContext context, string objectType, GetObjectOptions opts = null) where T : ObjectType
        => GetAsync<T>(_connection, context, objectType, opts);

    /// <summary>
    /// Get object type by name
    /// </summary>
    private static async Task<T> GetAsync<T>(MongoConnection connection, IEntityContext context, string objectType, GetObjectOptions opts = null)
        where T : ObjectType
    {
        var cached = opts?.GetFromCache(objectType);
        if (cached is T result) return result;

        result = await Query<T>(connection, context, ObjectType.GetFullName(objectType, opts?.Namespace), opts?.IncludeSuperNamespaces ?? false)
            .FirstOrDefaultAsync();

        if (opts?.LoadBaseObject ?? true)
        {
            await LoadBaseObjectAsync(connection, context, result, opts);
        }

        opts?.OnObjectTypeLoaded(result);

        return result;
    }

    private static async Task LoadBaseObjectAsync(MongoConnection connection, IEntityContext context, ObjectType objectType, GetObjectOptions opts = null)
    {
        if (string.IsNullOrEmpty(objectType?.BaseObjectType)) return;

        // load base type
        var baseType = await GetAsync(connection, context, objectType.BaseObjectType, opts);
        if (baseType == null) throw new NotFoundException($"Base Object Type {objectType.BaseObjectType} not found");

        Merge(baseType, objectType);
    }

    public static void Merge(ObjectType baseType, ObjectType intoObjectType)
    {
        // cache base object type
        intoObjectType.LoadedBaseObjectType = baseType;

        // merge fields
        foreach (var field in baseType.Fields)
        {
            intoObjectType.OverriddenFields ??= new Dictionary<string, FieldOverride>();

            if (intoObjectType.Fields.TryGetValue(field.Key, out var existing))
            {
                // merge
                intoObjectType.OverriddenFields[field.Key] = MergeField(existing, field.Value);
            }
            else
            {
                // add property
                intoObjectType.Fields.Add(field.Key, field.Value);
                intoObjectType.OverriddenFields[field.Key] = FieldOverride.None;
            }
        }

        if (baseType.Constraints != null)
        {
            intoObjectType.Constraints ??= new Dictionary<string, Criteria>();
            foreach (var constraint in baseType.Constraints)
            {
                if (intoObjectType.Constraints.TryGetValue(constraint.Key, out var existing))
                {
                    // add any missing 
                    var result = (IEnumerable<Condition>)existing.Conditions;
                    foreach (var condition in constraint.Value.Conditions)
                    {
                        if (existing.Conditions.Any(x => x.FieldName == condition.FieldName)) continue;
                        result = result.Append(condition);
                    }

                    existing.Conditions = result.ToArray();
                }
                else
                {
                    intoObjectType.Constraints.Add(constraint.Key, constraint.Value);
                }
            }
        }

        // only copy explicit (admin added) relations
        var baseRelations = baseType.RelatedObjectTypes?.Where(x => x.RelationType is RelationType.OneToMany or RelationType.OneToOne).ToArray();
        if (baseRelations?.Length > 0)
        {
            var existing = (intoObjectType.RelatedObjectTypes ?? Array.Empty<RelatedObjectType>()).ToList();
            foreach (var rot in baseRelations)
            {
                existing.Add(rot);
            }

            intoObjectType.RelatedObjectTypes = existing.ToArray();
        }

        if (baseType.UniqueIndices?.Length > 0)
        {
            intoObjectType.UniqueIndices = (intoObjectType.UniqueIndices ?? Enumerable.Empty<UniqueIndex>())
                .Concat(baseType.UniqueIndices)
                .ToArray();
        }

        // if the base is full text searchable, the child will be as well
        intoObjectType.IsFullTextSearchable |= baseType.IsFullTextSearchable;

        // copy indices
        if (baseType.Indices?.SearchIndex != null)
        {
            intoObjectType.Indices ??= new Indices();
            intoObjectType.Indices.SearchIndex ??= baseType.Indices.SearchIndex;
        }
    }

    public static FieldOverride MergeField(FieldTemplate existing, FieldTemplate baseField)
    {
        var overrides = FieldOverride.None;

        // copy base properties if not defined
        if (existing.Field == null)
        {
            existing.Field = baseField.Field;
        }
        else
        {
            overrides |= FieldOverride.Field;
        }

        if (existing.RBAC.IsEmpty())
        {
            existing.RBAC = baseField.RBAC;
        }
        else
        {
            overrides |= FieldOverride.RBAC;
        }

        if (existing.Field.Options == null)
        {
            if (existing.Field.GetType() == baseField.Field.GetType())
            {
                existing.Field.Options ??= baseField.Field.Options;
            }
        }
        else
        {
            overrides |= FieldOverride.Options;
        }

        existing.Indexed |= baseField.Indexed;

        if (existing.InitialValue == null)
        {
            existing.InitialValue = baseField.InitialValue;
        }
        else
        {
            // overrides |= FieldOverride.InitialValue;
        }

        if (existing.CalculatedValue == null)
        {
            existing.CalculatedValue = baseField.CalculatedValue;
        }
        else
        {
            // overrides |= FieldOverride.CalculatedValue;
        }

        return overrides;
    }

    public ObjectTypeService(
        ILogger<ObjectTypeService> logger,
        MongoConnection connection,
        IMessageBroker messageBroker,
        DataProtectionService dataProtectionService,
        IEntityIdentityAdapter identityAdapter
    )
    {
        _logger = logger;
        _connection = connection;
        _messageBroker = messageBroker;
        _dataProtectionService = dataProtectionService;
        _identityAdapter = identityAdapter;
    }

    public Task<ObjectType> GetObjectTypeAsync<T>(IEntityContext context)
        => GetAsync(context, typeof(T).Name, null);

    public Task<ObjectType> GetAsync(IEntityContext context, string objectType, GetObjectOptions opts = null) => GetAsync(_connection, context, objectType, opts);

    public async Task<List<ObjectStatus>> GetStatusesAsync(IEntityContext context, string objectType, GetObjectOptions opts = null)
    {
        var row = await GetAsync(context, objectType, opts);
        if (row == null) throw new NotFoundException($"Invalid Object Type: {objectType}");

        return await _connection.Filter<ObjectStatus>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.ObjectType, objectType)
            .SortAsc(x => x.Name)
            .FindAsync();
    }

    /// <summary>
    /// Get User action ONLY using the flow
    /// </summary>
    public async Task<EventType> GetUserActionUsingFlowAsync(IEntityContext context, Guid flowId, Guid eventTypeId, Guid? objectStatusId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        return await GetUserActionAsync(context, flow, eventTypeId, objectStatusId);
    }

    /// <summary>
    /// Get User action (shouldn't be here as it has nothing to do with object but... ) 
    /// </summary>
    public async Task<EventType> GetUserActionAsync(IEntityContext context, string objectTypeName, Guid eventTypeId)
    {
        return await _connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, eventTypeId)
            .Eq(x => x.ObjectType, objectTypeName)
            .In(x => x.EntityId, context.GetEntityIds())
            .OfTypeBuilder<EventType, Trigger, UserTrigger>(x => x.Trigger, q => UserTriggerQuery(context, q))
            .FirstOrDefaultAsync();
    }

    private async Task<EventType> GetUserActionAsync(IEntityContext context, Flow flow, Guid eventTypeId, Guid? objectStatusId)
    {
        var step = flow.Steps?
                .Where(x => !x.CurrentStatusId.HasValue || !objectStatusId.HasValue || x.CurrentStatusId.Value == objectStatusId.Value)
                .FirstOrDefault(x => x.EventIdTrigger == eventTypeId)
            ;

        if (step == null)
        {
            _logger.LogError("{EventTypeId} is not configured in {FlowId} / {ObjectStatusId}", eventTypeId, flow.Id, objectStatusId);
            return null;
        }

        switch (context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Manager:
            case EntityRoleId.User:
            case EntityRoleId.Profile:
                break;

            default:
                // invalid context to run actions
                throw new ForbiddenException(context);
        }

        var action = await GetUserActionAsync(context, flow.ObjectType, eventTypeId);
        if (action?.Trigger is not UserTrigger userTrigger) return null;

        if (objectStatusId.HasValue && userTrigger.ObjectStatusId.HasValue && objectStatusId.Value != userTrigger.ObjectStatusId.Value)
        {
            _logger.LogError("{EventTypeId} is limited to {ObjectStatusId}", eventTypeId, userTrigger.ObjectStatusId);
            return null;
        }

        return action;
    }

    /// <summary>
    /// Get User action enforcing access
    /// </summary>
    public async Task<EventType> GetUserActionUsingObjectTypeAsync(IEntityContext context, ObjectType objectType, Guid eventTypeId, Guid? objectStatusId)
    {
        switch (context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Manager:
            case EntityRoleId.User:
            case EntityRoleId.Profile:
                break;

            default:
                // invalid context to run actions
                throw new ForbiddenException(context);
        }

        var allTypes = objectType.GetLoadedBaseObjectTypeNames()
            .Append(objectType.FullName)
            .ToArray();

        // it may already be doing this since we are getting the object status id as a parameter
        // TODO: should load object and use status on it to confirm it should be available?
        // ...

        // as now it will include even if won't do anything for the object because it is not configured in the flow
        // TODO: should it somehow limit based on whether it is configured in the flow or not?
        // ... 

        var action = await _connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.EntityId, context.GetEntityIds())
            .In(x => x.ObjectType, allTypes)
            .OfTypeBuilder<EventType, Trigger, UserTrigger>(x => x.Trigger, userTriggerQuery)
            .Eq(x => x.Id, eventTypeId)
            .FirstOrDefaultAsync();

        return action;

        void userTriggerQuery(Query<UserTrigger> q)
        {
            UserTriggerQuery(context, q);

            if (objectStatusId.HasValue) q.In(x => x.ObjectStatusId, [null, objectStatusId]);
        }
    }

    public static string ProcessNextUrl(IEntityContext context, IDictionary<string, object> handlebarsContext, string urlStr)
    {
        if (string.IsNullOrWhiteSpace(urlStr)) return urlStr;

        var index = urlStr.IndexOf("?", StringComparison.Ordinal);
        if (index < 0)
        {
            if (!ExpressionEvaluatorService.TryResolve(context, handlebarsContext, urlStr, out var resolvedValue))
            {
                // failed
                return null;
            }

            return resolvedValue?.ToString();
        }

        var query = urlStr[index..];
        var queryArgs = query.Split("&", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var c = 0; c < queryArgs.Length; c++)
        {
            var parts = queryArgs[c].Split("=");
            switch (parts.Length)
            {
                case 2: // k=v
                {
                    if (!ExpressionEvaluatorService.TryResolve(context, handlebarsContext, parts[1], out var resolvedValue) || resolvedValue == null)
                    {
                        // failed to resolve
                        return null;
                    }

                    queryArgs[c] = $"{parts[0]}={Uri.EscapeDataString(resolvedValue.ToString())}";
                    break;
                }

                default:
                {
                    if (!ExpressionEvaluatorService.TryResolve(context, handlebarsContext, parts[c], out var resolvedValue))
                    {
                        // failed to resolve
                        return null;
                    }

                    queryArgs[c] = resolvedValue?.ToString();
                    break;
                }
            }
        }

        if (!ExpressionEvaluatorService.TryResolve(context, handlebarsContext, urlStr[..index], out var resolvedUrl) || resolvedUrl == null)
        {
            // failed
            return null;
        }

        return $"{resolvedUrl}{string.Join("&", queryArgs)}";
    }

    /// <summary>
    /// get user actions to the "parent object" of the items in the view
    /// </summary>
    [Obsolete("use overload that loads object")]
    public async Task<List<MenuItem>> GetUserActionsForObjectAsync(
        IEntityContext context,
        IFlowObject rootObject,
        string actionPath = null
    )
    {
        var objectType = await GetAsync(context, rootObject.ObjectType);
        if (objectType == null) throw NotFoundException.New("Object type not found");

        var items = new List<MenuItem>();

        switch (context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Manager:
            case EntityRoleId.User:
            case EntityRoleId.Profile:
                break;

            default:
                // invalid context to run actions
                return items;
        }

        var allTypes = objectType.GetLoadedBaseObjectTypeNames()
            .Append(objectType.FullName)
            .ToArray();

        var list = await _connection.Filter<EventType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.EntityId, context.GetEntityIds())
            .In(x => x.ObjectType, allTypes)
            .OfTypeBuilder<EventType, Trigger, UserTrigger>(x => x.Trigger, q =>
                UserTriggerQuery(context, q)
                    .Ne(x => x.IsHidden, true)
                    .In(x => x.ObjectStatusId, new[] { default(Guid?), rootObject.ObjectStatusId })
            )
            .FindAsync();

        // actionPath ??= $"api/v1/{rootObject.ObjectType}({rootObject.Id})";

        if (list.Count > 0)
        {
            foreach (var item in list)
            {
                if (item.Trigger is not UserTrigger trigger) continue;

                var baseActionPath = actionPath ?? $"api/v1/{item.ObjectType}({rootObject.Id})";
                items.Add(new ActionMenuItem
                {
                    Name = trigger.Name,
                    Action = $"dataForm://{baseActionPath}/Action({item.Id})",
                    Enable = new[] { "selectedCount=='0'" },

                    // TODO: disable based on status, instead of filtering out?
                    // ....
                });
            }
        }

        return items;
    }

    /// <summary>
    /// Get trigger subQuery to limit user actions to the ones the context has access
    /// </summary>
    /// <param name="context"></param>
    /// <param name="q"></param>
    private Query<UserTrigger> UserTriggerQuery(IEntityContext context, Query<UserTrigger> q)
    {
        q.In(x => x.Role, new[] { default(EntityRoleId?), context.Role });

        if (context.Role == EntityRoleId.Profile)
        {
            if (context.AllProfileIds.Length > 1)
            {
                q.AnyIn(x => x.ProfileIds, context.AllProfileIds);
            }
            else
            {
                q.AnyEq(x => x.ProfileIds, context.ProfileId.Value);
            }

            return q;
        }

        if (context.ProfileId.HasValue)
        {
            if (context.AllProfileIds.Length > 1)
            {
                q.In(nameof(UserTrigger.ProfileIds), context.AllProfileIds.Select(x => (Guid?)x).Append(null));
            }
            else
            {
                q.In(nameof(UserTrigger.ProfileIds), [default(Guid?), context.ProfileId.Value]);
            }
        }
        else
        {
            q.Exists(x => x.ProfileIds, false);
        }

        return q;
    }

    public async Task<Guid[]> BulkTagAsync(IContextWithActor context, ObjectType objectType, Guid[] ids, string[] tags, bool remove = false)
    {
        // TODO: check if the ObjectType allows bulk tag
        // ...

        var tagField = objectType.Fields.Values.FirstOrDefault(x => x.Field is TagsField && x.RBAC.CanUpdate(context));
        if (tagField == null) throw new BadRequestException("Missing tag field");

        var modified = Enumerable.Empty<Guid>();
        foreach (var id in ids)
        {
            var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                    .AddConstraints(context, objectType)
                    .Eq(Model.IdFieldName, id)
                    .Update
                ;

            var propertyPath = FormField.GetPathInCollection(tagField.Field.Name);
            if (remove)
            {
                query.PullAll(propertyPath, tags);
            }
            else
            {
                query.AddToSetEach(propertyPath, tags);
            }

            // do not add tags, as we don't have the resolved result (just the delta)
            var modifiedFields = new Dictionary<string, object>
            {
                { tagField.Field.Name, "[...]" }
            };

            var calcFields = objectType
                .Fields
                .Where(x => x.Value.CalculatedValue != null)
                .Select(x => x.Value)
                .ToArray();

            if (calcFields.Length > 0)
            {
                foreach (var field in calcFields)
                {
                    if (!TryResolveExpression(context, field, modifiedFields, field.CalculatedValue, out var modifiedValue)) continue;

                    query.SetOrUnset(FormField.GetPathInCollection(field.Field.Name), modifiedValue);

                    modifiedFields.Add(field.Field.Name, modifiedValue);
                }
            }

            var updateResult = await query.UpdateAndGetOneAsync();
            if (updateResult == null) continue;

            await FireObjectUpdatedAsync(context, objectType, updateResult, id, modifiedFields, e =>
            {
                e.Description = remove ? "Tags Removed" : "Tags Added";
                e.TryAddMetaValue(tagField.Field.Name, string.Join(", ", tags));
            });

            modified = modified.Append(id);
        }

        return modified.ToArray();
    }

    public async Task<IEnumerable<ReferenceValue>> LookupTagsAsync(IEntityContext context, ObjectType objectType, DataViewRequest request)
    {
        var value = request.Criteria?.FirstOrDefault(x => x.FieldName == Condition.AutoComplete)?.Value.ToString();

        if (string.IsNullOrEmpty(value) && context.UserId.HasValue)
        {
            // recent tags
            var result = await _connection.Filter<ObjectTypeUserSettings>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.EntityId, context.UserId.Value)
                .Eq(x => x.ObjectType, objectType.FullName)
                .Eq(x => x.Hash, null)
                .FirstOrDefaultAsync();

            return result?.Tags?
                .Reverse()
                .Select(x => new ReferenceValue
                {
                    Id = x,
                    Value = x,
                }) ?? Enumerable.Empty<ReferenceValue>();
        }

        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .AddConstraints(context, objectType)
            ;

        query.Regex(
            "Tags",
            string.IsNullOrEmpty(value) ? new BsonRegularExpression("^[a-z0-9]", "i") : new BsonRegularExpression($"^{value}", "i")
        );

        var cursor = await query.DistinctAsync<string>("Tags");

        var ret = new List<string>();
        while (await cursor.MoveNextAsync())
        {
            foreach (var row in cursor.Current)
            {
                if (value != null && !row.StartsWith(value, StringComparison.OrdinalIgnoreCase)) continue;

                ret.Add(row);
            }
        }

        return ret
            .OrderBy(x => x)
            .Select(x => new ReferenceValue
            {
                Id = x,
                Value = x,
            });
    }

    private async Task<Form.Models.Form> GetCustomizedFormAsync(IEntityContext context, ObjectType objectType, FormName formName, GetFormOptions opts = null)
    {
        switch (formName)
        {
            case FormName.Add:
                if (!objectType.CanCreate(context)) throw new ForbiddenException(context, $"{objectType.FullName}: {formName}");
                break;

            case FormName.Edit:
                if (!objectType.CanUpdate(context)) throw new ForbiddenException(context, $"{objectType.FullName}: {formName}");
                break;

            case FormName.View:
            case FormName.Details:
                if (!objectType.CanRead(context))
                {
                    // throw new ForbiddenException(context, $"{objectType.FullName}: {formName}");
                    return new Form.Models.Form
                    {
                        Title = objectType.Description ?? objectType.Name,
                        Fields = new FormField[]
                        {
                            new LabelField
                            {
                                Name = "Error",
                                Label = "Access Forbidden",
                                LabelFieldOptions = new LabelFieldOptions
                                {
                                    Color = PalletColor.Error,
                                }
                            }
                        },
                        Actions = Array.Empty<FormAction>(),
                        ObjectType = objectType.FullName,
                    };
                }

                break;
        }

        var form = await LoadCustomFormAsync(context, objectType.FullName, objectType.GetFormName(formName), opts);
        form?.Form?.ObjectType = objectType.FullName;
        
        return form?.Form;
    }

    private async Task<AppForm> LoadCustomFormAsync(IEntityContext context, string objectTypeName, string formName, GetFormOptions opts = null)
    {
        // try to get from cache
        var form = opts?.GetCustomFormCache(objectTypeName, formName);
        if (form != null) return form;

        // load from db if not found
        form = await _connection.GetProfileElementAsync<AppForm>(context,
            q => q
                .Eq(x => x.Name, formName)
                .Eq(x => x.ObjectType, objectTypeName)
        );

        // add to cache
        opts?.OnCustomFormLoaded(form ?? new AppForm
        {
            ObjectType = objectTypeName,
            Name = formName,
        });
        
        return form;
    }

    /// <summary>
    /// build subDocuments from field paths to avoid adding fields with "."s instead of subDocuments
    /// AND removing nulls
    /// </summary>
    public static Result<IDictionary<string, object>> AggregateSubDocumentsForCreation(IDictionary<string, object> flat)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in flat)
        {
            if (kvp.Value == null)
            {
                continue;
            }

            var error = SetValue(result, kvp.Key, kvp.Value);
            if (error != null) return Result.Error<IDictionary<string, object>>($"Failed to update {kvp.Key}: {error}");
        }

        return Result.Success<IDictionary<string, object>>(result);
    }

    /// <summary>
    /// make sure the object type is configured with all the latest requirements
    /// - existing _id field with initial value
    /// - Constraints (must not be null)
    /// - FlowId and ObjectStatusId without initial values (if it is defined in the object type)
    /// </summary>
    public string ValidateObjectType(ObjectType objectType)
    {
        var errors = validate().ToArray();
        return errors.Length > 0 ? string.Join("; ", errors) : null;

        IEnumerable<string> validate()
        {
            if (objectType.IsEmbedded)
            {
                if (!notExistOrHasInitialValue(Model.IdFieldName))
                {
                    yield return $"{Model.IdFieldName} field not configured";
                }
            }
            else
            {
                if (objectType.Fields == null || !objectType.Fields.TryGetValue(Model.IdFieldName, out var field))
                {
                    yield return $"{Model.IdFieldName} field not configured";
                }
                else if (field.InitialValue == null)
                {
                    if (!field.RBAC[EntityRoleId.Admin].HasFlag(FieldPermission.SetOnCreate))
                    {
                        yield return $"{Model.IdFieldName} field not configured with initial value and can't be set on create";
                    }
                }
                // if (!existsAndHasInitialValue(Model.IdFieldName))
                // {
                //     yield return $"{Model.IdFieldName} field not configured";
                // }
            }

            if (!objectType.IsEmbedded && objectType.Constraints == null)
            {
                yield return "No constraints configured for object type";
            }

            // if (objectType.InitialFlowId.HasValue && !existsAndHasInitialValue(nameof(IFlowObject.FlowId)))
            // {
            //     yield return "FlowId field is not configured with an initial value";
            // }
            //
            // if (objectType.InitialObjectStatusId.HasValue && !existsAndHasInitialValue(nameof(IFlowObject.ObjectStatusId)))
            // {
            //     yield return "ObjectStatusId field is not configured with an initial value";
            // }

            if (objectType.UniqueExternalId && objectType.UniqueIndices == null)
            {
                yield return "UniqueExternalId is no longer supported, set UniqueIndices";
            }

            if (objectType.CollectionName == nameof(CustomObject))
            {
                var customFields = new[]
                {
                    nameof(Model.CreatedOn),
                    nameof(CustomObject.ObjectTypeId),
                    nameof(CustomObject.ObjectType),
                    nameof(CustomObject.LastActor)
                };

                foreach (var f in customFields)
                {
                    if (!notExistOrHasInitialValue(f))
                    {
                        yield return $"{f} field not configured";
                    }
                }
            }
        }

        bool existsAndHasInitialValue(string fieldName) => objectType.Fields != null && (objectType.Fields.TryGetValue(fieldName, out var field) && field.InitialValue != null);
        bool notExistOrHasInitialValue(string fieldName) => objectType.Fields != null && (!objectType.Fields.TryGetValue(fieldName, out var field) || field.InitialValue != null);
    }

    /// <summary>
    /// Hack to add a Model using the service configuration 
    /// </summary>
    public async Task<Result<AddObjectResult>> AddObjectAsync(IEntityContext context, ObjectType objectType, Model input, AddObjectOptions options)
    {
        IDictionary<string, object> expando = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(input, JsonSerializerSettings), JsonSerializerSettings);

        // hack to handle "|" fields 
        foreach (var ft in objectType.Fields.Where(x => x.Key.Contains("|")))
        {
            if (expando.ContainsKey(ft.Key)) continue;
            if (expando.TryGetFieldValue(ft.Key, out var fieldValue))
            {
                expando.Add(ft.Key, fieldValue);
            }
        }

        return await AddObjectAsync(context, objectType, expando, options);
    }

    /// <summary>
    /// New-new way to add an object
    /// it will fail for object types that are not using the latest:
    /// </summary>
    public async Task<Result<AddObjectResult>> AddObjectAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> input, AddObjectOptions options)
    {
        if (!options.SkipObjectTypeValidation)
        {
            var errors = ValidateObjectType(objectType);
            if (errors != null) return Result<AddObjectResult>.Error(errors);
        }

        var result = await GetFieldValuesFromUserInputAsync(context, objectType, new GetValuesFromInputOptions
            {
                Input = input,
                ExcludeNulls = true,
                GetFormOptions = options,
                AllowInitialValueOverride = options.AllowInitialValueOverride,
            }
        );

        if (!result.IsSuccess) return result.ConvertTo<AddObjectResult>();

        if (objectType.IsEmbedded)
        {
            return Result.Success(new AddObjectResult
            {
                Object = JsonObjectConverter.Convert<ExpandoObject>(result.Value),
            });
        }

        // _id
        if (!objectType.Fields.TryGetValue(Model.IdFieldName, out var fieldTemplate))
        {
            return Result<AddObjectResult>.Error($"{Model.IdFieldName} not configured");
        }

        var record = result.Value;

        if (fieldTemplate.InitialValue is not string initialValue || !ExpressionEvaluatorService.TryResolve(context, input, initialValue, out var newId))
        {
            if (!fieldTemplate.RBAC.CanSetOnCreate(context))
            {
                return Result<AddObjectResult>.Error($"{Model.IdFieldName} can't be set");
            }

            if (!record.TryGetValue(fieldTemplate.Field.Name, out newId))
            {
                return Result<AddObjectResult>.Error($"{Model.IdFieldName} no value");
            }
        }

        if (options.OnBeforeSerializing != null)
        {
            var prepare = options.OnBeforeSerializing.Invoke(result.Value);
            if (!prepare.IsSuccess) return prepare.ConvertTo<AddObjectResult>();
            record = prepare.Value;
        }

        record[Model.IdFieldName] = newId;

        var nameField = objectType.LookupFields?.Name ?? nameof(Model.Name);
        var name = default(string);

        Result<AddObjectResult> upsertResult;
        if (objectType.UniqueIndices != null)
        {
            var (existingRecord, index) = await FindUsingUniqueIndicesAsync(context, objectType, record);
            if (existingRecord != null)
            {
                newId = ((IDictionary<string, object>)existingRecord)[Model.IdFieldName];
                existingRecord.TryGetStrParam(nameField, out name);
                upsertResult = await updateObjectAsync(existingRecord, index);
                await addRecentObjectAsync();
                return upsertResult;
            }
        }

        upsertResult = await addObjectAsync(record);
        record.TryGetStrParam(nameField, out name);
        await addRecentObjectAsync();
        return upsertResult;

        async Task addRecentObjectAsync()
        {
            if (!upsertResult.IsSuccess || options.IsImporting) return;
            if (!context.UserId.HasValue) return;
            if (!newId.TryToParseObjectId(out var objectId)) return;
            
            await AddRecentObjectAsync(context, objectType, objectId, name);
        }

        async Task<Result<AddObjectResult>> addObjectAsync(IDictionary<string, object> flatObject)
        {
            if (!newId.TryToParseObjectId(out var guid)) throw new Exception("Unexpected id");

            var doc = AggregateSubDocumentsForCreation(flatObject);
            var readyToAdd = doc.Value;

            await _connection
                .GetCollection<object>(objectType.CollectionName, objectType.DatabaseName)
                .InsertOneAsync(readyToAdd);

            var created = await _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(Model.IdFieldName, guid)
                .FirstOrDefaultAsync();

            if (created == null)
            {
                _logger.LogError("Couldn't find {ObjectType} just saved with {ObjectId}", objectType.FullName, guid);
                return Result.Error<AddObjectResult>("Failed to get created object");
            }

            // does it still make sense?
            await AddTagsToObjectTypeAsync(context, objectType, flatObject);

            return Result.Success(new AddObjectResult
            {
                Object = created,
                ObjectId = guid,
                FiredEvent = await FireCreateEventAsync(context, objectType, flatObject, guid, options.PrepareEvent),
            }, "Created");
        }

        async Task<Result<AddObjectResult>> updateObjectAsync(ExpandoObject expandoObject, UniqueIndex index)
        {
            // UPSERT
            if (!expandoObject.TryGetGuidParam(Model.IdFieldName, out var existingId))
            {
                return Result.Error<AddObjectResult>($"Unique index conflict");
            }

            var isUpsert = options.IsUpsert || index.Upsert;
            if (!isUpsert)
            {
                return Result.Error<AddObjectResult>($"Unique index: {index.Name}");
            }

            var update = await UpdateObjectAsync(context, objectType, input, existingId, expandoObject, new UpdateObjectOptions
            {
                SkipObjectTypeValidation = true,
                PartialUpdate = false,
            });

            if (!update.IsSuccess) return update.ConvertTo<AddObjectResult>();
            if (update.Value.Skipped)
            {
                return Result<AddObjectResult>.Success(new AddObjectResult
                {
                    Object = update.Value.Object,
                    Skipped = true,
                    Existing = true,
                    ObjectId = existingId,
                }, "Nothing changed");
            }

            return Result.Success(new AddObjectResult
            {
                Object = update.Value.Object,
                Existing = true,
                ObjectId = existingId,
                UpdatedFields = update.Value.UpdatedFields,
                FiredEvent = update.Value.FiredEvent,
            }, "Updated Existing");
        }
    }

    public async Task<IDictionary<string, object>> FindOrCreateUsingUniqueIndicesAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> knownValues, string requiredField = null)
    {
        if (objectType?.UniqueIndices == null) return null;
        var (existing, index) = await FindUsingUniqueIndicesAsync(context, objectType, knownValues, requiredField);
        if (existing != null) return existing;

        // try to create 
        // foreach (var index in objectType.UniqueIndices)
        // {
        //     if (requiredField != null && !index.Fields.Contains(requiredField)) continue;
        // }

        var newObject = await AddObjectAsync(context, objectType, knownValues, new AddObjectOptions());
        if (newObject.IsError)
        {
            _logger.LogError("Failed to create related {ObjectType}", objectType.FullName);
            return null;
        }

        return newObject.Value.Object;
    }

    private bool TryToInferFieldValue(IEntityContext context, ObjectType objectType, IDictionary<string, object> knownValues, string fieldName, out object value)
    {
        if (!objectType.Fields.TryGetValue(fieldName, out var field))
        {
            value = null;
            return false;
        }

        if (knownValues.TryGetValue(fieldName, out var fieldValue))
        {
            value = fieldValue;

            // TODO: should try to use field to autoconvert value
            // ...

            return true;
        }

        if (field.InitialValue != null)
        {
            if (TryResolveExpression(context, field, knownValues, field.InitialValue, out var resolvedValue))
            {
                value = resolvedValue;
                return true;
            }

            value = null;
            return false;
        }

        if (objectType.Constraints != null)
        {
            var constraint = objectType.GetEqConditions(context).FirstOrDefault(x => x.FieldName == fieldName);
            if (constraint != null)
            {
                if (TryResolveExpression(context, field, knownValues, constraint.Value, out var resolvedValue))
                {
                    value = resolvedValue;
                    return true;
                }

                value = null;
                return false;
            }
        }

        // default ?? 
        // ...

        value = null;
        return false;
    }

    public async Task<List<ExpandoObject>> FindNearAsync(IEntityContext context, ObjectType objectType, Condition[] criteria, int max = 0, string orderBy = null, bool? reverseOrder = null)
    {
        var locationCondition = criteria
            .FirstOrDefault(x => objectType.Fields.TryGetValue(x.FieldName, out var field) && field.Field is LocationField);
        if (locationCondition == null)
        {
            _logger.LogError("Can't find near without location field");
            return new List<ExpandoObject>();
        }

        var locationField = objectType.Fields[locationCondition.FieldName].Field;
        var value = locationField.AutoConvert(locationCondition.ResolveValue(context));
        var coordinates = value switch
        {
            Models.GeoJSON.Point pt => pt.Coordinates,
            decimal[] d => d,
            _ => null,
        };
        if (coordinates == null || coordinates.Length != 2)
        {
            _logger.LogError("Invalid location value");
            return new List<ExpandoObject>();
        }

        var longitude = coordinates[0];
        var latitude = coordinates[1];
        var maxDistance = default(decimal?);

        var locationDistanceCondition = criteria.FirstOrDefault(x => objectType.Fields.TryGetValue(x.FieldName, out var field) && field.Field is LocationDistanceField);
        if (locationDistanceCondition != null)
        {
            var locationDistanceField = objectType.Fields[locationCondition.FieldName].Field;
            var locationDistanceValue = locationDistanceField.AutoConvert(locationDistanceCondition.ResolveValue(context));
            if (locationDistanceValue is decimal distance)
            {
                maxDistance = distance * 1000; // assume max is in km for now 
            }
        }

        // build geoNear 
        var geoNear = new Dictionary<string, object>
        {
            {
                "near", new Dictionary<string, object>
                {
                    { "type", "Point" },
                    { "coordinates", new[] { longitude, latitude } }
                }
            },
            { "spherical", true },
            // key 
            // minDistance
            // query: { category: "Parks" },
            // includeLocs: "dist.location",
            // spherical: true                
        };

        if (maxDistance.HasValue)
        {
            geoNear["maxDistance"] = maxDistance.Value;
            geoNear["distanceField"] = locationDistanceCondition.FieldName;
            orderBy ??= locationCondition.FieldName;
        }
        else
        {
            var locationDistanceField = objectType.Fields.Values.FirstOrDefault(x => x.Field is LocationDistanceField && x.Indexed)?.Field;
            var fieldName = locationDistanceField?.Name ?? $"{locationCondition.FieldName}|LocationDistance";
            geoNear["distanceField"] = fieldName;
            orderBy ??= fieldName;
        }

        // build normal filter (without location fields)
        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .AddConstraints(context, objectType)
            ;
        var additionalCriteria = criteria
            .Where(x => objectType.Fields.TryGetValue(x.FieldName, out var field) && field.Field is not (LocationField or LocationDistanceField))?.ToArray();
        if (additionalCriteria?.Length > 0)
        {
            query.AddConditions(context, additionalCriteria);
        }

        geoNear["query"] = query.GetFilterAsBsonDocument();

        var pipeline = PipelineDefinition<ExpandoObject, ExpandoObject>.Create(getStages());
        var matches = _connection.Database
            .GetCollection<ExpandoObject>(objectType.CollectionName)
            .Aggregate(pipeline)
            .ToList();

        return matches;

        IEnumerable<BsonDocument> getStages()
        {
            yield return new BsonDocument(new Dictionary<string, object>
            {
                { "$geoNear", geoNear }
            });

            // sort 
            yield return new BsonDocument("$sort", new BsonDocument(new Dictionary<string, object> { { orderBy, reverseOrder.GetValueOrDefault(false) ? -1 : 1 } }));

            // limit
            if (max > 0)
            {
                yield return new BsonDocument("$limit", max);
            }
        }
    }

    public Task<List<ExpandoObject>> FindAsync(IEntityContext context, ObjectType objectType, Condition[] additionalCriteria = null, int max = 0, string orderBy = null, bool? reverseOrder = null)
        => FindAsync<ExpandoObject>(context, objectType, additionalCriteria, max, orderBy, reverseOrder);

    public async Task<List<T>> FindAsync<T>(IEntityContext context, ObjectType objectType, Condition[] additionalCriteria = null, int max = 0, string orderBy = null, bool? reverseOrder = null)
    {
        var query = _connection.Filter<T>(objectType.CollectionName, objectType.DatabaseName)
                .AddConstraints(context, objectType)
            ;

        if (additionalCriteria?.Length > 0)
        {
            query.AddConditions(context, additionalCriteria);
        }

        if (!string.IsNullOrEmpty(orderBy))
        {
            // TODO: will not handle "complex paths" (e.g. item in array,. ..)
            // ....
            var fieldPath = orderBy.Replace('|', '.');
            if (reverseOrder.GetValueOrDefault(false))
            {
                query.SortDesc(fieldPath);
            }
            else
            {
                query.SortAsc(fieldPath);
            }
        }

        if (max > 0)
        {
            query.Limit(max);
        }

        return await query.FindAsync();
    }

    private async Task<(ExpandoObject, UniqueIndex)> FindUsingUniqueIndicesAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> knownValues, string requiredField = null)
    {
        if (objectType?.UniqueIndices == null) return (null, null);

        foreach (var index in objectType.UniqueIndices)
        {
            if (requiredField != null && !index.Fields.Contains(requiredField)) continue;

            var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                // .AddConstraints(context, objectType)
                ;

            foreach (var fieldName in index.Fields)
            {
                if (!TryToInferFieldValue(context, objectType, knownValues, fieldName, out var fieldValue))
                {
                    query = null;
                    break;
                }

                query.Eq(fieldName, fieldValue);
            }

            if (query == null) continue;

            var existingRecord = await query
                .FirstOrDefaultAsync();

            if (existingRecord != null) return (existingRecord, index);
        }

        return (null, null);
    }

    /// <summary>
    /// add an object w/o having to rely on the model type
    /// </summary>
    [Obsolete("move to the overload that takes options and will not handle object types without the latest features")]
    public async Task<Result<Guid?>> AddObjectAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> parameters, Func<IDictionary<string, object>, string> prepare = null)
    {
        var result = await GetFieldValuesFromUserInputAsync(context, objectType, new GetValuesFromInputOptions
            {
                Input = parameters,
                ExcludeNulls = true,
            }
        );

        if (!result.IsSuccess) return result.ConvertTo<Guid?>();

        // always generate new id
        var id = GenerateId(context, objectType);
        result.Value[Model.IdFieldName] = id;

        var prepareError = prepare?.Invoke(result.Value);
        if (!string.IsNullOrEmpty(prepareError))
        {
            return Result.Error<Guid?>(prepareError);
        }

        var aggregate = AggregateSubDocumentsForCreation(result.Value);
        if (!aggregate.IsSuccess) return aggregate.ConvertTo<Guid?>();
        var value = aggregate.Value;

        if (objectType.InitialFlowId.HasValue)
        {
            // TODO: move to initial value?
            // ...
            setIfExistsAndIsMissing(nameof(IFlowObject.FlowId), objectType.InitialFlowId);
        }

        if (objectType.InitialObjectStatusId.HasValue)
        {
            // TODO: move to initial value?
            // ...
            setIfExistsAndIsMissing(nameof(IFlowObject.ObjectStatusId), objectType.InitialObjectStatusId);
        }

        if (objectType.Constraints == null)
        {
            // TODO: move all to constraints
            // ...
            // old implicit way
            switch (context.Role)
            {
                case EntityRoleId.Root:
                    break;

                case EntityRoleId.Admin:
                    setIfExistsAndIsMissing(nameof(IEntityOwnedModel.EntityId), context.AccountId.Value);
                    break;

                case EntityRoleId.Manager:
                    setIfExistsAndIsMissing(nameof(IEntityOwnedModel.EntityId), context.OrganizationId.Value);
                    break;

                case EntityRoleId.User:
                    setIfExistsAndIsMissing(nameof(IEntityOwnedModel.EntityId), context.UserId.Value);
                    break;

                default:
                    throw new ForbiddenException(context, "can't add object");
            }

            setIfExistsAndIsMissing(nameof(IFlowObject.AccountId), context.AccountId.Value);
        }

        if (objectType.CollectionName == nameof(CustomObject) || !objectType.IsCustom)
        {
            // TODO: get rid of it and just update objects?
            // ...
            // for backwards compatibility 
            setIfExistsAndIsMissing(nameof(Model.CreatedOn), DateTime.UtcNow);
            setIfExistsAndIsMissing(nameof(CustomObject.ObjectTypeId), objectType.Id);
            setIfExistsAndIsMissing(nameof(CustomObject.ObjectType), objectType.FullName);
            setIfExistsAndIsMissing(nameof(CustomObject.LastActor), Actor.Current);
        }

        // check uniqueness 
        // external id
        if (objectType.UniqueExternalId)
        {
            // TODO: move away from  other "uniqueness" (add config to object type)
            // ...

            if (!objectType.Fields.TryGetValue(nameof(IExternalId.ExternalId), out var externalField))
            {
                throw new Exception($"Invalid configuration, missing ExternalId field");
            }

            if (!value.TryGetValue(nameof(IExternalId.ExternalId), out var externalId))
            {
                return Result.Error<Guid?>($"Missing Required ExternalId");
            }

            var existing = await _connection.Filter<object>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(nameof(Model.AccountId), context.AccountId.Value)
                .Eq(nameof(IExternalId.ExternalId), externalId)
                .FirstOrDefaultAsync();

            // TODO: handle upsert?
            // ...
            if (existing != null)
            {
                return Result.Error<Guid?>($"ExternalId violation");
            }
        }

        if (!id.TryToParseObjectId(out var guid)) throw new Exception("Unexpected id");

        await _connection
            .GetCollection<object>(objectType.CollectionName, objectType.DatabaseName)
            .InsertOneAsync(value);

        await FireCreateEventAsync(context, objectType, value, guid);

        await AddTagsToObjectTypeAsync(context, objectType, value);

        return Result.Success<Guid?>(guid);

        void setIfExistsAndIsMissing(string fieldName, object defaultValue)
        {
            if (!objectType.Fields.TryGetValue(fieldName, out var field) || field.InitialValue != null)
            {
                // field not defined or was calculated - can be ignored
                return;
            }

            value.TryAdd(fieldName, defaultValue);
        }
    }

    /// <summary>
    /// Resolve object field values from input
    /// </summary>
    public async Task<Result<IDictionary<string, object>>> GetFieldValuesFromUserInputAsync(IEntityContext context, ObjectType objectType, GetValuesFromInputOptions options)
    {
        var values = new Dictionary<string, object>();

        // first pass, parse input
        foreach (var field in objectType.Fields)
        {
            if (!field.Value.RBAC.CanSetOnCreate(context)) continue; // can't set on create

            Result<object> fieldValue;
            if (field.Value.InitialValue != null)
            {
                if (!options.AllowInitialValueOverride)
                {
                    // has to wait for the end in case it depends on other values
                    continue;
                }

                fieldValue = await GetFieldValueFromUserInputAsync(context, field.Value, options);
                if (fieldValue.IsError || fieldValue.Value == null)
                {
                    // no value set, skip so it will use initial value
                    continue;
                }

                // if the context can set the field and there is already a value, use it
            }
            else
            {
                fieldValue = await GetFieldValueFromUserInputAsync(context, field.Value, options);
            }

            if (fieldValue.IsError) return fieldValue.ConvertTo<IDictionary<string, object>>();

            var value = fieldValue.Value;
            if (value == null && field.Value.Field.IsRequired && field.Value.InitialValue == null)
            {
                _logger.LogError("Required {Field} for {ObjectType} is null", field.Value.Field.Name, objectType.FullName);
                return Result.Error<IDictionary<string, object>>($"Missing required field: {field.Value.Field.Label ?? field.Value.Field.Name}");
            }

            if (value == null && options.ExcludeNulls) continue;

            var fieldPath = field.Key;
            // values[FormField.GetPathInCollection(fieldPath)] = value;

            // will create levels
            var err = SetValue(values, FormField.GetPathInCollection(fieldPath), value);
            if (err != null)
            {
                _logger.LogError("Failed to set {FieldPath} to {Value}", fieldPath, value);
                return Result.Error<IDictionary<string, object>>($"Failed to set {fieldPath}: {err}");
            }
        }

        // second pass, calculate initial values
        var error = CalculateInitialValues(context, objectType, values, options.AllowInitialValueOverride);
        if (error != null) return Result.Error<IDictionary<string, object>>(error);

        // third pass, constraints
        EnforceConstraints(context, objectType, values);

        return Result.Success<IDictionary<string, object>>(values);
    }

    private void EnforceConstraints(IEntityContext context, ObjectType objectType, IDictionary<string, object> values)
    {
        var conditions = objectType.GetEqConditions(context);
        foreach (var constraint in conditions)
        {
            var value = constraint.ResolveValue(context);
            // var fieldPath = prefix != null ? $"{prefix}|{constraint.FieldName}" : constraint.FieldName;
            var fieldPath = constraint.FieldName;
            values[FormField.GetPathInCollection(fieldPath)] = value;
        }
    }

    /// <summary>
    /// Calculate Initial Values when adding object
    /// </summary>
    private string CalculateInitialValues(IEntityContext context, ObjectType objectType, IDictionary<string, object> values, bool allowInitialValueOverride = false)
    {
        foreach (var field in objectType.Fields.Values)
        {
            if (field.InitialValue == null) continue;

            var fieldPath = field.Field.Name;
            if (allowInitialValueOverride)
            {
                var currentValue = values.ResolvePathValue(FormField.GetPathInCollection(fieldPath));
                if (currentValue != null)
                {
                    // there is already a value set for it
                    continue;
                }
            }

            if (!TryResolveExpression(context, field, values, field.InitialValue, out var value))
            {
                if (field.Field.IsRequired)
                {
                    return $"Couldn't calculate initial value for {field.Field.Name}";
                }
            }

            var error = SetValue(values, FormField.GetPathInCollection(fieldPath), value);
            if (error != null) return error;
        }

        return null;
    }

    /// <summary>
    /// Resolve the value for a field from the input
    /// </summary>
    private async Task<Result<object>> GetFieldValueFromUserInputAsync(IEntityContext context, FieldTemplate fieldTemplate, GetValuesFromInputOptions options)
    {
        var apiName = options.GetFormOptions.GetApiName(fieldTemplate.Field);
        options.Input.TryGetValue(apiName, out var value);
        return await GetFieldValueFromUserInputAsync(context, fieldTemplate.Field, value, options);
    }

    /// <summary>
    /// Auto convert value for field from user input  
    /// </summary>
    private async Task<Result<object>> GetFieldValueFromUserInputAsync(IEntityContext context, FormField field, object value, GetValuesFromInputOptions options)
    {
        if (value == null)
        {
            if (!ExpressionEvaluatorService.TryResolve(context, null, field.DefaultValue, out var resolved))
            {
                _logger.LogError("Couldn't resolve {DefaultValue} expression", field.DefaultValue);
                return Result.Error<object>("Couldn't resolve expression");
            }

            value = resolved;
        }

        value = field.AutoConvert(value);

        return field switch
        {
            ObjectField objectField => await GetObjectFieldValueFromUserInputAsync(context, objectField, value, options),
            PasswordField passwordField => await GetPasswordFieldValueFromUserInputAsync(context, passwordField, value),
            ChildrenField childrenField => await GetChildrenFieldValueFromUserInputAsync(context, childrenField, value, options),
            SelectField selectField => ValidateItemValue(context, selectField, value),
            _ => Result.Success(value),
        };
    }

    private async Task<Result<object>> GetChildrenFieldValueFromUserInputAsync(IEntityContext context, ChildrenField field, object input, GetValuesFromInputOptions options)
    {
        if (input == null) return Result.Success(input);

        var objectType = await GetAsync(context, field.ChildrenFieldOptions.ObjectType);

        if (field.ChildrenFieldOptions.KeyType == ChildrenFieldOptions.IndexKeyType)
        {
            // array
            if (input is not IEnumerable en) throw new BadRequestException($"Invalid value for {field.Name}: not array");

            var list = new List<object>();
            foreach (var child in en)
            {
                if (child is not IDictionary<string, object> childDict)
                {
                    throw new BadRequestException($"Invalid value for {field.Name} item");
                }

                var childObjectType = await ResolveSubTypeForUserInputAsync(context, objectType, childDict, options.GetFormOptions);
                var fieldValues = await GetFieldValuesFromUserInputAsync(context, childObjectType, options.WithInput(childDict));
                if (!fieldValues) return fieldValues.ConvertTo<object>();

                list.Add(fieldValues.Value);
            }

            return Result.Success<object>(list.ToArray());
        }

        // dict
        if (input is JObject jObject)
        {
            input = jObject.Properties().ToDictionary();
        }

        if (input is not IDictionary<string, object> dict)
        {
            throw new BadRequestException($"{field.Name}: Invalid value {input.GetType().FullName}");
        }

        if (string.IsNullOrWhiteSpace(field.ChildrenFieldOptions.ObjectType) || field.ChildrenFieldOptions.ObjectType == "*")
        {
            // generic object?
            return Result.Success<object>(dict);
        }

        var resolved = new Dictionary<string, object>();
        foreach (var kvp in dict)
        {
            if (kvp.Value is not IDictionary<string, object> childDict) throw new BadRequestException($"{field.Name}.{kvp.Key}: Invalid value {kvp.Value?.GetType().FullName}");

            var childObjectType = await ResolveSubTypeForUserInputAsync(context, objectType, childDict, options.GetFormOptions);
            var fieldValues = await GetFieldValuesFromUserInputAsync(context, childObjectType, options.WithInput(childDict));
            if (!fieldValues) return fieldValues.ConvertTo<object>();

            resolved.Add(kvp.Key, fieldValues.Value);
        }

        return Result.Success<object>(resolved);
    }

    private Result<object> ValidateItemValue(IEntityContext context, SelectField selectField, object value)
    {
        if (value == null)
        {
            if (selectField.IsRequired)
            {
                return Result<object>.Error($"{selectField.Label ?? selectField.Name} is required");
            }

            return Result<object>.Success(value);
        }

        if (selectField.SelectFieldOptions?.Items != null && selectField.SelectFieldOptions?.AllowUnknown != true)
        {
            if (!selectField.SelectFieldOptions.Items.Contains(value))
            {
                return Result.Error<object>($"Invalid value for {selectField.Label ?? selectField.Name}");
            }
        }

        return Result.Success(value);
    }

    private async Task<Result<object>> GetPasswordFieldValueFromUserInputAsync(IEntityContext context, PasswordField passwordField, object value)
    {
        if (passwordField.PasswordFieldOptions?.DataDataProtection == null) return Result.Success(value);
        if (value == null) return Result.Success(value);
        if (value is not string strValue) return Result.Error<object>($"{passwordField.Name} must be a string");

        var result = await _dataProtectionService.ProtectAsync(context, passwordField.PasswordFieldOptions.DataDataProtection, strValue);

        return Result.Success<object>(result);
    }

    /// <summary>
    /// Resolve object for field using input 
    /// </summary>
    private async Task<Result<object>> GetObjectFieldValueFromUserInputAsync(IEntityContext context, ObjectField objectField, object input, GetValuesFromInputOptions options)
    {
        if (input == null) return Result.Success(input);

        if (input is not IDictionary<string, object> dict) throw new BadRequestException($"Invalid value for {objectField.Name}");

        if (string.IsNullOrWhiteSpace(objectField.ObjectFieldOptions.ObjectType) || objectField.ObjectFieldOptions.ObjectType == "*")
        {
            // generic object?
            // ...
            return Result.Success<object>(dict);
        }

        var objectType = await GetAsync(context, objectField.ObjectFieldOptions.ObjectType);
        objectType = await ResolveSubTypeForUserInputAsync(context, objectType, dict, options.GetFormOptions);

        var fieldValues = await GetFieldValuesFromUserInputAsync(context, objectType, options.WithInput(dict));

        return fieldValues.ConvertTo<object>();
    }

    /// <summary>
    /// Build form to be used to create new object of type 
    /// </summary>
    public async Task<Form.Models.Form> BuildAddFormAsync(IEntityContext context, ObjectType objectType, bool loadLayout, GetFormOptions opts = null)
    {
        var form = BuildDataForm(objectType, context, FormName.Add, opts: opts);
        if (form == null) throw new ForbiddenException(context, "Can't add");

        // TODO: be extra-smart and try to resolve subtype using discriminator here?
        // ... 

        if (objectType.Constraints != null)
        {
            // seed values with any constraints
            var fields = form.Fields.ToDictionary(x => x.Name);
            var conditions = objectType.GetEqConditions(context);
            foreach (var condition in conditions)
            {
                if (fields.TryGetValue(condition.FieldName, out var field))
                {
                    field.DefaultValue = condition.Value;
                    field.Enable = new[] { "false" };
                }
            }
        }

        // post process fields
        foreach (var field in form.Fields)
        {
            await LoadFieldAsync(context, objectType, FormName.Add, field, field.DefaultValue, opts);
        }

        if (!loadLayout) return form;

        var layout = await LoadLayoutAsync(context, objectType, nameof(FormName.Add), opts);
        if (layout != null)
        {
            form.Layouts = layout;
        }

        return form;
    }

    /// <summary>
    /// Try Load Form Layout 
    /// </summary>
    private async Task<BreakpointLayouts> LoadLayoutAsync(IEntityContext context, ObjectType objectType, string formName, GetFormOptions options)
    {
        var layout = options?.GetLayoutFromCache(objectType, formName);
        if (layout != null) return layout.Layouts;

        layout = await _connection.GetProfileElementAsync<AppFormLayout>(context, query =>
        {
            query
                .Eq(x => x.ObjectType, objectType.FullName)
                .Eq(x => x.FormName, formName)
                .Ne(x => x.IsActive, false)
                ;
        });

        // add to cache
        options?.OnLayoutLoaded(layout ?? new AppFormLayout
        {
            ObjectType = objectType.FullName,
            FormName = formName,
            Layouts = null,
        });

        return layout?.Layouts;
    }

    /// <summary>
    /// Build form based on ObjectType, FormName, Object and Context access 
    /// </summary>
    private static Form.Models.Form BuildDataForm(ObjectType objectType, IEntityContext context, FormName formName, Guid? id = null, GetFormOptions opts = null)
    {
        var autoForm = new Form.Models.Form
        {
            Name = $"{objectType.FullName}_{formName}",
            Title = objectType.Label ?? objectType.Description ?? objectType.Name,
            IsReadOnly = formName is FormName.View or FormName.Details,
            ObjectType = objectType.FullName,
        };

        switch (formName)
        {
            case FormName.Add:
                autoForm.Fields = objectType.Fields.Values
                    .Where(x => x.RBAC.CanSetOnCreate(context) && x.Field != null)
                    .Select(x => cloneField(x.Field))
                    .ToArray();
                break;

            case FormName.Edit:
            {
                // var hideReadOnlyCondition = formName is FormName.Edit ? new[] { "#showReadOnly" } : null;
                var hideReadOnlyCondition = default(string[]);

                var editFields = objectType.Fields.Values
                    .Where(x => (x.RBAC.CanRead(context) || x.RBAC.CanUpdate(context)) && x.Field != null)
                    .Select(x =>
                    {
                        var f = cloneField(x.Field);
                        if (!x.RBAC.CanUpdate(context) && !x.RBAC.CanReset(context) && !x.RBAC.CanCreateOnDemand(context))
                        {
                            f.Enable = ["false"];
                        }

                        return f;
                    });

                autoForm.Fields = editFields.ToArray();

                var hiddenFields = autoForm.Fields.Any(x => x.Visible == hideReadOnlyCondition);
                if (hideReadOnlyCondition != null && hiddenFields)
                {
                    autoForm.Fields = autoForm.Fields.Append(new CheckboxField
                    {
                        Name = "#showReadOnly",
                        Label = "Show All Fields",
                        Visible = new[] { "!#showReadOnly" },
                        Options = new CheckboxFieldOptions
                        {
                            Style = CheckboxFieldOptionsStyle.Toggle,
                        },
                    });
                }

                break;
            }

            case FormName.View:
            case FormName.Details:
                autoForm.Fields = objectType.Fields.Values
                    .Where(x => x.RBAC.CanRead(context) && x.Field != null)
                    .Select(x => cloneField(x.Field))
                    .ToArray();

                break;
        }

        var hasRequiredFields = autoForm.Fields.Any(x => x.IsRequired);
        var requiredFieldsCondition = hasRequiredFields ? new[] { Form.Models.Form.RequiredFieldsName } : null;
        if (hasRequiredFields)
        {
            // add required fields if missing (should always be missing :) )
            switch (formName)
            {
                case FormName.Add:
                case FormName.Edit:
                    if (!objectType.Fields.ContainsKey(Form.Models.Form.RequiredFieldsName))
                    {
                        autoForm.Fields = autoForm.Fields
                            .Append(new HiddenField
                            {
                                Name = Form.Models.Form.RequiredFieldsName,
                            })
                            .ToArray();
                    }

                    break;
            }
        }

        // actions
        var actions = new List<FormAction>();
        switch (formName)
        {
            case FormName.Details:
                break;

            case FormName.View:
                if (id.HasValue)
                {
                    actions.Add(new FormAction
                    {
                        Name = $"page://api/v1/CustomObject/{objectType.FullName}({id})",
                        Label = "Expand",
                    });

                    // TODO: add permission to duplicate (and deep clone?)
                    // ...
                    if (objectType.Can(context, ObjectTypePermission.DeepClone))
                    {
                        // for now will go straight to the action
                        // TODO: actually make it become a form so the user can customize the Create/Update field values 
                        // before the clone
                        // ...
                        actions.Add(new FormAction
                        {
                            Name = $"action://api/v1/CustomObject/{objectType.FullName}({id})/Clone",
                            Label = "Clone",
                        });
                    }

                    if (objectType.CanUpdate(context))
                    {
                        actions.Add(new FormAction
                        {
                            Name = BuildDataFormUrl(objectType, id),
                            Label = "Edit",
                        });
                    }
                }

                break;

            case FormName.Edit:
                if (objectType.CanDelete(context))
                {
                    actions.Add(new FormAction
                    {
                        Name = FormAction.Delete,
                    });
                }

                if (objectType.CanUpdate(context))
                {
                    actions.Add(new FormAction
                    {
                        Name = FormAction.Update,
                        Enable = requiredFieldsCondition,
                    });
                }

                break;

            case FormName.Add:
                actions.Add(new FormAction
                {
                    Name = FormAction.Add,
                    Enable = requiredFieldsCondition,
                });
                break;
        }

        if (actions.Count > 0)
        {
            autoForm.Actions = actions.ToArray();
        }

        if (context.Role == EntityRoleId.Admin && context.ProfileId.HasValue)
        {
            autoForm.Menu ??= new Menu
            {
                Name = "Form",
                Label = "Popup",
            };

            autoForm.Menu.Items = (autoForm.Menu.Items ?? Enumerable.Empty<MenuItem>())
                .Append(new ActionMenuItem
                {
                    Icon = nameof(Icons.Design),
                    Name = FormAction.Client_Design,
                    Label = "Design",
                    Action = FormAction.Client_Design,
                    // could use action instead ... 
                    // Name = "Design", 
                    // Action = $"dataForm://api/v1/ObjectType({objectType.Name})/Profile({context.ProfileId.Value})/Form({formName})",
                })
                .ToArray();
        }

        return autoForm;

        FormField cloneField(FormField field)
        {
            var result = field.Copy();
            result.ApiName = opts.GetApiName(field);
            return result;
        }
    }

    [Obsolete("eventually use file import service instead")]
    public async Task<DataFormActionResponse> ImportCsvAsync(IEntityContext context, ObjectType objectType, Stream stream)
    {
        using var scope = _logger.AddScope(new
        {
            ObjectType = objectType.FullName,
            context.UserId,
            context.OrganizationId,
            context.AccountId,
        });

        _logger.LogInformation("Import CSV");

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, false);

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new BadRequestException("Failed to open/read header");
        }

        var identityProvider = default(string);
        var entityLookupColumn = default(string);
        var externalIdColumn = default(string);
        var idColumn = default(string);
        var addObjectMap = objectType.Can(context, ObjectTypePermission.Create) ? getColumnMap(FieldPermission.SetOnCreate) : null;
        var updateObjectMap = objectType.Can(context, ObjectTypePermission.Update) ? getColumnMap(FieldPermission.Update) : null;

        if (objectType.UniqueExternalId)
        {
            // find what is the column for the external id
            if (csv.Parser.Context.HeaderRecord.Contains(nameof(CustomObject.ExternalId)))
            {
                // there is an externalid header 
                externalIdColumn = nameof(CustomObject.ExternalId);
            }
            else if (externalIdColumn == null)
            {
                // try to find using label assigned to externalId field
                if (!objectType.Fields.TryGetValue(nameof(CustomObject.ExternalId), out var fieldConfig))
                {
                    throw new BadRequestException("Invalid Object, no external id field");
                }

                externalIdColumn = csv.Parser.Context.HeaderRecord.FirstOrDefault(x => x == fieldConfig.Field.Label);

                if (externalIdColumn == null)
                {
                    _logger.LogError("Can't add records because no ExternalId column found");
                    throw new BadRequestException("Can't add records because no ExternalId column found");
                }
            }
        }

        // TODO: should be configurable
        // ...
        if (objectType.Fields.TryGetValue("_id", out var ifFieldConfig))
        {
            idColumn = csv.Parser.Context.HeaderRecord.FirstOrDefault(x => x == ifFieldConfig.Field.Label || x == "_id");
        }

        // special auto map column
        var entityAutoMapCols = csv.Parser.Context.HeaderRecord
            .Where(x => !objectType.Fields.ContainsKey(x))
            .Where(x => x.StartsWith("Entity:"))
            .ToArray();

        if (entityAutoMapCols.Length > 0)
        {
            if (entityAutoMapCols.Length > 1) throw new BadRequestException("Only one entity column can be used");

            var column = entityAutoMapCols[0];
            entityLookupColumn = column;
            identityProvider = column["Entity:".Length..];

            _logger.LogInformation("Resolve entity using {provider} with {column}", identityProvider, column);
        }

        var entities = new Dictionary<string, Guid>();
        var errors = new List<string>();
        var modified = new List<Guid>();
        var added = new List<Guid>();
        var skipped = 0L;

        var defaultEntityId = context.Role switch
        {
            EntityRoleId.Account => context.AccountId.Value,
            EntityRoleId.Admin => context.AccountId.Value,
            EntityRoleId.Manager => context.OrganizationId.Value,
            EntityRoleId.Organization => context.OrganizationId.Value,
            EntityRoleId.User => context.UserId.Value,
            _ => throw new ForbiddenException(context)
        };

        while (csv.Read())
        {
            try
            {
                var existing = default(IDictionary<string, object>);
                var externalId = default(string);
                var objectId = default(Guid?);

                if (objectType.UniqueExternalId)
                {
                    externalId = csv.GetField(externalIdColumn);
                    if (string.IsNullOrWhiteSpace(externalId))
                    {
                        _logger.LogError("Missing Required field ExternalId on {LineNumber}", csv.Parser.Context.RawRow);
                        errors.Add($"Missing required field ExternalId on line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                        continue;
                    }

                    var dynamicRecord = await GetExpandoObjectByExternalIdAsync(context, objectType, externalId);
                    existing = (IDictionary<string, object>)dynamicRecord;
                }
                else if (idColumn != null)
                {
                    // TODO: should allow non GUID ids
                    // ...
                    // there is an _id column, do a lookup by id
                    var idStr = csv.GetField(idColumn);
                    if (string.IsNullOrWhiteSpace(idStr) || !idStr.TryToParseObjectId(out var id))
                    {
                        _logger.LogError("Missing or invalid required field {IdColumn} on {LineNumber}", idColumn, csv.Parser.Context.RawRow);
                        errors.Add($"Missing or invalid required field {idColumn} on line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                        continue;
                    }

                    objectId = id;
                    var dynamicRecord = await GetExpandoObjectByIdAsync(context, objectType, objectId.Value);
                    existing = (IDictionary<string, object>)dynamicRecord;
                }

                if (existing != null && updateObjectMap == null)
                {
                    _logger.LogError("Can't update existing object: {ExternalId} on {LineNumber}", externalId, csv.Parser.Context.RawRow);
                    errors.Add($"Can't update existing {externalId}, skip line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                    continue;
                }

                if (existing == null && addObjectMap == null)
                {
                    _logger.LogError("Create disabled: {ExternalId} on {LineNumber}", externalId, csv.Parser.Context.RawRow);
                    errors.Add($"Can't create {objectType.FullName}, skip line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                    continue;
                }

                var fieldMap = existing != null ? updateObjectMap : addObjectMap;
                var record = new Dictionary<string, object>();

                if (identityProvider != null && fieldMap.TryGetValue(nameof(CustomObject.EntityId), out var entityField))
                {
                    var entity = await getIdentityAsync(csv.GetField(entityLookupColumn));
                    // TODO: option to log error or assign to current context?
                    // ...
                    if (entity.HasValue) record.Add(entityField.Name, entity.Value);
                }

                // resolve values
                foreach (var column in fieldMap)
                {
                    if (identityProvider != null && column.Key == nameof(CustomObject.EntityId))
                    {
                        // ignore EntityId column if there is a "auto-map" column
                        continue;
                    }

                    var strValue = csv.GetField(column.Key);
                    if (string.IsNullOrWhiteSpace(strValue)) strValue = null;
                    var value = column.Value.AutoConvert(strValue);
                    if (value != null)
                    {
                        record.Add(column.Value.Name, value);
                    }
                }

                if (existing == null)
                {
                    // create
                    var error = false;
                    var initFields = objectType.Fields.Values
                        .Where(x => x.RBAC.CanSetOnCreate(context))
                        .Select(x => x.Field)
                        .ToArray();

                    // check all "creatable" fields (not just the ones mapped for this import)
                    foreach (var field in initFields)
                    {
                        if (field.DefaultValue != null) record.TryAdd(field.Name, field.DefaultValue);
                        if (field.IsRequired && !record.ContainsKey(field.Name))
                        {
                            _logger.LogError("{Field} is required and missing on {LineNumber}", field.Name, csv.Parser.Context.RawRow);
                            errors.Add($"{field.Name} is required and missing on line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                            error = true;
                            break;
                        }
                    }

                    if (error) continue;

                    var result = await DeprecatedImportAddObjectAsync(context, objectType, record);
                    if (!result)
                    {
                        _logger.LogError("{Error} adding object on {LineNumber}", result.Status, csv.Parser.Context.RawRow);
                        errors.Add($"{result.Status}: on line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                        continue;
                    }

                    // success
                    added.Add(result.Value.Value);
                }
                else
                {
                    // update existing
                    var modifiedFields = new Dictionary<string, object>();
                    var missingRequired = false;
                    foreach (var field in fieldMap.Values)
                    {
                        if (!record.TryGetValue(field.Name, out var newValue)) newValue = null;

                        var currValue = existing.ResolveValue(field.Name.Split('|'));
                        newValue = field.AutoConvert(newValue);
                        if (!IsEqual(field, currValue, newValue))
                        {
                            if (newValue == null && field.IsRequired)
                            {
                                _logger.LogError("Missing required {Field} on {LineNumber}", field.Name, csv.Parser.Context.RawRow);
                                errors.Add($"Missing Required Field: {field.Label ?? field.Name} on {csv.Parser.Context.RawRow}");
                                missingRequired = true;
                                continue;
                            }

                            modifiedFields.Add(field.Name, newValue);
                        }
                    }

                    if (missingRequired)
                    {
                        continue;
                    }

                    if (modifiedFields.Count < 1)
                    {
                        skipped++;
                        continue;
                    }

                    // if updating by externalId, figure out the actual id to update
                    if (objectType.UniqueExternalId && existing.TryGetGuidParam("_id", out var id))
                    {
                        objectId = id;
                    }

                    if (!objectId.HasValue)
                    {
                        _logger.LogError("Couldn't find _id for existing object on {LineNumber}", csv.Parser.Context.RawRow);
                        errors.Add($"couldn't find _id for existing object: line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                        continue;
                    }

                    var result = await DeprecatedImportUpdateObjectAsync(context, objectType, objectId.Value, modifiedFields);
                    if (!result)
                    {
                        _logger.LogError("{Error} updating object on {LineNumber}", result.Status, csv.Parser.Context.RawRow);
                        errors.Add($"{result.Status}: on line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
                        continue;
                    }

                    modified.Add(objectId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception on {LineNumber}", csv.Parser.Context.RawRow);
                errors.Add($"{ex.Message} on line #{csv.Parser.Context.RawRow}: {csv.Parser.Context.RawRecord}");
            }
        }

        var changes = modified.Count + added.Count + skipped;

        return new DataFormActionResponse
        {
            Action = "Import",
            Success = changes > 0,
            Message = string.Join(";\n", getMessage()),
            // Ids = 
            // NextUrl
        };

        IEnumerable<string> getMessage()
        {
            if (modified.Count > 0) yield return $"{modified.Count} modified";
            if (added.Count > 0) yield return $"{added.Count} inserted";
            if (skipped > 0) yield return $"{skipped} skipped";
            if (errors.Count > 0)
            {
                yield return $"{errors.Count} errors";
                for (var c = 0; c < errors.Count && c < 3; c++) yield return errors[c];
            }
        }

        async Task<Guid?> getIdentityAsync(string externalId)
        {
            if (entities.TryGetValue(externalId, out var existing)) return existing;
            var (entity, identity) = await _identityAdapter.FindAsync(context, identityProvider, externalId);
            if (entity == null) return null;

            entities.Add(externalId, entity.Id);
            return entity.Id;
        }

        Dictionary<string, FormField> getColumnMap(FieldPermission permission)
        {
            var writableFields = objectType.Fields.Values
                .Where(x => x.RBAC.Can(context, permission))
                .Select(x => x.Field);

            var fields = writableFields.ToDictionary(x => x.Name);

            foreach (var field in writableFields)
            {
                if (string.IsNullOrEmpty(field.Label)) continue;
                fields.TryAdd(field.Label, field);
            }

            var result = new Dictionary<string, FormField>();
            foreach (var column in csv.Parser.Context.HeaderRecord)
            {
                if (fields.TryGetValue(column, out var field))
                {
                    result.Add(column, field);
                }
                else if (column.StartsWith("Entity:") && fields.TryGetValue(nameof(IEntityOwnedModel.EntityId), out var field2))
                {
                    // auto mapping 
                    result.Add(field2.Name, field2);
                }
            }

            return result.Count > 0 ? result : null;
        }
    }

    public async Task<bool> UpdateObjectStatusAsync(IEntityContext context, string objectTypeName, Guid objectId, Guid objectStatusId)
    {
        var objectType = await GetAsync(context, objectTypeName);
        if (objectType == null) throw new NotFoundException($"Invalid Object Type {objectTypeName}");

        return await UpdateObjectStatusAsync(context, objectType, objectId, objectStatusId);
    }

    /// <summary>
    /// Update Object Status
    /// if it hasn't changed, will return null 
    /// </summary>
    public async Task<bool> UpdateObjectStatusAsync(IEntityContext context, ObjectType objectType, Guid objectId, Guid objectStatusId)
    {
        // assumes that the object type is based on FlowObjectModel
        // just to rely on the driver to map the columns
        var query = _connection.Filter<FlowObjectModel>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(x => x.Id, objectId)
                .Ne(x => x.ObjectStatusId, objectStatusId)
                .AddConstraints(context, objectType)
            ;

        var now = DateTime.UtcNow;
        var result = await query
            .Update
            .Set(x => x.ObjectStatusId, objectStatusId)
            // .Set(x => x.ObjectStatusMilestones.Transitions[objectStatusId], now)
            // .Unset(x => x.ObjectStatusMilestones.TriggeredEvents)
            .Set(x => x.LastModifiedOn, now)
            .Set(x => x.LastActor, context.Actor()) // ???
            .UpdateOneAsync();

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Insert Object and fire Create event if is IFlowObject
    /// </summary>
    public async Task<T> InsertAsync<T>(IEntityContext context, T obj, Action<GenericFlowEvent> prepare = null)
        where T : EntityOwnedModel
    {
        obj = await _connection.InsertAsync(obj);

        // TODO: call validator for objectype
        // ...

        if (obj is IFlowObject flowObjectModel)
        {
            await FireCreateEventAsync(context, flowObjectModel, prepare);
        }

        return obj;
    }

    /// <summary>
    /// Create "Native Object" of T
    /// Will not enforce access rules
    /// </summary>
    [Obsolete("Should not use native objects")]
    public async Task<T> CreateObjectAsync<T>(IEntityContext context)
        where T : EntityOwnedModel, new()
    {
        var objectType = await GetAsync(context, typeof(T).Name);
        if (objectType == null) throw new ForbiddenException(context);

        return InitObject<T>(context, objectType);
    }

    /// <summary>
    /// Initialize object using object config defaults and context information
    /// TODO: should user initialValue for fields
    /// ... 
    /// </summary>
    [Obsolete("Should not use native objects")]
    public T InitObject<T>(IEntityContext context, ObjectType objectType)
        where T : EntityOwnedModel, new()
    {
        var id = typeof(T).GetCustomAttribute<UseObjectIdAttribute>() != null ? Model.NewObjectId() : Model.NewGuid();

        var obj = new T
        {
            Id = id,
            AccountId = context.AccountId.Value,
            EntityId = context.Role switch
            {
                EntityRoleId.Admin => context.AccountId.Value,
                EntityRoleId.Account => context.AccountId.Value,
                EntityRoleId.Manager => context.OrganizationId.Value,
                EntityRoleId.Organization => context.OrganizationId.Value,
                EntityRoleId.User => context.EntityId.Value,
                _ => throw new ForbiddenException(context, "Role can't create objects of this type") // ????
            },
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            LastActor = Actor.Current,
        };

        if (obj is FlowObjectModel flowObjectModel)
        {
            // get "custom" flowid for account/objecttype
            // ...

            if (objectType.InitialFlowId.HasValue)
            {
                flowObjectModel.FlowId = objectType.InitialFlowId.Value;
            }

            if (objectType.InitialObjectStatusId.HasValue)
            {
                flowObjectModel.ObjectStatusId = objectType.InitialObjectStatusId.Value;
            }
        }

        return obj;
    }

    /// <summary>
    /// Handle DataForm actions for ObjectType using the C# Natvie Type
    /// TODO: try to replace with generic handling (w/o Type)
    /// ...
    /// </summary>
    public Task<DataFormActionResponse> ExecObjectActionAsync<T>(IEntityContext context, DataFormActionRequest request)
        where T : EntityOwnedModel, new()
    {
        return ExecObjectActionAsync(context, typeof(T).Name, request);
    }

    public async Task<DataFormActionResponse> ExecObjectActionAsync(IEntityContext context, string objectTypeName, DataFormActionRequest request, GetFormOptions opts = null)
    {
        return request?.Action switch
        {
            FormAction.Add => await ExecAddObjectAsync(context, objectTypeName, request, opts),
            FormAction.Update => await ExecUpdateAsync(context, objectTypeName, request, new UpdateObjectOptions(opts)),
            FormAction.Delete => await ExecDeleteObjectAsync(context, objectTypeName, request),
            "Clone" => await ExecDeepCloneAsync(context, objectTypeName, request, new UpdateObjectOptions(opts)),
            _ => new DataFormActionResponse(request, "Unsupported Action")
        };
    }

    /// <summary>
    /// Calculate a deep clone of an existing object
    /// - it does using a BsonDocument to "preserve" all properties from the original...
    /// TODO: maybe the safest/cleanest would be to use an expando
    /// ...  
    /// </summary>
    private async Task<DataFormActionResponse> ExecDeepCloneAsync(IEntityContext context, string objectTypeName, DataFormActionRequest request, UpdateObjectOptions opts = null)
    {
        if (string.IsNullOrEmpty(objectTypeName))
        {
            // auto discover
            // ...
        }

        var objectType = await GetAsync(context, objectTypeName, opts);
        if (objectType == null) return new DataFormActionResponse(request, "Forbidden");

        var id = ParseId(request);
        if (!id.HasValue) return new DataFormActionResponse(request, "Missing Id");

        if (!objectType.Can(context, ObjectTypePermission.DeepClone)) return new DataFormActionResponse(request, "Forbidden");

        var query = _connection.Filter<BsonDocument>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(Model.IdFieldName, id.AsSerializedId())
                .AddConstraints(context, objectType)
            ;

        var source = await query.FirstOrDefaultAsync();
        if (source == null) return new DataFormActionResponse(request, "Not Found");

        // TODO: when we allow the user to customize the value before cloning
        // var values = request.Parameters ?? new Dictionary<string, object>();
        // ...

        var values = new Dictionary<string, object>();
        var error = CalculateInitialValues(context, objectType, values);
        if (error != null) return new DataFormActionResponse(request, $"Initial Values: {error}");

        // third pass, constraints
        EnforceConstraints(context, objectType, values);

        var result = Merge(source, values);
        if (!result.IsSuccess) return new DataFormActionResponse(request, $"Merge: {result.Status}");

        var newIdValue = result.Value[Model.IdFieldName];
        if (newIdValue.BsonType != BsonType.String || !Guid.TryParse(newIdValue.AsString, out var newId) || newId == id.Value)
        {
            return new DataFormActionResponse(request, "Error generating new _id");
        }

        // TODO: ideally we would make sure it doesn't fail any unique index
        // ...

        await _connection.GetCollection<BsonDocument>(objectType.CollectionName, objectType.DatabaseName).InsertOneAsync(result.Value);

        var newObject = await GetFlatObjectAsync(context, objectType, newId);
        var evt = await FireCreateEventAsync(context, objectType, newObject, newId, e =>
        {
            e.Description ??= $"{e.ObjectType} Cloned";
            e.Action ??= "ObjectCloned";
            e.SetMetaValue("Source", id.Value);
            e.SetRefValue(objectType.FullName, id.Value);
        });

        return new DataFormActionResponse(request, "Cloned")
        {
            Success = true,
            Ids = [newId],
            NextUrl = BuildDataFormUrl(objectType, newId, FormName.Edit, "Cloned Object"),
            RunId = evt?.RunId,
        };
    }

    private Result<BsonDocument> Merge(BsonDocument into, IDictionary<string, object> values)
    {
        var result = MergeValue(into, values);
        return !result.IsSuccess ? result.ConvertTo<BsonDocument>() : Result.Success(into);
    }

    /// <summary>
    /// Experimental merge of a BsonDocument applying values to it
    /// </summary>
    private Result<BsonValue> MergeValue(BsonValue into, object value)
    {
        switch (value)
        {
            case null:
            {
                return Result.Success<BsonValue>(BsonNull.Value);
            }
            case IDictionary<string, object> dict:
            {
                switch (into.BsonType)
                {
                    case BsonType.Document:
                    case BsonType.Null:
                        break;
                    default:
                        return Result.Error<BsonValue>($"Unexpected type: {into.BsonType}");
                }

                var doc = into.BsonType == BsonType.Document ? into.AsBsonDocument : new BsonDocument();
                foreach (var kvp in dict)
                {
                    if (!doc.TryGetValue(kvp.Key, out var currValue) || currValue == null || currValue.BsonType == BsonType.Null)
                    {
                        if (kvp.Value != null) doc[kvp.Key] = BsonValue.Create(kvp.Value);
                        continue;
                    }

                    var mergedValue = MergeValue(doc[kvp.Key], kvp.Value);
                    if (!mergedValue.IsSuccess) return Result.Error<BsonValue>($"{kvp.Key}: {mergedValue.Status}");
                    doc[kvp.Key] = mergedValue.Value;
                }

                return Result.Success<BsonValue>(doc);
            }
            case string str:
            {
                return (into.BsonType) switch
                {
                    BsonType.String or BsonType.Null => Result.Success(BsonValue.Create(str)),
                    _ => Result.Error<BsonValue>($"Unexpected type: {into.BsonType}"),
                };
            }
            case Guid uuid:
            {
                // by convention we serialize as string
                return (into.BsonType) switch
                {
                    BsonType.String or BsonType.Null => Result.Success(BsonValue.Create(uuid.ToString())),
                    _ => Result.Error<BsonValue>($"Unexpected type: {into.BsonType}"),
                };
            }
            case ObjectId oid:
            {
                return (into.BsonType) switch
                {
                    BsonType.ObjectId or BsonType.Null => Result.Success(BsonValue.Create(oid)),
                    _ => Result.Error<BsonValue>($"Unexpected type: {into.BsonType}"),
                };
            }
            case DateTime dt:
            {
                return (into.BsonType) switch
                {
                    BsonType.DateTime or BsonType.Null => Result.Success(BsonValue.Create(dt)),
                    _ => Result.Error<BsonValue>($"Unexpected type: {into.BsonType}"),
                };
            }
        }

        if (value.GetType().IsEnum)
        {
            // enum, by convention we serialize as string
            return (into.BsonType) switch
            {
                BsonType.String or BsonType.Null => Result.Success(BsonValue.Create(value.ToString())),
                _ => Result.Error<BsonValue>($"Unexpected type: {into.BsonType}"),
            };
        }

        if (value.GetType().IsPrimitive)
        {
            var bsonValue = BsonValue.Create(value);
            return (bsonValue.BsonType == into.BsonType || into.BsonType == BsonType.Null) ? Result.Success(bsonValue) : Result.Error<BsonValue>($"Unexpected type: {into.BsonType}");
        }

        // complex type?
        switch (into.BsonType)
        {
            case BsonType.Document:
            case BsonType.Null:
                break;
            default:
                return Result.Error<BsonValue>($"Unexpected type: {into.BsonType}");
        }

        if (value.GetType().GetProperties().IsEmpty())
        {
            // attempt to prevent an unexpected merge
            return Result.Error<BsonValue>($"Unexpected value: {value.GetType().FullName}");
        }

        // merge using properties
        var asDict = value.GetPropertiesAsDictionary();
        return MergeValue(into, asDict);
    }

    private async Task<DataFormActionResponse> ExecAddObjectAsync(IEntityContext context, string objectTypeName, DataFormActionRequest request, GetFormOptions opts = null)
    {
        if (string.IsNullOrEmpty(objectTypeName))
        {
            // TODO: auto discover based on the request (for discriminators)
            // ...
        }

        var objectType = await GetAsync(context, objectTypeName, opts);
        objectType = await ResolveSubTypeForUserInputAsync(context, objectType, request.Parameters, opts);
        if (!objectType.CanCreate(context)) throw new ForbiddenException(context, FormAction.Add);

        var result = await AddObjectAsync(context, objectType, request.Parameters, new AddObjectOptions(opts));
        if (!result)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        var message = result.Value.Skipped ? "Nothing to update" :
            result.Value.Existing ? $"{objectType.Name} Updated" :
            $"Created {objectType.Name}";

        var nextUrl = default(string);

        if (result.Value.FiredEvent != null)
        {
            // TODO: get the next UI step?
            var flatObject = await RecursivelyFlattenAsync(context, objectType, result.Value.Object);
            var nextUrlResult = await GetDefaultNextUrlAsync(context, objectType, flatObject, result.Value.FiredEvent);
            if (nextUrlResult.IsSuccess)
            {
                nextUrl = nextUrlResult.Value;
            }
        }

        return new DataFormActionResponse(request)
        {
            Success = true,
            Ids = new[] { result.Value.ObjectId },
            Message = message,
            NextUrl = nextUrl,
            RunId = result.Value.FiredEvent?.RunId,
        };
    }

    /// <summary>
    /// Get default "next url" to follow up one event with a get user input action in the foreground 
    /// </summary>
    public async Task<Result<string>> GetDefaultNextUrlAsync(IEntityContext context, ObjectType objectType, Dictionary<string, object> flatObject, FlowEvent initialEvent, EventType eventType = null)
    {
        // TODO: ideally the fields names would not be hardcoded (could use objectType to look for a referenceField to the right object types)
        // ...
        if (!flatObject.TryGetGuidParam(nameof(IFlowObject.FlowId), out var flowId)) return Result.Unknown<string>("Object is not part of a flow");
        if (!flatObject.TryGetGuidParam(Model.IdFieldName, out var objectId)) return Result.Error<string>("Couldn't determine object id");
        var objectStatusId = flatObject.GetOptionalGuid(nameof(IFlowObject.ObjectStatusId));

        var eventTypeId = eventType?.Id ?? Guid.Empty; // ?? initialEvent.

        // load flow and see if there is an action for this event of the type ActionFormAction
        var steps = (await GetStepsAsync(context, flowId, eventTypeId, objectStatusId)).ToArray();
        var userEventTypeIds = steps
            .Where(x => x.ActionId == ActionIds.GetUserInput && x.Options is GetUserInputActionOptions { NextEventId: not null })
            .Select(x => ((GetUserInputActionOptions)x.Options).NextEventId.Value)
            .ToArray();

        if (userEventTypeIds.Length < 1) return Result.Unknown<string>("No candidates for next event");

        // find one that the user has access to
        // TODO: does not fallback to AllProfileIds - don't know whether it should
        // ....
        var nextEventType = (await _connection.Filter<EventType>()
                .Eq(x => x.AccountId, context.AccountId)
                .In(x => x.Id, userEventTypeIds)
                .FindAsync())
            .FirstOrDefault(x => x.Trigger is UserTrigger userTrigger &&
                                 (
                                     (userTrigger.ProfileIds == null && userTrigger.Role == context.Role) ||
                                     (context.ProfileId.HasValue && userTrigger.ProfileIds != null && userTrigger.ProfileIds.Contains(context.ProfileId.Value)) ||
                                     (userTrigger.Role == null && userTrigger.ProfileIds == null)
                                 )
            );

        if (nextEventType == null) return Result.Unknown<string>("No candidates for user");

        // TODO: check options: should it run "in the foreground", send notification to continue later or either
        // ...

        _logger.LogInformation("{UserActionId} is the next step in the flow, handle here", nextEventType.Id);

        var loadedObjects = default(Dictionary<string, ObjectWithType>);
        if (eventType?.Trigger is UserTrigger { RelatedObjects: not null } trigger)
        {
            var loaded = await LoadRelatedObjectsAsync(context, trigger, eventType.ObjectType, flatObject);
            if (!loaded.IsSuccess) return loaded.ConvertTo<string>();

            loadedObjects = loaded.Value;
        }

        // upsert... it may already have been processed and added or it may not yet (no guarantees) 
        // do not set steps (the flow service will when processing the event)
        var flowRun = await UpsertAndGetFlowRunAsync(flatObject, initialEvent, loadedObjects: loadedObjects);

        return Result.Success($"dataForm://api/v1/{objectType.FullName}({objectId})/Flow({flowRun.Id})/UserAction({nextEventType.Id})");
    }

    private async Task<IEnumerable<FlowStep>> GetStepsAsync(IEntityContext context, Guid flowId, Guid eventId, Guid? leadStatusId)
    {
        var flow = await _connection.Filter<Flow>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, flowId)
            .FirstOrDefaultAsync();

        var steps = flow?.Steps ?? Enumerable.Empty<FlowStep>();

        // TODO: could filter in the query but...
        return steps.Where(x =>
            (!x.CurrentStatusId.HasValue || x.CurrentStatusId.Value == leadStatusId) && x.EventIdTrigger == eventId
        );
    }

    private static void PrepareObjectCreatedEvent(GenericFlowEvent e)
    {
        e.Description ??= $"{e.ObjectType} Created";
        e.Action ??= "ObjectCreated";
    }

    public Task<GenericFlowEvent> FireCreateEventAsync<T>(IEntityContext context, T obj, Action<GenericFlowEvent> prepare = null)
        where T : IFlowObject
        => FireEventAsync(context, obj, EventIds.OnObjectCreated, FlowObjectEventRoute.Create.GetRoute(obj), prepare ?? PrepareObjectCreatedEvent);

    private async Task<GenericFlowEvent> FireCreateEventAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> obj, Guid objectId, Action<GenericFlowEvent> prepare = null)
    {
        var evt = BuildEvent<GenericFlowEvent>(context, objectType, obj, objectId, EventIds.OnObjectCreated);
        if (evt == null) return evt;

        (prepare ?? PrepareObjectCreatedEvent).Invoke(evt);

        await DispatchAsync(evt, FlowObjectEventRoute.Create.GetRoute(evt.ObjectType, objectId));
        await DispatchAsync(evt);

        return evt;
    }

    public Task<GenericFlowEvent> FireObjectUpdatedAsync<T>(IEntityContext context, T obj, IDictionary<string, object> modifiedFields, Action<GenericFlowEvent> prepare = null)
        where T : IFlowObject
    {
        var evt = BuildEvent<ObjectUpdatedEvent, T>(context, obj, EventIds.OnObjectUpdated);
        return FireObjectUpdatedEventAsync(obj.Id, evt, modifiedFields, prepare);
    }

    public async Task<GenericFlowEvent> FireObjectStatusUpdatedAsync<T>(IEntityContext context, T obj, Action<GenericFlowEvent> prepare = null)
        where T : IFlowObject
    {
        var evt = BuildEvent<GenericFlowEvent, T>(context, obj, EventIds.OnStatusEntered);

        prepare?.Invoke(evt);

        await DispatchAsync(evt);

        return evt;
    }

    public async Task<GenericFlowEvent> FireObjectUpdatedAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> obj, Guid objectId, IDictionary<string, object> modifiedFields, Action<GenericFlowEvent> prepare = null)
    {
        var evt = BuildEvent<ObjectUpdatedEvent>(context, objectType, obj, objectId, EventIds.OnObjectUpdated);
        if (evt == null) return evt;

        if (modifiedFields?.Count > 0)
        {
            var fieldLabels = objectType.Fields.Values
                .Where(x => modifiedFields.Keys.Any(y => y.StartsWith(x.Field.Name)))
                .Select(x => x.Field.Label ?? x.Field.Name);

            evt.Description = $"{objectType.Name} Updated: {string.Join(", ", fieldLabels)}";
        }

        return await FireObjectUpdatedEventAsync(objectId, evt, modifiedFields, prepare);
    }

    private async Task<GenericFlowEvent> FireObjectUpdatedEventAsync(Guid id, ObjectUpdatedEvent evt, IDictionary<string, object> modifiedFields, Action<GenericFlowEvent> prepare = null)
    {
        if (evt == null) return evt;

        evt.Action ??= "ObjectUpdated";

        if (modifiedFields?.Count > 0)
        {
            evt.Description ??= $"{evt.ObjectType} Updated: {string.Join(", ", modifiedFields.Keys)}";
            evt.ModifiedFields = modifiedFields.Keys.ToArray();
            evt.UpdatedValues = new Dictionary<string, object>(modifiedFields);
        }

        evt.Description ??= $"{evt.ObjectType} Updated";

        prepare?.Invoke(evt);

        var route = FlowObjectEventRoute.Update.GetRoute(evt.ObjectType, id);
        await DispatchAsync(evt, route);
        await DispatchAsync(evt);

        return evt;
    }

    public Task DispatchAsync(FlowEvent evt) => _messageBroker.DispatchAsync(evt);
    public Task DispatchAsync(FlowEvent evt, string additionalRoute) => _messageBroker.DispatchAsync(evt, additionalRoute);

    private static TOut BuildEvent<TOut, T>(IEntityContext context, T obj, Guid eventTypeId)
        where T : IFlowObject
        where TOut : GenericFlowEvent, new()
    {
        if (!obj.FlowId.HasValue) return null;

        var evt = new TOut
        {
            ObjectType = obj.ObjectType,
            TargetId = obj.Id,
            AccountId = obj.AccountId,
            StatusId = obj.ObjectStatusId,
            FlowId = obj.FlowId.GetValueOrDefault(),
            Actor = context.Actor(),
            MetaValues = new Dictionary<string, object>
            {
                { obj.ObjectType, obj.Name },
                { nameof(IFlowObject.Name), obj.Name }
            },
            RefValues = getReferences().ToList(),
            EventTypeId = eventTypeId,
        };

        return evt;

        IEnumerable<KeyValuePair<string, object>> getReferences()
        {
            yield return new KeyValuePair<string, object>($"{obj.ObjectType}Id", obj.Id);

            if (context.OrganizationId.HasValue)
            {
                yield return new KeyValuePair<string, object>("EntityId", context.OrganizationId.Value);
                yield return new KeyValuePair<string, object>("OrganizationId", context.OrganizationId.Value);
            }

            if (context.UserId.HasValue)
            {
                yield return new KeyValuePair<string, object>("EntityId", context.UserId.Value);
                yield return new KeyValuePair<string, object>("UserId", context.UserId.Value);
            }
        }
    }

    private static TOut BuildEvent<TOut>(IEntityContext context, ObjectType objectType, IDictionary<string, object> dict, Guid objectId, Guid eventTypeId)
        where TOut : GenericFlowEvent, new()
    {
        var objectTypeFullName = objectType.FullName;
        var safeObjectTypeName = objectType.SafeFullName;
        if (objectType.TryGetObjectTypeFromFlowField(out var ot))
        {
            objectTypeFullName = ot;
            safeObjectTypeName = ObjectType.GetSafeFullName(ot);
        }

        if (!dict.TryGetGuidParam(nameof(IFlowObject.FlowId), out var flowId) ||
            !dict.TryGetGuidParam(nameof(IFlowObject.AccountId), out var accountId))
        {
            return null;
        }

        var metaValues = new Dictionary<string, object>();
        if (dict.TryGetStrParam(nameof(IFlowObject.Name), out var name))
        {
            // TODO: should it be the safe full name
            metaValues.Add(safeObjectTypeName, name);
            metaValues.Add(nameof(IFlowObject.Name), name);
        }

        var objectStatusId = default(Guid?);
        if (dict.TryGetGuidParam(nameof(IFlowObject.ObjectStatusId), out var tmp)) objectStatusId = tmp;

        var evt = new TOut
        {
            ObjectType = objectTypeFullName,
            TargetId = objectId,
            AccountId = accountId,
            StatusId = objectStatusId,
            FlowId = flowId,
            Actor = context.Actor(),
            MetaValues = metaValues,
            RefValues = getReferences().ToList(),
            EventTypeId = eventTypeId,
        };

        return evt;

        IEnumerable<KeyValuePair<string, object>> getReferences()
        {
            yield return new KeyValuePair<string, object>($"{objectTypeFullName}Id", objectId);

            if (context.OrganizationId.HasValue)
            {
                yield return new KeyValuePair<string, object>("EntityId", context.OrganizationId.Value);
                yield return new KeyValuePair<string, object>("OrganizationId", context.OrganizationId.Value);
            }

            if (context.UserId.HasValue)
            {
                yield return new KeyValuePair<string, object>("EntityId", context.UserId.Value);
                yield return new KeyValuePair<string, object>("UserId", context.UserId.Value);
            }
        }
    }

    public async Task<GenericFlowEvent> FireEventAsync<T>(IEntityContext context, T obj, Guid eventTypeId, string additionalRoute = null, Action<GenericFlowEvent> prepare = null)
        where T : IFlowObject
    {
        var evt = BuildEvent<GenericFlowEvent, T>(context, obj, eventTypeId);
        if (evt == null) return evt;

        prepare?.Invoke(evt);

        if (!string.IsNullOrEmpty(additionalRoute))
        {
            await DispatchAsync(evt, additionalRoute);
        }

        await DispatchAsync(evt);

        return evt;
    }

    public Task<ExpandoObject> GetExpandoObjectByExternalIdAsync(IEntityContext context, ObjectType objectType, string externalId)
    {
        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(nameof(IExternalId.ExternalId), externalId)
                .AddConstraints(context, objectType)
            ;

        return query.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Delete object by id
    /// - Check RBAC
    /// - context has to have access to get the object (constraints)
    /// </summary>
    /// <param name="context"></param>
    /// <param name="objectType"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    /// <exception cref="ForbiddenException"></exception>
    /// <exception cref="NotFoundException"></exception>
    public async Task<ExpandoObject> DeleteObjectByIdAsync(IEntityContext context, ObjectType objectType, Guid id)
    {
        if (!objectType.CanDelete(context)) throw new ForbiddenException();

        var obj = await GetExpandoObjectByIdAsync(context, objectType, id);
        if (obj == null) throw new NotFoundException(objectType.FullName, id);

        // TODO: delete or simply archive (some setting?)
        // ...

        var result = await _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(Model.IdFieldName, id.AsSerializedId())
            .AddConstraints(context, objectType)
            .DeleteOneAsync();

        if (!result)
        {
            throw new ForbiddenException("Failed to delete object");
        }

        // TODO: fire event 
        // ...

        return obj;
    }

    /// <summary>
    /// 
    /// </summary>
    private Task<ExpandoObject> GetReferencedObjectAsync(IEntityContext context, ObjectType objectType, string foreignFieldName, object foreignFieldValue)
    {
        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(foreignFieldName ?? Model.IdFieldName, foreignFieldValue)
                .AddConstraints(context, objectType)
            ;

        return query.FirstOrDefaultAsync();
    }

    public async Task<ExpandoObject> GetExpandoObjectByIdAsync(IEntityContext context, ObjectType objectType, Guid id)
    {
        if (objectType.Constraints != null)
        {
            var allConditions = objectType.GetConditions(context).ToArray();
            var existsConditions = allConditions.Where(x => x.Operator == Operator.Exists).ToArray();
            if (existsConditions.Length > 0)
            {
                return await GetExpandoObjectByIdUsingAggregationAsync(context, objectType, id, existsConditions, allConditions);
            }

            return await _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(Model.IdFieldName, id.AsSerializedId())
                .AddConditions(context, allConditions)
                .FirstOrDefaultAsync();
        }
        
        // old implementation with handling for default/missing objectType.Constraints
        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(Model.IdFieldName, id.AsSerializedId())
                .AddConstraints(context, objectType)
            ;

        return await query.FirstOrDefaultAsync();
    }

    private async Task<ExpandoObject> GetExpandoObjectByIdUsingAggregationAsync(IEntityContext context, ObjectType objectType, Guid id, Condition[] existsConditions, Condition[] allConditions)
    {
        // TODO: need to do an aggregation to enforce exists constraints 
        // ...

        var otherStages = new List<BsonDocument>();
        foreach (var condition in existsConditions)
        {
            var relation = objectType.RelatedObjectTypes?.FirstOrDefault(x => x.Name == condition.FieldName);
            if (relation == null || !relation.RBAC.CanRead(context))
            {
                _logger.LogError("{ObjectType}: can't enforce exists {Constraint} (relation)", objectType.FullName, condition.FieldName);
                return null;
            }

            var relatedObject = await GetAsync(context, relation.ObjectType);
            if (relatedObject == null || !relatedObject.CanRead(context))
            {
                _logger.LogError("{ObjectType}: can't enforce exists {Constraint} (related object type)", objectType.FullName, condition.FieldName);
                return null;
            }

            var lookup = ObjectTypeDataViewResponseBuilder.CreateLookup(relation, relatedObject, relation.Name, true);
            var lookupStage = BuildLookupWithCriteriaStage(context, relatedObject.GetConditions(context).ToArray(), lookup);
            otherStages.Add(lookupStage);
            otherStages.Add(new BsonDocument("$unwind", $"${lookup.As}"));
            otherStages.Add(new BsonDocument("$project", new BsonDocument(lookup.As, 0)));
        }

        var result = await _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(Model.IdFieldName, id.AsSerializedId())
            .Limit(1)
            .AddConditions(context, allConditions.Where(x => x.Operator != Operator.Exists))
            .AggregateAsync<ExpandoObject>(otherStages);

        return result.FirstOrDefault();
    }

    private BsonDocument BuildLookupWithCriteriaStage(IEntityContext context, Condition[] allConditions,  DataViewResponseBuilder.Lookup lookup)
    {
        // TODO: add field substitutes 
        // ...         
        allConditions.ReplaceValuePlaceHolders(context);

        // var exists = allConditions.Where(x => x.Operator == Operator.Exists).ToArray();
        // var basicConditions = allConditions.Where(x => x.Operator != Operator.Exists).ToArray();

        // does not support exists constraints yet :(
        // it will just fail
        var pipeline = new BsonArray
        {
            new BsonDocument("$match", new BsonDocument(DataViewResponseBuilder.BuildMatch(lookup.ForeignFieldName, allConditions))), // basicConditions
            new BsonDocument("$limit", 1),
            new BsonDocument("$project", new BsonDocument("_id", 1)),
        };
        
        // if (!exists.IsEmpty())
        // {
        //     // TODO: would need to continue adding lookups recursively            
        // }

        return new BsonDocument(
            "$lookup",
            new BsonDocument
            {
                { "from", lookup.ObjectType.CollectionName },
                { "as", lookup.As },
                { "foreignField", lookup.ForeignFieldName },
                { "localField", lookup.LocalFieldName },
                { "pipeline", pipeline }
            }
        );
    }

    public Task<Dictionary<string, object>> RecursivelyFlattenAsync(IEntityContext context, ObjectType objectType, ExpandoObject expando, GetObjectOptions opts = null)
        => RecursivelyFlattenAsync(context, objectType, (IDictionary<string, object>)expando, opts);

    private async Task<Dictionary<string, object>> RecursivelyFlattenAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> dynamicObject, GetObjectOptions opts = null)
    {
        // cache objects resolved 
        opts ??= new GetObjectOptions
        {
            Cache = new GetObjectCache(),
        };
        
        var result = new Dictionary<string, object>();
        if (objectType.Fields?.IsEmpty() ?? true)
        {
            return result;
        }

        foreach (var ft in objectType.Fields.Values)
        {
            if (!ft.RBAC.CanRead(context)) continue;

            var field = ft.Field;
            var value = default(object);
            if (field is IDynamicFieldValue)
            {
                switch (field)
                {
                    case CalculatedField calcField:
                    {
                        // calculate value
                        // ...
                        field = calcField.Field;
                        field.Name ??= calcField.Name;
                        field.Label ??= calcField.Label;
                        field.Description ??= calcField.Description;
                        value = calcField.CalculateValue(dynamicObject);
                        break;
                    }
                }
            }
            else
            {
                // try to get it from dynamic
                if (!dynamicObject.TryGetFieldValue(ft.Field.Name, out value)) continue;
            }

            if (value == null) continue;

            if (field is IComplexFieldValue)
            {
                switch (field)
                {
                    case RelatedObjectsField:
                        // ignore?
                        break;

                    case ObjectField objectField:
                    {
                        if (value is IDictionary<string, object> objectValue)
                        {
                            var childObjectTypeName = objectField.ObjectFieldOptions?.ObjectType;
                            if (childObjectTypeName == "*")
                            {
                                result[field.Name] = objectValue;
                                break;
                            }

                            var childObjectType = string.IsNullOrEmpty(childObjectTypeName) ? null : await GetAsync(context, childObjectTypeName, opts);
                            if (childObjectType != null)
                            {
                                result[field.Name] = await RecursivelyFlattenAsync(context, childObjectType, objectValue, opts);
                            }
                        }

                        break;
                    }

                    case ChildrenField childrenField:
                    {
                        if (childrenField.ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.IndexKeyType)
                        {
                            result[childrenField.Name] = await convertArrayAsync(childrenField, value);
                        }
                        else
                        {
                            result[childrenField.Name] = await convertDictAsync(childrenField, value);
                        }

                        break;
                    }
                }

                continue;
            }

            // "simple": simply copy value
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                continue;
            }

            result[field.Name] = value;
        }

        return result;

        async Task<object> convertDictAsync(ChildrenField childrenField, object value)
        {
            var childObjectTypeName = childrenField.ChildrenFieldOptions?.ObjectType;
            var childObjectType = string.IsNullOrEmpty(childObjectTypeName) ? null : await GetAsync(context, childObjectTypeName, opts);
            if (value is not IDictionary<string, object> objectValue)
            {
                _logger.LogError("Unexpected ");
                return null;
            }

            var converted = new Dictionary<string, object>();
            foreach (var kvp in objectValue)
            {
                if (kvp.Value is not IDictionary<string, object> childDict)
                {
                    _logger.LogError("Failed to convert {Child}", kvp.Key);
                    continue;
                }

                converted[kvp.Key] = await RecursivelyFlattenAsync(context, childObjectType, childDict, opts);
            }

            return converted;
        }

        async Task<object> convertArrayAsync(ChildrenField childrenField, object value)
        {
            var childObjectTypeName = childrenField.ChildrenFieldOptions?.ObjectType;
            var childObjectType = string.IsNullOrEmpty(childObjectTypeName) ? null : await GetAsync(context, childObjectTypeName, opts);
            if (value is IEnumerable<object> list)
            {
                var resolved = new List<IDictionary<string, object>>();
                foreach (var child in list)
                {
                    if (child is IDictionary<string, object> childDict)
                    {
                        var converted = await RecursivelyFlattenAsync(context, childObjectType, childDict, opts);
                        resolved.Add(converted);
                        continue;
                    }

                    _logger.LogError("Failed to convert item for childrenfield");
                    resolved.Add(new Dictionary<string, object>());
                }

                return resolved;
            }

            _logger.LogError("Failed to convert childrenfield");

            return null;
        }
    }

    /// <summary>
    /// Get readable properties as a dictionary (the key is the field name)
    /// NOTE: it will not enforce RBAC for complex fields 
    /// </summary>
    public async Task<Dictionary<string, object>> UnsafeGetFlatObjectByIdAsync(IEntityContext context, ObjectType objectType, Guid id)
    {
        var obj = await GetExpandoObjectByIdAsync(context, objectType, id);
        return obj == null ? null : objectType.UnsafeFlatten(context, obj);
    }

    public async Task<bool> AddObjectToFlowRunAsync(IEntityContext context, ObjectType objectType, Guid id, FlowRun flowRun, string alias = null)
    {
        var flat = await GetFlatObjectAsync(context, objectType, id);
        if (flat == null)
        {
            _logger.LogError("Failed to get flatObject {ObjectType}{ObjectId}", objectType.FullName, id);
            return false;
        }

        return await AddObjectToFlowRunAsync(context, objectType, flat, flowRun, alias);
    }

    public Task<bool> AddObjectToFlowRunAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> flat, FlowRun flowRun, string alias = null)
        => AddObjectToFlowRunAsync(context, objectType, flat, flowRun.Id, alias);

    public async Task<bool> AddObjectToFlowRunAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> flat, Guid flowRunId, string alias = null)
    {
        alias = FlowRun.GetObjectAlias(alias ?? objectType.FullName);

        await _connection.Filter<FlowRun>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.Id, flowRunId)
            .Update
            .Set(x => x.Objects[alias], new ObjectWithType
            {
                ObjectType = objectType.FullName,
                Object = flat
            })
            .Set(x => x.LastActor, context.Actor())
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateOneAsync();

        return true;
    }

    /// <summary>
    /// Get Object honoring RBAC (for complex fields as well)
    /// </summary>
    public async Task<Dictionary<string, object>> GetFlatObjectAsync(IEntityContext context, ObjectType objectType, Guid id)
    {
        var obj = await GetExpandoObjectByIdAsync(context, objectType, id);
        if (obj == null) return null;

        var flatObject = await RecursivelyFlattenAsync(context, objectType, obj);
        return flatObject;
    }

    /// <summary>
    /// Get Referenced object using foreign field / value 
    /// </summary>
    public async Task<Dictionary<string, object>> GetFlatReferencedObjectAsync(IEntityContext context, ObjectType objectType, string foreignField, object foreignFieldValue)
    {
        var obj = await GetReferencedObjectAsync(context, objectType, foreignField, foreignFieldValue);
        if (obj == null) return null;

        var flatObject = await RecursivelyFlattenAsync(context, objectType, obj);
        return flatObject;
    }

    public Task<List<ExpandoObject>> GetExpandoObjectsByIdAsync(IEntityContext context, ObjectType objectType, IEnumerable<Guid> ids)
    {
        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .In(Model.IdFieldName, ids.Select(x => x.AsSerializedId()))
                .AddConstraints(context, objectType)
            ;

        return query.FindAsync();
    }

    private async Task<DataFormActionResponse> ExecDeleteObjectAsync(IEntityContext context, string objectTypeName, DataFormActionRequest request)
    {
        if (string.IsNullOrEmpty(objectTypeName))
        {
            // auto discover
            // ...
        }

        var objectType = await GetAsync(context, objectTypeName);
        if (objectType == null) return new DataFormActionResponse(request, "Forbidden");

        if (request.SelectedIds == null || request.SelectedIds.Length < 1)
        {
            // TODO: get id field from object
            // ...
            if (request.Parameters?.TryGetGuidParam("_id", out var id) ?? false)
            {
                request.SelectedIds = new[] { id };
            }
        }

        return await DeleteObjectAsync(context, objectType, request);
    }

    private async Task<DataFormActionResponse> DeleteObjectAsync(IEntityContext context, ObjectType objectType, DataFormActionRequest request)
    {
        if (!objectType.CanDelete(context)) throw new ForbiddenException(context, "Delete");

        Guid[] objectIds = request.SelectedIds?.Distinct().ToArray();
        if (objectIds == null)
        {
            var objectId = ParseId(request);
            if (objectId.HasValue) objectIds = new[] { objectId.Value };
        }

        if (objectIds == null || objectIds.Length < 1) throw new BadRequestException("Missing id");

        var deleted = await GetExpandoObjectsByIdAsync(context, objectType, objectIds);
        if (deleted.Count != objectIds.Length) throw new ForbiddenException(context, "Delete");

        var query = _connection.Filter<CustomObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value);

        var success = false;
        if (objectIds.Length == 1)
        {
            success = await query
                .Eq(x => x.Id, objectIds[0])
                .DeleteOneAsync();
        }
        else
        {
            // TODO: may need to delete one by one so it can fire the event only on success
            // ...

            if (!objectType.Can(context, ObjectTypePermission.BulkDelete)) throw new ForbiddenException(context, "BulkDelete");
            var deleteResult = await query.In(x => x.Id, objectIds).DeleteAsync();
            success = deleteResult == objectIds.Length;
        }

        // TODO: fire events
        // ...
        // if (success)
        // {
        //     foreach (var o in deleted)
        //     {
        //         await FireObjectUpdatedAsync(context, objectType, record, id.Value, e =>
        //         {
        //             e.Description = $"Updated {fieldList}";
        //             e.MetaValues ??= new Dictionary<string, object>();
        //             foreach (var modified in result.Value)
        //             {
        //                 e.MetaValues.TryAdd(modified.Key, modified.Value);
        //             }
        //         });

        //     }
        // }

        var subjective = objectIds.Length == 1 ? objectType.Name : $"{objectIds.Length} objects";
        var message = success ? $"{subjective} deleted successfully" : $"Failed to delete {subjective}";
        return new DataFormActionResponse(request, message)
        {
            Success = success,
            Ids = objectIds,
        };
    }

    /// <summary>
    /// Execute Update action
    /// </summary>
    private async Task<DataFormActionResponse> ExecUpdateAsync(IEntityContext context, string objectTypeName, DataFormActionRequest request, UpdateObjectOptions opts = null)
    {
        if (string.IsNullOrEmpty(objectTypeName))
        {
            // auto discover
            // ...
        }

        var objectType = await GetAsync(context, objectTypeName, opts);
        if (objectType == null) return new DataFormActionResponse(request, "Forbidden");

        return await ExecUpdateAsync(context, objectType, request, opts);
    }

    /// <summary>
    /// Execute Update action
    /// </summary>
    public async Task<DataFormActionResponse> ExecUpdateAsync(IEntityContext context, ObjectType objectType, DataFormActionRequest request, UpdateObjectOptions opts = null)
    {
        // TODO: use KeyField from DataView?
        // ...
        var id = ParseId(request);

        if (!objectType.CanUpdate(context)) throw new ForbiddenException(context, "Edit");
        var expando = id.HasValue ? await GetExpandoObjectByIdAsync(context, objectType, id.Value) : null;
        if (expando == null)
        {
            // error
            // ...
            return null;
        }

        if (objectType.Discriminator != null)
        {
            objectType = await ResolveSubTypeAsync(context, objectType, expando, opts);
        }

        var result = await UpdateObjectAsync(context, objectType, request.Parameters, id.Value, expando, opts ?? new UpdateObjectOptions());
        if (result.IsError)
        {
            return new DataFormActionResponse(request)
            {
                Success = false,
                Message = result.Status,
            };
        }

        if (result.IsUnknown)
        {
            return new DataFormActionResponse(request)
            {
                Success = true,
                Message = result.Status,
            };
        }

        if (result.Value.Skipped)
        {
            return new DataFormActionResponse(request)
            {
                Success = true,
                Message = "Nothing to update",
            };
        }

        var nextUrl = default(string);
        if (result.Value.FiredEvent != null)
        {
            // TODO: get the next UI step?
            var flatObject = await RecursivelyFlattenAsync(context, objectType, result.Value.Object);
            var nextUrlResult = await GetDefaultNextUrlAsync(context, objectType, flatObject, result.Value.FiredEvent);
            if (nextUrlResult.IsSuccess)
            {
                nextUrl = nextUrlResult.Value;
            }
        }

        var fieldLabels = objectType.Fields.Values
            .Where(x => result.Value.UpdatedFields.Keys.Any(y => y.StartsWith(x.Field.Name)))
            .Select(x => x.Field.Label ?? x.Field.Name);

        var fieldList = string.Join(", ", fieldLabels);

        return new DataFormActionResponse(request)
        {
            Success = true,
            Ids = new[] { id.Value },
            Message = $"Updated properties: {fieldList}",
            NextUrl = nextUrl,
            RunId = result.Value.FiredEvent?.RunId,
        };
    }

    private class ParsedUserInputForUpdate
    {
        public Dictionary<string, object> ModifiedFields { get; set; }
        public Dictionary<string, object> FlatPreview { get; set; }
    }

    private async Task<Result<ParsedUserInputForUpdate>> ParseUserInputForUpdateAsync(
        IEntityContext context,
        ObjectType objectType,
        Form.Models.Form form,
        ExpandoObject currExpando,
        IDictionary<string, object> input,
        UpdateObjectOptions options
    )
    {
        var modifiedFields = new Dictionary<string, object>();
        foreach (var field in form.Fields)
        {
            // skip "front end" only fields
            if (field.Name.StartsWith("#")) continue;

            // can't modify id field (it was used to determine the id of the object already)
            // TODO: use the DataView.KeyField, infer from the visibility, ... 
            // ... 
            if (field.Name is nameof(Model.Id) or Model.IdFieldName) continue;

            // readonly field
            if (field.IsReadOnly) continue;

            var apiName = options.GetApiName(field);
            input.TryGetValue(apiName, out var newValue);

            if (newValue == null)
            {
                if (options.PartialUpdate)
                {
                    // partial update, ignore defaults
                    continue;
                }

                if (field.DefaultValue is string defaultValue)
                {
                    if (!ExpressionEvaluatorService.TryResolve(context, null, defaultValue, out var resolved))
                    {
                        _logger.LogError("Couldn't resolve {DefaultValue} expression", defaultValue);
                        return Result<ParsedUserInputForUpdate>.Error($"Couldn't resolve DefaultValue for {field.Name}");
                    }

                    newValue = resolved;
                }
                else if (field.DefaultValue is IEnumerable<object> en)
                {
                    if (!ExpressionEvaluatorService.TryResolve(context, null, en, out var resolved))
                    {
                        _logger.LogError("Couldn't resolve {DefaultValue} expression", en);
                        return Result<ParsedUserInputForUpdate>.Error($"Couldn't resolve DefaultValue for {field.Name}");
                    }

                    newValue = resolved;
                }
                else
                {
                    newValue = field.DefaultValue;
                }
            }

            if (newValue == null)
            {
                if (options.PartialUpdate)
                {
                    // partial update, ignore nulls
                    continue;
                }

                if (field.IsRequired)
                {
                    _logger.LogError("Required {Field} for {ObjectType} is null", field.Name, objectType.FullName);
                    return Result<ParsedUserInputForUpdate>.Error($"Missing required field: {field.Label ?? field.Name}");
                }
            }

            var update = await SetFieldValueAsync(context, field, currExpando, modifiedFields, newValue, options);
            if (update.IsError)
            {
                return update.ConvertTo<ParsedUserInputForUpdate>();
            }
        }

        var result = new ParsedUserInputForUpdate
        {
            ModifiedFields = modifiedFields,
        };

        if (modifiedFields.IsEmpty()) return Result.Success(result);

        var calcFields = objectType.Fields.Values
            .Where(x => x.CalculatedValue != null)
            .ToArray();

        if (calcFields.Length > 0)
        {
            // merge current values with modified so it can calculate expressions using modified + unmodified fields. 
            result.FlatPreview = await CalculateFlatPreviewAsync(context, objectType, currExpando, modifiedFields);

            foreach (var field in calcFields)
            {
                var valueOrExpression = field.CalculatedValue;
                if (!TryResolveExpression(context, field, result.FlatPreview, valueOrExpression, out var modifiedValue)) continue;

                var update = await SetFieldValueAsync(context, field.Field, currExpando, modifiedFields, modifiedValue, options);
                if (update.IsError)
                {
                    return update.ConvertTo<ParsedUserInputForUpdate>();
                }

                // update merged object so it can be used in other calculations
                result.FlatPreview[field.Field.Name] = modifiedValue;
            }
        }

        return Result.Success(result);
    }

    /// <summary>
    /// compare new value with current value for field and add to modified fields if changed. 
    /// </summary>
    private async Task<Result<object>> SetFieldValueAsync(
        IEntityContext context,
        FormField field,
        IDictionary<string, object> currentExpando,
        IDictionary<string, object> modifiedFields,
        object userInput,
        GetFormOptions opts = null)
    {
        var result = await GetFieldValueFromUserInputAsync(context, field, userInput, new GetValuesFromInputOptions
            {
                GetFormOptions = opts,
            }
        );

        if (result.IsError) return result;

        userInput = result.Value;

        var currValue = currentExpando.ResolveValue(field.Name.Split('|'));

        if (userInput == null)
        {
            if (currValue != null)
            {
                // unset current
                modifiedFields.Add(field.Name, userInput);
            }

            return Result.Success(userInput);
        }

        if (currValue == null)
        {
            // current value is null, can just set the entire object
            modifiedFields.Add(field.Name, userInput);
            return Result.Success(userInput);
        }

        if (currValue is IDictionary<string, object> currValueDict && userInput is IDictionary<string, object> userInputDict)
        {
            DeepCompare(currValueDict, userInputDict, modifiedFields, field.Name);
            return Result.Success(userInput);
        }

        // TODO: dictionary, object[],  ... 
        // ...

        // TODO: what about "complex" types?
        // ...
        if (!IsEqual(field, currValue, userInput))
        {
            modifiedFields.Add(field.Name, userInput);
        }

        return Result.Success(userInput);
    }

    private void DeepCompare(IDictionary<string, object> current, IDictionary<string, object> input, IDictionary<string, object> modifiedFields, string prefix = null)
    {
        foreach (var field in input)
        {
            var fieldPath = prefix != null ? $"{prefix}|{field.Key}" : field.Key;
            if (!current.TryGetValue(field.Key, out var currentValue) || currentValue == null)
            {
                // was null 
                if (field.Value != null)
                {
                    modifiedFields.Add(fieldPath, field.Value);
                }

                continue;
            }

            if (field.Value == null)
            {
                // unset
                modifiedFields.Add(fieldPath, field.Value);
                continue;
            }

            if (field.Value is IDictionary<string, object> inputDict && currentValue is IDictionary<string, object> currentDict)
            {
                // deep compare
                DeepCompare(currentDict, inputDict, modifiedFields, fieldPath);
                continue;
            }

            if (!Equals(currentValue, field.Value))
            {
                modifiedFields.Add(fieldPath, field.Value);
            }
        }
    }

    private bool TryResolveExpression(IEntityContext context, FieldTemplate field, IDictionary<string, object> modifiedFields, object valueOrExpression, out object modifiedValue)
    {
        if (valueOrExpression is not string str)
        {
            // not an expression
            modifiedValue = valueOrExpression;
            return true;
        }

        return ExpressionEvaluatorService.TryResolve(context, modifiedFields, str, out modifiedValue);
    }

    // TODO: replace with ExpressionEvaluationService
    // ....
    /// <summary>
    /// Post process "other fields"
    /// record==null => adding; else updating
    /// </summary>
    [Obsolete("replace with ExpressionEvaluationService")]
    private bool DeprecatedTryResolveExpression(IEntityContext context, FieldTemplate field, IDictionary<string, object> modifiedFields, object valueOrExpression, out object modifiedValue)
    {
        if (valueOrExpression is not string str || !str.StartsWith("{{") || !str.EndsWith("}}"))
        {
            // not an expression
            modifiedValue = valueOrExpression;
            return true;
        }

        if (context.DeprecatedTryResolveExpression(str, out var resolved))
        {
            // resolved using context/new ...
            modifiedValue = resolved;
            return true;
        }

        var matches = Regex.Matches(str, "({{[_a-zA-Z\\.\\|0-9]+}})");
        if (matches.Count > 1)
        {
            var newStr = str;
            foreach (var m in matches)
            {
                var expr = m.ToString();
                if (expr == null)
                {
                    modifiedValue = null;
                    return false;
                }

                var fieldName = expr[2..^2];
                if (!modifiedFields.TryGetValue(fieldName, out var fv) || fv == null)
                {
                    // copy of another field
                    modifiedValue = null;
                    return false;
                }

                newStr = newStr.Replace(expr, fv.ToString());
            }

            modifiedValue = newStr;
            return true;
        }

        var tokens = str[2..^2].Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 1)
        {
            if (modifiedFields.TryGetValue(tokens[0], out var otherFieldValue))
            {
                // copy of another field
                modifiedValue = field.Field.AutoConvert(otherFieldValue);
                return true;
            }

            _logger.LogInformation("{OtherField} was not modified. Can't use it in {Expression} for {Field}", tokens[0], valueOrExpression, field.Field.Name);
            modifiedValue = null;
            return false;
        }

        // basic functions using other fields
        if (tokens.Length >= 2)
        {
            // for now assume all arguments for functions are (string) fields
            var args = new string[tokens.Length - 1];
            for (var c = 1; c < tokens.Length; c++)
            {
                if (!modifiedFields.TryGetValue(tokens[c], out var otherFieldValue))
                {
                    _logger.LogInformation("{OtherField} was not modified. Can't use it in {Expression} for {Field}", tokens[0], valueOrExpression, field.Field.Name);
                    modifiedValue = null;
                    return false;
                }

                if (otherFieldValue is not string fieldValueStr)
                {
                    // set to null, copy null
                    _logger.LogError("{Field} / {Expression}: {Value} is not string", field.Field.Name, valueOrExpression, otherFieldValue);
                    modifiedValue = null;
                    return true;
                }

                args[c - 1] = fieldValueStr;
            }

            // string functions
            switch (tokens[0])
            {
                case "firstName":
                case "lastName":
                    if (PersonName.TryParse(args[0], out var parsed))
                    {
                        modifiedValue = tokens[0] == "firstName" ? parsed.FirstName : parsed.LastName;
                        return true;
                    }

                    _logger.LogError("Couldn't parse name from {Name}", args[0]);
                    modifiedValue = null;
                    return false;

                case "fullName":
                    modifiedValue = string.Join(" ", args);
                    return true;

                case "normalize":
                    modifiedValue = field.Field switch
                    {
                        EmailField => Lead.GetNormalizedEmail(args[0]),
                        PhoneField => Lead.GetNormalizedPhoneNumber(args[0]),
                        // PostalCodeField
                        _ => args[0],
                    };
                    return true;
            }

            _logger.LogError("Unexpected {Function} in {Expression} for {Field}", tokens[0], valueOrExpression, field.Field.Name);
            modifiedValue = null;
            return false;
        }

        // ...

        _logger.LogError("Unexpected expression: {Expression} for {Field}", valueOrExpression, field.Field.Name);
        modifiedValue = null;
        return false;
    }

    private bool IsEqual(FormField field, object oldValue, object newValue)
    {
        if (field is TagsField)
        {
            if (oldValue == null || newValue == null) return oldValue == newValue;
            if (oldValue is IEnumerable<string> v1 && newValue is IEnumerable<string> v2)
            {
                return v1.Except(v2).IsEmpty() && v2.Except(v1).IsEmpty();
            }
        }

        // since the guid is stored as string (and when deserializing without a type the driver doesn't know it is a guid)
        if (newValue is Guid uuid && oldValue is string str)
        {
            return Equals(uuid.ToString(), str);
        }

        return Equals(oldValue, newValue);
    }

    /// <summary>
    /// Add object 
    /// - It uses Upsert so it will override if the externalId is unique
    /// </summary>
    [Obsolete("Only used by the import right now, should be replaced by the new version")]
    private async Task<Result<Guid?>> DeprecatedImportAddObjectAsync(IEntityContext context, ObjectType objectType, Dictionary<string, object> modifiedFields)
    {
        if (!modifiedFields.TryGetValue("_id", out var id))
        {
            // generate a new id
            id = GenerateId(context, objectType);
        }
        else
        {
            // remove id from modified fields since we will explicitly set it
            // setting twice causes error (even with the same value)
            modifiedFields.Remove("_id");
        }

        // seed if not set
        if (objectType.InitialFlowId.HasValue)
        {
            modifiedFields.TryAdd(nameof(IFlowObject.FlowId), objectType.InitialFlowId.Value);
        }

        if (objectType.InitialObjectStatusId.HasValue)
        {
            modifiedFields.TryAdd(nameof(IFlowObject.ObjectStatusId), objectType.InitialObjectStatusId.Value);
        }

        // use custom object just for reference
        var query = _connection.Filter<object>(objectType.CollectionName, objectType.DatabaseName)
                .AddConstraints(context, objectType)
            ;

        if (objectType.UniqueExternalId)
        {
            // unique external Id per objecttype/ACCOUNT
            if (!modifiedFields.TryGetStrParam(nameof(IExternalId.ExternalId), out var externalId))
            {
                return Result<Guid?>.Error("Missing required ExternalId");
            }

            query.Eq(nameof(IExternalId.ExternalId), externalId);
        }
        else
        {
            // TODO: should go the insert route explicitly?
            // if this _id is defined, it can be used to overwrite an existing item
            // .... 
            query.Eq("_id", id);
        }

        // will not set the _id if ExternalId is unique and one exists
        var updateQuery = query.Update.SetOnInsert("_id", id);

        if (objectType.CollectionName == nameof(CustomObject) || !objectType.IsCustom)
        {
            // for native objects and any object saved on CustomObject
            // implicitly set "Standard properties" if they haven't been explicitly defined
            if (!modifiedFields.ContainsKey(nameof(Model.CreatedOn)))
            {
                updateQuery.SetOnInsert(nameof(Model.CreatedOn), DateTime.UtcNow);
            }

            if (!modifiedFields.ContainsKey(nameof(CustomObject.ObjectTypeId)))
            {
                updateQuery.SetOnInsert(nameof(CustomObject.ObjectTypeId), objectType.Id);
            }

            if (!modifiedFields.ContainsKey(nameof(CustomObject.ObjectType)))
            {
                updateQuery.SetOnInsert(nameof(CustomObject.ObjectType), objectType.FullName);
            }

            if (!modifiedFields.ContainsKey(nameof(CustomObject.LastActor)))
            {
                updateQuery.Set(nameof(CustomObject.LastActor), Actor.Current);
            }
        }

        if (objectType.Constraints == null)
        {
            // old implicit way
            switch (context.Role)
            {
                case EntityRoleId.Root:
                    break;

                case EntityRoleId.Admin:
                    modifiedFields.TryAdd(nameof(IEntityOwnedModel.EntityId), context.AccountId.Value);
                    break;

                case EntityRoleId.Manager:
                    modifiedFields.TryAdd(nameof(IEntityOwnedModel.EntityId), context.OrganizationId.Value);
                    break;

                case EntityRoleId.User:
                    modifiedFields[nameof(IEntityOwnedModel.EntityId)] = context.UserId.Value;
                    break;

                default:
                    throw new ForbiddenException(context, "can't add object");
            }

            modifiedFields.Remove(nameof(IFlowObject.AccountId));
            updateQuery.SetOnInsert(nameof(IFlowObject.AccountId), context.AccountId.Value);
        }
        else
        {
            var conditions = objectType.GetEqConditions(context);

            foreach (var constraint in conditions)
            {
                var fieldName = FormField.GetPathInCollection(constraint.FieldName);
                var value = constraint.ResolveValue(context);
                modifiedFields.Remove(constraint.FieldName);
                updateQuery.SetOnInsert(fieldName, value);
            }
        }

        UpdateQuery(modifiedFields, updateQuery);

        var result = default(dynamic);
        try
        {
            result = await updateQuery.UpdateAndGetOneAsync(true);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "DuplicateKey")
        {
            // violation of unique index
            // the default one is to AccountId, EntityId, ObjectType, ExternalId
            _logger.LogError(ex, "Failed to add object");
            return Result<Guid?>.Error("There is already an object for the same Entity");
        }
        catch (MongoCommandException ex)
        {
            _logger.LogError(ex, "Failed to add object");
            return Result<Guid?>.Error("Error writing to database: " + ex.CodeName);
        }

        // set just to make the event work
        result.ObjectTypeId = objectType.Id;
        result.ObjectType = objectType.FullName;

        var iDict = (IDictionary<string, object>)result;
        if (!iDict.TryGetGuidParam("_id", out var objectId))
        {
            throw new Exception("Unexpected id");
        }

        await FireCreateEventAsync(context, objectType, iDict, objectId);

        await AddTagsToObjectTypeAsync(context, objectType, modifiedFields);

        return Result.Success<Guid?>(objectId);
    }

    /// <summary>
    /// just a fallback for objects that don't define a InitialValue for the _id field
    /// </summary>
    [Obsolete("after updating all object types, remove it")]
    private object GenerateId(IEntityContext context, ObjectType objectType)
    {
        if (objectType.Fields.TryGetValue("_id", out var fieldTemplate) && fieldTemplate.InitialValue != null)
        {
            if (fieldTemplate.InitialValue is string initialValue)
            {
                if (!ExpressionEvaluatorService.TryResolve(context, null, initialValue, out var resolved))
                {
                    _logger.LogError("Failed to resolve {InitialValue} for _id", initialValue);
                }
                else
                {
                    return resolved;
                }
            }

            // TODO: makes no sense as would mean that every new item would have the same _id
            // ...
            return fieldTemplate.InitialValue;
        }

        var useObjectId = false;

        // TODO: should use BackingType instead
        // ...
        if (!objectType.IsCustom)
        {
            try
            {
                var nativeType = objectType.GetNativeType();
                if (nativeType == null)
                {
                    _logger.LogError("Can't find {Class} for {ObjectType}: {NativeType}", objectType.NativeType, objectType.FullName, objectType.NativeType);
                }
                else
                {
                    useObjectId = nativeType.GetCustomAttribute<UseObjectIdAttribute>() != null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trying to check native type: {ObjectType}", objectType.FullName);
            }
        }

        return (useObjectId ? Model.NewObjectId() : Model.NewGuid()).AsSerializedId();
    }

    /// <summary>
    /// Update objects using "raw representation" (without using model type)
    /// </summary>
    public async Task<Result<UpdateObjectResult>> UpdateObjectAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> updates, Guid id, ExpandoObject currExpando, UpdateObjectOptions options)
    {
        using var scope = _logger.AddScope(new
        {
            ObjectType = objectType.FullName,
            Id = id,
            context.UserId
        });

        _logger.LogInformation("Update Object");

        if (!options.SkipObjectTypeValidation)
        {
            var errors = ValidateObjectType(objectType);
            if (errors != null) return Result<UpdateObjectResult>.Error(errors);
        }

        var formName = FormName.Edit;
        var form = await GetCustomizedFormAsync(context, objectType, formName, options) ?? BuildDataForm(objectType, context, formName, id, options);
        if (form == null) throw new ForbiddenException(context, "Can't edit");

        var parsedInput = await ParseUserInputForUpdateAsync(context, objectType, form, currExpando, updates, options);
        if (!parsedInput)
        {
            _logger.LogError("Update Failed: {Error}", parsedInput.Status);
            return parsedInput.ConvertTo<UpdateObjectResult>();
        }

        var modifiedFields = parsedInput.Value.ModifiedFields;
        if (modifiedFields == null || modifiedFields.Count == 0)
        {
            _logger.LogInformation("No changes detected, skip");
            return Result.Success(new UpdateObjectResult
            {
                Skipped = true,
                Object = currExpando,
            });
        }

        if (objectType.UniqueIndices != null)
        {
            parsedInput.Value.FlatPreview ??= await CalculateFlatPreviewAsync(context, objectType, currExpando, modifiedFields);

            var error = await checkUniqueIndicesAsync(parsedInput.Value.FlatPreview);
            if (error != null)
            {
                return Result<UpdateObjectResult>.Error(error);
            }
        }

        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .AddConstraints(context, objectType)
                .Eq(Model.IdFieldName, id)
                .Update
            ;

        UpdateQuery(modifiedFields, query);

        var updated = await query.UpdateAndGetOneAsync();
        if (updated == null)
        {
            return Result.Error<UpdateObjectResult>("Failed to update Object");
        }

        _logger.LogInformation("Object Updated: {Properties}", string.Join(", ", modifiedFields.Keys));

        await AddTagsToObjectTypeAsync(context, objectType, modifiedFields);

        return Result.Success(new UpdateObjectResult
        {
            Object = updated,
            UpdatedFields = modifiedFields,
            FiredEvent = await FireObjectUpdatedAsync(context, objectType, currExpando, id, modifiedFields),
        });

        async Task<string> checkUniqueIndicesAsync(IDictionary<string, object> previewObject)
        {
            foreach (var index in objectType.UniqueIndices)
            {
                var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                        .Ne(Model.IdFieldName, id)
                    // .AddConstraints(context, objectType)
                    ;

                foreach (var fieldName in index.Fields)
                {
                    if (!TryToInferFieldValue(context, objectType, previewObject, fieldName, out var fieldValue))
                    {
                        query = null;
                        break;
                    }

                    query.Eq(fieldName, fieldValue);
                }

                if (query == null) continue;

                var existingRecord = await query
                    .FirstOrDefaultAsync();

                if (existingRecord != null)
                {
                    return $"{index.Name}: Uniqueness conflict";
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Calculate preview of modified object (flat)
    /// </summary>
    private async Task<Dictionary<string, object>> CalculateFlatPreviewAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> currExpando, Dictionary<string, object> modifiedFields)
    {
        var result = await RecursivelyFlattenAsync(context, objectType, currExpando);

        foreach (var kvp in modifiedFields)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// only used by the import csv
    /// Update Object with already parsed [Field.Name=Value]
    /// Performs last minute validation (prevent leaks) 
    /// </summary>
    [Obsolete("Only used by the import right now, should be replaced by the new version")]
    private async Task<Result<IDictionary<string, object>>> DeprecatedImportUpdateObjectAsync(IEntityContext context, ObjectType objectType, Guid id, IDictionary<string, object> modifiedFields)
    {
        if (modifiedFields.Count == 0) return Result.Success(modifiedFields);

        // check context can update it?
        if (modifiedFields.TryGetGuidParam(nameof(Model.AccountId), out var accountId) && accountId != context.AccountId.Value) throw new ForbiddenException(context, "Can't modify this object");

        // for now only admin can change the EntityId
        // TODO: may need to allow changing by managers when entityid represents userid
        if (modifiedFields.ContainsKey(nameof(IEntityOwnedModel.EntityId)))
        {
            if (context.Role != EntityRoleId.Admin) throw new ForbiddenException(context, "Can't reassign object");
        }

        // external id can't change after it was set
        if (objectType.UniqueExternalId && modifiedFields.ContainsKey(nameof(CustomObject.ExternalId)))
        {
            var externalId = modifiedFields[nameof(CustomObject.ExternalId)];
            if (string.IsNullOrEmpty(externalId?.ToString()))
            {
                return Result<IDictionary<string, object>>.Error("Can't unset ExternalId");
            }

            var existing = await _connection.Filter<CustomObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ExternalId, externalId)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                return Result<IDictionary<string, object>>.Error("Duplicated ExternalId");
            }
        }

        var query = _connection.Filter<CustomObject>(objectType.CollectionName, objectType.DatabaseName)
                .AddConstraints(context, objectType)
                .Eq(x => x.Id, id)
                .Update
            ;

        if (objectType.CollectionName == nameof(CustomObject) || !objectType.IsCustom)
        {
            query
                .Set(x => x.LastActor, Actor.Current)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow);
        }

        UpdateQuery(modifiedFields, query);

        var updateResult = await query.UpdateOneAsync();
        if (updateResult.MatchedCount != 1)
        {
            return Result<IDictionary<string, object>>.Error("Object not found");
        }

        return Result.Success(modifiedFields);
    }

    private async Task AddTagsToObjectTypeAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> modifiedFields)
    {
        if (!context.UserId.HasValue) return;

        var tagField = objectType.Fields
            .Select(x => x.Value.Field)
            .OfType<TagsField>()
            .FirstOrDefault();

        if (tagField == null) return;

        if (!modifiedFields.TryGetValue(tagField.Name, out var value)) return;
        if (value is not IEnumerable<string> tags) return;

        var result = await _connection.Filter<ObjectTypeUserSettings>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.UserId.Value)
            .Eq(x => x.ObjectType, objectType.FullName)
            .Eq(x => x.Hash, null)
            .NotBuilder(q => q.All(x => x.Tags, tags))
            .Update
            .SetOnInsert(x => x.AccountId, context.AccountId.Value)
            .SetOnInsert(x => x.EntityId, context.UserId.Value)
            .SetOnInsert(x => x.ObjectType, objectType.FullName)
            .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
            .AddToSetEach(x => x.Tags, tags)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .UpdateOneAsync();

        if (result.ModifiedCount == 1)
        {
            _logger.LogInformation("Tags added to {ObjectType} user settings: {Tags}", objectType.FullName, tags);
        }

        // // special handling for tags for admins 
        // if (context.Role != EntityRoleId.Admin) return;

        // // add any new tags to the object
        // var result = await _connection.Filter<ObjectType>()
        //     .Eq(x => x.AccountId, context.AccountId.Value)
        //     .Eq(x => x.Id, objectType.Id)
        //     .NotBuilder(q => q.All(x => x.Tags, tags))
        //     .Update
        //         .AddToSetEach(x => x.Tags, tags)
        //         .Set(x => x.LastModifiedOn, DateTime.UtcNow)
        //         .Set(x => x.LastActor, context.Actor())
        //     .UpdateOneAsync();

        // if (result.ModifiedCount == 1)
        // {
        //     _logger.LogInformation("Tags added to {objectType}: {tags}", objectType.Name, tags);
        // }
    }

    /// <summary>
    /// Update "Update Query" with modified field values
    /// Currently replaces the entire "field"
    /// TODO: ideally it would not touch unmodified child properties
    /// ... 
    /// </summary>
    private static void UpdateQuery<T>(IDictionary<string, object> modifiedFields, UpdateQuery<T> query, string prefix = null)
    {
        foreach (var field in modifiedFields)
        {
            var propertyPath = FormField.GetPathInCollection(field.Key);
            if (prefix != null) propertyPath = $"{prefix}.{propertyPath}";

            // TODO: for []s, can enumerate and set index by index?
            // ...

            // there must be a clever way but this is so far the only way 
            // to avoid mongo adding type information
            if (field.Value is IEnumerable<string> strArray)
            {
                query.Set(propertyPath, strArray);
            }
            else if (field.Value is IEnumerable<Guid> guidArray)
            {
                query.Set(propertyPath, guidArray);
            }
            else if (field.Value is IEnumerable<DateTime> dateArray)
            {
                query.Set(propertyPath, dateArray);
            }
            else if (field.Value is Guid guid)
            {
                query.Set(propertyPath, guid.AsSerializedId());
            }
            else if (field.Value is IDictionary<string, object> dict)
            {
                // TODO: process properties independently?
                // ....
                // query.SetOrUnset(propertyPath, field.Value);
                UpdateQuery<T>(dict, query, propertyPath);
            }
            // TODO: other complex fields ([], dictionary, ...)
            // ...
            else
            {
                query.SetOrUnset(propertyPath, field.Value);
            }
        }
    }

    private static Guid? ParseId(DataFormActionRequest request)
    {
        if (!request.TryGetGuidParam(nameof(IFlowObject.Id), out var id) && !request.TryGetGuidParam("_id", out id))
        {
            return default;
        }

        return id;
    }

    /// <summary>
    /// Resolve Sub Type, if any, from user input information 
    /// </summary>
    public Task<ObjectType> ResolveSubTypeForUserInputAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> userInput, GetObjectOptions opts = null)
        => _ResolveSubTypeAsync(context, objectType, userInput, true, opts);
    
    /// <summary>
    /// Resolve Sub Type if any based on the record
    /// </summary>
    public Task<ObjectType> ResolveSubTypeAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> dynamicRecord, GetObjectOptions opts = null)
        => _ResolveSubTypeAsync(context, objectType, dynamicRecord, false, opts);

    /// <summary>
    /// Resolve Sub Type if any based on the record
    /// </summary>
    private async Task<ObjectType> _ResolveSubTypeAsync(IEntityContext context, ObjectType objectType, IDictionary<string, object> dynamicRecordOrUserInput, bool fromUserInput, GetObjectOptions opts = null)
    {
        if (objectType.Discriminator == null)
        {
            return objectType;
        }

        foreach (var discriminator in objectType.Discriminator)
        {
            var match = true;
            foreach (var condition in discriminator.Value.Conditions)
            {
                if (!objectType.Fields.TryGetValue(condition.FieldName, out var field))
                {
                    _logger.LogError("Evaluating {Discriminator}: {Field} does not exist in {ObjectType}", discriminator.Key, condition.FieldName, objectType.FullName);
                    match = false;
                    break;
                }

                // TODO: could allow condition.Value to be an expression so it could be dynamic (e.g. field A == field b)
                // ...

                var value = default(object);
                if (fromUserInput)
                {
                    // user input
                    var fieldPath = (opts?.UseFieldApiNames ?? false) ? (field.Field.ApiName ?? field.Field.Name) : field.Field.Name;
                    if (dynamicRecordOrUserInput.TryGetValue(fieldPath, out var fieldValue))
                    {
                        value = fieldValue;
                    }
                }
                else
                {
                    // dynamic object loaded from database
                    value = dynamicRecordOrUserInput.ResolvePathValue(field.Field.Name);    
                }
                
                if (!EvaluateCondition(condition, field.Field, value))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                var subType = await GetAsync(context, discriminator.Key, opts);
                return await _ResolveSubTypeAsync(context, subType, dynamicRecordOrUserInput, fromUserInput, opts);
            }
        }

        return objectType;
    }

    /// <summary>
    /// AnyTrue condition for a field
    ///     it has special handling for array fields (compared to the condition w/o knowledge of the field type)
    /// </summary>
    private static bool EvaluateCondition(Condition condition, FormField field, object fieldValue)
    {
        if (condition == null) return false;
        
        if (field.GetBackingType().IsArray)
        {
            if (fieldValue == null)
            {
                // current value is null
                return condition.Operator switch
                {
                    Operator.Eq => condition.Value == null,
                    Operator.Ne => condition.Value != null,
                    _ => false,
                };
            }

            // TODO: check if the value is an array as well 
            // ...
            // but for now just assume it is NOT and treat as in
            if (fieldValue is not IEnumerable<object> array)
            {
                // error?
                // ...
                return false;
            }

            var exists = array.Any(x => x.Equals(condition.Value));
            return condition.Operator switch
            {
                Operator.Eq => exists,
                Operator.Ne => !exists,
                _ => false,
            };
        }

        // copy of standard implementation for condition 
        var equals = (condition.Value == null && fieldValue == null) || (condition.Value != null && condition.Value.Equals(fieldValue));
        return condition.Operator switch
        {
            Operator.Eq => equals,
            Operator.Ne => !equals,
            // ...
            _ => false,
        };
    }
        
    /// <summary>
    /// Get form for Object using id 
    /// </summary>
    public async Task<Form.Models.Form> GetDataFormForObjectAsync(IEntityContext context, ObjectType objectType, Guid objectId, ExpandoObject expandoObject, FormName formName, GetFormOptions opts = null)
    {
        // use cache by default
        opts ??= new GetFormOptions
        {
            Cache = new GetFormCache(),
        };
        
        // TODO: handle other form names
        // ...

        // TODO: automatically build form from edit form when formName=="View" ????
        // make it readonly
        // add "edit" action?
        // ignore actions?
        // remove null fields?
        // ...

        switch (formName)
        {
            case FormName.View:
            case FormName.Edit:
            case FormName.Details:
                break;

            default:
                throw new BadRequestException("Invalid form");
        }

        var form = await GetEditDataFormAsync(context, objectType, objectId, expandoObject, formName, opts);

        if (form == null) throw new NotFoundException($"{objectType.FullName} not found.");

        // replace id in actions
        if (form.Actions != null)
        {
            foreach (var action in form.Actions)
            {
                action.Name = action.Name?.Replace("{{id}}", objectId.ToString());
            }
        }

        foreach (var field in form.Fields)
        {
            if (string.IsNullOrEmpty(field.Options?.LinkUrl)) continue;
            field.Options.LinkUrl = field.Options.LinkUrl.Replace("{{id}}", objectId.ToString());
        }

        return form;
    }

    private async Task AddRecentObjectAsync(IEntityContext context, ObjectType objectType, Guid objectId, Form.Models.Form form)
    {
        if (!context.UserId.HasValue) return;

        var nameField = objectType.LookupFields?.Name ?? nameof(Model.Name);
        var name = form.Fields.FirstOrDefault(x => x.Name == nameField)?.DefaultValue?.ToString();
        
        await AddRecentObjectAsync(context, objectType, objectId, name);
    }
    
    /// <summary>
    /// Add object to list of recent objects for the user
    /// - it will swallow any exceptions 
    /// </summary>
    public async Task AddRecentObjectAsync(IEntityContext context, ObjectType objectType, Guid objectId, string name = null)
    {
        try
        {
            if (!context.UserId.HasValue) return;
        
            // TODO: exclude when impersonating?
            // ...

            var now = DateTime.UtcNow;
            await _connection.Filter<RecentObject>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.EntityId, context.UserId.Value)
                .Eq(x => x.ObjectId, objectId)
                .Eq(x => x.ObjectType, objectType.FullName)
                .Update
                .SetOnInsert(x => x.Id, Guid.NewGuid())
                .SetOnInsert(x => x.AccountId, context.AccountId.Value)
                .SetOnInsert(x => x.EntityId, context.UserId.Value)
                .SetOnInsert(x => x.CreatedOn, now)
                .SetOnInsert(x => x.AllObjectTypes, objectType.GetLoadedBaseObjectTypeNames().Prepend(objectType.FullName))
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, now)
                .SetOrUnset(x => x.Name, name)
                .Inc(x => x.Count, 1)
                .UpdateOneAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add to recents");
        }
    }

    public async Task<Form.Models.Form> GetDataFormAsync(IEntityContext context, string objectTypeName, Guid? id, FormName formName = FormName.Edit, GetFormOptions opts = null)
    {
        if (string.IsNullOrEmpty(objectTypeName))
        {
            // auto discover
            throw new NotImplementedException("Auto discover not available yet");
        }

        if (!id.HasValue)
        {
            return await GetAddDataFormAsync(context, objectTypeName, opts);
        }

        var objectType = await GetAsync(context, objectTypeName);
        return await GetEditDataFormAsync(context, objectType, id.Value, formName, opts);
    }

    /// <summary>
    /// Get customized or build add form (including layouts)
    /// </summary>
    public async Task<Form.Models.Form> GetAddDataFormAsync(IEntityContext context, string objectTypeName, GetFormOptions opts = null)
    {
        var objectType = await GetAsync(context, objectTypeName);

        return await GetAddDataFormAsync(context, objectType, opts);
    }

    public async Task<Form.Models.Form> GetUpsertFormAsync(IEntityContext context, string objectTypeName, IDictionary<string, object> args, GetFormOptions opts = null)
    {
        var objectType = await GetAsync(context, objectTypeName);
        if (objectType?.UniqueIndices == null) return Form.Models.Form.BuildErrorForm($"{objectTypeName} not supported", $"Upsert {objectTypeName}");

        var (expando, index) = await FindUsingUniqueIndicesAsync(context, objectType, args);
        if (expando == null)
        {
            // didn't find the object, add
            var addForm = await GetAddDataFormAsync(context, objectType, opts);
            return SetDefaultValues(addForm, FormName.Add, args, true);
        }

        if (index is { Upsert: false }) return Form.Models.Form.BuildErrorForm($"{objectTypeName} does not support operation", $"Upsert {objectTypeName}");

        IDictionary<string, object> record = expando;
        if (!record.TryGetGuidParam(Model.IdFieldName, out var id)) return Form.Models.Form.BuildErrorForm($"Found object but _id is not UUID", $"Upsert {objectTypeName}");
        var updateForm = await GetDataFormForObjectAsync(context, objectType, id, expando, FormName.Edit, opts);
        return SetDefaultValues(updateForm, FormName.Edit, args);
    }

    private Form.Models.Form SetDefaultValues(Form.Models.Form form, FormName formName, IDictionary<string, object> args, bool disableFields = false)
    {
        if (form == null) throw new NotFoundException();

        var fields = form.Fields.ToDictionary(x => x.Name);
        foreach (var kvp in args)
        {
            if (!fields.TryGetValue(kvp.Key, out var field)) continue;
            if (field.IsReadOnly) continue;

            if (formName != FormName.Add && field.DefaultValue != null) continue;
            field.DefaultValue = field.AutoConvert(kvp.Value);
            if (disableFields) field.Enable = ["false"];
        }

        return form;
    }

    public async Task<Form.Models.Form> GetAddDataFormAsync(IEntityContext context, ObjectType objectType, GetFormOptions opts = null)
    {
        var form = default(Form.Models.Form);
        if (!(opts?.SkipLoadingCustomForm ?? false))
        {
            form = await GetCustomizedFormAsync(context, objectType, FormName.Add, opts);
        }

        form ??= await BuildAddFormAsync(context, objectType, opts?.LoadLayout ?? true, opts);

        opts?.FilterBeforeLoading?.Invoke(form);

        // seed placeholders if current context
        var placeHolders = context.GetPlaceholders();
        foreach (var field in form.Fields)
        {
            field.FillPlaceHolders(context, placeHolders);
        }

        return form;
    }

    /// <summary>
    /// Build add form using expandoObject for defaults
    /// </summary>
    public async Task<Form.Models.Form> BuildAddFormAsync(IEntityContext context, ObjectType objectType, TemplateObject templateObject, GetFormOptions opts = null)
    {
        var form = await GetCustomizedFormAsync(context, objectType, FormName.Add, opts);
        form ??= await BuildAddFormAsync(context, objectType, true, opts);

        // seed placeholders (using template values)
        foreach (var field in form.Fields)
        {
            field.FillPlaceHolders(context, templateObject.Object);
        }

        // override values with object template
        foreach (var field in form.Fields)
        {
            if (!templateObject.Object.TryGetFieldValue(field.Name, out var value))
            {
                // no value
                value = null;
            }

            await LoadFieldAsync(context, objectType, FormName.Add, field, value, opts);
        }

        return form;
    }

    public async Task<DataFormActionResponse> ExecAddObjectAsync(IEntityContext context, ObjectType objectType, DataFormActionRequest request, TemplateObject templateObject)
    {
        if (!objectType.CanCreate(context)) throw new ForbiddenException(context, FormAction.Add);

        var result = await AddObjectAsync(context, objectType, request.Parameters, new AddObjectOptions
        {
            IsUpsert = false,
            OnBeforeSerializing = apply,
        });

        if (!result)
        {
            return new DataFormActionResponse(request, result.Status);
        }

        var message = result.Value.Skipped ? "Nothing to update" :
            result.Value.Existing ? $"{objectType.Name} Updated" :
            $"Created {objectType.Name}";

        return new DataFormActionResponse(request)
        {
            Success = true,
            Ids = new[] { result.Value.ObjectId },
            Message = message,
            RunId = result.Value.FiredEvent?.RunId,
        };

        Result<IDictionary<string, object>> apply(IDictionary<string, object> parsed)
        {
            var unwrapped = AggregateSubDocumentsForCreation(parsed);
            if (!unwrapped.IsSuccess) return unwrapped.ConvertTo<IDictionary<string, object>>();

            var mergeResult = Merge(templateObject.Object, unwrapped.Value);
            if (!mergeResult.IsSuccess) return mergeResult;

            // reapply initial values (special implementation using setValue to expand paths) 
            // JUST TOP LEVEL: will not do anything for children/object fields
            foreach (var field in objectType.Fields.Values)
            {
                if (field.InitialValue == null) continue;

                if (!TryResolveExpression(context, field, mergeResult.Value, field.InitialValue, out var value))
                {
                    if (field.Field.IsRequired)
                    {
                        return Result.Error<IDictionary<string, object>>($"Couldn't calculate initial value for {field.Field.Name}");
                    }
                }

                var error = SetValue(mergeResult.Value, field.Field.Name.Replace('|', '.'), value);
                if (error != null) return Result.Error<IDictionary<string, object>>($"Couldn't update value for {field.Field.Name}: {error}");
            }

            // reapply constraints (special implementation using setValue to expand paths)
            // JUST TOP LEVEL: will not do anything for children/object fields
            var conditions = objectType.GetEqConditions(context);
            foreach (var constraint in conditions)
            {
                var value = constraint.ResolveValue(context);
                var error = SetValue(mergeResult.Value, constraint.FieldName.Replace('|', '.'), value);
                if (error != null) return Result.Error<IDictionary<string, object>>($"Couldn't update value for {constraint.FieldName}: {error}");
            }

            return mergeResult;
        }
    }

    private static string SetValue(IDictionary<string, object> dict, string path, object value)
    {
        var parts = path.Split('.');
        for (var c = 0; c < parts.Length - 1; c++)
        {
            var name = parts[c];
            if (!dict.TryGetValue(name, out var level))
            {
                var newLevel = new Dictionary<string, object>();
                dict[name] = newLevel;
                dict = newLevel;
                continue;
            }

            if (level is not IDictionary<string, object> curr)
            {
                return $"{name} in {path} is not object";
            }

            dict = curr;
        }

        dict[parts[^1]] = value;

        return null;
    }

    public static Result<IDictionary<string, object>> Merge(IDictionary<string, object> into, IDictionary<string, object> from)
    {
        foreach (var kvp in from)
        {
            if (!into.TryGetValue(kvp.Key, out var dest))
            {
                into.Add(kvp.Key, kvp.Value);
                continue;
            }

            if (dest is IDictionary<string, object> dict)
            {
                if (kvp.Value is not IDictionary<string, object> fromProp)
                {
                    return Result.Error<IDictionary<string, object>>($"{kvp.Key}: from not an object");
                }

                var merge = Merge(dict, fromProp);
                if (!merge.IsSuccess) return merge;
                into[kvp.Key] = merge.Value;
                continue;
            }

            if (dest is IEnumerable<object> enumDist)
            {
                if (kvp.Value is not IEnumerable<object> enumSrc)
                {
                    if (kvp.Value is IDictionary<string, object> arrayAsDict && arrayAsDict.Keys.All(x => int.TryParse(x, out _)))
                    {
                        // special case where the fields are ArrayField.INDEX.field
                        foreach (var ikvp in arrayAsDict)
                        {
                            // right now only support object into object[]
                            if (ikvp.Value is not IDictionary<string, object> srcObj)
                            {
                                return Result.Error<IDictionary<string, object>>($"{ikvp.Key} is not an object");
                            }

                            var dst = enumDist.Skip(int.Parse(ikvp.Key)).FirstOrDefault();
                            if (dst is not IDictionary<string, object> destObj)
                            {
                                return Result.Error<IDictionary<string, object>>("Destination position in the from is not an object");
                            }

                            var merge = Merge(destObj, srcObj);
                            if (!merge.IsSuccess) return merge;
                        }

                        continue;
                    }

                    return Result.Error<IDictionary<string, object>>($"{kvp.Key}: from not an enumerable");
                }

                // TODO: handle arrays?
                return Result.Error<IDictionary<string, object>>("array merge not implemented");
            }

            if (kvp.Value is IDictionary<string, object>)
            {
                return Result.Error<IDictionary<string, object>>($"{kvp.Key}: into not an object");
            }

            if (kvp.Value is IEnumerable<object>)
            {
                return Result.Error<IDictionary<string, object>>($"{kvp.Key}: into not an array");
            }

            into[kvp.Key] = kvp.Value;
        }

        return Result.Success(into);
    }

    public async Task<Form.Models.Form> GetEditDataFormAsync(IEntityContext context, ObjectType objectType, Guid id, FormName formName, GetFormOptions opts = null)
    {
        var expandoObject = await GetExpandoObjectByIdAsync(context, objectType, id);
        if (expandoObject == null) throw new NotFoundException(objectType.FullName, id);
        return await GetEditDataFormAsync(context, objectType, id, expandoObject, formName, opts);
    }

    public async Task<Form.Models.Form> BuildEditFormAsync(IEntityContext context, ObjectType objectType,
        FormName formName = FormName.Edit, Guid? id = null, GetFormOptions opts = null)
    {
        var form = BuildDataForm(objectType, context, formName, id, opts);

        if (opts?.LoadLayout == false) return form;

        var layout = await LoadLayoutAsync(context, objectType, formName.ToString(), opts);
        if (layout != null)
        {
            form.Layouts = layout;
        }

        return form;
    }

    public async Task<Form.Models.Form> GetEditDataFormAsync(IEntityContext context, ObjectType objectType, Guid? id,
        IDictionary<string, object> record, FormName formName = FormName.Edit, GetFormOptions opts = null)
    {
        if (record.TryGetStrParam(nameof(IFlowObject.ObjectType), out var curr) && curr != objectType.FullName)
        {
            // should try to load objectType for the object? 
            // ...
            // (Config, Role) = await GetRoleConfigAsync(context, curr);
        }

        if (objectType.Discriminator != null)
        {
            var subType = await ResolveSubTypeAsync(context, objectType, record, opts);

            // only use subtype if the context has access to it
            switch (formName)
            {
                case FormName.Add:
                    if (subType.CanCreate(context)) objectType = subType;
                    break;

                case FormName.Edit:
                    if (subType.CanUpdate(context)) objectType = subType;
                    break;

                case FormName.View:
                case FormName.Details:
                    if (subType.CanRead(context)) objectType = subType;
                    break;
            }
        }

        var form = default(Form.Models.Form);
        if (!(opts?.SkipLoadingCustomForm ?? false))
        {
            form = await GetCustomizedFormAsync(context, objectType, formName, opts);
        }

        form ??= await BuildEditFormAsync(context, objectType, formName, id, opts);

        opts?.FilterBeforeLoading?.Invoke(form);

        for (var c = 0; c < form.Fields.Length; c++)
        {
            var field = form.Fields[c];
            if (!record.TryGetFieldValue(field.Name, out var value))
            {
                // no value
                value = null;
            }

            await LoadFieldAsync(context, objectType, formName, field, value, opts);

            // special handling by field type 
            switch (field)
            {
                case CalculatedField calc:
                {
                    field = calc.Field;
                    field.Name ??= calc.Name;
                    field.Label ??= calc.Label;
                    field.Enable ??= calc.Enable;
                    field.Visible ??= calc.Visible;
                    field.Description ??= calc.Description;
                    field.DefaultValue = calc.CalculateValue(record);
                    form.Fields[c] = field;
                    break;
                }
            }
        }

        // replace placeholders using current record (and context values)
        foreach (var field in form.Fields)
        {
            field.FillPlaceHolders(context, record);
        }

        if (formName != FormName.Edit && (opts?.LoadUserActions ?? true))
        {
            // view/readonly 
            var c = new GetUserActionsMenuContext
            {
                Context = context,
                ObjectType = objectType,
                ObjectId = id,
                AppDataViewId = null,
                IncludeMultiple = true,
                FlatObject = objectType.UnsafeFlatten(context, record),
                SkipToNextUrlWhenNotForm = opts?.SkipToNextUrlWhenNotForm ?? true,
            };

            var (_, userActions) = await UserActionsMenuItemsAsync(c);
            // var (_, userActions) = await GetUserActionsMenuItemsAsync(context, objectType, id, includeMultiple: true, flatObject: objectType.UnsafeFlatten(context, record));

            if (!userActions.IsEmpty())
            {
                var menu = new Menu
                {
                    Name = "Main",
                    Items =
                    [
                        new Menu
                        {
                            Name = "Actions",
                            Icon = nameof(Icons.Action),
                            Items = userActions.ToArray(),
                        }
                    ]
                };

                if (form.Menu != null)
                {
                    form.Menu.Items = form.Menu.Items.Concat(menu.Items).ToArray();
                }
                else
                {
                    form.Menu = menu;
                }
            }
        }

        if (id.HasValue)
        {
            await AddRecentObjectAsync(context, objectType, id.Value, form);
        }

        return form;
    }

    private async Task PostProcessPasswordFieldAsync(IEntityContext context, PasswordField field, object value)
    {
        if (value is string str)
        {
            field.DefaultValue = await _dataProtectionService.UnprotectAsync(context, field.PasswordFieldOptions.DataDataProtection, str);
        }
        else
        {
            field.DefaultValue = null;
        }
    }

    private void PostProcessReferenceField(IEntityContext context, FormName formName, FieldRBAC rbac, ReferenceField field)
    {
        // TODO: check permissions of related object, if can modify, clear default actions?
        // ...
        // field.ReferenceFieldOptions.Actions = null;

        if (formName == FormName.View)
        {
            // do not show actions in view form
            return;
        }

        if (rbac == null)
        {
            // no rbac so won't add actions, nothing else to do
            return;
        }

        var actions = field.ReferenceFieldOptions?.Actions ?? Enumerable.Empty<FormAction>();

        if (field.DefaultValue != null)
        {
            if (!rbac.CanReset(context) && field.IsRequired)
            {
                // field can't be unset after is set
                field.IsRequired = true;
            }
            else if (rbac.CanReset(context) && !field.ReferenceFieldOptions.AutoComplete && !field.IsRequired)
            {
                // explicit action to reset
                actions = actions.Append(new FormAction
                {
                    Action = FormAction.Client_Reset,
                    Name = "Reset",
                });
            }
        }

        if (rbac.CanCreateOnDemand(context))
        {
            // TODO: check whether the user can actually create object type ("safer")
            // ...

            actions = actions.Append(new FormAction
            {
                Action = FormAction.Client_New,
                Name = "New",
            });
        }

        if (!actions.IsEmpty())
        {
            field.ReferenceFieldOptions ??= new ReferenceFieldOptions();
            field.ReferenceFieldOptions.Actions = actions.ToArray();
        }
    }

    /// <summary>
    /// Load form for object field 
    /// </summary>
    public async Task LoadObjectFieldAsync(IEntityContext context, FormName formName, ObjectField field, object value, GetFormOptions opts = null)
    {
        if (field?.Options is not ObjectFieldOptions options)
        {
            throw new Exception($"Unexpected object type for options: {field.Options?.GetType().FullName}");
        }

        // if (!ft.RBAC.CanUpdate(context) || formName == FormName.View)
        // {
        //     // can't update field so no need to calculate forms
        //     // will use dataview
        //     field.DefaultValue = null;
        //     field.Enable = new[] { "false" };
        //     return;
        // }

        if (field.ObjectFieldOptions.ObjectType == "*" && value is null or IDictionary<string, object>)
        {
            field.DefaultValue = value;
            return;
        }

        var objectType = await GetAsync(context, field.ObjectFieldOptions.ObjectType, opts);
        if (objectType == null) throw new NotFoundException(field.ObjectFieldOptions.ObjectType);

        if (value == null)
        {
            if (objectType.Discriminator?.Count > 0)
            {
                var names = await GetAllSubTypeNames(context, objectType);
                options.AddFormUrls = new Dictionary<string, string>
                (
                    names
                        .OrderBy(x => x.Value)
                        .Select(x => new KeyValuePair<string, string>(x.Value, opts.BuildAddFormUrl(x.Key)))
                );
            }
            else if (!objectType.IsAbstract)
            {
                options.AddFormUrls = new Dictionary<string, string>
                {
                    { objectType.Description ?? objectType.FullName, opts.BuildAddFormUrl(objectType.FullName) }
                };

                // // embed form
                // options.EditForm = await BuildAddFormAsync(context, objectType, true);
                // options.EditForm.Name = field.Name;
                // options.EditForm.Menu = null;
                // options.EditForm.Actions = null;
            }
            else
            {
                throw new BadRequestException($"Can't create object of {objectType.FullName}");
            }

            field.DefaultValue = null; // ???
            return;
        }

        // edit 
        if (value is not IDictionary<string, object> dictObj)
        {
            throw new Exception($"Unexpected object type for child object: {value.GetType().FullName}");
        }

        options.EditForm = await GetEditDataFormAsync(context, objectType, formName, dictObj, opts);
        options.EditForm.Name = field.Name;
        options.EditForm.Menu = null;
        options.EditForm.Actions = null;

        var fieldValues = ResolveFieldValues(context, objectType, options.EditForm, dictObj);
        field.DefaultValue = fieldValues;
    }

    private async Task<Dictionary<string, string>> GetAllSubTypeNames(IEntityContext context, ObjectType objectType)
    {
        var found = new Dictionary<string, string>();
        var queue = new HashSet<string>();
        if (!objectType.IsAbstract)
        {
            found.Add(objectType.FullName, objectType.Description ?? objectType.FullName);
        }

        if (objectType.Discriminator != null)
        {
            foreach (var objectName in objectType.Discriminator.Keys)
            {
                queue.Add(objectName);
            }
        }

        while (!queue.IsEmpty())
        {
            // this won't use the cache even if defined and can happen a lot.
            // TODO: use cacheable method?
            // ....
            var batch = await _connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, context.AccountId)
                .In(x => x.FullName, queue)
                .IncludeFields(
                    x => x.IsAbstract,
                    x => x.Discriminator,
                    x => x.Name,
                    x => x.Namespace,
                    x => x.Description
                )
                .FindAsync();

            foreach (var obj in batch.Where(x => !x.IsAbstract))
            {
                found.Add(obj.FullName, obj.Description ?? obj.Name);
            }

            queue = batch
                .Where(x => x.Discriminator != null)
                .SelectMany(x => x.Discriminator.Keys)
                .Distinct()
                .Except(found.Keys)
                .ToHashSet();
        }

        return found;
    }

    private async Task GetAllSubObjectTypes(IEntityContext context, ObjectType objectType, Dictionary<string, ObjectType> childTypes)
    {
        if (!objectType.IsAbstract)
        {
            childTypes.Add(objectType.FullName, objectType);
        }

        if (objectType.Discriminator == null) return;

        foreach (var kvp in objectType.Discriminator)
        {
            var subType = await GetAsync(context, kvp.Key);
            if (subType == null) return;

            await GetAllSubObjectTypes(context, subType, childTypes);
        }
    }

    private async Task LoadFieldAsync(IEntityContext context, ObjectType objectType, FormName formName, FormField field, object value, GetFormOptions opts = null)
    {
        if (!objectType.Fields.TryGetValue(field.Name, out var ft) || !ft.RBAC.CanRead(context))
        {
            // can't read, reset value
            field.DefaultValue = null;
            return;
        }

        if (formName == FormName.Add && !ft.RBAC.CanSetOnCreate(context))
        {
            // can't add so no need to do anything else?
            return;
        }

        await LoadFieldAsync(context, formName, field, value, ft.RBAC, opts);
    }

    private async Task LoadFieldAsync(IEntityContext context, FormName formName, FormField field, object value, FieldRBAC rbac = null, GetFormOptions opts = null)
    {
        if (formName == FormName.Edit && rbac != null && !rbac.CanUpdate(context))
        {
            formName = FormName.View;
        }

        if (formName == FormName.Add && rbac != null && !rbac.CanSetOnCreate(context))
        {
            formName = FormName.View;
        }

        // special handling by field type 
        switch (field)
        {
            case PasswordField pwd:
                if (pwd.PasswordFieldOptions?.DataDataProtection != null)
                {
                    await PostProcessPasswordFieldAsync(context, pwd, value);
                }

                break;

            case ReferenceField referenceField:
                field.DefaultValue = value;
                PostProcessReferenceField(context, formName, rbac, referenceField);
                break;

            case ObjectField objField:
                // load forms for object
                await LoadObjectFieldAsync(context, formName, objField, value, formOptions());
                break;

            case ChildrenField childrenField:
                // load forms for object
                await LoadObjectFieldAsync(context, formName, childrenField, value, formOptions());
                break;

            case ExpressionField expressionField:
                // load forms for object
                await LoadExpressionFieldAsync(context, formName, expressionField, value, formOptions());
                break;

            default:
                field.DefaultValue = value;
                break;
        }

        return;

        GetFormOptions formOptions() => opts != null ? new GetFormOptions(opts, loadUserActions: false) : new GetFormOptions { LoadUserActions = false };
    }

    private async Task LoadExpressionFieldAsync(IEntityContext context, FormName formName, ExpressionField field, object value, GetFormOptions opts = null)
    {
        if (field.Options is not ExpressionFieldOptions options)
        {
            throw new Exception($"Unexpected object type for options: {field.Options?.GetType().FullName}");
        }

        // if the value is an expression don't try to use it on the wrapped field 
        // or will cause issues for example trying to load as an object for the objectfield
        var currValue = (value is string str && str.Contains("{{") && str.Contains("}}")) ? null : value;

        // process wrapped field
        await LoadFieldAsync(context, formName, options.ValueField, currValue, null, opts);

        field.DefaultValue = value;
    }

    /// <summary>
    /// If the field can be updated, create embedded form to allow editing the object
    /// </summary>
    private async Task LoadObjectFieldAsync(IEntityContext context, FormName formName, ChildrenField field, object value, GetFormOptions opts = null)
    {
        if (field.Options is not ChildrenFieldOptions childrenOptions)
        {
            throw new Exception($"Unexpected object type for options: {field.Options?.GetType().FullName}");
        }

        if ((field.IsReadOnly || formName == FormName.View) && childrenOptions.Url != null)
        {
            // can't update field so no need to calculate forms
            // will use dataview
            // field.DefaultValue = null;
            // field.Enable = new[] { "false" };
            return;
        }

        var objectType = await GetAsync(context, field.ChildrenFieldOptions.ObjectType, opts);
        if (objectType == null) throw new NotFoundException(field.ChildrenFieldOptions.ObjectType);

        var editForms = new Dictionary<string, Form.Models.Form>();

        if (formName != FormName.View && formName != FormName.Details)
        {
            // only calculate add for urls for editable forms
            if (objectType.Discriminator?.Count > 0)
            {
                var names = await GetAllSubTypeNames(context, objectType);
                childrenOptions.AddFormUrls = new Dictionary<string, string>
                (
                    names
                        .OrderBy(x => x.Value)
                        .Select(x => new KeyValuePair<string, string>(x.Value, opts.BuildAddFormUrl(x.Key)))
                );
            }
            else if (!objectType.IsAbstract)
            {
                childrenOptions.AddFormUrls = new Dictionary<string, string>
                {
                    { objectType.Description ?? objectType.FullName, opts.BuildAddFormUrl(objectType.FullName) }
                };
            }
        }

        if (value == null)
        {
            field.DefaultValue = null;
            return;
        }

        switch (childrenOptions.KeyType)
        {
            case ChildrenFieldOptions.IndexKeyType:
            {
                if (value is not IEnumerable<object> list)
                {
                    throw new Exception("Unexpected type for children");
                }

                var values = new List<IDictionary<string, object>>();
                foreach (var obj in list)
                {
                    if (obj is not IDictionary<string, object> dictObj)
                    {
                        throw new Exception($"Unexpected object type for child object: {obj.GetType().FullName}");
                    }

                    if (!dictObj.TryGetValue(nameof(Model.Name), out var objectName))
                    {
                        objectName = "";
                    }

                    var form = await GetEditDataFormAsync(context, objectType, formName, dictObj, opts);
                    form.Title = $"{field.Label ?? field.Name} #{values.Count + 1}: {objectName}";
                    var fieldValues = ResolveFieldValues(context, objectType, form, dictObj);
                    editForms.Add(values.Count.ToString(), form);
                    values.Add(fieldValues);
                }

                field.DefaultValue = values.ToArray();
                childrenOptions.EditForms = editForms;
                return;
            }

            case ChildrenFieldOptions.StringKeyType:
            {
                // dictionary
                if (value is not IDictionary<string, object> dict)
                {
                    throw new Exception("Unexpected type for children");
                }

                var values = new Dictionary<string, Dictionary<string, object>>();
                foreach (var kvp in dict)
                {
                    if (kvp.Value is not IDictionary<string, object> dictObj)
                    {
                        throw new Exception($"Unexpected object type for child object: {kvp.Key}");
                    }

                    var form = await GetEditDataFormAsync(context, objectType, formName, dictObj, opts);
                    form.Title = $"{field.Label ?? field.Name}: {kvp.Key}";
                    var fieldValues = ResolveFieldValues(context, objectType, form, dictObj);
                    editForms.Add(kvp.Key, form);
                    values.Add(kvp.Key, fieldValues);
                }

                field.DefaultValue = values;
                childrenOptions.EditForms = editForms;
                return;
            }
        }

        throw new Exception("Invalid Key Type");
    }

    private Dictionary<string, object> ResolveFieldValues(IEntityContext context, ObjectType objectType, Form.Models.Form form, IDictionary<string, object> dictObj)
    {
        var fieldValues = new Dictionary<string, object>();
        foreach (var f in form.Fields)
        {
            if (!dictObj.TryGetFieldValue(f.Name, out var fieldValue)) continue;
            if (!objectType.Fields.TryGetValue(f.Name, out var kvp) || !kvp.RBAC.CanRead(context)) continue;
            fieldValues.Add(f.Name, fieldValue);
        }

        return fieldValues;
    }

    /// <summary>
    /// Build form 
    /// </summary>
    private async Task<Form.Models.Form> GetEditDataFormAsync(IEntityContext context, ObjectType objectType, FormName formName, IDictionary<string, object> dictObj, GetFormOptions opts)
    {
        // create edit form but not directly associated with one id (since it is a child object)
        var form = await GetEditDataFormAsync(context, objectType, null, dictObj, formName, opts);

        // TODO: update name, actions, url, ... based on field
        // ...

        return form;
    }

    /// <summary>
    /// Calculate dataForm url for objectType/formName
    /// - when building urls for forms that required id and omitting id, will create with placeholder {{Object._id}} 
    /// </summary>
    public static string BuildDataFormUrl(ObjectType objectType, Guid? id = null, FormName formName = FormName.Edit, string title = null, string idPlaceholder = "{{id}}")
    {
        var objectTypeName = objectType.FullName;
        var endpoint = formName switch
        {
            FormName.View or FormName.Details => ActionEndpoint.Get,
            FormName.Edit => ActionEndpoint.Update,
            FormName.Add => ActionEndpoint.Create,
            _ => throw new Exception("Invalid form Name"),
        };

        if (objectType.ApiPaths == null || !objectType.ApiPaths.TryGetValue(endpoint.ToString(), out var basePath))
        {
            basePath = "/api/";
        }

        var idParam = id?.ToString() ?? idPlaceholder;
        var url = formName switch
        {
            FormName.Add => $"dataForm:{basePath}v1/CustomObject/{objectTypeName}/{formName}",
            _ => $"dataForm:{basePath}v1/CustomObject/{objectTypeName}({idParam})/{formName}",
        };

        return string.IsNullOrEmpty(title) ? url : $"{url}#title={title}";
    }

    /// <summary>
    /// Build page for object
    /// </summary>
    public async Task<LayoutPage> BuildLayoutPageAsync(IEntityContext context, string objectTypeName, Guid id)
    {
        var objectType = await GetAsync(context, objectTypeName);
        if (objectType == null || !objectType.CanRead(context)) throw new ForbiddenException(context, objectTypeName);
        return await BuildLayoutPageAsync(context, objectType, id);
    }

    /// <summary>
    /// Build page for object
    /// - it will use placeholders when id is omitted
    /// </summary>
    public async Task<LayoutPage> BuildLayoutPageAsync(IEntityContext context, ObjectType objectType, Guid? id = null)
    {
        var obj = default(ExpandoObject);
        if (id.HasValue)
        {
            obj = await GetExpandoObjectByIdAsync(context, objectType, id.Value);
            if (obj == null)
            {
                throw new NotFoundException(objectType.FullName, id);
            }
        }

        // TODO: should try to check if there is a subtype
        // ...
        // update object type name in case it has resolved to a different type
        // objectTypeName = objectType.Name;

        // TODO: try to fina AppPage and use it instead ?
        //      will have to process urls to replace place holder parameters?
        // ...

        var url = BuildDataFormUrl(objectType, id, FormName.Details, idPlaceholder: "{{Object._id}}");

        var body = new List<LayoutItem>
        {
            new ObjectLayoutItem
            {
                Url = url,
                Name = objectType.FullName,
                Label = objectType.Description ?? objectType.Name,
                // Style = new LayoutItemCssStyle
                // {
                //     MinWidth = "100%",
                //     MaxWidth = "100%",
                // }
            }
        };

        var appLinks = await _connection.GetProfileElementsAsync<AppPageLink>(
            context,
            q => q
                .Eq(x => x.ObjectType, objectType.FullName)
                .Ne(x => x.IsHidden, true)
                .Ne(x => x.IsActive, false)
        );

        var objectsContext = default(Dictionary<string, object>);

        if (appLinks.Count > 0)
        {
            foreach (var link in appLinks)
            {
                var pageUrl = link.Url;
                if (obj != null)
                {
                    // object defined, resolve urls
                    objectsContext ??= new Dictionary<string, object>
                    {
                        { "Object", await RecursivelyFlattenAsync(context, objectType, obj) }
                    };

                    if (link.Conditions.AnyFalseUsingExpressions(context, objectsContext)) continue;

                    if (!ExpressionEvaluatorService.TryResolve(context, objectsContext, pageUrl, out var urlObj) || urlObj is not string resolvedUrl)
                    {
                        // can't resolve, skip
                        continue;
                    }

                    pageUrl = resolvedUrl;
                }

                body.Add(
                    new ObjectLayoutItem
                    {
                        Url = pageUrl,
                        Name = link.Name,
                        Label = link.Description ?? link.Name,
                    }
                );
            }
        }

        var side = new List<ObjectLayoutItem>();

        foreach (var relatedObject in objectType.RelatedObjectTypes ?? Enumerable.Empty<RelatedObjectType>())
        {
            if (!relatedObject.RBAC.CanRead(context)) continue;

            if (obj != null)
            {
                // object defined, check conditions 
                if (relatedObject.Conditions?.Length > 0)
                {
                    objectsContext ??= new Dictionary<string, object>
                    {
                        { "Object", await RecursivelyFlattenAsync(context, objectType, obj) }
                    };

                    if (relatedObject.Conditions.AnyFalseUsingExpressions(context, objectsContext)) continue;
                }
            }

            // no need to also check object type?
            // var relatedObjectType = await GetAsync(context, relatedObject.Name);
            // if (relatedObjectType == null || !relatedObjectType.CanRead(context)) continue;

            if (relatedObject.RelationType == RelationType.OneToOne)
            {
                // TODO: add a special end point so it can defer finding the match? 
                // ...

                if (obj == null)
                {
                    // right now doesn't know how to build url without resolving related object 
                    continue;
                }

                try
                {
                    var uri = await GetDataFormUrlForRelatedObjectAsync(context, relatedObject, obj);
                    if (uri == null) continue;

                    side.Add(new ObjectLayoutItem
                    {
                        Name = relatedObject.Name,
                        Label = relatedObject.Label,
                        Url = uri,
                        LazyLoad = !(relatedObject.Options?.AutoExpand ?? false),
                        // Style = new LayoutItemCssStyle
                        // {
                        //     MinWidth = "100%",
                        //     MaxWidth = "100%",
                        // }
                    });
                }
                catch (FailedToResolveExpressionException ex)
                {
                    // may happen when one of the conditions can't be satisfied because the expression can't be resolved
                    // in normal circumstances for example when it depends on a field that is not required  
                    _logger.LogInformation("Couldn't resolve {Relation}: {Expression}", relatedObject.Name, ex.Expression);
                }

                continue;
            }

            // TODO: add a special end point so it can defer finding the match? 
            // ...

            body.Add(new ObjectLayoutItem
            {
                Name = relatedObject.Name,
                Label = relatedObject.Label,
                Url = obj != null ? GetDataViewUrlForRelatedObject(relatedObject, obj) : GetDataViewUrlForRelatedObject(relatedObject),
                // Style = new LayoutItemCssStyle
                // {
                //     MinWidth = "100%",
                //     MaxWidth = "100%",
                // }
            });
        }

        // var userActions = await GetUserActionsAsync(context, objectType, id);
        // var menu = userActions.IsEmpty() ?
        //     null :
        //     new Menu
        //     {
        //         Items = new MenuItem[]
        //         {
        //             new Menu
        //             {
        //                 Name = "Actions",
        //                 Icon = Icons.Action,
        //                 Items = userActions.ToArray(),
        //             }
        //         }
        //     };

        var page = new LayoutPage
        {
            Name = $"/api/v1/CustomObject/{objectType.FullName}", // unique name for page so it can be safely cached
            Label = objectType.Description ?? objectType.Name,
            Layout = new LayoutContainer
            {
                Type = LayoutContainerType.Row,
                Children = getColumns().ToArray(),
                Spacing = 12,
                Justify = LayoutJustify.Between,
            },
            // Menu = menu,
        };

        return page;

        IEnumerable<LayoutItem> getColumns()
        {
            var main = body.Count > 1
                ? new LayoutContainer
                {
                    Type = LayoutContainerType.Tabs,
                    Weight = 1,
                    Children = body.ToArray(),
                }
                : body[0];

            if (side.Count == 0)
            {
                // only main tab
                yield return main;
                yield break;
            }

            main.Weight = 2;
            // main.Style = new LayoutItemCssStyle
            // {
            //     MaxWidth = "100%",
            //     MinWidth = "100%",
            //     MaxHeight = "100%",
            // };

            yield return main;

            // multiple 
            yield return new LayoutContainer
            {
                Type = LayoutContainerType.Column,
                Weight = 1,
                Spacing = 12,
                Justify = LayoutJustify.Between,
                Children = side.ToArray(),
                // Style = new LayoutItemCssStyle
                // {
                //     MaxWidth = "33%",
                //     MinWidth = "33%",
                // }
            };
        }
    }

    /// <summary>
    /// Build url for related object dataForm
    /// </summary>
    private async Task<string> GetDataFormUrlForRelatedObjectAsync(IEntityContext context, RelatedObjectType relation, IDictionary<string, object> obj)
    {
        var objectType = await GetAsync(context, relation.ObjectType);

        // object defined, find match to defined object id
        var match = await _connection.Filter<object>(objectType.CollectionName, objectType.DatabaseName)
            .AddConstraints(context, objectType)
            .AddConditions(context, relation.Criteria.Conditions, obj)
            .IncludeField(Model.IdFieldName)
            .FirstOrDefaultAsync();

        if (match is not IDictionary<string, object> dynObj) return null;

        // right now assume that IT IS ALWAYS THE _id property that will match the _id property
        if (!dynObj[Model.IdFieldName].TryToParseObjectId(out var objectId)) throw new Exception("Invalid id");
        
        return BuildDataFormUrlForRelatedObject(relation, objectType, objectId);
    }

    /// <summary>
    /// Build url for related object dataForm
    /// - if id is omitted, will use {{id}} placeholder 
    /// </summary>
    private static string BuildDataFormUrlForRelatedObject(RelatedObjectType relation, ObjectType objectType, Guid? objectId)
    {
        if (objectId.HasValue)
        {
            return new UriBuilder
            {
                Scheme = "dataForm",
                Host = "api",
                Path = $"/v1/CustomObject/{relation.ObjectType}({objectId})/View",
                Fragment = $"title={relation.Label ?? relation.Name}",
            }.Uri.ToString();
        }

        // use placeholders
        return BuildDataFormUrl(objectType, null, FormName.View, relation.Label ?? relation.Name, idPlaceholder: "{{Object._id}}");
    }

    /// <summary>
    /// Build url for related object dataView 
    /// </summary>
    private static string GetDataViewUrlForRelatedObject(RelatedObjectType relatedObject, IDictionary<string, object> obj)
    {
        var query = new QueryBuilder();

        if (!relatedObject.Criteria.Conditions.ReplaceValuePlaceHolders(obj))
        {
            throw new BadRequestException("Failed to substitute place holders");
        }

        foreach (var c in relatedObject.Criteria.Conditions)
        {
            var value = c.Value switch
            {
                Guid uuid => uuid.ToString(),
                ObjectId oid => oid.ToGuid().ToString(),
                _ => c.Value?.ToString(),
            };

            query.Add(c.FieldName, value ?? "{{NULL}}");
        }

        return BuildDataViewUrlForRelatedObject(relatedObject, query);
    }

    /// <summary>
    /// Build url for related object dataView using placeholders 
    /// </summary>
    private static string GetDataViewUrlForRelatedObject(RelatedObjectType relatedObject)
    {
        var url = $"dataGrid:/api/v1/CustomObject({relatedObject.ObjectType})";

        for (var c = 0; c < relatedObject.Criteria.Conditions.Length; c++)
        {
            var condition = relatedObject.Criteria.Conditions[c];
            url += c == 0 ? '?' : '&';
            if (condition.Value is string value && value.StartsWith("{{") && value.EndsWith("}}"))
            {
                // replace with placeholder
                value = "{{Object." + value[2..];
                url += $"{condition.FieldName}={value}";
                continue;
            }

            url += $"{condition.FieldName}={condition.Value}";
        }

        url += $"#title={Uri.EscapeDataString(relatedObject.Label ?? relatedObject.Name)}";

        return url;
    }

    /// <summary>
    /// Build url for related object dataView 
    /// </summary>
    private static string BuildDataViewUrlForRelatedObject(RelatedObjectType relatedObject, QueryBuilder query)
    {
        return new UriBuilder
        {
            Scheme = "dataGrid",
            Host = "api",
            Path = $"/v1/CustomObject({relatedObject.ObjectType})",
            Query = query.ToString(),
            Fragment = $"title={relatedObject.Label ?? relatedObject.Name}",
        }.Uri.ToString();
    }

    public async Task<UpdateResult> UpsertFlowRunAsync(Dictionary<string, object> flatObject, FlowEvent evt, FlowStep[] steps = null, Dictionary<string, ObjectWithType> loadedObjects = null)
    {
        var query = _connection.Filter<FlowRun>()
                .Eq(x => x.Id, evt.RunId)
                .Update
                .SetOnInsert(x => x.AccountId, evt.AccountId)
                .SetOnInsert(x => x.Id, evt.RunId)
                // .SetOnInsert(x => x.Name, $"{objectType.Name}::{DateTime.UtcNow.}")
                .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                .SetOnInsert(x => x.InitialEvent, evt)
                .SetOnInsert(x => x.InitialObject, flatObject)
                .SetOnInsert(x => x.ObjectType, evt.ObjectType)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.Objects[FlowRun.GetObjectAlias(evt.ObjectType)], new ObjectWithType
                {
                    ObjectType = evt.ObjectType,
                    Object = flatObject,
                })
            ;

        if (steps != null)
        {
            query.Push(x => x.Steps, new RunStep { Event = evt, Steps = steps });
        }

        if (loadedObjects != null)
        {
            foreach (var kvp in loadedObjects.Where(kvp => kvp.Key != evt.ObjectType))
            {
                query.Set(x => x.Objects[FlowRun.GetObjectAlias(kvp.Key)], kvp.Value);
            }
        }

        if (steps == null || steps.IsEmpty())
        {
            query.Push(x => x.FinalEvents, evt);
        }

        return await query.UpdateOneAsync(true);
    }

    /// <summary>
    /// Upsert FlowRun and return it (w/o steps)
    /// </summary>
    public async Task<FlowRun> UpsertAndGetFlowRunAsync(Dictionary<string, object> flatObject, FlowEvent evt, FlowStep[] steps = null, Dictionary<string, ObjectWithType> loadedObjects = null)
    {
        var result = await UpsertFlowRunAsync(flatObject, evt, steps, loadedObjects);

        if (result.ModifiedCount == 0)
        {
            // first time for run
            _logger.LogDebug("First for {RunId}", evt.RunId);
        }

        return await _connection.Filter<FlowRun>()
            .Eq(x => x.Id, evt.RunId)
            .ExcludeField(x => x.Steps)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Update "one" field in an object  (currently is not capable of updating any fields that depend on modified fields)
    /// </summary>
    public async Task<ExpandoObject> UpdateObjectAsync(IEntityContext context, ObjectType objectType, Guid objectId, Func<Query<ExpandoObject>, UpdateQuery<ExpandoObject>> update, IDictionary<string, object> modifiedFields)
    {
        // has some assumptions about the model (mainly that has a _id)
        var query = _connection.Filter<ExpandoObject>(objectType.CollectionName, objectType.DatabaseName)
                .Eq(Model.IdFieldName, objectId)
                .AddConstraints(context, objectType)
            ;

        var updateQuery = update(query);

        var calcFields = objectType.Fields.Values
            .Where(x => x.CalculatedValue != null);

        foreach (var field in calcFields)
        {
            if (!TryResolveExpression(context, field, modifiedFields, field.CalculatedValue, out var calculatedValue)) continue;

            updateQuery.Set(field.Field.GetPathInCollection(), calculatedValue);

            modifiedFields[field.Field.Name] = calculatedValue;
        }

        var record = await updateQuery.UpdateAndGetOneAsync();
        if (record == null)
        {
            // failed
            // ...
            return null;
        }

        if (!record.TryGetGuidParam(nameof(IFlowObject.Id), out var id))
        {
            return record;
        }

        await FireObjectUpdatedAsync(context, objectType, record, id, modifiedFields);
        return record;
    }

    /// <summary>
    /// Load related objects
    /// returns error message if failed, otherwise null 
    /// </summary>
    public async Task<string> LoadRelatedObjectAsync(IEntityContext entityContext, Dictionary<string, ObjectWithType> objects, string[] list, string baseObjectType)
    {
        using var scope = _logger.AddScope(new
        {
            ObjectType = baseObjectType,
        });

        _logger.LogInformation("Load related Object(s)");

        // var objectsToBeLoaded = action.Options.RelatedObjects;
        // objectsToBeLoaded ??= "{{" + (action.Options.ParentObject ?? evt.ObjectType) + "}}.{{" + action.Options.RelatedObject + "}}";
        // var list = objectsToBeLoaded.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var objectToBeLoaded in list)
        {
            // TODO: could go one step further and support more than two parts
            // e.g. {{Appointment}}.{LeadId}}.{{EntityId}} => {{Appointment}}.{{LeadId}} + {{Appointment|LeadId}}.{{EntityId}}
            // ...

            // TODO: should it be simply Appointment.LeadId ?
            // or {{load Appointment LeadId}} or {{from Appointment LeadId}}
            // or should go even further {{Objects.Appointment}}.{{LeadId}}?
            // ...

            var parts = objectToBeLoaded.Split("}}.{{");
            var parentObjectName = parts.Length == 1 ? baseObjectType : parts[0][2..];
            var relatedObject = parts.Length == 1 ? (parts[0].StartsWith("{{") ? parts[0][2..^2] : parts[0]) : parts[^1][..^2];
            var targetObjectName = $"{parentObjectName}|{relatedObject}";

            if (!objects.TryGetValue(parentObjectName, out var parentObjectWithType))
            {
                _logger.LogError("Didn't find {ParentObject} in this run", parentObjectName);
                return $"Didn't find {parentObjectName}";
            }

            var parentObjectType = await GetAsync(entityContext, parentObjectWithType.ObjectType);
            if (parentObjectType == null)
            {
                _logger.LogError("Object Type not found");
                return $"{parentObjectWithType.ObjectType} type not found";
            }

            if (parentObjectWithType.Object.TryGetValue(relatedObject, out var fieldValue))
            {
                // try fields
                if (parentObjectType.Fields.TryGetValue(relatedObject, out var field)
                    && field.Field is ReferenceField referenceField
                    && referenceField.ReferenceFieldOptions != null
                   )
                {
                    _logger.LogInformation("Lookup using {Field}={Value}", referenceField.Name, fieldValue);

                    if (await LoadRelatedObjectUsingFieldAsync(entityContext, objects, targetObjectName, parentObjectType, referenceField, fieldValue))
                    {
                        continue;
                    }
                }
            }

            // try related objects (field value doesn't matter, will use relation criteria)
            var relatedObjectType = parentObjectType.RelatedObjectTypes?.FirstOrDefault(x => x.Name == relatedObject);
            if (relatedObjectType != null && relatedObjectType.RBAC.CanRead(entityContext))
            {
                _logger.LogInformation("Lookup using {RelatedObject} to {ObjectType}", relatedObjectType.Name, relatedObjectType.ObjectType);
                if (await LoadRelatedObjectUsingRelationAsync(entityContext, objects, targetObjectName, parentObjectWithType.Object, relatedObjectType))
                {
                    continue;
                }
            }

            // failed!
            return "Object not found to load";
        }

        return null;
    }

    /// <summary>
    /// Load related object (using relation)
    /// </summary>
    private async Task<bool> LoadRelatedObjectUsingRelationAsync(IEntityContext context, Dictionary<string, ObjectWithType> objects, string targetObjectName, IDictionary<string, object> parentObject, RelatedObjectType relation)
    {
        using var scope = _logger.AddScope(new
        {
            relation.RelationType,
            relation.ObjectType,
            relation.Name
        });

        _logger.LogInformation("Try to load related object (using relation)");

        var relatedObjectType = await GetAsync(context, relation.ObjectType);

        var query = _connection.Filter<ExpandoObject>(relatedObjectType.CollectionName, relatedObjectType.DatabaseName)
                .AddConstraints(context, relatedObjectType)
                .AddConditions(context, relation.Criteria.Conditions, parentObject)
            ;

        return relation.RelationType switch
        {
            RelationType.OneToOne => await loadSingleAsync(),
            RelationType.OneToMany => await loadManyAsync(),
            _ => false
        };

        async Task<bool> loadSingleAsync()
        {
            var oneToOne = await query.FirstOrDefaultAsync();
            if (oneToOne == null)
            {
                _logger.LogInformation("Single Object matching criteria not found");
                return false;
            }

            var flatObject = await RecursivelyFlattenAsync(context, relatedObjectType, oneToOne);

            _logger.LogInformation("Found Single Related Object");

            objects[targetObjectName] = new ObjectWithType
            {
                ObjectType = relatedObjectType.FullName,
                Object = flatObject,
            };

            return true;
        }

        async Task<bool> loadManyAsync()
        {
            var list = await query.FindAsync();
            var result = new Dictionary<string, object>();
            for (var c = 0; c < list.Count; c++)
            {
                var flatObject = await RecursivelyFlattenAsync(context, relatedObjectType, list[c]);
                result.Add(c.ToString(), flatObject);
            }

            objects[targetObjectName] = new ObjectWithType
            {
                ObjectType = $"{relatedObjectType.ObjectType}[]", // ????!
                Object = result,
            };

            return true;
        }
    }

    /// <summary>
    /// Load related object (for referenceField) 
    /// </summary>
    private async Task<bool> LoadRelatedObjectUsingFieldAsync(IEntityContext context, Dictionary<string, ObjectWithType> objects, string targetObjectName, ObjectType parentObjectType, ReferenceField referenceField, object fieldValue)
    {
        if (fieldValue == null)
        {
            _logger.LogError("Missing field");
            return false;
        }

        var relatedObjectType = await GetAsync(context, referenceField.ReferenceFieldOptions.ObjectType);
        var foreignFieldName = referenceField.ReferenceFieldOptions.ForeignFieldName ??
                               relatedObjectType.LookupFields?.Key ??
                               Model.IdFieldName;

        var query = _connection.Filter<ExpandoObject>(relatedObjectType.CollectionName, relatedObjectType.DatabaseName)
                .Eq(foreignFieldName, fieldValue)
                .AddConstraints(context, relatedObjectType)
                .AddConditions(context, referenceField.ReferenceFieldOptions.Criteria)
            ;

        var relatedObject = await query.FirstOrDefaultAsync();
        if (relatedObject != null)
        {
            var flatObject = await RecursivelyFlattenAsync(context, relatedObjectType, relatedObject);

            _logger.LogInformation("Found Related Object");

            objects[targetObjectName] = new ObjectWithType
            {
                ObjectType = relatedObjectType.FullName,
                Object = flatObject,
            };

            return true;
        }

        _logger.LogInformation(
            "Didn't find {ObjectType} with {Field} = {FieldValue}",
            referenceField.ReferenceFieldOptions.ObjectType,
            foreignFieldName,
            fieldValue
        );

        return false;
    }

    /// <summary>
    /// Load related objects into flow run
    /// It will always include Context|User and Context|Organization if defined 
    /// </summary>
    public async Task<Result<Dictionary<string, ObjectWithType>>> LoadRelatedObjectsAsync(IEntityContext context, UserTrigger trigger, string baseObjectType, IDictionary<string, object> flatObject)
    {
        var result = new Dictionary<string, ObjectWithType>
        {
            {
                baseObjectType, new ObjectWithType
                {
                    ObjectType = baseObjectType,
                    Object = flatObject
                }
            },
        };

        // TODO: this can get messy if there is an object type named Context 
        // ...

        if (context.UserId.HasValue)
        {
            var userObjectType = await GetAsync(context, nameof(User));
            if (userObjectType != null)
            {
                var user = await GetFlatObjectAsync(context, userObjectType, context.UserId.Value);
                result.TryAdd("Context|User", new ObjectWithType
                {
                    ObjectType = userObjectType.FullName,
                    Object = user,
                });
            }
        }

        if (context.OrganizationId.HasValue)
        {
            var orgObjectType = await GetAsync(context, nameof(Organization));
            if (orgObjectType != null)
            {
                var organization = await GetFlatObjectAsync(context, orgObjectType, context.OrganizationId.Value);
                result.TryAdd("Context|Organization", new ObjectWithType
                {
                    ObjectType = orgObjectType.FullName,
                    Object = organization,
                });
            }
        }

        if (trigger.RelatedObjects.Length > 0)
        {
            var error = await LoadRelatedObjectAsync(context, result, trigger.RelatedObjects, baseObjectType);
            if (error != null) return Result.Error<Dictionary<string, ObjectWithType>>(error);
        }

        return Result.Success(result);
    }

    /// <summary>
    /// Expand all keys that have a "|"
    /// this is very fragile, will only expand the first level
    /// TODO: add handling for array element in the path
    /// ...
    /// </summary>
    public static Dictionary<string, object> UnflattenFirstLevel(IDictionary<string, object> flat)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in flat)
        {
            var path = kvp.Key.Split('|');
            set(result, path, kvp.Value);
        }

        return result;

        void set(IDictionary<string, object> parent, string[] path, object value)
        {
            if (value == null) return;

            if (path.Length == 1)
            {
                parent[path[0]] = value;
                return;
            }

            if (!parent.TryGetValue(path[0], out var child))
            {
                var newChild = new Dictionary<string, object>();
                parent.Add(path[0], newChild);
                set(newChild, path[1..], value);
                return;
            }

            if (child is not IDictionary<string, object> childDict)
            {
                throw new Exception("Unexpected property type");
            }

            set(childDict, path[1..], value);
        }
    }

    /// <summary>
    /// Handle Getting Object DataForm for API 
    /// </summary>
    public async Task<Form.Models.Form> GetDataFormAsync(IEntityContext context, string objectTypeName, Guid? id, string formName, HttpRequest request, GetFormOptions options)
    {
        var result = default(Form.Models.Form);
        
        if (Enum.TryParse(formName, out FormName resolvedFormName))
        {
            // validate 
            switch (resolvedFormName)
            {
                case FormName.Add:
                    if (id.HasValue) Form.Models.Form.BuildErrorForm($"Form {formName} does not expect object id.");
                    break;

                case FormName.Details:
                case FormName.Edit:
                case FormName.View:
                    if (id.HasValue) Form.Models.Form.BuildErrorForm($"Form {formName} expects object id.");
                    break;
            }
            
            if (resolvedFormName == FormName.Add || !id.HasValue)
            {
                result = await GetAddDataFormAsync(context, objectTypeName, opts: options);
            }
            else
            {
                var objectType = await GetAsync(context, objectTypeName);
                var dynamicRecord = await GetExpandoObjectByIdAsync(context, objectType, id.Value);
                if (dynamicRecord == null) throw new NotFoundException($"{objectTypeName} not found");

                result = await GetDataFormForObjectAsync(context, objectType, id.Value, dynamicRecord, resolvedFormName, opts: options);
            }            
        }
        else
        {
            // special handling for "Upsert"
            if (formName == "Upsert")
            {
                var args = request.Query.ToDictionary(arg => arg.Key, arg => (object)arg.Value.FirstOrDefault());
                result = await GetUpsertFormAsync(context, objectTypeName, args,  options);
                return result ?? throw new NotFoundException($"{objectTypeName}/${formName})");
            }

            // load named form 
            // right now, no support for forms with object?
            if (id.HasValue) Form.Models.Form.BuildErrorForm($"Form {formName} does not expect object id.");

            var appForm = await LoadCustomFormAsync(context, objectTypeName, formName, options);

            result = appForm?.Form;
        }

        if (result == null) throw new NotFoundException($"{objectTypeName}/${formName})");

        // init with values from request? 
        var fields = result.Fields.ToDictionary(x => x.Name);
        foreach (var query in request.Query)
        {
            if (!fields.TryGetValue(query.Key, out var field)) continue;
            if (field.IsReadOnly) continue;

            if (resolvedFormName != FormName.Add && field.DefaultValue != null) continue;

            object value = query.Value.Count > 1 ? query.Value.Select(object (x) => x).ToArray() : query.Value.FirstOrDefault();
            field.DefaultValue = field.AutoConvert(value); // query.Value.FirstOrDefault()
        }

        return result;
    }

    public async Task<bool> HasPermission(IEntityContext context, string objectTypeName, ObjectTypePermission permission)
    {
        // slim version 
        var objectType = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.FullName, objectTypeName)
            .IncludeFields(
                f => f.FullName,
                f => f.Namespace,
                f => f.Name,
                f => f.RBAC,
                f => f.IsActive
            )
            .FirstOrDefaultAsync();

        return HasPermission(context, objectType, permission);
    }
    
    public bool HasPermission(IEntityContext context, ObjectType objectType, ObjectTypePermission permission) => objectType?.Can(context, permission) ?? false;
}