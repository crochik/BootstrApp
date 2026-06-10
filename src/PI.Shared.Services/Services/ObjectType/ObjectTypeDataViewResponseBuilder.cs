using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Operator = PI.Shared.Models.Expressions.Operator;

namespace PI.Shared.Services;

/// <summary>
/// Build response for Object Type (with optional View)
/// </summary>
public class ObjectTypeDataViewResponseBuilder : DataViewResponseBuilder
{
    private readonly ObjectTypeIntrospector _introspector;

    /// <summary>
    /// whether to include hidden fields
    /// </summary>
    public bool IncludeHiddenFields { get; set; } = true;

    /// <summary>
    /// whether to include all fields 
    /// </summary>
    public bool IncludeAllFields { get; set; } = false;

    private AppDataView AppDataView { get; }
    private Dictionary<string, ObjectTypeIntrospector.ReadableField> RequestedFields { get; set; }
    private Leaf ProjectionTree { get; set; }

    /// <summary>
    /// Whether to limit result to "recent/favorite" objects for the user
    /// </summary>
    public bool LimitToRecents { get; set; }

    public static ObjectTypeDataViewResponseBuilder New(MongoConnection connection, ObjectTypeIntrospector introspector, DataViewRequest request, AppDataView dataView, Projection projection)
        => new(connection, introspector, request, dataView, projection);

    private ObjectTypeDataViewResponseBuilder(MongoConnection connection, ObjectTypeIntrospector introspector, DataViewRequest request, AppDataView dataView, Projection projection) :
        base(connection, introspector.Context, request, dataView, introspector.ObjectType)
    {
        _introspector = introspector;
        AppDataView = dataView;
        Projection = projection;
    }

    protected ObjectTypeDataViewResponseBuilder(MongoConnection connection, IEntityContext context, DataViewRequest request, AppDataView dataView, ObjectType objectType) :
        base(connection, context, request, dataView, objectType)
    {
        AppDataView = dataView;
    }

    protected override async Task InitAsync()
    {
        if (_introspector == null)
        {
            await ObsoleteInitAsync();
            return;
        }

        ParseRequest();
        ValidateRequestedFields();
        ParseRequestedFields();

        // exclude any field that is a constraint for the profile so the user can't change it
        var objectTypeConstraints = _objectType
            .GetConditions(_context)
            .Where(x => x.Operator == Operator.Eq)
            .Select(x => x.FieldName)
            .Distinct()
            .ToHashSet();

        var indexedFieldsKvp = _introspector.IndexedFieldsRecursively;

        // any field that is indexed and can be read 
        var indexedFields = indexedFieldsKvp
            .Where(x => !objectTypeConstraints.Contains(x.Key))
            .ToDictionary();

        // flag the fields that can be sorted/filtered
        // having them in the filterform does not make them automatically filterable/sortable?!?!?!
        _dataView.DataView.Filter ??= indexedFieldsKvp.Keys.ToArray();

        CalculateFilterableFields(indexedFields);
        CalculateLookupsAsync();

        await AddReferenceFieldsToLookupsAsync();
    }

    /// <summary>
    /// When using API names, parse the request to replace any api name with the field name
    /// </summary>
    private void ParseRequest()
    {
        if (_request.GroupedFields != null)
        {
            if (_request.GroupedFields.IsEmpty())
            {
                _request.GroupedFields = null;
            }
            else
            {
                var fields = _request.GroupedFields.Select(x => x.Key);
                if (_request.Fields != null) fields = fields.Concat(_request.Fields);
                _request.Fields = fields.Distinct().ToArray();
            }
        }

        if (!UseApiNames) return;

        // allow using api names 
        var map = _introspector.ReadableFieldsRecursively.ToDictionary(x => x.Key, x => x.Key);
        foreach (var kvp in _introspector.ReadableFieldsRecursively)
        {
            map.TryAdd(kvp.Value.ApiAbsoluteName, kvp.Key);
        }

        var requestedFields = _request.Fields?.Select(x => map.TryGetValue(x, out var fn) ? fn : x)
            .ToArray();

        _request.Fields = requestedFields;

        if (_request.GroupedFields != null)
        {
            var fields = new Dictionary<string, GroupedFieldProjection>();
            foreach (var kvp in _request.GroupedFields)
            {
                if (map.TryGetValue(kvp.Key, out var fn))
                {
                    fields.TryAdd(fn, kvp.Value);
                }
                else
                {
                    throw new BadRequestException($"{kvp.Key}: grouped field not found");
                }
            }

            _request.GroupedFields = fields;
        }

        if (!string.IsNullOrEmpty(_request.OrderBy))
        {
            var reverseOrder = _request.OrderBy[0] == '-';
            var orderBy = reverseOrder ? _request.OrderBy[1..] : _request.OrderBy;
            if (map.TryGetValue(orderBy, out orderBy))
            {
                _request.OrderBy = reverseOrder ? $"-{orderBy}" : orderBy;
            }
        }

        if (_request.Criteria?.Length > 0)
        {
            _request.Criteria = _request.Criteria?.Select(x => Condition.New(map.TryGetValue(x.FieldName, out var fieldName) ? fieldName : x.FieldName, x.Operator, x.Value)).ToArray();
        }
    }

    private void CalculateFilterableFields(Dictionary<string, ObjectTypeIntrospector.ReadableField> indexedFields)
    {
        if (indexedFields == null)
        {
            FilterableFields = [];
            return;
        }

        // TODO: could/should also explicit exclude any already in the ReferenceFields
        // ...
        FilterableFields = indexedFields
            // .Where(x => x.Value.Visibility switch
            // {
            //     // FOR NOW exclude fields in other collections
            //     ObjectTypeIntrospector.FieldVisibility.RelationalField => false,
            //     ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => false,
            //     _ => true,
            // })
            .Where(x => x.Value.FieldTemplate.Field switch
            {
                ReferenceField => true, // they are handled elsewhere
                SelectField => true,
                TagsField => true,
                DateField => true,
                DateTimeField => true,
                CheckboxField => true,
                LocationDistanceField => true,
                LocationField => true,
                // level 2 
                TextField => true,
                NumberField => true,
                _ => false,
            })
            .Select(x =>
            {
                var cloned = CloneForFilter(x.Value.FieldTemplate.Field);
                cloned.Name = x.Key;
                cloned.Label = x.Value.AbsoluteLabel;
                cloned.Description = x.Value.Description;
                return cloned;
            })
            .ToArray();
    }

    private async Task AddReferenceFieldsToLookupsAsync()
    {
        // reference fields
        // implicit lookups, ReferenceFields:
        // - that are requested when AutoGenerateReferenceFieldNames is on
        // - with join behavior Exclude (works as a constraint)
        var filtered = _objectType.Fields
            .Where(x => x.Value.Field is ReferenceField referenceField &&
                        referenceField.ReferenceFieldOptions?.ObjectType != null &&
                        !referenceField.ReferenceFieldOptions.ObjectType.StartsWith("/") &&
                        ((AutoGenerateReferenceFieldNames && RequestedFields.ContainsKey(x.Key)) || referenceField.ReferenceFieldOptions?.JoinBehavior == JoinBehavior.Exclude)
            )
            .Select(x => (ReferenceField)x.Value.Field);

        var lookups = Lookups.Values.ToDictionary(x => $"{x.ObjectType.FullName}.{x.LocalFieldName}"
        );

        foreach (var referenceField in filtered)
        {
            var lookupKey = $"{referenceField.ReferenceFieldOptions.ObjectType}.{referenceField.Name}";

            var objectType = await _introspector.GetObjectTypeAsync(referenceField.ReferenceFieldOptions.ObjectType);
            if (objectType == null || !objectType.CanRead(_context)) continue;

            if (lookups.TryGetValue(lookupKey, out var lookup))
            {
                // already included 
                if (AutoGenerateReferenceFieldNames && RequestedFields.TryGetValue(referenceField.Name, out var childField))
                {
                    var fieldName = UseApiNames && referenceField.ApiName != null ? referenceField.ApiName : referenceField.Name;
                    var projection = (objectType.LookupFields?.Name ?? "Name").Replace('|', '.');

                    lookup.Fields ??= new Dictionary<string, string>();
                    lookup.Fields.TryAdd(fieldName, projection);
                }

                continue;
            }

            lookup = BuildLookup(referenceField, objectType, AutoGenerateReferenceFieldNames && RequestedFields.ContainsKey(referenceField.Name));
            Lookups.Add(referenceField.Name, lookup);
        }
    }

