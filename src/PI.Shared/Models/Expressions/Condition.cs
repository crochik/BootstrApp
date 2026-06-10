using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crochik.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace PI.Shared.Models.Expressions;

[JsonConverter(typeof(StringEnumConverter))]
public enum Operator
{
    Eq,
    Ne,
    Gt,
    Gte,
    Lt,
    Lte,
    In,
    Nin,

    // TODO: to be implemented 
    Or, // value is going to be an array of conditions 
    Exists, // value is going to be true or false 
    Null, // value is going to be true or false
    RegEx,

    // special for Arrays
    ArrayAll, // just for Array fields: for now, only used internally   
    ArrayNotAll, // just for Array fields: for now, only used internally  
    ArrayAnyIn, // just for Array fields: for now, only used internally
}

public static class OperatorExtensions
{
    public static string ToOperatorString(this Operator op)
    {
        return op switch
        {
            Operator.Eq => "$eq",
            Operator.Ne => "$ne",
            Operator.Gt => "$gt",
            Operator.Gte => "$gte",
            Operator.Lt => "$lt",
            Operator.Lte => "$lte",
            Operator.In or Operator.ArrayAnyIn => "$in",
            Operator.Nin => "$nin",
            Operator.ArrayAll => "$all",
            // Operator.ArrayNotAll => "$not_all",
            _ => throw new ArgumentOutOfRangeException($"{op} not supported"),
        };
    }
}

public class Condition
{
    private static object Not_A_Match_Value = new object();

    public const string LookupId = "#id";
    public const string AutoComplete = "#autocomplete";
    public const string FullTextSearch = "#text";

    /// <summary>
    /// Field Name (or in some "advanced" cases, any expression) 
    /// </summary>
    public string FieldName { get; init; }

    public Operator Operator { get; init; }

    private object _value;

    /// <summary>
    /// Value, in most cases can be an expression
    /// </summary>
    public object Value
    {
        get => _value;
        set => _value = value is JToken jToken ? ConvertJToken(jToken) : value;
    }

    public static Condition New(string field, Operator op, object value)
        => new()
        {
            FieldName = field,
            Operator = op,
            Value = value
        };

    public static Condition Eq(string field, object value)
        => new()
        {
            FieldName = field,
            Operator = Operator.Eq,
            Value = value
        };

    public static Condition Ne(string field, object value)
        => new()
        {
            FieldName = field,
            Operator = Operator.Ne,
            Value = value
        };

    public static Condition In<T>(string field, IEnumerable<T> list)
        => new()
        {
            FieldName = field,
            Operator = Operator.In,
            Value = list.ToArray(),
        };

    public static Condition In<T>(string field, params T[] list)
        => new()
        {
            FieldName = field,
            Operator = Operator.In,
            Value = list,
        };

    public static Condition Nin<T>(string field, params T[] list)
        => new()
        {
            FieldName = field,
            Operator = Operator.Nin,
            Value = list,
        };

    public static Condition IsTrue(string field) => Eq(field, true);
    public static Condition IsFalse(string field) => Eq(field, false);

    public bool Evaluate(IDictionary<string, object> values)
    {
        var value = values.ResolvePathValue(FieldName);
        return EvaluateValue(value);
    }

    // TODO: handle equals for arrays/enumerables?
    // ... 
    public bool EvaluateValue(object value)
    {
        return Operator switch
        {
            Operator.Eq => eq(),
            Operator.Ne => !eq(),
            Operator.In => IsIn(value),
            Operator.Nin => !IsIn(value),

            Operator.Gt => Greater(value),
            Operator.Lt => LessThan(value),
            Operator.Gte => LessThan(value, false),
            Operator.Lte => Greater(value, false),

            // ...
            _ => false,
        };

        bool eq()
        {
            if (AreEqual(Value, value)) return true;

            if (value is IEnumerable e1 and not string)
            {
                if (Value is IEnumerable e2 and not string)
                {
                    // contains all
                    return e2.ToEnumerableObject().All(x => e1.ToEnumerableObject().Any(y => AreEqual(x, y)));
                }

                // contains value 
                return e1.ToEnumerableObject().Any(x => AreEqual(Value, x));
            }

            return false;
        }
    }

