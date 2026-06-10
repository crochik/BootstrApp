using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class NumberField : FormField
{
    [JsonIgnore]
    [BsonIgnore]
    public NumberFieldOptions NumberFieldOptions
    {
        get => Options as NumberFieldOptions;
        set => Options = value;
    }

    public NumberField()
    {
        Options = new NumberFieldOptions();
    }

    public override BackingType GetBackingType()
    {
        return NumberFieldOptions?.DecimalPlaces < 1 ? BackingType.Int32 : BackingType.Decimal;
    }

    public override object AutoConvert(object value)
    {
        if (value is string strValue && string.IsNullOrWhiteSpace(strValue))
        {
            if (IsRequired) throw new Exception($"Missing required {Label ?? Name}");
            return null;
        }

        return base.AutoConvert(value);
    }
}