    public override void ApplyConditionsToMatchQuery(Condition[] criteria, Query<ExpandoObject> query, Dictionary<string, Parameter> parameters, Dictionary<string, FormField> fields)
    {
        if (_introspector == null)
        {
            base.ApplyConditionsToMatchQuery(criteria, query, parameters, fields);
            return;
        }

        // remove any lookups
        var filtered = criteria
            .Where(x => !_introspector.ReadableFieldsRecursively.TryGetValue(x.FieldName, out var readableField) ||
                        readableField.Visibility switch
                        {
                            ObjectTypeIntrospector.FieldVisibility.RelationalField => false,
                            ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => false,
                            _ => true,
                        }).ToArray();

        base.ApplyConditionsToMatchQuery(filtered, query, parameters, fields);
    }

    private void CalculateLookupsAsync()
    {
        var lookups = new Dictionary<string, Lookup>();

        // related objects => lookups
        var relationalFields = RequestedFields.Values
            .Where(x => x.Visibility is ObjectTypeIntrospector.FieldVisibility.RelationalField)
            .ToDictionary(x => x.AbsolutePath);

        foreach (var relationalField in relationalFields.Values)
        {
            var lookup = CreateLookup(relationalField);
            if (lookup == null) continue;

            lookups.Add(relationalField.AbsolutePath, lookup);
        }

        // check if the constraints include relations
        var relationNames = (_objectType.RelatedObjectTypes ?? Enumerable.Empty<RelatedObjectType>())
            .Where(x => x.RBAC.CanRead(_context))
            .Select(x => x.Name)
            .ToHashSet();

        var constraints = _objectType.GetConditions(_context)
            .Where(x => x.Operator == Operator.Exists && relationNames.Contains(x.FieldName));
        foreach (var constraint in constraints)
        {
            if (!_introspector.ReadableFieldsRecursively.TryGetValue(constraint.FieldName, out var readableField))
            {
                throw new BadRequestException($"Relation in the constraint is not readable: {constraint.FieldName}");
            }

            var lookup = CreateLookup(readableField);
            if (lookup == null)
            {
                throw new BadRequestException($"Failed to create Lookup for Relation in the constraint: {constraint.FieldName}");
            }

            lookup.JoinBehavior = JoinBehavior.Exclude;
            if (lookups.TryGetValue(readableField.AbsolutePath, out var existing))
            {
                existing.JoinBehavior = JoinBehavior.Exclude;
            }
            else
            {
                lookups.Add(readableField.AbsolutePath, lookup);
            }
        }

        // check the filter criteria to see if any of the filters are for this lookup
        foreach (var condition in _request?.Criteria ?? Enumerable.Empty<Condition>())
        {
            if (!_introspector.ReadableFieldsRecursively.TryGetValue(condition.FieldName, out var readableField)) continue;
            if (readableField.Visibility switch
                {
                    ObjectTypeIntrospector.FieldVisibility.RelationalField => false,
                    ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => false,
                    _ => true,
                }) continue;

            if (!readableField.TryGetRecursively(ObjectTypeIntrospector.FieldVisibility.RelationalField, out var relationalField))
            {
                throw new Exception("Relation not found: should never happen");
            }

            if (!lookups.TryGetValue(relationalField.AbsolutePath, out var lookup))
            {
                lookup = CreateLookup(relationalField);
                if (lookup == null) continue;

                lookups.Add(relationalField.AbsolutePath, lookup);
            }

            if (!ExpressionEvaluatorService.TryResolve(_context, null, condition.Value, out var resolved))
            {
                // failed to resolve value ?!?!
                continue;
            }

            var pathInCollection = readableField.PathInCollection;

            if (lookup.Criteria.Any(x => x.FieldName == pathInCollection))
            {
                // TODO: can't handle refining constraint for now, just ignore
                continue;
            }

            lookup.Criteria = lookup.Criteria
                .Append(Condition.New(pathInCollection, condition.Operator, resolved))
                .ToArray();

            // it was commented out (I assume because it can take too long) - 2026/05/14
            // limit results to matches
            lookup.JoinBehavior = JoinBehavior.Exclude;
        }

        Lookups = lookups;
    }

    private Lookup CreateLookup(ObjectTypeIntrospector.ReadableField relationalField)
    {
        if (relationalField.FieldTemplate.Field is not ObjectField objectField)
        {
            // TODO: add support for childrenfield?
            // ...
            return null;
        }

        return CreateLookup(relationalField.RelatedObjectType, relationalField.ObjectType, relationalField.AbsolutePath);
    }

    public static Lookup CreateLookup(RelatedObjectType relation, ObjectType relatedObjectType, string absolutePath, bool single = true)
    {
        var condition = relation?.Criteria?.Conditions?.FirstOrDefault();
        if (condition == null)
        {
            // no conditions, can't determine the localField/foreignField 
            return null;
        }

        // TODO: improve the "parser" here,  could define a fake object with all fields = $FieldName and use the resolver
        // ...

        // TODO: could infer from the "other conditions" (not foreign=>local) that some other fields should be added in the "let" and used in the match 
        // ....

        // start simple
        // for now assumes the first condition is the localField => foreignField
        if (condition.Value is not string stringValue || !stringValue.StartsWith("{{") || !stringValue.EndsWith("}}") || condition.Operator != Operator.Eq)
        {
            // unexpected/unhandled
            return null;
        }

        var foreignField = condition.FieldName;
        var localField = FormField.GetPathInCollection(stringValue[2..^2]);

        // TODO: make sure the localField is actually a field in the current object 
        // ... 

        var lookup = new Lookup
        {
            // ReferenceField =  null,
            ObjectType = relatedObjectType,
            ForeignFieldName = foreignField,
            LocalFieldName = localField,
            Fields = new Dictionary<string, string>(),
            As = $"__{absolutePath}__",
            Criteria = relation.Criteria?.Conditions ?? [],
            AtMostOne = single,
        };

        return lookup;
    }

    [Obsolete("get rid of after making _inspector required")]
    private async Task ObsoleteInitAsync()
    {
        ValidateRequestedFields();

        RequestedFields = _request.Fields
            .Select(x => _objectType.Fields.TryGetValue(x, out var field) ? field : null)
            .Where(x => x != null)
            .ToDictionary(
                x => x.Field.Name,
                x => new ObjectTypeIntrospector.ReadableField(null)
                {
                    FieldTemplate = x,
                    Visibility = ObjectTypeIntrospector.FieldVisibility.Normal,
                }
            );

        var existingConstraints = _objectType
            .GetConditions(_context)
            .Where(x => x.Operator == Operator.Eq)
            .Select(x => x.FieldName)
            .Distinct()
            .ToHashSet();

        var indexedFieldsKvp = _objectType.Fields
            .Where(x => x.Value.RBAC.CanRead(_context))
            .Where(x => x.Value.Indexed)
            .ToDictionary();

        // any field that is indexed and can be read 
        var indexedFields = indexedFieldsKvp
            .Where(x => !existingConstraints.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value.Field);

        // if not explicitly set, assume all 
        _dataView.DataView.Filter ??= indexedFieldsKvp.Keys.ToArray();

        CalculateFilterableFields(indexedFields);

        var referenceFields = _objectType.Fields
            .Where(x => x.Value.RBAC.CanRead(_context))
            .Select(x => x.Value.Field)
            .OfType<ReferenceField>()
            .Where(x => x.ReferenceFieldOptions != null && !x.ReferenceFieldOptions.ObjectType.StartsWith("/"))
            // .Select(CloneForFilter)
            .ToArray();

        await LoadReferencedObjectsAsync(referenceFields);
    }

    protected override Dictionary<string, FormField> GetIndexedFields()
    {
        if (_introspector == null)
        {
            return base.GetIndexedFields();
        }

        return _introspector.IndexedFieldsRecursively.ToDictionary(x => x.Key, x => x.Value.FieldTemplate.Field);
    }

