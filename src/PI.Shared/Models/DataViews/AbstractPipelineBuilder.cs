using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using Crochik.Dipper;
using Crochik.Mongo;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

public abstract class AbstractPipelineBuilder
{
    protected readonly ObjectType _objectType;
    protected readonly IDataView _dataView;
    protected readonly IEntityContext _context;
    protected readonly MongoConnection _connection;
    protected readonly DataViewRequest _request;

    public bool UseApiNames { get; set; }
    public bool AutoGenerateReferenceFieldNames { get; set; } = true;

    public Criteria MatchCriteria { get; private set; }

    /// <summary>
    /// Whether the match is using an index search (e.g. Atlas search)
    /// </summary>
    public string SearchMetaDataField { get; private set; }

    private Criteria AppDataViewCriteria => (_dataView as AppDataView)?.Criteria;
    protected Form.Models.Form FilterForm => _dataView.DataView?.FilterForm;
    protected virtual AggregateStoredProcedure StoredProcedure => _dataView.StoredProcedure;
    protected string Collection => StoredProcedure?.Collection ?? _objectType?.CollectionName;
    protected string DatabaseName => StoredProcedure?.DatabaseName ?? _objectType?.DatabaseName;
    private Parameter[] Parameters => StoredProcedure?.Parameters ?? Array.Empty<Parameter>();

    /// <summary>
    /// Field to be used when doing a "fulltextsearch" without a index or when trying to autocomplete
    /// </summary>
    protected virtual string SearchFieldName => nameof(EntityOwnedModel.Name);

    protected static object CalculateFieldProjection(string fieldName)
    {
        var parts = fieldName.Split('|');
        if (parts.Length == 1)
        {
            // normal field projection 1:1
            return "$" + fieldName;
        }

        if (parts.Any(IsNumber))
        {
            // element of array(s)
            return GetArrayElementProjection(parts);
        }

        // no arrays
        // return new KeyValuePair<string, object>(fieldName, "$" + FormField.GetPathInCollection(fieldName));
        return "$" + string.Join(".", parts);
    }

    private static bool IsNumber(string str)
    {
        return str != null && str.All(x => x is >= '0' and <= '9');
    }

    protected static string LookupPath(FormField field) => field.Name.Replace("|", string.Empty).ToLower();

    protected AbstractPipelineBuilder(MongoConnection connection, IEntityContext context, DataViewRequest request, IDataView dataView, ObjectType objectType)
    {
        _connection = connection;
        _context = context;
        _request = request;
        _dataView = dataView;
        _objectType = objectType;
    }

    protected IDictionary<string, object> InferValuesForStoredProcedureParameters()
    {
        var args = default(IDictionary<string, object>);
        if (Parameters.Length < 1)
        {
            return args;
        }

        var entityId = _context.Role switch
        {
            EntityRoleId.Account => _context.AccountId.Value.AsSerializedId(),
            EntityRoleId.Admin => _context.AccountId.Value.AsSerializedId(),
            EntityRoleId.Manager => _context.OrganizationId.Value.AsSerializedId(),
            EntityRoleId.User => _context.UserId.Value.AsSerializedId(),
            _ => throw new ForbiddenException(_context)
        };

        args = _request?.Criteria?
                   .Where(x => x.Operator == Operator.Eq)
                   .ToDictionary(x => x.FieldName, x => AutoConvertValue(x, _objectType)) ??
               new Dictionary<string, object>();

        // default values from context
        if (_context.Role != EntityRoleId.Root)
        {
            // always override
            args[nameof(IEntityContext.AccountId)] = _context.AccountId.Value.AsSerializedId();
        }
        else if (_context.AccountId.HasValue)
        {
            // add if missing
            args.TryAdd(nameof(IEntityContext.AccountId), _context.AccountId.Value.AsSerializedId());
        }

        // other default values from context
        if (_context.UserId.HasValue) args.TryAdd(nameof(IEntityContext.UserId), _context.UserId.Value.AsSerializedId());
        if (_context.OrganizationId.HasValue) args.TryAdd(nameof(IEntityContext.OrganizationId), _context.OrganizationId.Value.AsSerializedId());

        // entity id
        switch (_context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Root:
                // add if missing
                args.TryAdd(nameof(IEntityContext.EntityId), entityId);
                break;

            default:
                // always overwrite
                args[nameof(IEntityContext.EntityId)] = entityId;
                break;
        }

        return args;
    }

