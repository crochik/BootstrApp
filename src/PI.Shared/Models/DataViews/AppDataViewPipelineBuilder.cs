using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Bson;
using PI.Shared.Form.Models;

namespace PI.Shared.Models;

/// <summary>
/// used "internally" to generate just the pipeline for other "tools" 
/// </summary>
public class AppDataViewPipelineBuilder : AbstractPipelineBuilder
{
    private readonly HashSet<string> _fieldNames;
    
    public static AppDataViewPipelineBuilder New(MongoConnection connection, IEntityContext context, AppDataView dataView, ObjectType objectType) => new(connection, context, dataView, objectType);

    private AppDataViewPipelineBuilder(MongoConnection connection, IEntityContext context, AppDataView dataView, ObjectType objectType) :
        base(connection, context, new DataViewRequest(), dataView, objectType)
    {
        _fieldNames = dataView.Fields?.ToHashSet() ??
                      dataView.DataView?.Fields.Select(x => x.Name).ToHashSet();
    }
    
    public IEnumerable<BsonDocument> BuildPipeline()
    {
        var stages = BuildMatch();
        
        if (StoredProcedure?.Pipeline?.Length > 0)
        {
            var spPipeline = StoredProcedure.ToBsonPipeline(InferValuesForStoredProcedureParameters());
            stages = stages.Concat(spPipeline);
        }
        
        stages = stages.Concat(BuildAppDataViewSort());

        // if (_expandReferenceFields)
        // {
        //     var (referenceFields, referenceObjects) = await LoadAppDataViewReferencedObjectsAsync();
        //     stages = stages.Concat(BuildAppDataViewLookupStages(referenceFields, referenceObjects));
        //     stages = stages.Concat(BuildAppDataViewProjection(referenceFields, referenceObjects));
        // }
        // else
        // {
            stages = stages.Concat(BuildAppDataViewProjection(Array.Empty<ReferenceField>(), new Dictionary<string, ObjectType>()));
        // }

        return stages;
    }

    // /// <summary>
    // /// Build lookup stages for visible fields in the AppDataView 
    // /// </summary>
    // private async Task<IEnumerable<BsonDocument>> BuildAppDataViewLookupStagesAsync()
    // {
    //     var referenceFields = _objectType.Fields
    //         .Where(x => _fieldNames.Contains(x.Key) && x.Value.RBAC.CanRead(_context))
    //         .Select(x => x.Value.Field)
    //         .OfType<ReferenceField>()
    //         .ToArray();
    //
    //     if (referenceFields.Length < 1) return Enumerable.Empty<BsonDocument>();
    //
    //     (referenceFields, var referenceObjects) = await LoadReferencedObjectsAsync(_connection, _context, referenceFields);
    //     return BuildLookupStages(_context, _fieldNames, referenceFields, referenceObjects);
    // }
    //
    // private IEnumerable<BsonDocument> BuildAppDataViewLookupStages(ReferenceField[] referenceFields, Dictionary<string, ObjectType> referencedObjects)
    // {
    //     return BuildLookupStages(_context, _fieldNames, referenceFields, referencedObjects);
    // }

    /// <summary>
    /// Calculate sort stage using AppDataView
    /// </summary>
    private IEnumerable<BsonDocument> BuildAppDataViewSort()
    {
        var orderBy = _dataView.DataView?.DefaultSort ?? (_dataView as AppDataView)?.OrderBy;
        if (string.IsNullOrEmpty(orderBy)) yield break;

        var reverseOrder = orderBy.StartsWith('-');
        orderBy = reverseOrder ? orderBy[1..] : orderBy;

        yield return new BsonDocument("$sort", new BsonDocument(FormField.GetPathInCollection(orderBy), reverseOrder ? -1 : 1));
    }

    /// <summary>
    /// Attempt to create document with subdocuments from projected fields 
    /// </summary>
    public static BsonDocument GetProjectionAsSubDocuments(IEnumerable<string> fieldNames, ReferenceField[] referenceFields)
    {
        var visible = fieldNames.ToHashSet();

        var refFieldNames = (referenceFields ?? Enumerable.Empty<ReferenceField>())
            .ToDictionary(x => x.Name);

        var tree = new Dictionary<string, object>();
        foreach (var field in visible)
        {
            if (refFieldNames.TryGetValue(field, out var refField))
            {
                // TODO: probably should replace the actual field with a subdocument 
                // ...
                // this will be a property at the top level with a | path 
                // ...

                tree.Add($"{field}|Name", $"${LookupPath(refField)}.Name");
            }

            addToTree(tree, field, field);
        }

        return new BsonDocument("$project", new BsonDocument(tree));

        void addToTree(Dictionary<string, object> level, string fieldName, string fieldPath)
        {
            var index = fieldName.IndexOf("|", StringComparison.Ordinal);
            if (index < 0)
            {
                level[fieldName] = "$" + FormField.GetPathInCollection(fieldPath);
                return;
            }

            var subDocName = fieldName[..index];
            Dictionary<string, object> subLevel;
            if (!level.TryGetValue(subDocName, out var value))
            {
                subLevel = new Dictionary<string, object>();
                level[subDocName] = subLevel;
            }
            else
            {
                if (value is not Dictionary<string, object> dict)
                {
                    throw new Exception("Invalid object");
                }

                subLevel = dict;
            }

            addToTree(subLevel, fieldName[(index + 1)..], fieldPath);
        }
    }

    private IEnumerable<BsonDocument> BuildAppDataViewProjection(ReferenceField[] referenceFields, Dictionary<string, ObjectType> referencedObjects)
    {
        // if (_projectReferencesAsSubDocuments)
        // {
        //     yield return GetProjectionAsSubDocuments(_fieldNames, referenceFields);
        //     yield break;
        // }

        var fields = GetProjectedFields(_fieldNames, referenceFields, referencedObjects);
        if (fields.IsEmpty()) fields = fields.Append(new KeyValuePair<string, object>("_id", 1));

        yield return new BsonDocument("$project", new BsonDocument(fields));
    }

    // /// <summary>
    // /// Load reference fields and objects for AppDataView
    // /// </summary>
    // /// <returns></returns>
    // private async Task<(ReferenceField[] ReferenceFields, Dictionary<string, ObjectType> ReferencedObjects)> LoadAppDataViewReferencedObjectsAsync()
    // {
    //     var referenceFields = _objectType.Fields
    //         .Where(x => _fieldNames.Contains(x.Key) && x.Value.RBAC.CanRead(_context))
    //         .Select(x => x.Value.Field)
    //         .OfType<ReferenceField>()
    //         .ToArray();
    //
    //     if (referenceFields.Length < 1) return (referenceFields, null);
    //
    //     return await LoadReferencedObjectsAsync(_connection, _context, referenceFields);
    // }
}