    protected override void ValidateRequestedFields()
    {
        if (_request.Fields?.Length > 0 || _dataView.DataView.Fields?.Length > 0)
        {
            base.ValidateRequestedFields();
            return;
        }

        var fields = defaultFields()
            .Distinct()
            .Where(x => x != null)
            .ToHashSet();

        _request.Fields = _objectType.Fields
            .Where(x => x.Value.RBAC.CanRead(_context) && (fields.Contains(x.Key) || IncludeAllFields))
            .Select(x => x.Key)
            .ToArray();

        var required = GetNeededFields(requiredOutput: true).ToArray();
        if (required.Length > 0)
        {
            _request.Fields = _request.Fields
                .Concat(required)
                .Distinct()
                .ToArray();
        }

        return;

        IEnumerable<string> defaultFields()
        {
            if (AppDataView.Fields?.Length > 0)
            {
                foreach (var fieldName in AppDataView.Fields)
                {
                    yield return fieldName;
                }

                yield break;
            }

            if (_objectType.LookupFields != null)
            {
                yield return _objectType.LookupFields.ImageUrl;
                yield return _objectType.LookupFields.Name;
                yield return _objectType.LookupFields.Description;
                yield return _objectType.LookupFields.Key;
            }

            yield return Model.IdFieldName;
            yield return nameof(Model.Name);
        }
    }

    /// <summary>
    /// Determine sort order, for lookup projections use NameField. 
    /// </summary>
    protected override string GetOrderByField()
    {
        // sort/page before the rest of the pipeline
        var orderBy = base.GetOrderByField();
        if (!string.IsNullOrEmpty(orderBy)) return orderBy;

        switch (Projection)
        {
            case Projection.Lookup:
                return _objectType?.LookupFields?.Name ?? nameof(Model.Name);

            case Projection.TopValues:
                return _request.LookupField;
        }

        return orderBy;
    }

    protected override IEnumerable<BsonDocument> BuildStages()
    {
        return LimitToRecents ? BuildStagesWithRecentObjectsLookup() : base.BuildStages();
    }

    /// <summary>
    /// "Hacked" version of buildStages that will limit the results to the recent objects for the user
    /// </summary>
    private IEnumerable<BsonDocument> BuildStagesWithRecentObjectsLookup()
    {
        // "optimized" lookup for recent objects using pipeline
        if (string.IsNullOrEmpty(Collection)) throw new BadRequestException("Missing Collection");
        if (StoredProcedure?.Pipeline?.Length > 0) throw new BadRequestException("Stored procedure pipelines not supported yet");
        if (DatabaseName != null) throw new BadRequestException("Can't lookup across databases");
        // if (_request.GroupedFields != null) throw new BadRequestException("group fields not supported");

        ProjectedFields = CalculateProjectedFields()?
            .DistinctBy(x => x.Key)
            .ToDictionary();

        var stages = new List<BsonDocument>();

        // match recent objects for user
        stages.Add(new BsonDocument
        {
            {
                "$match", new BsonDocument
                {
                    { nameof(RecentObject.AccountId), _context.AccountId.Value.ToString() },
                    { nameof(RecentObject.EntityId), new BsonDocument("$in", new BsonArray(_context.GetAllUserIds().Select(x => x.ToString()))) },
                    { nameof(RecentObject.AllObjectTypes), _objectType.FullName },
                    // {
                    //     "$or", new BsonArray([
                    //         new BsonDocument(nameof(RecentObject.ObjectType), _objectType.FullName),
                    //         new BsonDocument(nameof(RecentObject.AllObjectTypes), _objectType.FullName)
                    //     ])
                    // },
                }
            },
        });

        var mainMatch = BuildMatch().ToArray();

        // lookup into main collection
        stages.Add(new BsonDocument
        {
            {
                "$lookup", new BsonDocument
                {
                    { "from", Collection },
                    { "as", "__main_collection__" },
                    { "localField", nameof(RecentObject.ObjectId) },
                    { "foreignField", Model.IdFieldName },
                    {
                        "pipeline", new BsonArray(mainMatch)
                    }
                }
            }
        });

        stages.Add(new BsonDocument
        {
            {
                "$unwind", "$__main_collection__"
            }
        });

        stages.Add(new BsonDocument
        {
            {
                "$sort", new BsonDocument(nameof(RecentObject.LastModifiedOn), -1)
            }
        });

        stages.AddRange(BuildSkipAndLimitStages());

        stages.Add(new BsonDocument("$replaceRoot", new BsonDocument("newRoot", "$__main_collection__")));

        // REFERENCE/NORMAL IMPLEMENTATION  
        // stages.AddRange(BuildMatch().ToArray());
        // stages.AddRange(StoredProcedurePipelineStages());
        // stages.AddRange(BuildMainSortStages());
        // stages.AddRange(BuildMainLimitStages());
        stages.AddRange(BuildLookupStages());
        // stages.AddRange(BuildLimitAfterLookupsStages());
        stages.AddRange(BuildProjectionStages());
        stages.AddRange(BuildGroupStages());
        stages.AddRange(BuildFinalSortStages());

        return stages;
    }

    /// <summary>
    /// override base implementation when limiting to recent objects as we start with a different collection  
    /// </summary>
    protected override async Task<List<ExpandoObject>> AggregateAsync(IEnumerable<BsonDocument> stages)
    {
        if (!LimitToRecents) return await base.AggregateAsync(stages);

        var pipeline = PipelineDefinition<ExpandoObject, ExpandoObject>.Create(stages);
        var collection = _connection.GetCollection<ExpandoObject>(RecentObject.CollectionName);

        return await collection.Aggregate(pipeline).ToListAsync();
    }

    /// <summary>
    /// override sort to handle search and not sort if groupping
    /// </summary>
    /// <returns></returns>
    protected override IEnumerable<BsonDocument> BuildMainSortStages()
    {
        switch (Projection)
        {
            case Projection.TopValues:
                return base.BuildMainSortStages();
        }

        if (_request.GroupedFields != null)
        {
            // do not sort here if grouping
            return [];
        }

        if (SearchMetaDataField != null)
        {
            // when using the search index, select the most relevant results
            return [new BsonDocument("$sort", new BsonDocument($"{SearchMetaDataField}.Score", -1))];
        }

        return base.BuildMainSortStages();
    }

    /// <summary>
    /// override so it will not limit if grouping
    /// </summary>
    protected override IEnumerable<BsonDocument> BuildMainLimitStages()
    {
        if (_request.GroupedFields != null) return [];
        return base.BuildMainLimitStages();
    }

    /// <summary>
    /// override so it will not limit if grouping
    /// </summary>
    protected override IEnumerable<BsonDocument> BuildLimitAfterLookupsStages()
    {
        if (_request.GroupedFields != null) return [];
        return base.BuildLimitAfterLookupsStages();
    }