    /// <summary>
    /// Always on constraints based on the context
    /// If they (e.g. AccountId, EntityId) are parameters in the stored procedure they will be handled elsewhere and not applied here
    /// </summary>
    public virtual void ApplyDefaultConstraints(Query<ExpandoObject> query, Dictionary<string, Parameter> parameters)
    {
        if (_objectType != null)
        {
            ApplyDefaultConstraints(_objectType, _context, query, parameters);
        }
        else
        {
            ApplyContextConstraints(_context, query, parameters);
        }
    }

    /// <summary>
    /// Indexed Fields (e.g. that can be used in the filter)
    /// </summary>
    /// <returns></returns>
    protected virtual Dictionary<string, FormField> GetIndexedFields() =>
        _objectType?.Fields.Values.Where(x => x.Indexed).ToDictionary(x => x.Field.Name, x => x.Field) ??
        FilterForm?.Fields?.ToDictionary(x => x.Name);

    /// <summary>
    /// Concatenate request criteria with view criteria
    /// </summary>
    public void CalculateMatchCriteria()
    {
        var criteria = new List<Condition>((_request.Criteria ?? Enumerable.Empty<Condition>()).Select(x => x.Copy()));

        // for now limit to when we know the object type and the field is found in it
        // add defaults from filter form 
        // the client should have already added them but... 
        var withDefault = FilterForm?.Fields?
            .Where(x => x.DefaultValue != null)
            .Where(x => _objectType.Fields != null && _objectType.Fields.ContainsKey(x.Name))
            .ToDictionary(x => x.Name);
        if (withDefault?.Count > 0)
        {
            var alreadySet = _request?.Criteria?.Select(x => x.FieldName).Distinct().ToHashSet() ?? new HashSet<string>();
            foreach (var field in withDefault)
            {
                if (!alreadySet.Add(field.Key)) continue;

                criteria.Add(Condition.Eq(field.Value.Name, field.Value.DefaultValue));
            }
        }

        if (AppDataViewCriteria?.Conditions.Length > 0)
        {
            // add criteria from saved view
            //      do not override any filters sent by client FOR NOW
            //      so one can start with a saved view and change the filter 
            var existing = criteria.Select(x => x.FieldName).ToHashSet();
            criteria.AddRange(AppDataViewCriteria.Conditions.Where(x => !existing.Contains(x.FieldName)));
        }

        MatchCriteria = new Criteria
        {
            Conditions = criteria.ToArray(),
        };

        MatchCriteria.Conditions.ReplaceValuePlaceHolders(_context);
    }

    /// <summary>
    /// If is a Geo near filter, return condition 
    /// </summary>
    /// <returns></returns>
    private (Condition Location, Condition Distance) GetGeoNearCondition()
    {
        var fields = GetIndexedFields();
        if (fields == null) return (null, null);

        var locationCondition = MatchCriteria.Conditions?.FirstOrDefault(x => fields.TryGetValue(x.FieldName, out var field) && field is LocationField);
        var distanceCondition = MatchCriteria.Conditions?.FirstOrDefault(x => fields.TryGetValue(x.FieldName, out var field) && field is LocationDistanceField);

        return (locationCondition, distanceCondition);
    }

