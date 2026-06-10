using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Form.Models;

public class LabelField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public LabelFieldOptions LabelFieldOptions
    {
        get => Options as LabelFieldOptions;
        set => Options = value as LabelFieldOptions;
    }

    public LabelField()
    {
        Options = new LabelFieldOptions();
    }

    public override BackingType GetBackingType() => BackingType.String;

    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        base.FillPlaceHolders(context, objectContext);
        
        if (Label?.Contains("{{") ?? false)
        {
            if (ExpressionEvaluatorService.TryResolve(context, objectContext, Label, out var value))
            {
                Label = value?.ToString();
            }
        }
    }
}