    /// <summary>
    /// override calculation of group stages (as it is not supported generically yet)
    /// </summary>
    protected override IEnumerable<BsonDocument> BuildGroupStages()
    {
        if (_request.GroupedFields == null) yield break;

        if (ProjectedFields == null) throw new BadRequestException("Grouping not supported without projected fields");

        var distinctFields = new HashSet<string>();
        var fields = new Dictionary<string, GroupedFieldProjection>();
        foreach (var kvp in _request.GroupedFields)
        {
            if (!_introspector.ReadableFieldsRecursively.TryGetValue(kvp.Key, out var readableField))
            {
                throw new BadRequestException($"Grouped Field {kvp.Key} not readable");
            }

            var fieldName = UseApiNames ? readableField.ApiAbsolutePath : readableField.AbsolutePath;
            if (!ProjectedFields.ContainsKey(fieldName))
            {
                throw new BadRequestException($"Grouped Field {kvp.Key} not projected");
            }

            fields.Add(fieldName, kvp.Value);
            if (kvp.Value == GroupedFieldProjection.Distinct) distinctFields.Add(fieldName);
        }

        if (distinctFields.IsEmpty()) throw new BadRequestException("No distinct fields to group");

        var group = new Dictionary<string, object>
        {
            { "_id", new BsonDocument(distinctFields.ToDictionary(x => x, x => "$" + x)) },
        };

        if (SearchMetaDataField == null || !_introspector.ReadableFieldsRecursively.TryGetValue(SearchMetaDataField, out var searchMetaDataField))
        {
            searchMetaDataField = null;
        }

        var searchMetaDataScoreFieldName = UseApiNames ? searchMetaDataField?.ApiAbsolutePath : searchMetaDataField?.AbsolutePath;
        if (searchMetaDataScoreFieldName != null)
        {
            searchMetaDataScoreFieldName += UseApiNames ? ".score" : ".Score";
        }

        var topLevel = new HashSet<string>();
        foreach (var fieldName in ProjectedFields.Keys)
        {
            if (fieldName == "_id")
            {
                // hack to store _id for later
                group.Add("__id__", new BsonDocument("$first", "$_id"));
                topLevel.Add(fieldName);
                continue;
            }

            // TODO: assuming the SearchMetaData object type has a Score field mapped as "score"
            if (fieldName == searchMetaDataScoreFieldName)
            {
                // grouping search, adds 
                group.Add("__search_metadata_score__", new BsonDocument("$max", "$" + fieldName));
                continue;
            }

            var parts = fieldName.Split('.');
            if (!topLevel.Add(parts[0]))
            {
                // already added
                continue;
            }

            if (!fields.TryGetValue(parts[0], out var projection))
            {
                projection = GroupedFieldProjection.First;
            }

            group.Add(parts[0], projection switch
            {
                // GroupedFieldProjection.Distinct => new BsonDocument("$first", fieldName),
                // GroupedFieldProjection.First => new BsonDocument("$first", fieldName),
                _ => new BsonDocument("$first", "$" + parts[0]),
            });
        }

        // TODO: have some magic way to include/exclude it 
        // ...
        // if (!ProjectedFields.ContainsKey("_count"))
        // {
        //     group.Add("_count", new BsonDocument("$sum", 1));
        // }

        yield return new BsonDocument("$group", new BsonDocument(group));

        var explicitSortStage = default(BsonDocument);
        var orderBy = GetOrderByField();
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            var reverseOrder = orderBy.StartsWith('-');
            orderBy = reverseOrder ? orderBy[1..] : orderBy;
            if (_objectType?.Fields != null && _objectType.Fields.TryGetValue(orderBy, out var ft))
            {
                var projectedFieldName = (UseApiNames ? ft.Field.ApiName : null) ?? FormField.GetPathInCollection(ft.Field.Name);
                explicitSortStage = new BsonDocument("$sort", new BsonDocument(projectedFieldName, reverseOrder ? -1 : 1));
            }
        }

        if (searchMetaDataScoreFieldName != null)
        {
            // sort by score relevance
            yield return new BsonDocument("$sort", new BsonDocument("__search_metadata_score__", -1));

            // skip and limit
            var limited = false;
            foreach (var stage in BuildSkipAndLimitStages())
            {
                limited = true;
                yield return stage;
            }

            // sort by field
            if (limited && explicitSortStage != null) yield return explicitSortStage;
        }
        else
        {
            // sort by field
            if (explicitSortStage != null) yield return explicitSortStage;

            // skip and limit
            foreach (var stage in BuildSkipAndLimitStages())
            {
                yield return stage;
            }
        }

