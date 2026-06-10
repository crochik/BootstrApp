using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MongoDB.Bson.Serialization;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace PI.Shared.Services;

public interface IGetFormCache : IGetObjectCache
{
    AppFormLayout GetLayoutFromCache(ObjectType objectType, string formName);
    void OnLayoutLoaded(AppFormLayout layout);

    AppForm GetCustomFormCache(string objectType, string formName);
    void OnCustomFormLoaded(AppForm appForm);
}

public class GetFormOptions : GetObjectOptions, IGetFormCache
{
    public Action<PI.Shared.Form.Models.Form> FilterBeforeLoading { get; init; }
    protected bool WrapWithExpressionField { get; init; }
    public Func<string, string> OverrideAddAction { get; init; }
    public bool SkipLoadingCustomForm { get; init; } = false;
    public bool LoadLayout { get; init; } = true;
    
    public bool LoadUserActions { get; init; } = true;
    
    /// <summary>
    /// Whether to skip to NextUrl (true) or build action: uri (false), 
    ///     when there is no form and the next url
    /// Original behavior as to "skip" (true)
    /// </summary>
    public bool SkipToNextUrlWhenNotForm { get; set; } = true;

    public GetFormOptions()
    {
    }

    public GetFormOptions(GetFormOptions options, bool? loadUserActions = null) : base(options)
    {
        if (options != null)
        {
            FilterBeforeLoading = options.FilterBeforeLoading;
            WrapWithExpressionField = options.WrapWithExpressionField;
            OverrideAddAction = options.OverrideAddAction;
            SkipLoadingCustomForm = options.SkipLoadingCustomForm;
            LoadLayout = options.LoadLayout;
            LoadUserActions = loadUserActions ?? options.LoadUserActions;
        }
    }
    
    public GetFormOptions(GetObjectOptions options) : base(options)
    {
    }

    public virtual AppFormLayout GetLayoutFromCache(ObjectType objectType, string formName) => (Cache as IGetFormCache)?.GetLayoutFromCache(objectType, formName);
    public virtual void OnLayoutLoaded(AppFormLayout layout) => (Cache as IGetFormCache)?.OnLayoutLoaded(layout);
    public virtual AppForm GetCustomFormCache(string objectType, string formName) => (Cache as IGetFormCache)?.GetCustomFormCache(objectType, formName);
    public virtual void OnCustomFormLoaded(AppForm appForm) => (Cache as IGetFormCache)?.OnCustomFormLoaded(appForm);
}

public static class GetFormOptionsExtensions
{
    public static string BuildAddFormUrl(this GetFormOptions opts, string fullName)
    {
        if (opts?.OverrideAddAction != null) return opts.OverrideAddAction(fullName);
        return $"/api/v1/CustomObject/{fullName}/Add";
    }

    public static string GetApiName(this GetFormOptions opts, FormField field) => opts?.UseFieldApiNames == true ? field.ApiName ?? field.Name : field.Name;
}

public class ActionBuilderGetFormOptions : GetFormOptions
{
    private readonly AccountContext _accountContext;

    public ActionBuilderGetFormOptions(AccountContext accountContext)
    {
        _accountContext = accountContext;
        FilterBeforeLoading = (f) =>
        {
            var fields = Filter(f.Fields);

            f.Fields = fields;
        };

        WrapWithExpressionField = true;
        OverrideAddAction = ot => $"/api/v1/FlowActionBuilder/{ot}/Add";
    }

    public override void OnObjectTypeLoaded(ObjectType objectType)
    {
        if (objectType == null) return;

        base.OnObjectTypeLoaded(objectType);
        
        foreach (var field in objectType.Fields)
        {
            if (field.Value.InitialValue != null || !field.Value.RBAC.CanSetOnCreate(_accountContext))
            {
                field.Value.RBAC[EntityRoleId.Account] = FieldPermission.None;
                continue;
            }

            field.Value.RBAC[EntityRoleId.Account] = FieldPermission.SetOnCreate | FieldPermission.Read | FieldPermission.Update;
        }
    }

    public FormField[] Filter(IEnumerable<FormField> fields) => fields.Select(Filter).ToArray();

    private FormField Filter(FormField field)
    {
        if (field.Name.StartsWith("#")) return field;

        // field = field switch
        // {
        //     ObjectField objectField => FilterField(objectField),
        //     ChildrenField childrenField => FilterField(childrenField),
        //     ExpressionField expressionField => FilterField(expressionField),
        //     _ => field,
        // };

        if (WrapWithExpressionField)
        {
            if (field.IsReadOnly || field is ExpressionField) return field;

            return new ExpressionField
            {
                Name = field.Name,
                Label = field.Label,
                Description = field.Description,
                DefaultValue = field.DefaultValue,
                Visible = field.Visible,
                Enable = field.Enable,
                ExpressionFieldOptions = new ExpressionFieldOptions
                {
                    ValueField = field,
                }
            };
        }

        return field;
    }

