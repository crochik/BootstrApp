using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Form.Models;

/// <summary>
/// used in filters for now ...
/// </summary>
public class DateRangeField : FormField
{
    public const string DefaultTimeZoneId = "America/New_York";
    
    public override string Type => "dateRange";

    [BsonIgnore]
    [JsonIgnore]
    public DateRangeFieldOptions DateRangeFieldOptions
    {
        get => Options as DateRangeFieldOptions;
        set => Options = value;
    }
    
    public override object AutoConvert(object value)
    {
        if (value is IEnumerable enumerable)
        {
            var array = convert().ToArray();
            if (array.Length != 2) throw new Exception("Invalid value");
            return array;
        }
        
        // TBD
        return value;

        IEnumerable<DateTime?> convert()
        {
            foreach (var v in enumerable)
            {
                if (v == null)
                {
                    yield return null;
                    continue;
                }

                yield return v switch
                {
                    DateTime dateTime => dateTime,
                    string str => DateTime.Parse(str),
                    _ => throw new Exception("Invalid value")
                };
            }
        }
    }

    public override BackingType GetBackingType() => BackingType.DateRange;

    public override void SetDefaultValue(Condition[] conditions)
    {
        var eq = conditions?.FirstOrDefault(x => x.Operator == Operator.Eq);
        if (eq != null)
        {
            DefaultValue = eq.Value;
            return;
        }

        var after = conditions?.FirstOrDefault(x => x.Operator is Operator.Gt or Operator.Gte);
        var before = conditions?.FirstOrDefault(x => x.Operator is Operator.Lt or Operator.Lte);
        if (after == null && before == null)
        {
            // do nothing
            return;
        }

        DefaultValue = new[]
        {
            ParseValue(after?.Value),
            ParseValue(before?.Value),
        };
    }
    
    private DateTime? ParseValue(object value)
    {
        return value switch
        {
            null => null,
            DateTime date => date,
            string str => ParseStrValue(str),
            _ => null
        };
    }

    private DateTime? ParseStrValue(string str)
    {
        // TODO: we will need the real time zone :(
        return DateRangePreset.Calculate(str, TimeZoneInfo.FindSystemTimeZoneById(DateRangeField.DefaultTimeZoneId));
    }
}