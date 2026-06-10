using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Crochik.Dipper;
using Crochik.Mongo;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace PI.Shared.Services;

public class ChildrenObjectTypeDataViewResponseBuilder : ObjectTypeDataViewResponseBuilder
{
    public class Breadcrumb
    {
        public string Property { get; set; }
        public FormField Field { get; set; }
    };

    public static ChildrenObjectTypeDataViewResponseBuilder New(MongoConnection connection, IEntityContext context, DataViewRequest request, AppDataView dataView, ObjectType objectType)
    {
        dataView.DataView.DefaultSort = "#key";
        dataView.DataView.KeyField = "#key";

        return new ChildrenObjectTypeDataViewResponseBuilder(connection, context, request, dataView, objectType);
    }

    private ObjectType SourceObjectType { get; set; }
    private Guid SourceId { get; set; }

    private AggregateStoredProcedure _storedProcedure;
    protected override AggregateStoredProcedure StoredProcedure => _storedProcedure;

    private ChildrenObjectTypeDataViewResponseBuilder(MongoConnection connection, IEntityContext context, DataViewRequest request, AppDataView dataView, ObjectType objectType) :
        base(connection, context, request, dataView, objectType)
    {
    }

    public ChildrenObjectTypeDataViewResponseBuilder WithSource(ObjectType sourceObjectType, Guid id, IEnumerable<Breadcrumb> breadcrumb)
    {
        SourceObjectType = sourceObjectType;
        SourceId = id;

        _storedProcedure = new AggregateStoredProcedure
        {
            Collection = sourceObjectType.CollectionName,
            DatabaseName = sourceObjectType.DatabaseName,
            Pipeline = pipeline().ToArray(),
        };

        // TODO: ...
        // use this information to do initial match and then replace root
        // ...
        return this;

        IEnumerable<string> pipeline()
        {
            foreach (var bc in breadcrumb)
            {
                if (bc.Field is ChildrenField childrenField)
                {
                    if (childrenField.ChildrenFieldOptions.KeyType == ChildrenFieldOptions.StringKeyType)
                    {
                        // dict
                        yield return "{\"$set\": {\"_array_\": {\"$objectToArray\": \"$" + bc.Property + "\"}}}";
                        yield return "{\"$unwind\": \"$_array_\" }";
                        yield return "{ \"$set\": { \"_array_.v.#key\": \"$_array_.k\" } }";
                        yield return "{\"$replaceRoot\": { \"newRoot\": \"$_array_.v\" } }";
                    }
                    else if (childrenField.ChildrenFieldOptions.KeyType == ChildrenFieldOptions.IndexKeyType)
                    {
                        // array
                        yield return "{\"$unwind\": { \"path\": \"$" + bc.Property + "\", \"includeArrayIndex\": \"" + $"{bc.Property}.#key" + "\" } }";
                        yield return "{\"$set\": { \"" + $"{bc.Property}.#key" + "\" : {\"$add\": [ \"$" + $"{bc.Property}.#key" + "\", 1 ] } } }";
                        yield return "{\"$replaceRoot\": { \"newRoot\": \"$" + bc.Property + "\" } }";
                    }
                }
            }

            // var path = string.Join(".", breadcrumb.Select(x => x.Property));
            // yield return "{ \"$replaceRoot\": { \"newRoot\": \"$" + path + "\"} }";
            // 
        }
    }

    /// <summary>
    /// Apply default constraints from "source object type"
    /// </summary>
    public override void ApplyDefaultConstraints(Query<ExpandoObject> query, Dictionary<string, Parameter> parameters)
    {
        ApplyDefaultConstraints(SourceObjectType, _context, query, parameters);

        // limit to object
        query.Eq(Model.IdFieldName, SourceId);
    }

    // protected override void ValidateRequestedFields()
    // {
    //     base.ValidateRequestedFields();
    //
    //     if (Request.Fields.All(x => x != "#key"))
    //     {
    //         Request.Fields = Request.Fields.Prepend("#key").ToArray();
    //     }
    // }

    protected override void BuildDataViewFields()
    {
        base.BuildDataViewFields();

        if (_request.Fields.Contains("#key"))
        {
            _dataView.DataView.Fields = _dataView.DataView.Fields.Prepend(new TextField
            {
                Name = "#key",
                Label = "Index"
            }).ToArray();
        }
        else
        {
            _dataView.DataView.Fields = _dataView.DataView.Fields.Prepend(new HiddenField
            {
                Name = "#key",
                Label = "Index"
            }).ToArray();
        }
    }
}