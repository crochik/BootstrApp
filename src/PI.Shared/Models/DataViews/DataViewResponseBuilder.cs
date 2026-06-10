using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

public class DataViewResponseBuilder : AbstractPipelineBuilder
{
    public static DataViewResponseBuilder New(MongoConnection connection, IEntityContext context,
        DataViewRequest request, IDataView dataView) => new(connection, context, request, dataView);

    /// <summary>
    /// Pipeline created during the build
    /// </summary>
    public IEnumerable<BsonDocument> Stages { get; private set; }

    /// <summary>
    /// ResultSet generated during the build
    /// </summary>
    public List<ExpandoObject> ResultSet { get; private set; }

    /// <summary>
    /// Projected fields (calculated in BuildPipeline)
    /// can be null
    /// </summary>
    public Dictionary<string, object> ProjectedFields { get; protected set; }

    protected Projection Projection { get; set; }
    protected FormField[] FilterableFields { get; set; }

    protected Dictionary<string, Lookup> Lookups { get; set; }

    protected override string SearchFieldName => Projection switch
    {
        Projection.TopValues => FormField.GetPathInCollection(_request.LookupField),
        // TODO: can we do something smarter here for the others than hard-coding name ?
        // ...
        _ => base.SearchFieldName,
    };

    protected DataViewResponseBuilder(MongoConnection connection, IEntityContext context, DataViewRequest request,
        IDataView dataView, ObjectType objectType = null) :
        base(connection, context, request, dataView, objectType)
    {
    }

    /// <summary>
    /// Handle all pre-processing based in the object
    /// </summary>
    protected virtual async Task InitAsync()
    {
        ValidateRequestedFields();

        CalculateFilterableFields(_dataView.DataView?.Fields?.ToDictionary(x => x.Name));

        var referenceFields = _dataView.DataView?.Fields?
            .OfType<ReferenceField>()
            .Where(x => x.ReferenceFieldOptions != null && !x.ReferenceFieldOptions.ObjectType.StartsWith("/"))
            // .Select(CloneForFilter)
            .ToArray();

        await LoadReferencedObjectsAsync(referenceFields);
    }

    /// <summary>
    /// Load referenced objects (making sure to enforce access rules)
    /// will look for objects in the same namespace or base namespace 
    /// </summary>
    protected async Task LoadReferencedObjectsAsync(ReferenceField[] referenceFields)
    {
        if (referenceFields.Length < 1)
        {
            Lookups = [];
            return;
        }

        var visible = _request.Fields.ToHashSet();
        var filtered = _objectType.Fields
            .Where(x => x.Value.Field is ReferenceField referenceField &&
                        referenceField.ReferenceFieldOptions?.ObjectType != null &&
                        !referenceField.ReferenceFieldOptions.ObjectType.StartsWith("/") &&
                        ((AutoGenerateReferenceFieldNames && visible.Contains(x.Key)) || referenceField.ReferenceFieldOptions?.JoinBehavior == JoinBehavior.Exclude)
            )
            .Select(x => (ReferenceField)x.Value.Field)
            .ToArray();

        var result = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, _context.AccountId)
            .In(x => x.FullName, filtered.Select(x => x.ReferenceFieldOptions.ObjectType))
            .SortDesc(x => x.Namespace) // so the ones with a defined namespace will have priority over the ones without a namespace
            .FindAsync();

        // get only one of each objectType
        var referencedObjects = new Dictionary<string, ObjectType>();
        foreach (var objectType in result)
        {
            if (!objectType.CanRead(_context)) continue;
            referencedObjects.TryAdd(objectType.FullName, objectType);
        }

        var lookups = new Dictionary<string, Lookup>();
        foreach (var referenceField in filtered)
        {
            if (!referencedObjects.TryGetValue(referenceField.ReferenceFieldOptions.ObjectType, out var objectType)) continue;

            var lookup = BuildLookup(referenceField, objectType, AutoGenerateReferenceFieldNames && visible.Contains(referenceField.Name));

            lookups.Add(referenceField.Name, lookup);
        }