        // project
        yield return new BsonDocument("$project", new BsonDocument(topLevel.ToDictionary(x => x, x => (object)(x == "_id" ? "$__id__" : 1))));
    }

    /// <summary>
    /// override so it will use introspector to get projections
    /// and will add "lookup fields" to LookupProjection
    /// </summary>
    /// <returns></returns>
    protected override IEnumerable<KeyValuePair<string, object>> CalculateProjectedFields()
    {
        switch (Projection)
        {
            case Projection.All:
            case Projection.TopValues:
                return base.CalculateProjectedFields();
        }

        var fields = Projection switch
        {
            Projection.Lookup => GetProjectionForLookup().ToList(),
            _ => getProjectedFields().ToList(),
        };

        // eliminate projection overlaps
        var keys = fields.Select(x => x.Key).ToArray();
        foreach (var key in keys)
        {
            var parent = $"{key}.";
            for (var i = fields.Count - 1; i >= 0; i--)
            {
                if (fields[i].Key.StartsWith(parent))
                {
                    fields.RemoveAt(i);
                }
            }
        }

        return fields.IsEmpty() ? [new KeyValuePair<string, object>("_id", 1)] : fields;

        // For lookup projection includes specific fields 
        IEnumerable<KeyValuePair<string, object>> getProjectedFields()
        {
            if (_introspector == null)
            {
                // TODO: _introspector to be a must
                return GetProjectedFields();
            }

            var projected = GetProjectionUsingTree();

            // add other lookup fields (not projected) 
            // e.g. the ReferenceField|Names
            foreach (var lookup in Lookups.Values)
            {
                foreach (var kvp in lookup.Fields)
                {
                    projected = projected.Append(new KeyValuePair<string, object>($"{kvp.Key}|{kvp.Value}", "$" + $"{lookup.As}.{kvp.Value}"));

                    if (kvp.Value != "Name")
                    {
                        // fallback to previously hardcoded behavior of using "*|Name"
                        projected = projected.Append(new KeyValuePair<string, object>($"{kvp.Key}|Name", "$" + $"{lookup.As}.{kvp.Value}"));
                    }
                }
            }

            return projected;
        }
    }

    /// <summary>
    /// Calculate lookups to other collections 
    /// </summary>
    protected override IEnumerable<BsonDocument> BuildLookupStages()
    {
        switch (Projection)
        {
            case Projection.Lookup:
            case Projection.TopValues:
                yield break;
        }

        foreach (var lookupKvp in Lookups)
        {
            KeyValuePair<string, object>[] projections = null;
            if (_introspector != null)
            {
                // fields selected
                var leaf = ProjectionTree.Find(lookupKvp.Key);
                if (leaf != null)
                {
                    projections = GetLookupProjections(leaf, lookupKvp.Value, "", "").ToArray();
                }
            }

            if (!lookupKvp.Value.Fields.IsEmpty())
            {
                // automatically added by ReferenceField
                // exclude any that has been explicitly selected already
                projections ??= [];
                var existing = projections.Select(x => x.Key).ToHashSet();

                projections = projections.Concat(
                    lookupKvp.Value.Fields
                        .Where(x => !existing.Contains(x.Key))
                        .Select(x => new KeyValuePair<string, object>(x.Value, "$" + x.Value))
                ).ToArray();
            }

            foreach (var stage in BuildLookupStages(_context, lookupKvp.Key, lookupKvp.Value, projections))
            {
                yield return stage;
            }
        }
    }

    protected override BsonDocument BuildLookupWithCriteriaStage(IEntityContext context, string fieldName, Condition[] allConditions, KeyValuePair<string, object>[] projections, Lookup lookup)
    {
        // TODO: add field substitutes 
        // ...         
        allConditions.ReplaceValuePlaceHolders(context);

        var exists = allConditions.Where(x => x.Operator == Operator.Exists).ToArray();
        var basicConditions = allConditions.Where(x => x.Operator != Operator.Exists).ToArray();

        var pipeline = new BsonArray
        {
            new BsonDocument("$match", new BsonDocument(BuildMatch(lookup.ForeignFieldName, basicConditions))),
        };

        if (lookup.AtMostOne)
        {
            pipeline.Add(new BsonDocument("$limit", 1));
        }
        
        pipeline.AddRange(BuildExistsStages(context, lookup.ObjectType, exists, fieldName));
        
        if (projections?.Length > 0)
        {
            pipeline.Add(new BsonDocument("$project", new BsonDocument(projections.DistinctBy(x => x.Key))));
        }
        else
        {
            // prevent all fields from making to the result
            pipeline.Add(new BsonDocument("$project", new BsonDocument("_id", 1)));
        }
        
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

    private IEnumerable<BsonDocument> BuildExistsStages(IEntityContext context, ObjectType objectType, Condition[] existsConditions, string fieldName)
    {
        if (existsConditions.IsEmpty()) yield break;

        foreach (var c in existsConditions)
        {
            if (objectType.Constraints == null)
            {
                // no additional constraints, 
                // TODO: add lookup to enforce that there is a match
                // ignore for now....
                continue;
            }

            // add lookup and unwind to limit results
            var relation = objectType.RelatedObjectTypes?.FirstOrDefault(x => x.Name == c.FieldName);
            if (relation == null) throw new ForbiddenException($"Couldn't enforce exists constraint: {c.FieldName}");
            if (!_introspector.TryGetObjectTypeFromCache(relation.ObjectType, out var relatedObjectType))
            {
                throw new ForbiddenException($"Couldn't enforce exists constraint: {c.FieldName}, {relation.ObjectType} not found");
            }

            var allConditions = relatedObjectType.GetConditions(context).ToArray();
            if (allConditions.IsEmpty())
            {
                // TODO: add lookup to enforce that there is a match
                // ignore for now....
                continue;
            }

            var childFieldName = $"{fieldName}|{relation.Name}";
            if (_introspector.ChildObjects.TryGetValue(childFieldName, out var readableField))
            {
                // TODO: use it to define projections ????? 
                // ...

                // TODO: use it, instead of building a new lookup?
                // ...
            }

            var lookup = CreateLookup(relation, relatedObjectType, childFieldName);
            var lookupStage = BuildLookupWithCriteriaStage(context, childFieldName, allConditions, null, lookup);
            yield return lookupStage;
            yield return new BsonDocument("$unwind", $"${lookup.As}");
        }
    }


    private IEnumerable<KeyValuePair<string, object>> GetProjectionUsingTree()
    {
        var projected = Enumerable.Empty<KeyValuePair<string, object>>();
        foreach (var leaf in ProjectionTree.Children.Values)
        {
            if (leaf.Field.FieldTemplate.Field is CalculatedField)
            {
                // calculated field, nothing to project here 
                continue;
            }

            var projectOut = leaf.ProjectOut; // RequestedFields.ContainsKey(leaf.Field.FieldPath);
            if (leaf.Field.Visibility == ObjectTypeIntrospector.FieldVisibility.RelationalField)
            {
                if (!Lookups.TryGetValue(leaf.Field.AbsolutePath, out var lookup))
                {
                    throw new Exception("something wrong");
                }

                if (projectOut)
                {
                    // new behavior, project out the entire lookup. 
                    // the lookup phase SHOULD limit the fields projected out there
                    var projectTo = UseApiNames ? leaf.Field.ApiAbsolutePath ?? leaf.Field.AbsolutePath : FormField.GetPathInCollection(leaf.Field.AbsolutePath);
                    projected = projected.Append(new KeyValuePair<string, object>(projectTo, "$" + lookup.As));
                }
                else
                {
                    projected = projected.Concat(GetLookupChildrenProjections(leaf, $"{lookup.As}."));
                }
            }
            else if (leaf.Field.Visibility == ObjectTypeIntrospector.FieldVisibility.ObjectField)
            {
                projected = projected.Concat(leaf.Field.FieldTemplate.Field switch
                {
                    ObjectField f => projectOut ? GetObjectProjections(leaf, f) : GetChildrenProjections(leaf, f),
                    ChildrenField f => projectOut ? MapChildrenArrayField(leaf, f) : GetChildrenProjections(leaf, f),
                    _ => [],
                });
            }
            else if (leaf.Field.Visibility == ObjectTypeIntrospector.FieldVisibility.Normal)
            {
                projected = projected.Append(ProjectField(leaf.Field));
            }
            else
            {
                throw new Exception("something wrong");
            }
        }

        return projected;
    }

    private IEnumerable<KeyValuePair<string, object>> GetObjectProjections(Leaf leaf, ObjectField field)
    {
        foreach (var child in leaf.Children.Values)
        {
            if (child.Field.FieldTemplate.Field is CalculatedField)
            {
                // calculated field, nothing to project here 
                continue;
            }

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                {
                    var projectFrom = "$" + FormField.GetPathInCollection(child.Field.AbsolutePath);
                    var projectTo = UseApiNames ? child.Field.ApiAbsolutePath ?? child.Field.AbsolutePath : FormField.GetPathInCollection(child.Field.AbsolutePath);
                    var arrayMap = GetMapProjectionOfArray(child, childrenField, projectTo, projectFrom);
                    if (arrayMap.HasValue) yield return arrayMap.Value;
                    break;
                }

                case ObjectField objectField:
                {
                    foreach (var kvp in GetObjectProjections(child, objectField)) yield return kvp;
                    break;
                }

                default:
                {
                    var projection = "$" + FormField.GetPathInCollection(child.Field.AbsolutePath);
                    var projectTo = UseApiNames ? child.Field.ApiAbsolutePath ?? child.Field.AbsolutePath : FormField.GetPathInCollection(child.Field.AbsolutePath);
                    yield return new KeyValuePair<string, object>(projectTo, projection);
                    break;
                }
            }
        }
    }

    private KeyValuePair<string, object>? GetMapProjectionOfArray(Leaf child, ChildrenField childrenField, string projectTo, string projectFrom)
    {
        if (childrenField.ChildrenFieldOptions?.KeyType != ChildrenFieldOptions.IndexKeyType)
        {
            // TODO: handle dicts ....very likley not possible without simply including all
            // ...
            return null;
        }

        var inMap = new Dictionary<string, object>(GetArrayProjections(child, childrenField, "$$this."));
        if (inMap.Count == 0) return null;

        return new KeyValuePair<string, object>(projectTo, new BsonDocument
        {
            {
                "$map", new BsonDocument
                {
                    { "input", projectFrom },
                    { "in", new BsonDocument(inMap) }
                }
            }
        });
    }

    /// <summary>
    /// Return projections for an array of objects
    /// TODO: could merge with MapChildrenArrayField
    /// </summary>
    private IEnumerable<KeyValuePair<string, object>> GetArrayProjections(Leaf leaf, ChildrenField field, string projectionPrefix = "$")
    {
        foreach (var child in leaf.Children.Values)
        {
            if (child.Field.FieldTemplate.Field is CalculatedField)
            {
                // calculated field, nothing to project here 
                continue;
            }

            var projection = projectionPrefix + FormField.GetPathInCollection(child.Field.FieldTemplate.Field.Name);
            var projectTo = UseApiNames ? child.Field.FieldTemplate.Field.ApiName ?? child.Field.FieldTemplate.Field.Name : FormField.GetPathInCollection(child.Field.FieldTemplate.Field.Name);

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                    if (childrenField.ChildrenFieldOptions?.KeyType == ChildrenFieldOptions.IndexKeyType)
                    {
                        // TODO: how to handle an array inside objects in an array, probably another map ... not sure it works
                        // ... 
                        var inMap = new Dictionary<string, object>(GetArrayProjections(child, childrenField, "$$this."));
                        if (inMap.Count > 0)
                        {
                            yield return new KeyValuePair<string, object>(projectTo, new BsonDocument
                            {
                                {
                                    "$map", new BsonDocument
                                    {
                                        { "input", projection },
                                        { "in", new BsonDocument(inMap) }
                                    }
                                }
                            });
                        }
                    }
                    else
                    {
                        // TODO: still no good way to handle a dictionary
                        // ...
                    }

                    break;

                case ObjectField objectField:
                {
                    // TODO: use GetObjectProjections but with relative to parent paths
                    // foreach (var kvp in GetObjectProjections(child, objectField)) yield return kvp;
                    break;
                }

                default:
                {
                    // relative to parent (array)
                    yield return new KeyValuePair<string, object>(projectTo, projection);
                    break;
                }
            }
        }
    }

    private IEnumerable<KeyValuePair<string, object>> GetChildrenProjections(Leaf leaf, ObjectField field)
    {
        foreach (var child in leaf.Children.Values)
        {
            if (child.Field.FieldTemplate.Field is CalculatedField)
            {
                // calculated field, nothing to project here 
                continue;
            }

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                {
                    var projectFrom = "$" + FormField.GetPathInCollection(child.Field.AbsolutePath);
                    var projectTo = UseApiNames ? child.Field.ApiAbsoluteName ?? child.Field.AbsolutePath : child.Field.AbsolutePath;

                    var arrayMap = GetMapProjectionOfArray(child, childrenField, projectTo, projectFrom);
                    if (arrayMap.HasValue) yield return arrayMap.Value;
                    break;
                }

                case ObjectField objField:
                    // TODO: project children but use this field path is the starting point?
                    // ...
                    break;

                default:
                    yield return ProjectField(child.Field);
                    break;
            }
        }
    }

    /// <summary>
    /// Project object field children inside an array map
    /// </summary>
    private IEnumerable<KeyValuePair<string, object>> GetChildrenProjectionsForArrayMap(Leaf leaf, ObjectField field, string replacePrefix, string newPrefix)
    {
        foreach (var child in leaf.Children.Values)
        {
            // relative to parent, so use field name (not value in the projection tree)
            var projectTo = (UseApiNames ? child.Field.FieldTemplate.Field.ApiName : null) ?? FormField.GetPathInCollection(child.Field.FieldTemplate.Field.Name);

            if (child.Field.FieldTemplate.Field is CalculatedField)
            {
                // calculated field, nothing to project here 
                continue;
            }

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                    // TODO: map array within array projection 
                    // ...
                    break;

                case ObjectField objField:
                    // TODO: project children but use this field path is the starting point?
                    // ...
                    break;

                default:
                {
                    // "normal fields", replace the "from path" to use relative the $$this input
                    var childField = child.Field;
                    var from = CalculateFieldProjection(childField.AbsolutePath);
                    if (from is string fromStr)
                    {
                        if (!fromStr.StartsWith(replacePrefix))
                        {
                            // TODO: some case we are ignoring
                            // ...
                            break;
                        }

                        from = newPrefix + fromStr.Substring(replacePrefix.Length + 1);
                    }

                    yield return new KeyValuePair<string, object>(projectTo, from);
                    break;
                }
            }
        }
    }

    // $map: {
    //     input: "$details",
    //     in: { 
    //         color: "$$this.color",
    //         size: "$$this.size"
    //     }
    // }        
    private IEnumerable<KeyValuePair<string, object>> MapChildrenArrayField(Leaf leaf, ChildrenField field)
    {
        var projectTo = UseApiNames ? leaf.Field.ApiAbsolutePath ?? leaf.Field.AbsolutePath : FormField.GetPathInCollection(leaf.Field.AbsolutePath);

        var from = leaf.Field.PathInCollection;
        return MapChildrenArrayField(leaf, field, projectTo, input: "$" + from);
    }


    /// <summary>
    /// get map for array
    /// TODO: could merge with  GetArrayProjections?
    /// </summary>
    private IEnumerable<KeyValuePair<string, object>> MapChildrenArrayField(Leaf leaf, ChildrenField field, string projectTo, string input)
    {
        if (field.ChildrenFieldOptions?.KeyType != ChildrenFieldOptions.IndexKeyType)
        {
            // TODO: add support for dicts?
            // only supports arrays
            // without knowing the keys, there is no real way to write a projection 
            // could add for the ones where the key is a selectField but it seems like an edge scenario
            // ...
            yield break;
        }

        if (leaf.Children.IsEmpty())
        {
            // nothing to do
            yield break;
        }

        var levelPath = UseApiNames ? leaf.Field.ApiAbsolutePath : leaf.Field.AbsolutePath;

        var fieldsMap = new BsonDocument();
        foreach (var child in leaf.Children.Values)
        {
            var fieldPath = child.Field.FieldTemplate.Field.Name; // child.Field.FieldPath[(levelPath.Length + 1)..];
            var fieldName = UseApiNames ? child.Field.ApiAbsolutePath[(levelPath.Length + 1)..] : fieldPath;

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                {
                    // should be only one (but in theory can be none)
                    foreach (var kvp in MapChildrenArrayField(child, childrenField, fieldName, input: "$$this." + FormField.GetPathInCollection(child.Field.FieldTemplate.Field.Name)))
                    {
                        fieldsMap[kvp.Key] = kvp.Value as BsonDocument;
                    }

                    break;
                }

                case ObjectField objectField:
                {
                    var childObject = GetChildrenProjectionsForArrayMap(
                        child,
                        objectField,
                        replacePrefix: "$" + FormField.GetPathInCollection(child.Field.AbsolutePath),
                        newPrefix: "$$this." + FormField.GetPathInCollection(fieldPath) + "."
                    ).ToArray();
                    fieldsMap[fieldName] = new BsonDocument(childObject);
                    break;
                }

                default:
                    fieldsMap[fieldName] = "$$this." + FormField.GetPathInCollection(fieldPath);
                    break;
            }
        }

        var map = new BsonDocument
        {
            {
                "$map", new BsonDocument
                {
                    { "input", input },
                    { "in", fieldsMap }
                }
            }
        };

        yield return new KeyValuePair<string, object>(projectTo, map);
    }

    private IEnumerable<KeyValuePair<string, object>> GetChildrenProjections(Leaf leaf, ChildrenField field)
    {
        // TODO: add support to expand fields in a array?
        // it would to either unwind the array or change the field in the response to become an array
        // ... 

        yield break;
    }

    private KeyValuePair<string, object> ProjectField(ObjectTypeIntrospector.ReadableField field)
    {
        // add projection
        var projectTo = UseApiNames ? field.ApiAbsoluteName ?? field.AbsolutePath : field.AbsolutePath;
        return new KeyValuePair<string, object>(projectTo, CalculateFieldProjection(field.AbsolutePath));
    }

    private IEnumerable<KeyValuePair<string, object>> GetLookupProjections(Leaf leaf, Lookup lookup, string fromPrefix, string toPrefix)
    {
        foreach (var child in leaf.Children.Values)
        {
            var projection = "$" + fromPrefix + FormField.GetPathInCollection(child.Field.PathInCollection);
            var projectTo = toPrefix + (UseApiNames ? child.Field.ApiName : child.Field.Name);

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                {
                    var input = $"{fromPrefix}{child.Field.PathInCollection}";
                    foreach (var kvp in MapChildrenArrayField(child, childrenField, projectTo, input: "$" + input)) yield return kvp;
                    break;
                }

                case ObjectField:
                {
                    var objectProjections = GetLookupProjections(child, lookup, "", "").ToDictionary();
                    yield return new KeyValuePair<string, object>(projectTo, objectProjections);
                    break;
                }

                default:
                {
                    yield return new KeyValuePair<string, object>(projectTo, projection);
                    break;
                }
            }
        }
    }

    private IEnumerable<KeyValuePair<string, object>> GetLookupChildrenProjections(Leaf leaf, string fromPrefix)
    {
        foreach (var child in leaf.Children.Values)
        {
            var projectTo = UseApiNames ? child.Field.ApiAbsoluteName ?? child.Field.AbsolutePath : child.Field.AbsolutePath;

            switch (child.Field.FieldTemplate.Field)
            {
                case HiddenField:
                    break;

                case ChildrenField childrenField:
                {
                    var input = $"{fromPrefix}{child.Field.PathInCollection}";
                    foreach (var kvp in MapChildrenArrayField(child, childrenField, projectTo, input: "$" + input)) yield return kvp;
                    break;
                }

                case ObjectField:
                {
                    foreach (var kvp in projectChildren(projectTo, child.Field.AbsolutePath, child)) yield return kvp;
                    break;
                }

                default:
                {
                    yield return new KeyValuePair<string, object>(projectTo, "$" + $"{fromPrefix}{child.Field.PathInCollection}");
                    break;
                }
            }
        }

        IEnumerable<KeyValuePair<string, object>> projectChildren(string projectTo, string startPath, Leaf level)
        {
            foreach (var child in level.Children.Values)
            {
                switch (child.Field.FieldTemplate.Field)
                {
                    case HiddenField:
                        break;

                    case ChildrenField childrenField:
                        // TODO: ????
                        break;

                    case ObjectField:
                        foreach (var kvp in projectChildren(projectTo, startPath, child)) yield return kvp;
                        break;

                    default:
                    {
                        var fieldPath = child.Field.AbsolutePath[(startPath.Length + 1)..];
                        yield return new KeyValuePair<string, object>($"{projectTo}.{fieldPath}", "$" + $"{fromPrefix}{child.Field.PathInCollection}");
                        break;
                    }
                }
            }
        }
    }

    private IEnumerable<KeyValuePair<string, object>> GetProjectionForLookup()
    {
        var fieldNames = getLookupFieldNames()
            .Where(x => x != null)
            .Where(x => _objectType.Fields.TryGetValue(x, out var ft) && ft.RBAC.CanRead(_context))
            .Distinct();

        foreach (var fieldName in fieldNames)
        {
            yield return new KeyValuePair<string, object>(fieldName, CalculateFieldProjection(fieldName));
        }

        IEnumerable<string> getLookupFieldNames()
        {
            yield return _objectType.LookupFields?.Key ?? Model.IdFieldName;
            yield return _objectType.LookupFields?.Name ?? nameof(Model.Name);
            yield return _objectType.LookupFields?.Description;
            yield return _objectType.LookupFields?.ImageUrl;

            yield return _dataView.DataView.KeyField;
            yield return _request.LookupField;
        }
    }

    /// <summary>
    /// Parse Requested fields (assumes _introspector)
    /// </summary>
    private void ParseRequestedFields()
    {
        RequestedFields = new Dictionary<string, ObjectTypeIntrospector.ReadableField>();
        ProjectionTree = new Leaf();

        foreach (var fieldName in _request.Fields)
        {
            if (_introspector.ReadableFieldsRecursively.TryGetValue(fieldName, out var field))
            {
                RequestedFields[fieldName] = field;
            }
            else
            {
                // TODO: field not readable, log
            }
        }

        // indirectly required fields (to be added to the projection) 
        // used in calculations 
        // var requiredFields = new Dictionary<string, ObjectTypeIntrospector.ReadableField>(RequestedFields);
        var neededFields = GetNeededFieldsUsingIntrospector(true);
        foreach (var fieldName in neededFields)
        {
            if (!_introspector.ReadableFieldsRecursively.TryGetValue(fieldName, out var field)) continue;

            RequestedFields.TryAdd(fieldName, field);
        }

        // check parents 
        // either implicitly include parent 
        var inspectFields = RequestedFields;
        do
        {
            var newInspectFields = new Dictionary<string, ObjectTypeIntrospector.ReadableField>();

            foreach (var kvp in inspectFields)
            {
                if (kvp.Value.Parent == null) continue;

                if (kvp.Value.TryGetRecursively(ObjectTypeIntrospector.FieldVisibility.RelationalField, out var relationalField))
                {
                    // is part of a lookup
                    if (!RequestedFields.ContainsKey(relationalField.AbsolutePath) && !newInspectFields.ContainsKey(relationalField.AbsolutePath))
                    {
                        if (!_introspector.ReadableFieldsRecursively.TryGetValue(relationalField.AbsolutePath, out var parentField))
                        {
                            throw new BadRequestException($"Can't read Parent ${relationalField.AbsolutePath}");
                        }

                        newInspectFields.TryAdd(relationalField.AbsolutePath, parentField);
                    }

                    continue;
                }

                var parent = kvp.Value.Parent;
                if (!RequestedFields.ContainsKey(parent.AbsolutePath) && !newInspectFields.ContainsKey(parent.AbsolutePath))
                {
                    // parent not found
                    if (!_introspector.ReadableFieldsRecursively.TryGetValue(parent.AbsolutePath, out var parentField))
                    {
                        throw new BadRequestException($"Can't read Parent ${parent.AbsolutePath}");
                    }

                    // parent may have already been added to the list because of another sibling field 
                    newInspectFields.TryAdd(parent.AbsolutePath, parentField);
                }

                parent = parent.Parent;
                if (parent != null)
                {
                    // add parent, if any, so it will be tested as well recursively
                    if (!_introspector.ReadableFieldsRecursively.TryGetValue(parent.AbsolutePath, out var parentField))
                    {
                        throw new BadRequestException($"Can't read Parent ${parent.AbsolutePath}");
                    }

                    // parent may have already been added to the list because of another sibling field 
                    newInspectFields.TryAdd(parent.AbsolutePath, parentField);
                }
            }

            inspectFields = newInspectFields;
            if (inspectFields.IsEmpty()) break;
            foreach (var kvp in inspectFields)
            {
                // requiredFields.Add(kvp.Key, kvp.Value);
                RequestedFields.TryAdd(kvp.Key, kvp.Value);
            }
        } while (!inspectFields.IsEmpty());

        // all parents
        var allParents = RequestedFields.Values
            .SelectMany(x => x.GetParents())
            .ToHashSet();

        // check relational/children objects included without any children
        var requiredFields = new Dictionary<string, ObjectTypeIntrospector.ReadableField>(RequestedFields);
        inspectFields = requiredFields;
        do
        {
            var newInspectFields = new Dictionary<string, ObjectTypeIntrospector.ReadableField>();

            foreach (var kvp in inspectFields)
            {
                if (kvp.Value.Visibility switch
                    {
                        ObjectTypeIntrospector.FieldVisibility.RelationalField => false,
                        ObjectTypeIntrospector.FieldVisibility.ObjectField => false,
                        ObjectTypeIntrospector.FieldVisibility.EmbeddedInObjectField => false,
                        ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => false,
                        _ => true,
                    }) continue;

                if (allParents.Contains(kvp.Value)) continue;

                // add all readable children
                var children = _introspector.ReadableFieldsRecursively
                    .Where(x => x.Value.Parent == kvp.Value)
                    .Select(x => x);

                foreach (var child in children)
                {
                    newInspectFields.TryAdd(child.Key, child.Value);
                }
            }

            inspectFields = newInspectFields;
            if (inspectFields.IsEmpty()) break;
            foreach (var kvp in inspectFields)
            {
                requiredFields.TryAdd(kvp.Key, kvp.Value);
                // RequestedFields.Add(kvp.Key, kvp.Value);
            }
        } while (!inspectFields.IsEmpty());

        // build Tree
        var forest = new Dictionary<string, Leaf>();
        foreach (var field in requiredFields)
        {
            var leaf = new Leaf
            {
                Field = field.Value,
                ProjectOut = true,
            };

            if (!forest.TryAdd(leaf.Field.AbsolutePath, leaf)) continue;

            foreach (var parent in field.Value.GetParents())
            {
                if (forest.TryGetValue(parent.AbsolutePath, out var parentLeaf))
                {
                    parentLeaf.Children.Add(leaf.Field.AbsolutePath, leaf);
                    leaf = null;
                    break;
                }

                leaf = new Leaf
                {
                    Field = parent,
                    Children = new Dictionary<string, Leaf>
                    {
                        { leaf.Field.AbsolutePath, leaf }
                    },
                    ProjectOut = RequestedFields.ContainsKey(parent.AbsolutePath),
                };

                forest.Add(leaf.Field.AbsolutePath, leaf);
            }

            if (leaf != null)
            {
                ProjectionTree.Children.Add(leaf.Field.AbsolutePath, leaf);
            }
        }
    }

    /// <summary>
    /// Get all readable fields (assumes _introspector)
    /// </summary>
    private IEnumerable<FormField> GetAllAvailableFields()
    {
        var fields = _introspector.ReadableFieldsRecursively
            // .Where(x =>
            // {
            //     var parts = x.Key.Split('|');
            //     if (parts.Length == 1)
            //     {
            //         // top level
            //         return true;
            //     }
            //
            //     if (RequestedFields.TryGetValue(string.Join('|', parts[..^1]), out _))
            //     {
            //         // parent included in the request 
            //         return true;
            //     }
            //
            //     if (!_introspector.ReadableFieldsRecursively.TryGetValue(x.Key, out var readableField))
            //     {
            //         // not readable?
            //         return false;
            //     }
            //
            //     // parent not visible
            //     var shouldShow = readableField.Visibility switch
            //     {
            //         ObjectTypeIntrospector.FieldVisibility.Normal => true,
            //         ObjectTypeIntrospector.FieldVisibility.RelationalField => false,
            //         ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => false,
            //         _ => true,
            //     };
            //
            //     return shouldShow;
            // })
            .Select(x =>
            {
                var field = x.Value.FieldTemplate.Field.Copy();
                var label = field.Label ?? field.Name;
                field.Name = x.Key;
                field.Label = label;

                // field.Label = x.Value.Visibility switch
                // {
                //     // ObjectTypeIntrospector.FieldVisibility.ObjectField or ObjectTypeIntrospector.FieldVisibility.FilteredObjectField => $"{field.Label ?? field.Name} (Embedded)",
                //     // ObjectTypeIntrospector.FieldVisibility.RelationalField => $"{field.Label ?? field.Name} (Related)",
                //     ObjectTypeIntrospector.FieldVisibility.EmbeddedInObjectField => $"{x.Value.ParentLabel}: {label}",
                //     ObjectTypeIntrospector.FieldVisibility.EmbeddedInRelationField => $"{x.Value.ParentLabel}: {label}",
                //     _ => field.Label,
                // };

                return field;
            });

        return fields;
    }

    /// <summary>
    /// filter request fields that are visible
    /// </summary>
    protected override void BuildDataViewFields()
    {
        if (_introspector == null)
        {
            ObsoleteBuildDataViewFields();
            return;
        }

        _dataView.DataView.Fields = filterUsingInspector().ToArray();

        return;

        IEnumerable<FormField> filterUsingInspector()
        {
            foreach (var readableField in RequestedFields.Values)
            {
                switch (readableField.FieldTemplate.Field)
                {
                    case HiddenField:
                        continue;
                }

                // if (RequestedFields.TryGetValue(fieldName.Key, out var visibility) && visibility == FieldVisibility.EmbeddedInObjectField)
                // {
                //     // embedded in parent ObjectField, do not create field for it
                //     continue;
                // }

                var field = readableField.FieldTemplate.Field.Copy();
                field.Name = readableField.AbsolutePath;
                field.Label = readableField.AbsoluteLabel;
                field.Description = readableField.Description;

                yield return field;
            }
        }
    }

    [Obsolete("get rid of it")]
    private void ObsoleteBuildDataViewFields()
    {
        // adjust fields based on the request
        _dataView.DataView.Fields = filterUsingObjectType().ToArray();

        IEnumerable<FormField> filterUsingObjectType()
        {
            foreach (var fieldName in _request.Fields)
            {
                if (!_objectType.Fields.TryGetValue(fieldName, out var fieldConfig)) continue;
                switch (fieldConfig.Field)
                {
                    case HiddenField:
                        continue;
                }

                if (!fieldConfig.RBAC.CanRead(_context)) continue;

                yield return fieldConfig.Field;
            }
        }
    }

    protected override DataViewResponse PrepareResponse(List<ExpandoObject> result)
    {
        var response = base.PrepareResponse(result);

        response.ObjectType = _objectType.FullName;

        // if (RequestedFields.Count > 0)
        // {
        //     // add embedded fields back to list of requested fields
        //     _request.Fields = RequestedFields.Where(x=>x.Value == FieldVisibility.EmbeddedInObjectField).Select(x=>x.Key)
        //         .Concat(_dataView.DataView.Fields.Select(x => x.Name))
        //         .Distinct()
        //         .ToArray();
        // }

        if (IncludeHiddenFields)
        {
            AddHiddenFields(response);
        }

        return response;
    }

    private void ObsoleteAddVisibleFields(DataViewResponse response)
    {
        // var visibleFields = RequestedFields
        //     .Select(x => x.Key)
        //     .ToHashSet();

        var visibleFields = response.View.Fields.Select(x => x.Name).ToHashSet();

        var readableFields = _objectType.Fields
                .Where(x => x.Value.RBAC.CanRead(_context))
                .Select(x => x.Value.Field)
            ;

        var hiddenFields = readableFields
            .Where(x => x switch
            {
                HiddenField => false,
                LabelField => false,
                CalculatedField => false,

                _ => true,
            })
            .Where(x => !visibleFields.Contains(x.Name))
            .ToArray();

        if (hiddenFields.IsEmpty()) return;

        hiddenFields = hiddenFields
            .Select(x =>
            {
                x.Visible = (x.Visible ?? Enumerable.Empty<string>())
                    .Append("false")
                    .ToArray();
                return x;
            })
            .OrderBy(x => x.Name)
            .ToArray();

        response.View.Fields = response.View.Fields
            .Concat(hiddenFields)
            .ToArray();
    }

    /// <summary>
    /// Add fields that can be added to the grid
    /// </summary>
    private void AddHiddenFields(DataViewResponse response)
    {
        if (_introspector == null)
        {
            ObsoleteAddVisibleFields(response);
            return;
        }

        // var visibleFields = RequestedFields
        //     .Select(x => x.Key)
        //     .ToHashSet();

        var visibleFields = response.View.Fields.Select(x => x.Name).ToHashSet();

        var readableFields = GetAllAvailableFields();

        var hiddenFields = readableFields
            .Where(x => x switch
            {
                HiddenField => false,
                LabelField => false,
                _ => true,
            })
            .Where(x => !visibleFields.Contains(x.Name))
            .ToArray();

        if (hiddenFields.Length > 0)
        {
            hiddenFields = hiddenFields
                .Select(x =>
                {
                    x.Visible = (x.Visible ?? Enumerable.Empty<string>())
                        .Append("false")
                        .ToArray();

                    if (_introspector.ReadableFieldsRecursively.TryGetValue(x.Name, out var readableField))
                    {
                        x.Label = readableField.AbsoluteLabel;
                        x.Description = readableField.Description;
                    }

                    return x;
                })
                .OrderBy(x => x.Name)
                .ToArray();

            response.View.Fields = response.View.Fields
                .Concat(hiddenFields)
                .ToArray();
        }
    }

    protected override IEnumerable<string> GetNeededFields(bool forCalculation = false, bool requiredOutput = false)
    {
        if (_introspector == null)
        {
            return GetNeededFieldsObsolete(forCalculation, requiredOutput);
        }

        return GetNeededFieldsUsingIntrospector(forCalculation, requiredOutput);
    }

    private IEnumerable<string> GetNeededFieldsUsingIntrospector(bool forCalculation = false, bool requiredOutput = false)
    {
        var requested = _request.Fields.ToHashSet();
        var queue = _request.Fields.ToList();

        // of the requested fields, see if they need others
        for (var c = 0; c < queue.Count; c++)
        {
            var fieldName = queue[c];

            if (!_introspector.ReadableFieldsRecursively.TryGetValue(fieldName, out var readableField)) continue;

            var field = readableField.FieldTemplate;
            foreach (var dependency in field.Field.GetDependencies(forCalculation, requiredOutput))
            {
                if (requested.Add(dependency))
                {
                    yield return dependency;
                    queue.Add(dependency);
                }
            }
        }

        // // include any relational fields for "Required Constraint" relations
        // if (requiredOutput)
        // {
        //     var requiredRelations = (_introspector.ObjectType.RelatedObjectTypes ?? Enumerable.Empty<RelatedObjectType>())
        //             .Where(x => x.RBAC.Can(_context, RelatedObjectTypePermission.Constraint))
        //         ;
        //
        //     foreach (var rel in requiredRelations)
        //     {
        //         foreach (var field in rel.Criteria.Conditions)
        //         {
        //             var projection = $"{rel.Name}|{field.FieldName}";
        //             if (requested.Add(projection)) yield return projection;
        //         }
        //     }
        // }
    }

    /// <summary>
    /// Calculate the needed fields without using the introspector 
    /// </summary>
    [Obsolete("get rid of it")]
    private IEnumerable<string> GetNeededFieldsObsolete(bool forCalculation = false, bool requiredOutput = false)
    {
        var requested = _request.Fields.ToHashSet();
        var queue = _request.Fields.ToList();
        for (var c = 0; c < queue.Count; c++)
        {
            var fieldName = queue[c];

            if (!_objectType.Fields.TryGetValue(fieldName, out var field)) continue;
            foreach (var dependency in field.Field.GetDependencies(forCalculation, requiredOutput))
            {
                if (requested.Add(dependency))
                {
                    yield return dependency;
                    queue.Add(dependency);
                }
            }
        }
    }

    private class Leaf
    {
        public ObjectTypeIntrospector.ReadableField Field { get; set; }
        public Dictionary<string, Leaf> Children { get; set; } = new();
        public bool ProjectOut { get; set; }

        public Leaf Find(string fieldName)
        {
            if (Field?.AbsolutePath == fieldName) return this;
            foreach (var child in Children.Values)
            {
                var found = child.Find(fieldName);
                if (found != null) return found;
            }

            return null;
        }
    }
}