    /// <summary>
    /// Build match stage(s)
    /// </summary>
    public IEnumerable<BsonDocument> BuildMatch()
    {
        SearchMetaDataField = null;
        CalculateMatchCriteria();

        var parameters = Parameters.ToDictionary(x => x.Name);

        var fields = GetIndexedFields();

        if (MatchCriteria.IsSearch())
        {
            if (_objectType.Indices?.SearchIndex != null)
            {
                // build sort stage instead of match 
                if (_objectType?.Constraints == null)
                {
                    throw new BadRequestException("Objects must have constraints");
                }

                var defaultConstraints = _objectType
                    .GetConditions(_context)
                    .Select(x => new Condition
                    {
                        FieldName = x.FieldName,
                        Operator = x.Operator,
                        Value = x.ResolveValue(_context),
                    });

                var matchConditions = MatchCriteria.Conditions
                    .Select(x => PreProcessCondition(x, parameters, fields))
                    .Where(x => x != null)
                    .ToArray();

                var conditions = defaultConstraints.Concat(matchConditions);
                var builder = new SearchStageBuilder
                {
                    Index = _objectType.Indices.SearchIndex,
                    Conditions = conditions.ToArray(),
                };

                var search = builder.Build();

                yield return new BsonDocument("$search", search);

                if (!builder.UnappliedConditions.IsEmpty())
                {
                    // add match
                    var matchQuery = _connection.Filter<ExpandoObject>(Collection);
                    ApplyConditionsToMatchQuery(builder.UnappliedConditions, matchQuery, parameters, fields);
                    var matchFilter = matchQuery.GetFilterAsBsonDocument();
                    yield return new BsonDocument("$match", matchFilter);
                }

                // project meta
                var metaDataField = _objectType.Fields.Values.FirstOrDefault(x => x.Field is ObjectField objectField && objectField.ObjectFieldOptions.ObjectType == "SearchMetaData");
                if (metaDataField != null)
                {
                    SearchMetaDataField = metaDataField.Field.Name;

                    yield return new BsonDocument("$set", new BsonDocument
                    {
                        {
                            SearchMetaDataField, new BsonDocument
                            {
                                { "Score", new BsonDocument("$meta", "searchScore") },
                                { "Highlights", new BsonDocument("$meta", "searchHighlights") },
                                // {
                                //     "Highlights", new BsonDocument("$arrayToObject", new BsonDocument("$map", new BsonDocument
                                //     {
                                //         { "input", new BsonDocument("$meta", "searchHighlights") },
                                //         {
                                //             "in", new BsonDocument
                                //             {
                                //                 { "k", "$$this.path" },
                                //                 { "v", new BsonDocument("Texts","$$this.texts") }
                                //             }
                                //         }
                                //     }))
                                // }
                            }
                        }
                    });
                }

                yield break;
            }
        }

        // apply conditions to filter
        var query = _connection.Filter<ExpandoObject>(Collection);
        ApplyDefaultConstraints(query, parameters);
        if (MatchCriteria?.Conditions != null)
        {
            ApplyConditionsToMatchQuery(MatchCriteria.Conditions, query, parameters, fields);
        }

        var filter = query.GetFilterAsBsonDocument();

        // special handling for $geoMatch
        var (locationCondition, distanceCondition) = GetGeoNearCondition();
        if (locationCondition != null)
        {
            yield return geoNearFilter();
            yield break;
        }

        // normal match
        if (filter == null) yield break;

        yield return new BsonDocument("$match", filter);

        BsonDocument geoNearFilter()
        {
            var value = fields[locationCondition.FieldName].AutoConvert(locationCondition.Value);
            // if (value is not LocationDistanceFilterValue distanceFilter) throw new BadRequestException("Invalid condition");

            if (value is not decimal[] coordinates || coordinates.Length != 2) throw new BadRequestException("Invalid condition");
            var longitude = coordinates[0];
            var latitude = coordinates[1];

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

            if (distanceCondition != null)
            {
                geoNear["distanceField"] = distanceCondition.FieldName;

                var maxDistanceValue = fields[distanceCondition.FieldName].AutoConvert(distanceCondition.Value);
                if (maxDistanceValue is decimal maxDistance)
                {
                    geoNear["maxDistance"] = maxDistance * 1000; // assume max is in km for now 
                }
            }
            else
            {
                var locationDistanceField = GetIndexedFields().Values.OfType<LocationDistanceField>().FirstOrDefault(x => x.LocationDistanceFieldOptions?.LocationFieldName == locationCondition.FieldName);
                if (locationDistanceField != null)
                {
                    geoNear["distanceField"] = locationDistanceField.Name;
                }
                else
                {
                    geoNear["distanceField"] = $"{locationCondition.FieldName}|LocationDistance";
                }
            }

            if (filter != null)
            {
                geoNear["query"] = filter;
            }

            return new BsonDocument(new Dictionary<string, object>
            {
                { "$geoNear", geoNear }
            });
        }
    }