        Lookups = lookups;
    }

    /// <summary>
    /// Calculate "other" filterable fields (e.g. in addition to ReferenceFields)
    /// </summary>
    /// <param name="indexedFields"></param>
    protected void CalculateFilterableFields(Dictionary<string, FormField> indexedFields)
    {
        if (indexedFields == null)
        {
            FilterableFields = [];
            return;
        }

        // TODO: could/should also explicit exclude any already in the ReferenceFields
        // ...
        FilterableFields = indexedFields
            .Where(x => x.Value switch
            {
                ReferenceField => true, // they are handled elsewhere
                SelectField => true,
                TagsField => true,
                DateField => true,
                DateTimeField => true,
                CheckboxField => true,
                LocationDistanceField => true,
                LocationField => true,
                _ => false,
            })
            .Select(x =>
            {
                var cloned = CloneForFilter(x.Value);
                cloned.Name = x.Key;
                return cloned;
            })
            .ToArray();
    }

    private static DateRangeFieldOptions DateRangeFieldOptions = new()
    {
        Presets =
        [
            new DateRangePreset
            {
                Name = "Last 30 days",
                Range =
                [
                    "{{today -30d}}",
                    null
                ]
            },
            new DateRangePreset
            {
                Name = "Last 7 days",
                Range =
                [
                    "{{today -7d}}",
                    null
                ]
            },
            new DateRangePreset
            {
                Name = "This month",
                Range =
                [
                    "{{month}}",
                    null
                ]
            },
            new DateRangePreset
            {
                Name = "Previous month",
                Range =
                [
                    "{{month -1M}}",
                    "{{month}}"
                ]
            }
        ]
    };

    private FormField ConvertToFilterField(FormField field)
    {
        return field switch
        {
            CheckboxField f => new CheckboxField
            {
                Name = f.Name,
                Label = f.Label,
                IsRequired = false,
                DefaultValue = f.DefaultValue,
                Enable = f.Enable,
                Visible = f.Visible,
                CheckboxFieldOptions = new CheckboxFieldOptions
                {
                    Style = CheckboxFieldOptionsStyle.Dropdown,
                }
            },
            DateTimeField f => new DateRangeField
            {
                Name = f.Name,
                Label = f.Label,
                IsRequired = false,
                DefaultValue = null,
                Enable = null,
                Options = DateRangeFieldOptions,
            },
            DateField f => new DateRangeField
            {
                Name = f.Name,
                Label = f.Label,
                IsRequired = false,
                DefaultValue = null,
                Enable = null,
                Options = DateRangeFieldOptions,
            },
            SelectField f => new MultiSelectField
            {
                Name = f.Name,
                Label = f.Label,
                IsRequired = false,
                DefaultValue = null,
                Enable = null,
                MultiSelectFieldOptions = new MultiSelectFieldOptions
                {
                    Items = toDictionary(f.SelectFieldOptions.Items),
                }
            },
            ReferenceField f => new MultiReferenceField
            {
                Name = f.Name,
                Label = f.Label,
                IsRequired = false,
                DefaultValue = null,
                Enable = null,
                MultiReferenceFieldOptions = new MultiReferenceFieldOptions
                {
                    ObjectType = f.ReferenceFieldOptions.ObjectType,
                    Criteria = f.ReferenceFieldOptions.Criteria,
                    ForeignFieldName = f.ReferenceFieldOptions.ForeignFieldName,
                    Items = toDictionary(f.ReferenceFieldOptions.Items),
                    AutoComplete = true,
                }
            },
            TextField f => _objectType != null
                ? new ReferenceField
                {
                    Name = f.Name,
                    Label = f.Label,
                    IsRequired = false,
                    DefaultValue = f.DefaultValue,
                    Enable = f.Enable,
                    Visible = f.Visible,
                    ReferenceFieldOptions = new ReferenceFieldOptions()
                    {
                        // AllowUnknown = true,
                        ObjectType = $"/api/v1/CustomObject/{_objectType.FullName}/Field({f.Name})/Top"
                    },
                }
                : f,
            // TextField f => _objectType != null
            //     ? new MultiReferenceField
            //     {
            //         Name = f.Name,
            //         Label = f.Label,
            //         IsRequired = false,
            //         DefaultValue = f.DefaultValue,
            //         Enable = f.Enable,
            //         Visible = f.Visible,
            //         MultiReferenceFieldOptions = new MultiReferenceFieldOptions()
            //         {
            //             // AllowUnknown = true,
            //             ObjectType = $"/api/v1/CustomObject/{_objectType.FullName}/Field({f.Name})/Top"
            //         },
            //     }
            //     : f,
            // TimeField
            _ => CloneForFilter(field),
        };

        IDictionary toDictionary(IDictionary original)
        {
            var result = new Dictionary<string, object>();
            if (original?.Keys != null)
            {
                foreach (var key in original.Keys)
                {
                    result.TryAdd(key.ToString(), original[key]);
                }
            }

            return result;
        }
    }

    protected static T CloneForFilter<T>(T field)
        where T : FormField
    {
        var result = field.Copy();
        result.IsRequired = false;
        result.DefaultValue = null;
        result.Enable = null;
        return result;
    }

    public DataViewResponseBuilder With(Projection projection)
    {
        Projection = projection;
        return this;
    }


    /// <summary>
    /// Build (just) pipeline
    /// </summary>
    private async Task<IEnumerable<BsonDocument>> BuildPipelineAsync()
    {
        await InitAsync();

        BuildDataViewFields();

        // TODO: why before and not as part of the prepare response?
        // ...
        BuildFilterForm();

        return IsValidRequest() ? BuildStages() : null;
    }

    /// <summary>
    /// Build (just) Result set
    /// </summary>
    public async Task<List<ExpandoObject>> BuildResultSetAsync()
    {
        Stages = await BuildPipelineAsync();
        if (Stages == null) return [];

        var rs = await AggregateAsync(Stages);
        UpdateCalculatedFields(rs);
        return rs;
    }

    public async Task<DataViewResponse> BuildAsync()
    {
        ResultSet = await BuildResultSetAsync();

        return PrepareResponse(ResultSet);
    }

    protected virtual DataViewResponse PrepareResponse(List<ExpandoObject> result)
    {
        var response = new DataViewResponse
        {
            View = _dataView.DataView,
            Options = _dataView.Options ?? DataViewOptions.Default,
            Result = result,
            Request = _request
        };

        UpdateCurrentFilterFormValues();

        // list of (visible) fields included in the response 
        _request.Fields = _dataView.DataView.Fields.Select(x => x.Name).ToArray();

        // CalculatedCriteria may be null if the request is not valid
        if (MatchCriteria != null)
        {
            _request.Criteria = MatchCriteria.Conditions;
        }

        response.Options = _dataView.CalculateDataViewOptions(_request);

        return response;
    }

    /// <summary>
    /// Update values for calculated fields
    /// IMPORTANT: DOES NOT WORK WITH API NAMES unless the calculations are defined using them :(
    /// </summary>
    private void UpdateCalculatedFields(List<ExpandoObject> result)
    {
        var calculatedFields = _dataView.DataView.Fields.OfType<CalculatedField>().ToArray();
        if (calculatedFields.Length < 1) return;

        foreach (var record in result)
        {
            var dict = (IDictionary<string, object>)record;
            foreach (var field in calculatedFields)
            {
                var value = field.CalculateValue(record);
                if (value == null)
                {
                    dict.Remove(field.Name);
                    continue;
                }

                dict[field.Name] = value;
            }
        }

        foreach (var field in calculatedFields)
        {
            var resultField = field.Field;
            resultField.Name ??= field.Name;
            resultField.Label ??= field.Label;
            resultField.Enable ??= field.Enable;
            resultField.Visible ??= field.Visible;
            resultField.Description ??= field.Description;

            for (var c = 0; c < _dataView.DataView.Fields.Length; c++)
            {
                if (_dataView.DataView.Fields[c].Name == field.Name)
                {
                    _dataView.DataView.Fields[c] = resultField;
                    break;
                }
            }
        }
    }

    protected virtual void ValidateRequestedFields()
    {
        if (_request.Fields == null || _request.Fields.Length < 1)
        {
            // not customized or saved 
            if (_dataView.DataView.Fields?.Length > 0)
            {
                // use field list from the dataview...
                _request.Fields = _dataView.DataView.Fields
                    .Select(x => x.Name)
                    .ToArray();
            }
            else
            {
                // ...or initialize with "basic" fields 
                _request.Fields = new[]
                    {
                        nameof(IModel.Name),
                        _dataView.DataView.DefaultSort,
                    }
                    .Where(x => x != null)
                    .Distinct()
                    .ToArray();
            }
        }

        var required = GetNeededFields(requiredOutput: true).ToArray();
        if (required.Length > 0)
        {
            _request.Fields = _request.Fields
                .Concat(required)
                .Distinct()
                .ToArray();
        }
    }

    protected virtual IEnumerable<string> GetNeededFields(bool forCalculation = false, bool requiredOutput = false)
    {
        var allFields = _dataView.DataView.Fields.ToDictionary(x => x.Name);
        var requested = _request.Fields.ToHashSet();

        foreach (var fieldName in _request.Fields)
        {
            if (!allFields.TryGetValue(fieldName, out var field)) continue;
            foreach (var dependency in field.GetDependencies(forCalculation, requiredOutput))
            {
                if (requested.Add(dependency))
                {
                    yield return dependency;
                }
            }
        }
    }

    protected virtual void BuildDataViewFields()
    {
        var fields = _dataView.DataView.Fields.ToDictionary(x => x.Name);

        // adjust fields based on the request
        _dataView.DataView.Fields = filterFields().ToArray();

        IEnumerable<FormField> filterFields()
        {
            foreach (var fieldName in _request.Fields)
            {
                if (!fields.TryGetValue(fieldName, out var field)) continue;
                switch (field)
                {
                    case HiddenField:
                        continue;

                    default:
                        yield return field;
                        break;
                }
            }
        }
    }

    private void BuildFilterForm()
    {
        if (_dataView.DataView.FilterForm?.Fields != null) return;

        var filterable = FilterableFields ?? Enumerable.Empty<FormField>();

        // TODO: instead of removing the fields, make their visibility conditional on the value of the filterable fields used in the conditions
        // ...
        // remove reference fields with client-side conditions
        filterable = filterable.Where(x => x switch
        {
            ReferenceField referenceField => referenceField?.ReferenceFieldOptions?.Criteria == null ||
                                             referenceField.ReferenceFieldOptions.Criteria.All(x =>
                                                 x.Value == null || x.Value is not string str ||
                                                 !str.StartsWith("#{{") || !str.EndsWith("}}")),
            MultiReferenceField referenceField => referenceField?.MultiReferenceFieldOptions?.Criteria == null ||
                                                  referenceField.MultiReferenceFieldOptions.Criteria.All(x =>
                                                      x.Value == null || x.Value is not string str ||
                                                      !str.StartsWith("#{{") || !str.EndsWith("}}")),
            _ => true,
        });

        var fields = filterable.Select(ConvertToFilterField).ToArray();
        if (fields.IsEmpty()) return;

        foreach (var field in fields)
        {
            field.IsRequired = false;

            if (field.IsVisible) continue;
            field.Visible = field.Visible.Where(x => x != "false").ToArray();
            if (field.Visible.Length < 1) field.Visible = null;
        }

        _dataView.DataView.FilterForm ??= new Form.Models.Form
        {
            Name = "Filter",
            Title = "Filter",
            Fields = fields.OrderBy(x => x.Label ?? x.Name).ToArray(),
        };
    }

    protected IEnumerable<BsonDocument> StoredProcedurePipelineStages()
    {
        if (StoredProcedure?.Pipeline?.Length > 0)
        {
            return StoredProcedure.ToBsonPipeline(InferValuesForStoredProcedureParameters());
        }

        return [];
    }

    protected virtual bool DoLookupsLimitResults()
    {
        // TODO: when we add support for user filtering of looked up collections we need to also consider here 
        // ...

        return Lookups?.Any(x => x.Value.JoinBehavior == JoinBehavior.Exclude) ?? false;
    }

    protected virtual IEnumerable<BsonDocument> BuildStages()
    {
        if (string.IsNullOrEmpty(Collection)) throw new BadRequestException("Missing Collection");

        ProjectedFields = CalculateProjectedFields()?
            .DistinctBy(x => x.Key)
            .ToDictionary();

        var stages = BuildMatch().ToArray()
                .Concat(StoredProcedurePipelineStages().ToArray())
                .Concat(BuildMainSortStages().ToArray())
                .Concat(BuildMainLimitStages().ToArray())
                .Concat(BuildLookupStages().ToArray())
                .Concat(BuildLimitAfterLookupsStages().ToArray())
                .Concat(BuildProjectionStages().ToArray())
                .Concat(BuildGroupStages().ToArray())
                .Concat(BuildFinalSortStages().ToArray())
                .ToArray()
            ;

        return stages;
    }

    protected virtual IEnumerable<BsonDocument> BuildMainLimitStages()
    {
        // if no lookup is filtered, skip/limit before lookup
        return DoLookupsLimitResults() ? [] : BuildSkipAndLimitStages();
    }

    protected virtual IEnumerable<BsonDocument> BuildLimitAfterLookupsStages()
    {
        // if lookup is filtered, have to skip/limit after it
        return !DoLookupsLimitResults() ? [] : BuildSkipAndLimitStages();
    }

    protected virtual IEnumerable<BsonDocument> BuildGroupStages()
    {
        if (_request.GroupedFields != null)
        {
            // TODO: generic implementation 
            // ...
        }

        yield break;
    }

    protected IEnumerable<BsonDocument> BuildFinalSortStages()
    {
        if (Projection == Projection.TopValues)
        {
            // additional sort (only for top values)
            // sort by _id (after having limited to the top values)
            yield return new BsonDocument("$sort", new BsonDocument { { "_id", 1 } });
        }
    }

    /// <summary>
    /// Get resultSet using calculated pipeline
    /// </summary>
    protected virtual async Task<List<ExpandoObject>> AggregateAsync(IEnumerable<BsonDocument> stages)
    {
        var pipeline = PipelineDefinition<ExpandoObject, ExpandoObject>.Create(stages);
        var collection = _connection.GetCollection<ExpandoObject>(Collection, DatabaseName);

        return await collection.Aggregate(pipeline).ToListAsync();
    }

    /// <summary>
    /// resolve order by field criteria
    /// - the prefix "-" will indicate reverse order
    /// </summary>
    protected virtual string GetOrderByField()
    {
        return !string.IsNullOrWhiteSpace(_request.OrderBy) ? _request.OrderBy : _dataView.DataView.DefaultSort;
    }

    /// <summary>
    /// Sort before the projection 
    /// </summary>
    /// <returns></returns>
    protected virtual IEnumerable<BsonDocument> BuildMainSortStages()
    {
        switch (Projection)
        {
            case Projection.TopValues:
                yield return new BsonDocument("$sortByCount", $"${FormField.GetPathInCollection(_request.LookupField)}");
                yield break;
        }

        // sort/page before the rest of the pipeline
        var orderBy = GetOrderByField();
        if (string.IsNullOrWhiteSpace(orderBy)) yield break;

        var reverseOrder = orderBy.StartsWith('-');
        orderBy = reverseOrder ? orderBy[1..] : orderBy;

        FormField field;
        if (_objectType?.Fields != null && _objectType.Fields.TryGetValue(orderBy, out var ft))
        {
            field = ft.Field;
        }
        else
        {
            field = _dataView.DataView.Fields.FirstOrDefault(x => x.Name == orderBy);
        }

        if (field switch
            {
                null => true,
                LocationDistanceField => true,
                LocationField => true,
                CalculatedField => true,
                _ => false
            })
        {
            // can't sort by fields not in the database
            yield break;
        }

        orderBy = FormField.GetPathInCollection(orderBy);
        BsonValue direction = reverseOrder ? -1 : 1;
        yield return new BsonDocument("$sort", new BsonDocument(orderBy, direction));
    }

    protected IEnumerable<BsonDocument> BuildSkipAndLimitStages()
    {
        if (_request.Skip == 0 && _request.Top == 0 && _request.ContentType == "text/csv")
        {
            // exporting csv, do not enforce pageSize
            yield break;
        }

        if (_request.Skip > 0)
        {
            yield return new BsonDocument("$skip", _request.Skip);
        }


        var pageSize = _dataView.DataView.PageSize ?? 200;
        if (_request.Top > 0 && _request.Top < pageSize) pageSize = _request.Top;

        if (pageSize > 0)
        {
            yield return new BsonDocument("$limit", pageSize);
        }
    }

    /// <summary>
    /// Calculate lookups to other collections 
    /// </summary>
    protected virtual IEnumerable<BsonDocument> BuildLookupStages()
    {
        switch (Projection)
        {
            case Projection.Lookup:
            case Projection.TopValues:
                yield break;
        }

        foreach (var lookup in Lookups)
        {
            foreach (var stage in BuildLookupStages(_context, lookup.Key, lookup.Value))
            {
                yield return stage;
            }
        }
    }

    protected Lookup BuildLookup(ReferenceField referenceField, ObjectType objectType, bool addNameField = false)
    {
        var foreignFieldName = referenceField.ReferenceFieldOptions.ForeignFieldName ??
                               objectType.LookupFields?.Key ??
                               Model.IdFieldName;

        var lookup = new Lookup
        {
            ObjectType = objectType,
            ForeignFieldName = foreignFieldName,
            LocalFieldName = FormField.GetPathInCollection(referenceField.Name),
            Fields = new Dictionary<string, string>(),
            As = $"__{referenceField.Name}__",

            JoinBehavior = referenceField.ReferenceFieldOptions?.JoinBehavior ?? JoinBehavior.Unsafe,
            Criteria = referenceField.ReferenceFieldOptions?.Criteria,
        };

        if (addNameField)
        {
            var fieldName = UseApiNames && referenceField.ApiName != null ? referenceField.ApiName : referenceField.Name;
            var projection = (objectType.LookupFields?.Name ?? "Name").Replace('|', '.');
            lookup.Fields.Add(fieldName, projection);
        }

        return lookup;
    }

    protected IEnumerable<BsonDocument> BuildLookupStages(IEntityContext context, string fieldName, Lookup l, KeyValuePair<string, object>[] projections = null)
    {
        var behavior = l.JoinBehavior;

        var conditions = GetConditions(context, l)
            .Select(x => new Condition
            {
                FieldName = x.FieldName,
                Operator = x.Operator,
                Value = x.Value
            })
            .ToArray();

        if (conditions.IsEmpty())
        {
            // no need to filter foreign collection
            yield return BuildLookup(l, projections);
        }
        else
        {
            // has conditions 
            yield return BuildLookupWithCriteriaStage(context, fieldName, conditions, projections, l);

            if (conditions.Any(x => x.Operator == Operator.Exists))
            {
                // has exists: force exclude non matches
                behavior = JoinBehavior.Exclude;
            }
        }

        if (behavior != JoinBehavior.Exclude)
        {
            // left join
            yield return BuildUnwindWithPreserve(l.As);
            yield break;
        }

        // EXCLUDE: INNER JOIN
        var hasItems = l.AlwaysIncludeValues?.Count > 0;
        if (!hasItems)
        {
            // optimization since we know the field is required 
            yield return new BsonDocument("$unwind", $"${l.As}");
            yield break;
        }

        yield return BuildUnwindWithPreserve(l.As);

        // exclude no-matches but allows null and any item
        yield return new BsonDocument("$match", new BsonDocument("$or", new BsonArray
        {
            new BsonDocument($"{FormField.GetPathInCollection(l.LocalFieldName)}", new BsonDocument("$in", new BsonArray(keys()))),
            new BsonDocument($"{l.As}", new BsonDocument("$ne", BsonNull.Value))
        }));

        IEnumerable<BsonValue> keys()
        {
            yield return BsonNull.Value;
            foreach (var key in l.AlwaysIncludeValues)
            {
                yield return BsonValue.Create(key);
            }
        }
    }

    protected static IEnumerable<Condition> GetConditions(IEntityContext context, Lookup l)
    {
        if (l.Criteria != null)
        {
            foreach (var condition in l.Criteria)
            {
                // skip foreign field (if it was also included in the constraints)
                if (condition.FieldName == l.ForeignFieldName) continue;

                // if (condition.Operator != Operator.Eq) throw new NotImplementedException($"{condition.Operator} not implemented yet");
                yield return condition;
            }
        }

        if (l.ObjectType.Constraints != null)
        {
            var conditions = l.ObjectType.GetConditions(context);
            foreach (var condition in conditions)
            {
                // if (condition.Operator != Operator.Eq) throw new NotImplementedException($"{condition.Operator} not implemented yet");
                yield return condition;
            }
        }
    }

    protected static BsonDocument BuildUnwindWithPreserve(string path)
    {
        // TODO: build using BSON directly?
        var str = new StringBuilder();
        str.Append("{ \"$unwind\": {");
        str.Append("\"path\": ");
        str.Append("\"$");
        str.Append(path);
        str.Append("\", ");
        str.Append("\"preserveNullAndEmptyArrays\": true");
        str.Append("} }");

        return BsonDocument.Parse(str.ToString());
    }

    protected virtual BsonDocument BuildLookupWithCriteriaStage(IEntityContext context, string fieldName, Condition[] allConditions, KeyValuePair<string, object>[] projections, Lookup lookup)
    {
        // TODO: add field substitutes 
        // ...         
        allConditions.ReplaceValuePlaceHolders(context);
        
        var pipeline = new BsonArray
        {
            new BsonDocument("$match", new BsonDocument(BuildMatch(lookup.ForeignFieldName, allConditions))),
        };

        if (lookup.AtMostOne)
        {
            pipeline.Add(new BsonDocument("$limit", 1));
        }

        if (projections?.Length > 0)
        {
            pipeline.Add(new BsonDocument("$project", new BsonDocument(projections.DistinctBy(x => x.Key))));
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

    public static IEnumerable<KeyValuePair<string, object>> BuildMatch(string foreignFieldName, Condition[] additionalConditions)
    {
        foreach (var condition in additionalConditions)
        {
            if (condition.FieldName == foreignFieldName) continue;

            switch (condition.Operator)
            {
                // case Operator.NotAll
                // ...

                case Operator.In:
                case Operator.Nin:
                case Operator.ArrayAll:
                case Operator.ArrayAnyIn:
                {
                    if (condition.Value is not IEnumerable en) throw new NotImplementedException($"Not supported value for {condition.Operator}");
                    var bsonElements = en.ToEnumerableObject().Select(x => x switch
                    {
                        Guid uuid => uuid.AsSerializedId(),
                        _ => x,
                    });
                    yield return new KeyValuePair<string, object>(
                        FormField.GetPathInCollection(condition.FieldName),
                        new BsonDocument
                        {
                            { condition.Operator.ToOperatorString(), new BsonArray(bsonElements) }
                        });
                    break;
                }

                default:
                {
                    // since it will not use the serializer settings we have to convert the values here for Guid 
                    // and any other type that we have special serialization settings
                    var value = condition.Value switch
                    {
                        Guid guid => guid.AsSerializedId(),
                        _ => condition.Value,
                    };

                    yield return new KeyValuePair<string, object>(
                        FormField.GetPathInCollection(condition.FieldName),
                        new BsonDocument
                        {
                            { condition.Operator.ToOperatorString(), BsonValue.Create(value) }
                        });

                    break;
                }
            }
        }
    }

    protected static BsonDocument BuildLookup(Lookup l, KeyValuePair<string, object>[] projections)
    {
        var lookupStage = new BsonDocument
        {
            { "from", l.ObjectType.CollectionName },
            { "as", l.As },
            { "foreignField", l.ForeignFieldName },
            { "localField", l.LocalFieldName },
        };

        if (projections?.Length > 0)
        {
            lookupStage["pipeline"] = new BsonArray
            {
                new BsonDocument("$project", new BsonDocument(projections)),
            };
        }

        return new BsonDocument("$lookup", lookupStage);
    }

    protected virtual IEnumerable<KeyValuePair<string, object>> CalculateProjectedFields()
    {
        if (Projection == Projection.All)
        {
            // (unsafe) special case, do not add projection
            return null;
        }

        var fields = Projection switch
        {
            Projection.TopValues =>
            [
                new KeyValuePair<string, object>("_id", 1),
                new KeyValuePair<string, object>("count", 1)
            ],
            _ => GetProjectedFields().ToArray(),
        };

        return fields.IsEmpty() ? [new KeyValuePair<string, object>("_id", 1)] : fields;
    }

    protected IEnumerable<BsonDocument> BuildProjectionStages()
    {
        if (ProjectedFields == null || ProjectedFields.Count == 0) yield break;

        var project = new BsonDocument(ProjectedFields);
        yield return new BsonDocument("$project", project);
    }

    protected IEnumerable<KeyValuePair<string, object>> GetProjectedFields()
    {
        var fields = _dataView.DataView.Fields
            .Select(x => x.Name)
            .Concat(GetNeededFields(forCalculation: true));

        foreach (var field in fields)
        {
            yield return new KeyValuePair<string, object>(field, CalculateFieldProjection(field));
        }

        // reference fields (names)
        foreach (var lookup in Lookups.Values)
        {
            foreach (var kvp in lookup.Fields)
            {
                yield return new KeyValuePair<string, object>(kvp.Key, "$" + $"{lookup.As}.{kvp.Value}");
            }
        }
    }

    private bool IsValidRequest()
    {
        if (FilterForm?.Fields == null)
        {
            // nothing to look for
            return true;
        }

        // copy defaults to Request
        var list = new List<Condition>();
        foreach (var field in FilterForm.Fields.Where(x => x.DefaultValue != null))
        {
            list.Add(new Condition
            {
                FieldName = field.Name,
                Value = field.DefaultValue
            });
        }

        var existing = (_request?.Criteria ?? Enumerable.Empty<Condition>())
            .Where(x => x.Operator == Operator.Eq)
            .ToDictionary(x => x.FieldName);

        if (list.Count > 0)
        {
            if (MatchCriteria.Conditions.Length > 0)
            {
                list.RemoveAll(x => existing.ContainsKey(x.FieldName));
                list.AddRange(MatchCriteria.Conditions);
            }

            // update criteria
            MatchCriteria.Conditions = list.ToArray();
            existing = MatchCriteria.Conditions
                .Where(x => x.Operator == Operator.Eq)
                .ToDictionary(x => x.FieldName);
        }

        foreach (var field in FilterForm.Fields.Where(x => x.IsRequired))
        {
            if (!existing.ContainsKey(field.Name))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateCurrentFilterFormValues()
    {
        // adjust filter in view 
        if (FilterForm?.Fields == null || MatchCriteria?.Conditions == null) return;

        var fixedFields = _request.FixedFields?.ToHashSet();
        var fields = fixedFields?.Count > 0
            ? FilterForm.Fields.Where(x => !fixedFields.Contains(x.Name)).ToArray()
            : FilterForm.Fields;

        var hideEmpty = fields.Length > 5;
        foreach (var field in FilterForm.Fields)
        {
            var conditions = MatchCriteria.Conditions.Where(x => x.FieldName == field.Name).ToArray();
            field.SetDefaultValue(conditions);

            if (hideEmpty && field.DefaultValue == null)
            {
                field.Visible = ["false"];
            }

            if (fixedFields?.Contains(field.Name) ?? false)
            {
                field.Visible = ["false"];
            }
        }
    }

    public class Lookup
    {
        public ObjectType ObjectType { get; init; }
        public string As { get; init; }

        public string ForeignFieldName { get; init; }

        public string LocalFieldName { get; init; }

        /// <summary>
        /// Additional criteria
        /// </summary>
        public Condition[] Criteria { get; set; }

        public JoinBehavior JoinBehavior { get; set; }

        /// <summary>
        /// key = project to (e.g. field name)
        /// Value = projection  
        /// </summary>
        public Dictionary<string, string> Fields { get; set; }

        /// <summary>
        /// Allowed localField Values that will not exclude
        /// record if a match is not found in lookup
        /// (e.g. ReferenceField Items Keys) 
        /// </summary>
        public HashSet<string> AlwaysIncludeValues { get; set; }

        /// <summary>
        /// Whether the lookup will return at most 1 result (1:1)
        /// </summary>
        public bool AtMostOne { get; set; }

        public Lookup()
        {
        }
    }
}