    private decimal? GetNumber(object value)
    {
        return value switch
        {
            decimal d => d,
            int i => i,
            double d => (decimal)d,
            string str => decimal.TryParse(str, out var d) ? d : null,
            _ => null,
        };
    }
    
    /// <summary>
    /// compare using less than
    /// - it will return false if it can't compare
    /// - if it can compare and invert=true it will invert the result
    /// </summary>
    private bool LessThan(object value, bool trueResult = true)
    {
        if (value == null || Value == null)
        {
            // invalid, no way to respond
            return false;
        }

        var v1 = GetNumber(value);
        var refValue = GetNumber(Value);
        if (v1.HasValue && refValue.HasValue)
        {
            return v1 < refValue ? trueResult : !trueResult;
        }

        if (value is string str1 && Value is string str2)
        {
            return String.Compare(str2, str1, StringComparison.Ordinal) < 0 ? trueResult : !trueResult;
        }
        
        // don't know how to compare ... could coerce to string
        return false;
    }

    /// <summary>
    /// compare using greater than
    /// - it will return false if it can't compare
    /// - if it can compare and invert=true it will invert the result
    /// </summary>
    private bool Greater(object value, bool trueResult = true)
    {
        if (value == null || Value == null)
        {
            // invalid, no way to respond
            return false;
        }
        
        var v1 = GetNumber(value);
        var refValue = GetNumber(Value);
        if (v1.HasValue && refValue.HasValue)
        {
            return v1 > refValue ? trueResult : !trueResult;
        }
        
        if (value is string str1 && Value is string str2)
        {
            return String.Compare(str2, str1, StringComparison.Ordinal) > 0 ? trueResult : !trueResult;
        }

        // don't know how to compare ... could coerce to string
        return false;
    }

    /// <summary>
    /// compare values (try to use .Equals)
    /// - null == null => true
    /// - if one of the two values is a string, compare string representation (DANGER) 
    /// </summary>
    private static bool AreEqual(object value, object other)
    {
        return (other == null && value == null) ||
               (other != null && other.Equals(value)) ||
               (other is string str && str.Equals(value?.ToString())) ||
               (value is string valueStr && valueStr.Equals(other?.ToString()));
    }

    private object ConvertJToken(JToken x)
    {
        if (x is JArray jArray)
        {
            return jArray.Select(ConvertJToken).ToArray();
        }

        object value = x.Type switch
        {
            JTokenType.Boolean => x.Value<bool>(),
            JTokenType.String => x.Value<string>(),
            JTokenType.Date => x.Value<DateTime>(),
            JTokenType.Guid => x.Value<Guid>(),
            JTokenType.Float => x.Value<float>(),
            JTokenType.Integer => x.Value<int>(),
            JTokenType.Null => null,
            _ => (object)x,
        };

        return value;
    }

    private bool IsIn(object value)
    {
        if (Value is string || Value is not IEnumerable e1) return false;

        if (value is IEnumerable e2 and not string)
        {
            // any in   
            return e1.ToEnumerableObject().Any(x => e2.ToEnumerableObject().Any(y => AreEqual(x, y)));
        }

        // value is in Value
        return e1.ToEnumerableObject().Any(x => AreEqual(x, value));
    }

    public bool TryGetUidValue(out Guid id)
    {
        if (Value is Guid native)
        {
            id = native;
            return true;
        }

        if (Value is string str && Guid.TryParse(str, out id)) return true;
        id = default;
        return false;
    }

    public bool TryGetDate(out DateTime date)
    {
        if (Value is DateTime native)
        {
            date = native;
            return true;
        }

        if (Value is string str && DateTime.TryParse(str, out date)) return true;
        date = default;
        return false;
    }

    public bool TryGetDecimal(out decimal? parsedValue)
    {
        if (Value is decimal native)
        {
            parsedValue = native;
            return true;
        }

        if (Value is string str && decimal.TryParse(str, out var decValue))
        {
            parsedValue = decValue;
            return true;
        }

        try
        {
            parsedValue = Convert.ToDecimal(Value);
            return true;
        }
        catch (Exception)
        {
        }

        parsedValue = null;
        return false;
    }
}