    /// <summary>
    /// Get Projection for an object in an array
    /// </summary>
    // TODO: make recursive so it can handle accessing item in array of an item in array.... 
    // ...
    public static object GetArrayElementProjection(string[] parts)
    {
        var indexPos = -1;
        var index = default(int);
        for (var c = 0; c < parts.Length; c++)
        {
            if (int.TryParse(parts[c], out index))
            {
                indexPos = c;
                break;
            }
        }

        if (indexPos < 0) throw new Exception("Invalid field name");
        var arrayPath = string.Join(".", parts[..indexPos]);
        var item = new BsonDocument("$arrayElemAt", new BsonArray { "$" + arrayPath, index });
        if (index == parts.Length - 1) return item;

        var propPath = string.Join(".", parts[(indexPos + 1)..]);
        return new BsonDocument("$getField", new BsonDocument
        {
            { "input", item },
            { "field", propPath }
        });
    }

    protected IEnumerable<KeyValuePair<string, object>> GetProjectedFields(IEnumerable<string> fieldNames, ReferenceField[] referenceFields, Dictionary<string, ObjectType> referencedObjects, Projection projection = Projection.Fields)
    {
        if (!AutoGenerateReferenceFieldNames)
        {
            if (projection == Projection.Fields)
            {
                foreach (var field in fieldNames)
                {
                    yield return new KeyValuePair<string, object>(field, CalculateFieldProjection(field));
                }
            }

            yield break;
        }

        // automatically generate ReferenceField|Name fields
        var visible = fieldNames.ToHashSet();

        var lookupFields = referenceFields
            .Where(x => visible.Contains(x.Name))
            .ToDictionary(x => $"{x.Name}|Name");

        if (projection == Projection.Fields)
        {
            // add all fields (excludes conflicting with auto generated by this method)
            foreach (var field in visible.Except(lookupFields.Keys))
            {
                yield return new KeyValuePair<string, object>(field, CalculateFieldProjection(field));
            }
        }

        if (lookupFields.IsEmpty()) yield break;

        foreach (var kvp in lookupFields)
        {
            var path = LookupPath(kvp.Value);
            var fieldName = "Name";
            if (referencedObjects != null && kvp.Value.ReferenceFieldOptions?.ObjectType != null && referencedObjects.TryGetValue(kvp.Value.ReferenceFieldOptions.ObjectType, out var objectType) && objectType.LookupFields?.Name != null)
            {
                fieldName = objectType.LookupFields.Name.Replace('|', '.');
            }

            yield return new KeyValuePair<string, object>(kvp.Key, $"${path}.{fieldName}"); // TODO: use the actual field name (instead of assuming "Name")
        }
    }

    /// <summary>
    /// Convert criteria in the request into filter constraints 
    /// </summary>
    public virtual void ApplyConditionsToMatchQuery(Condition[] criteria, Query<ExpandoObject> query, Dictionary<string, Parameter> parameters, Dictionary<string, FormField> fields)
    {
        var criteriaByField = criteria.GroupBy(x => x.FieldName);
        foreach (var group in criteriaByField)
        {
            var conditions = group.ToArray();
            if (conditions.Length == 1)
            {
                // single criteria for field
                ApplyConditionToQuery(query, parameters, fields, conditions[0]);
                continue;
            }

            query.AndBuilder(conditions.Select(calculate));
        }

        Action<Query<ExpandoObject>> calculate(Condition c)
        {
            void action(Query<ExpandoObject> q)
            {
                ApplyConditionToQuery(q, parameters, fields, c);
            }

            return action;
        }
    }