    private FormField FilterField(ExpressionField expressionField)
    {
        expressionField.ExpressionFieldOptions.ValueField = Filter(expressionField.ExpressionFieldOptions.ValueField);
        return expressionField;
    }

    private FormField FilterField(ChildrenField field)
    {
        // if (OverrideAddAction != null)
        // {
        //     if (field.ChildrenFieldOptions.AddFormUrls == null)
        //     {
        //         // field.ChildrenFieldOptions.ObjectType = options.WrapObjectType(field.ChildrenFieldOptions.ObjectType); // $"/openapi/v1/Operation/{field.ChildrenFieldOptions.ObjectType}";
        //     }
        //     else
        //     {
        //         foreach (var kvp in field.ChildrenFieldOptions.AddFormUrls)
        //         {
        //             field.ChildrenFieldOptions.AddFormUrls[kvp.Key] = OverrideAddAction(kvp.Key); // $"/openapi/v1/Operation/{kvp.Key}";
        //         }
        //     }
        //
        //     // field.ObjectFieldOptions.EditForm
        // }

        return field;
    }

    private FormField FilterField(ObjectField field)
    {
        // if (OverrideAddAction != null)
        // {
        //     if (field.ObjectFieldOptions.AddFormUrls == null)
        //     {
        //         // field.ObjectFieldOptions.ObjectType = options.WrapObjectType(field.ObjectFieldOptions.ObjectType); // $"/openapi/v1/Operation/{field.ObjectFieldOptions.ObjectType}";
        //     }
        //     else
        //     {
        //         foreach (var kvp in field.ObjectFieldOptions.AddFormUrls)
        //         {
        //             field.ObjectFieldOptions.AddFormUrls[kvp.Key] = OverrideAddAction(kvp.Key); // $"/openapi/v1/Operation/{kvp.Key}";
        //         }
        //     }
        //
        //     // field.ObjectFieldOptions.EditForm
        // }

        return field;
    }
}

/// <summary>
/// Get Form Options with cache for object type and layout
/// CRITICAL... assumes any additional criteria (other than the fields passed to look in the cache are the same)
///        e.g. EntityContext (for profile elements)
/// </summary>
public class GetFormCache : GetObjectCache, IGetFormCache
{
    public int Hits { get; set; }
    public Dictionary<string, int> HitCounters { get; } = new();
    public Dictionary<string, AppFormLayout> LayoutCache { get; private set; }
    public Dictionary<string, AppForm> CustomFormCache { get; private set; }
    
    private static T DeepClone<T>(T source)
    {
        if (source == null) return default;
        
        // var dotNetToBson = source.ToBsonDocument();
        // return BsonSerializer.Deserialize<T>(dotNetToBson);

        using var ms = new MemoryStream();
        using (var writer = new MongoDB.Bson.IO.BsonBinaryWriter(ms))
        {
            BsonSerializer.Serialize(writer, source);
        }
        ms.Position = 0;
        using (var reader = new MongoDB.Bson.IO.BsonBinaryReader(ms))
        {
            return BsonSerializer.Deserialize<T>(reader);
        }
    }

    public AppFormLayout GetLayoutFromCache(ObjectType objectType, string formName)
    {
        var key = $"{objectType.FullName}:{formName}";
        LayoutCache ??= new Dictionary<string, AppFormLayout>();
        if (!LayoutCache.TryGetValue(key, out var layout)) return null;
        
        Hits++;
        HitCounters[key]++;
        return layout;
    }

    public void OnLayoutLoaded(AppFormLayout layout)
    {
        if (layout == null) return;
        LayoutCache ??= new Dictionary<string, AppFormLayout>();
        var key = $"{layout.ObjectType}:{layout.FormName}";
        if (LayoutCache.TryAdd(key, layout))
        {
            HitCounters.Add(key, 0);
        }
    }

    public AppForm GetCustomFormCache(string objectType, string formName)
    {
        var key = $"{objectType}:{formName}";
        CustomFormCache ??= new Dictionary<string, AppForm>();
        if (!CustomFormCache.TryGetValue(key, out var appForm)) return null;
        
        Hits++;
        HitCounters[key]++;
        // clone form since it will be manipulated 
        return appForm.Form != null ? DeepClone(appForm) : appForm;
    }

    public void OnCustomFormLoaded(AppForm appForm)
    {
        if (appForm == null) return;
        CustomFormCache ??= new Dictionary<string, AppForm>();
        var key = $"{appForm.ObjectType}:{appForm.Name}";
        if (CustomFormCache.TryAdd(key, appForm))
        {
            HitCounters.Add(key, 0);
        }
    }
}