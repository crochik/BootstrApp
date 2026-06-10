using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Services;

public class HybridSalesforceObjectEditor
{
    private readonly ILogger<HybridSalesforceObjectEditor> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly SalesforceService _salesforceService;

    private IEntityContext Context { get; set; }
    private ObjectType ObjectType { get; set; }
    private Guid ObjectId { get; set; }
    private ObjectField SfObjectField { get; set; }
    private ObjectType SfObjectType { get; set; }
    private string ExternalId { get; set; }
    private ExpandoObject LocalObject { get; set; }
    private IDictionary<string, object> LocalSfObject { get; set; }
    private IDictionary<string, object> RemoteSfObject { get; set; }

    public HybridSalesforceObjectEditor(
        ILogger<HybridSalesforceObjectEditor> logger,
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

    public async Task<Result<Form>> BuildFormAsync(IEntityContext context, string objectTypeName, object objectId, FormName formName)
    {
        if (formName == FormName.Add)
        {
            return BuildAddForm();
        }

        // TODO: could handle using the external id
        // ...
        var objectGuid = objectId switch
        {
            Guid uid => uid,
            string str => Guid.TryParse(str, out var uuid) ? uuid : null,
            _ => default(Guid?),
        };
        
        if (!objectGuid.HasValue)
        {
            return Result.Error<Form>("Invalid Id");
        }

        var error = await LoadObjectAsync(context, objectTypeName, objectGuid.Value);
        if (error != null) return Result.Error<Form>(error);

        return await BuildEditFormAsync(formName);
    }

    /// <summary>
    /// Build add form
    /// NOT SUPPORTED YET!
    /// </summary>
    private Result<Form> BuildAddForm()
    {
        // // formName = FormName.Add;
        // var form = await _objectTypeService.GetAddDataFormAsync(Context, objectTypeName);
        //
        // // init with values from request? 
        // var fields = form.Fields.ToDictionary(x => x.Name);
        // foreach (var query in Request.Query)
        // {
        //     if (!fields.TryGetValue(query.Key, out var field)) continue;
        //     if (field.IsReadOnly) continue;
        //
        //     if (formName != FormName.Add && field.DefaultValue != null) continue;
        //     field.DefaultValue = field.AutoConvert(query.Value.FirstOrDefault());
        // }
        //
        // return form;

        return Result.Error<Form>("Creating Salesforce object is not supported");
    }

    private string GetSalesforceObjectField()
    {
        var sfField = ObjectType.Fields.Values.FirstOrDefault(x =>
            x.Field is ObjectField objectField &&
            objectField.ObjectFieldOptions?.ObjectType != null
            && objectField.ObjectFieldOptions.ObjectType.StartsWith("salesforce.api"));

        if (sfField?.Field is not ObjectField objectField)
        {
            return "Salesforce object field not found";
        }

        SfObjectField = objectField;
        return null;
    }

    private async Task<string> LoadObjectAsync(IEntityContext context, string objectTypeName, Guid id)
    {
        Context = context;
        ObjectType = await _objectTypeService.GetAsync(context, objectTypeName);
        ObjectId = id;

        var error = GetSalesforceObjectField();
        if (error != null) return error;

        // Load cached copy
        var dynamicRecord = await _objectTypeService.GetExpandoObjectByIdAsync(Context, ObjectType, ObjectId);
        if (dynamicRecord == null)
        {
            // TODO: could add support to get, if using a salesforce id, from salesforce and save locally 
            // .... 
            return $"{ObjectType.FullName} not found locally";
        }

        if (!dynamicRecord.TryGetFieldValue(SfObjectField.Name, out var sfFieldValue) || sfFieldValue is not IDictionary<string, object> sfProperties)
        {
            return "Couldn't resolve Salesforce object";
        }

        if (!sfProperties.TryGetStrParam("Id", out var externalIdStr))
        {
            return "Couldn't determine Salesforce object id";
        }

        SfObjectType = await _objectTypeService.GetAsync(Context, SfObjectField.ObjectFieldOptions.ObjectType);
        ExternalId = externalIdStr;
        LocalObject = dynamicRecord;
        LocalSfObject = sfProperties;

        return null;
    }

    private async Task<string> GetSalesforceObjectAsync()
    {
        RemoteSfObject = await _salesforceService.GetObjectAsync(Context, SfObjectType.Name, ExternalId);
        return null;
    }

    private bool WasSfObjectModified()
    {
        var notModified = LocalSfObject.TryGetValue("LastModifiedDate", out var oLastModifiedDateObj) &&
                          oLastModifiedDateObj is DateTime oLastModifiedDate &&
                          RemoteSfObject.TryGetValue("LastModifiedDate", out var nLastModifiedDateObj) &&
                          nLastModifiedDateObj is DateTime nLastModifiedDate &&
                          oLastModifiedDate == nLastModifiedDate;

        return !notModified;
    }

    private async Task<string> SaveModifiedSfObjectAsync()
    {
        // update dynamic object
        if (!LocalObject.SetFieldValue(SfObjectField.Name, RemoteSfObject))
        {
            return "Couldn't update cached object";
        }

        var modified = new Dictionary<string, object>();
        var update = _connection.Filter<ExpandoObject>(ObjectType.CollectionName, ObjectType.DatabaseName)
                .Eq(Model.IdFieldName, ObjectId)
                .Eq(nameof(Model.AccountId), Context.AccountId.Value)
                .Update
                .Set(nameof(Model.LastModifiedOn), DateTime.UtcNow)
            ;

        foreach (var kvp in SfObjectType.Fields)
        {
            LocalSfObject.TryGetValue(kvp.Key, out var oldValue);
            RemoteSfObject.TryGetValue(kvp.Key, out var newValue);

            if (oldValue == null)
            {
                if (newValue == null) continue;
                modified.Add(kvp.Key, newValue);
                update.SetOrUnset($"{FormField.GetPathInCollection(SfObjectField.Name)}.{kvp.Key}", newValue);
                continue;
            }

            if (oldValue.Equals(newValue)) continue;
            modified.Add(kvp.Key, newValue);
            update.SetOrUnset($"{FormField.GetPathInCollection(SfObjectField.Name)}.{kvp.Key}", newValue);
        }

        // TODO: handle calculated properties (in the wrapper object)
        // ...

        LocalObject = await update.UpdateAndGetOneAsync();

        await _objectTypeService.FireObjectUpdatedAsync(Context, ObjectType, LocalObject, ObjectId, modified, e =>
        {
            e.Action = "SalesforceRefresh"; // ????
            e.Description = "Refreshed with latest from Salesforce";
        });

        return null;
    }

    private async Task<Result<Form>> BuildEditFormAsync(FormName formName)
    {
        // TODO: check whether the use has access to the Field and ObjectType
        // if not, skip loading salesforce object
        // ...
        
        var error = await GetSalesforceObjectAsync();
        if (error != null) return Result.Error<Form>(error);

        if (WasSfObjectModified())
        {
            error = await SaveModifiedSfObjectAsync();
            if (error != null) return Result.Error<Form>(error);
        }

        var form = await _objectTypeService.GetDataFormForObjectAsync(Context, ObjectType, ObjectId, LocalObject, formName);

        return Result.Success(form);
    }

    public async Task<DataFormActionResponse> ExecUpdateActionAsync(IEntityContext context, string objectTypeName, Guid id, DataFormActionRequest request, ObjectTypeService.UpdateObjectOptions opts = null)
    {
        var error = await LoadObjectAsync(context, objectTypeName, id);
        if (error != null) return DataFormActionResponse.Error(request, error);
        
        // TODO: check whether the use has access to the Field and ObjectType
        // if not, skip loading salesforce object
        // ...
        
        // GetFieldValuesFromUserInputAsync is only for "adding" (will check the wrong permission) and the one for update will require a form
        // var input = await _objectTypeService.GetFieldValuesFromUserInputAsync(Context, childObjectType, newProperties);
        // newProperties = input.Value;

        if (!request.Parameters.TryGetFieldValue(SfObjectField.Name, out var propField) || propField == null)
        {
            return DataFormActionResponse.Error(request, "Salesforce object not found in request");
        }

        var newSfProperties = (IDictionary<string, object>)JsonObjectConverter.Convert<ExpandoObject>(propField);

        var modified = new Dictionary<string, object>();
        foreach (var kvp in SfObjectType.Fields)
        {
            if (!kvp.Value.RBAC.CanUpdate(Context)) continue;

            LocalSfObject.TryGetValue(kvp.Key, out var oldValue);
            
            var apiName = (opts?.UseFieldApiNames ?? false) ? (kvp.Value.Field.ApiName ?? kvp.Value.Field.Name) : kvp.Value.Field.Name;
            newSfProperties.TryGetValue(apiName, out var newValue);

            oldValue = kvp.Value.Field.AutoConvert(oldValue);
            newValue = kvp.Value.Field.AutoConvert(newValue);

            if (oldValue == null)
            {
                if (newValue == null) continue;
                modified.Add(kvp.Key, newValue);
                continue;
            }

            if (oldValue.Equals(newValue)) continue;
            modified.Add(kvp.Key, newValue);
        }

        if (modified.Count < 1)
        {
            // not salesforce changes, uses standard method 
            return await _objectTypeService.ExecUpdateAsync(Context, ObjectType, request, opts);
        }

        if (!LocalSfObject.TryGetStrParam("Id", out var externalId))
        {
            return DataFormActionResponse.Error(request, "Couldn't determine Salesforce object id");
        }

        var result = await _salesforceService.UpdateObjectAsync(Context, SfObjectType.Name, externalId, modified, new GetTokenOptions { UseIntegration = false });
        if (!result.IsSuccess)
        {
            return DataFormActionResponse.Error(request, result.Status);
        }

        IDictionary<string,object> sfObject = await _salesforceService.GetObjectAsync(Context, SfObjectType.Name, externalId);

        if (opts?.UseFieldApiNames ?? false)
        {
            // using api names, has to convert the salesforce properties to api names so they handled in the update correctly
            var sfObjectWithApiNames = new Dictionary<string, object>();
            foreach (var kvp in SfObjectType.Fields)
            {
                if (sfObject.TryGetValue(kvp.Key, out var value))
                {
                    sfObjectWithApiNames[kvp.Value.Field.ApiName ?? kvp.Value.Field.Name] = value;
                }
            }

            sfObject = sfObjectWithApiNames;
        }
        
        // update request parameters with modified salesforce object
        request.Parameters.SetFieldValue(SfObjectField.Name, sfObject);

        return await _objectTypeService.ExecUpdateAsync(Context, ObjectType, request, opts);
    }
}