    /// <summary>
    /// Tweak input to make search better 
    /// </summary>
    private string HackFullTextSearchInput(object value)
    {
        if (value == null) return null;

        // TODO: should it check some property or, at least, if the object has a phone number field
        // ... 
        var str = value.ToString();
        if (long.TryParse(str, out var _))
        {
            // value is numeric
            str = str.Length switch
            {
                7 => $"\"{str[..3]}-{str[3..]}\"",
                10 => $"\"{str[..3]}-{str[3..6]}-{str[6..]}\"",
                11 => str[0] == '1' ? $"\"+{str[0]} {str[1..4]}-{str[4..7]}-{str[7..]}\"" : str,
                _ => str,
            };
        }
        else if (str?.IndexOf('"') > 0)
        {
            // assumes exact match
            str = $"\"{str}\"";
        }

        return str;
    }

    /// <summary>
    ///  Prepare condition to be applied to query
    /// </summary>
    public Condition PreProcessCondition(Condition condition, Dictionary<string, Parameter> parameters, Dictionary<string, FormField> fields)
    {
        // special conditions
        // don't do anything with the value
        if (condition.FieldName switch
            {
                Condition.FullTextSearch => true,
                Condition.AutoComplete => true,
                _ => false,
            }) return condition;

        if (condition.FieldName == Condition.LookupId)
        {
            // lookup
            var actualFieldName = _request.LookupField ?? Model.IdFieldName;
            return new Condition
            {
                FieldName = actualFieldName,
                Operator = condition.Operator,
                Value = condition.GetSerializableValue(_objectType, actualFieldName)
            };
        }

        if (parameters.ContainsKey(condition.FieldName))
        {
            // criteria is a parameter, will be handled later by the caller
            return null;
        }

        var value = condition.GetSerializableValue(_objectType);

        if (_objectType == null || !_objectType.Fields.TryGetValue(condition.FieldName, out var objectField))
        {
            objectField = null;
        }

        var field = objectField?.Field;
        if (field == null && fields != null && !fields.TryGetValue(condition.FieldName, out field))
        {
            // field is not filterable, ignore?
            // ...
            throw new BadRequestException($"{condition.FieldName} is not filterable");
        }

        // special field types
        // check if the field (type) can be used direct in the filter 
        switch (field)
        {
            case LocationField:
            case LocationDistanceField:
                // these are applied independently in the $geoNear
                return null;
        }

        // special handling for some operators 
        return condition.Operator switch
        {
            Operator.Eq => eqCondition(),
            Operator.Ne => eqCondition(false),
            Operator.In => inCondition(),
            Operator.Nin => inCondition(false),

            _ => new Condition
            {
                FieldName = condition.FieldName,
                Operator = condition.Operator,
                Value = value,
            }
        };

        Condition inCondition(bool positive = true)
        {
            if (field == null) throw new BadRequestException("Unexpected In operator");
            if (value is not IEnumerable<object> objArray)
            {
                throw new BadRequestException($"{condition.FieldName} couldn't parse value");
            }

            var backingType = field.GetBackingType();
            if (backingType.IsArray)
            {
                // array fields will be any in (and none of in)
                return new Condition
                {
                    FieldName = condition.FieldName,
                    Operator = positive ? Operator.ArrayAnyIn : Operator.Nin,
                    Value = objArray,
                };
            }

            return new Condition
            {
                FieldName = condition.FieldName,
                Operator = positive ? Operator.In : Operator.Nin,
                Value = objArray,
            };
        }

        Condition eqCondition(bool positive = true)
        {
            if (field != null)
            {
                // if (value is IEnumerable<string> array)
                // {
                //     // TODO: this should be based on the type of field (maybe even move into the autoconvert)
                //     // ...
                //     var enumerable = array.Select(x => Guid.TryParse(x, out var uid) ? uid.AsSerializedId() : x);
                //
                //     var backingType = field.GetBackingType();
                //     if (backingType.IsArray)
                //     {
                //         // special handling for Eq operator when the value is an array
                //         // for array fields must include all values
                //         return new Condition
                //         {
                //             FieldName = condition.FieldName,
                //             Operator = positive ? Operator.ArrayAll : Operator.ArrayNotAll,
                //             Value = enumerable,
                //         };
                //     }
                //
                //     return new Condition
                //     {
                //         FieldName = condition.FieldName,
                //         Operator = positive ? Operator.In : Operator.Nin,
                //         Value = enumerable,
                //     };
                // }

                var backingType = field.GetBackingType();
                if (backingType.IsArray)
                {
                    // special handling for Eq operator when the value is an array
                    // for array fields must include all values
                    if (value is not IEnumerable<object> objArray)
                    {
                        throw new BadRequestException($"{condition.FieldName} couldn't parse value");
                    }

                    return new Condition
                    {
                        FieldName = condition.FieldName,
                        Operator = positive ? Operator.ArrayAll : Operator.ArrayNotAll,
                        Value = objArray,
                    };
                }
            }

            return new Condition
            {
                FieldName = condition.FieldName,
                Operator = positive ? Operator.Eq : Operator.Ne,
                Value = value
            };
        }
    }
    
    private void ApplyConditionToQuery(Query<ExpandoObject> query, Dictionary<string, Parameter> parameters, Dictionary<string, FormField> fields, Condition condition)
    {
        var prepared = PreProcessCondition(condition, parameters, fields);
        if (prepared == null)
        {
            return;
        }

        // special fields
        switch (prepared.FieldName)
        {
            case Condition.FullTextSearch:
            {
                // TODO: some extra setting to determine whether there is a full text index search or not? 
                if (_dataView.DataView?.Searchable ?? false)
                {
                    // supports text search
                    query.Text(HackFullTextSearchInput(prepared.Value));

                    // TODO: automatically add sort or extra field so it can be sorted later?
                    // { score: { $meta: "textScore" } }
                    // ...
                }
                else
                {
                    var str = prepared.Value?.ToString();
                    if (str != null) query.Regex(SearchFieldName, new BsonRegularExpression($"^{Regex.Escape(str)}", "i"));
                }

                return;
            }

            case Condition.AutoComplete:
            {
                // TODO: should it check some property ?
                // ...
                var strValue = prepared.Value?.ToString()?.Trim();

                if (!string.IsNullOrEmpty(strValue))
                {
                    if ((_dataView.DataView?.Searchable ?? false) && (_objectType?.IsFullTextSearchable ?? false) && strValue.Length > 3)
                    {
                        query.Text(HackFullTextSearchInput(prepared.Value));
                    }
                    else
                    {
                        query.Regex(SearchFieldName, new BsonRegularExpression($"^{Regex.Escape(strValue)}", "i"));
                    }
                }

                return;
            }
        }

        // transform field name
        var pathInCollection = FormField.GetPathInCollection(prepared.FieldName);

        // if it is not a parameter, apply directly to the query
        switch (prepared.Operator)
        {
            case Operator.ArrayAll:
            {
                if (prepared.Value is not IEnumerable<object> array) throw new BadRequestException($"{prepared.FieldName} All: unexpected value");
                query.All(pathInCollection, array);
                break;
            }

            case Operator.ArrayNotAll:
            {
                if (prepared.Value is not IEnumerable<object> array) throw new BadRequestException($"{prepared.FieldName} All: unexpected value");
                query.NotBuilder(q => q.All(pathInCollection, array));
                break;
            }

            case Operator.ArrayAnyIn:
            {
                if (prepared.Value is not IEnumerable<object> array) throw new BadRequestException($"{prepared.FieldName} AnyIn: unexpected value");
                query.AnyIn(pathInCollection, array);
                break;
            }

            case Operator.In:
            {
                if (prepared.Value is not IEnumerable<object> array) throw new BadRequestException($"{prepared.FieldName} In: unexpected value");
                query.In(pathInCollection, array);
                break;
            }

            case Operator.Nin:
            {
                if (prepared.Value is not IEnumerable<object> array) throw new BadRequestException($"{prepared.FieldName} Nin: unexpected value");
                query.Nin(pathInCollection, array);
                break;
            }

            case Operator.Eq:
                query.Eq(pathInCollection, prepared.Value);
                break;

            case Operator.Ne:
                query.Ne(pathInCollection, prepared.Value);
                break;

            case Operator.Gt:
                query.Gt(pathInCollection, prepared.Value);
                break;

            case Operator.Gte:
                query.Gte(pathInCollection, prepared.Value);
                break;

            case Operator.Lt:
                query.Lt(pathInCollection, prepared.Value);
                break;

            case Operator.Lte:
                query.Lte(pathInCollection, prepared.Value);
                break;

            default:
                throw new BadRequestException($"{prepared.FieldName}, unexpected Operator: {prepared.Operator}");
        }
    }

    protected static void ApplyDefaultConstraints(ObjectType objectType, IEntityContext context, Query<ExpandoObject> query, Dictionary<string, Parameter> parameters)
    {
        if (objectType.Constraints == null)
        {
            // old (implicit) logic 
            if (objectType.NativeType == null && objectType.CollectionName == nameof(CustomObject))
            {
                // custom object
                query.Eq(nameof(CustomObject.ObjectType), objectType.Name);
            }

            // context constraints
            ApplyContextConstraints(context, query, parameters);
            return;
        }

        // explicit constraints
        var conditions = objectType.GetConditions(context);

        if (objectType.RelatedObjectTypes?.Length > 0)
        {
            // exclude conditions that are applied in lookups (e.g. Exists)
            // these will break everywhere else until they are properly implemented :)
            var relationNames = objectType.RelatedObjectTypes
                .Where(x => x.RBAC.CanRead(context))
                .Select(x => x.Name)
                .ToHashSet();

            conditions = conditions
                .Where(x => x.Operator != Operator.Exists || !relationNames.Contains(x.FieldName));
        }

        query.AddConditions(context, conditions);        
    }

    [Obsolete("object types should explicitly add constraints")]
    protected static void ApplyContextConstraints(IEntityContext Context, Query<ExpandoObject> query, Dictionary<string, Parameter> parameters)
    {
        switch (Context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Account:
                // DANGER: can use it to not limit to an account
                // by adding a parameter AccountId but not actually using it to filter the results 
                if (!parameters.ContainsKey(nameof(EntityOwnedModel.AccountId)))
                {
                    // if there isn't an explicitly defined AccountId parameter,
                    // implicitly limit results 
                    query.Eq(nameof(EntityOwnedModel.AccountId), Context.AccountId.Value);
                }

                break;

            // case EntityRoleId.Profile:
            //     // always limit to account
            //     query.Eq(nameof(EntityOwnedModel.AccountId), Context.AccountId.Value);
            //     break;

            default:
                // always limit to account
                query.Eq(nameof(EntityOwnedModel.AccountId), Context.AccountId.Value);

                if (!parameters.ContainsKey(nameof(EntityOwnedModel.EntityId)))
                {
                    // if there isn't an explicitly defined entityid parameter,
                    // implicitly limit results 
                    query.Eq(nameof(EntityOwnedModel.EntityId), Context.GetOwnerEntityId());
                }

                break;
        }
    }

    // TODO: REFACTOR to do all the conversion based on the field/backingType
    // ...
    private object AutoConvertValue(Condition criteria, ObjectType objectType)
    {
        // instead of relying on type should rely on backingType?
        // ... 

        var value = criteria.Value;
        if (value == null) return value;

        var actualFieldName = (criteria.FieldName == Condition.LookupId ? _request.LookupField : null) ?? criteria.FieldName;
        if (objectType == null || !objectType.Fields.TryGetValue(actualFieldName, out var field))
        {
            field = null;
        }

        if (value is string str && str.StartsWith("{{") && str.EndsWith("}}"))
        {
            value = field?.Field switch
            {
                DateTimeField => DateRangePreset.Calculate(str, TimeZoneInfo.FindSystemTimeZoneById(DateRangeField.DefaultTimeZoneId)),
                DateField => DateRangePreset.Calculate(str, TimeZoneInfo.FindSystemTimeZoneById(DateRangeField.DefaultTimeZoneId)),
                // ...
                _ => value
            };
        }

        // BIG HACK TO HANDLE ObjectIds 
        // TODO: replace this with BackingType
        var canBeObjectId = field?.Field switch
        {
            ReferenceField _ or MultiReferenceField _ => true,
            _ => criteria.FieldName == Model.IdFieldName || criteria.FieldName.EndsWith("Id") || criteria.FieldName.EndsWith("Ids") || criteria.FieldName == Condition.LookupId,
        };
        if (canBeObjectId && value is string idStr && Guid.TryParse(idStr, out var id))
        {
            // convert to ObjectId if necessary (probably will be an issue for Guid.Empty :() 
            value = id.AsSerializedId();

            return value;
        }

        if (value is JArray jArray)
        {
            // SHOULDN'T HAPPEN ANYMORE...
            if (jArray.Count < 1) return null;

            if (field != null)
            {
                var backingType = field.Field.GetBackingType();
                if (backingType.IsArray || criteria.Operator == Operator.In || criteria.Operator == Operator.Nin)
                {
                    return field.Field.AutoConvert(jArray);
                }
            }
            else if (criteria.FieldName == Condition.LookupId)
            {
                // for now assumes the id field is Guid
                // TODO: look for id field and check backingtype
                // ...
                var ids = jArray.Select(x => PropertyValueConverter.ConvertTo<Guid>(x)).ToArray();
                // return ids.Length == 1 ? ids[0] : ids;
                return ids;
            }
        }

        if (value is IEnumerable<object> list)
        {
            if (field != null)
            {
                var backingType = field.Field.GetBackingType();
                if (backingType.IsArray)
                {
                    return field.Field.AutoConvert(list);
                }

                if (criteria.Operator is Operator.In or Operator.Nin)
                {
                    // BIG HACK TO HANDLE ObjectIds 
                    // TODO: replace this with BackingType
                    if (criteria.FieldName == Model.IdFieldName || criteria.FieldName.EndsWith("Id") || criteria.FieldName.EndsWith("Ids") || criteria.FieldName == Condition.LookupId)
                    {
                        return list.Select(x => x switch
                        {
                            ObjectId oid => oid,
                            string strValue => Guid.TryParse(strValue, out var uuid) ? uuid.AsSerializedId() : strValue,
                            Guid guid => guid.AsSerializedId(),
                            _ => field.Field.AutoConvert(x),
                        }).ToArray();
                    }

                    return list.Select(field.Field.AutoConvert).ToArray();
                }
            }
            else if (criteria.FieldName == Condition.LookupId)
            {
                // BIG HACK TO HANDLE ObjectIds 
                // for now assumes the id field is Guid AND IT WILL NOT handle objectIds!!!!!!
                // TODO: look for id field and check backingtype
                // ...
                // var ids = list.Select(x => PropertyValueConverter.ConvertTo<Guid>(x)).ToArray();
                // return ids;
                return list.Select(x => x switch
                {
                    ObjectId oid => oid,
                    string strValue => Guid.TryParse(strValue, out var uuid) ? uuid.AsSerializedId() : strValue,
                    Guid guid => guid.AsSerializedId(),
                    _ => x,
                }).ToArray();
            }
        }

        if (field != null)
        {
            value = field.Field.AutoConvert(value);
        }

        return value;
    }
}