using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crochik.Extensions;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Form.Models;

/// <summary>
/// Field value is calculated dynamically 
/// </summary>
public interface IDynamicFieldValue
{
}

public class CalculatedField : FormField, IDynamicFieldValue
{
    /// <summary>
    /// Field used to represent the calculated value
    /// </summary>
    public FormField Field { get; set; }

    /// <summary>
    /// Operations to calculate value
    /// </summary>
    public Calculation Calculation { get; set; }

    /// <summary>
    /// Fields used in the expression
    /// </summary>
    public string[] InputFields { get; set; }

    public object CalculateValue(IDictionary<string, object> flatObject)
        => Calculation?.Calculate(flatObject);

    public override IEnumerable<string> GetDependencies(bool forCalculation = false, bool requiredOutput = false) => (forCalculation ? InputFields : null) ?? Enumerable.Empty<string>();
}

[BsonKnownTypes(
    typeof(PathCalculation),
    typeof(IndexCalculation),
    typeof(LookupCalculation),
    typeof(SwitchCalculation),
    typeof(DefaultCalculation),
    typeof(ExpressionCalculation),
    typeof(EvaluateExpressionCalculation)
)]
[BsonDiscriminator(Required = true)]
public abstract class Calculation
{
    [BsonIgnore] [JsonProperty("type")] public string Type => GetType().Name;

    public abstract object Calculate(object objectContext);

    public virtual object Calculate(object objectContext, out bool done)
    {
        done = false;
        return Calculate(objectContext);
    }
}

/// <summary>
/// return the value from a path
/// </summary>
[BsonDiscriminator("path")]
public class PathCalculation : Calculation
{
    /// <summary>
    /// path in the input 
    /// </summary>
    public string Path { get; set; }

    public override object Calculate(object objectContext)
    {
        if (string.IsNullOrWhiteSpace(Path)) return objectContext;

        if (objectContext is IDictionary<string, object> dict)
        {
            return dict.TryResolvePathValue(Path, out var value) ? value : null;
        }

        // not implemented/handled 
        return null;
    }
}

[BsonDiscriminator("index")]
public class IndexCalculation : Calculation
{
    /// <summary>
    /// Index in the array, negative values mean from the end (e.g. -1  => last)
    /// </summary>
    public int Index { get; set; }

    public override object Calculate(object objectContext)
    {
        if (objectContext == null) return objectContext;
        if (objectContext is not IEnumerable e) return null;

        var array = e.ToEnumerableObject().ToArray();
        var index = Index < 0 ? array.Length + Index : Index;
        if (index < 0 || index >= array.Length) return null;

        return array[index];
    }
}

[BsonDiscriminator("lookup")]
public class LookupCalculation : Calculation
{
    public Condition[] Conditions { get; set; }

    public override object Calculate(object objectContext)
    {
        if (objectContext == null) return objectContext;
        if (objectContext is not IEnumerable e) return null;

        foreach (var child in e)
        {
            if (child is not IDictionary<string, object> dict) continue;
            if (Conditions.All(condition => condition.Evaluate(dict))) return child;
        }

        return null;
    }
}

[BsonDiscriminator("switch")]
public class SwitchCalculation : Calculation
{
    public Condition[] Conditions { get; set; }
    public object Value { get; set; }

    public override object Calculate(object objectContext, out bool done)
    {
        if (objectContext is IDictionary<string, object> dict)
        {
            if (Conditions.All(condition => condition.Evaluate(dict)))
            {
                done = true;
                return Value;
            }
        }

        done = false;
        return objectContext;
    }

    public override object Calculate(object objectContext)
    {
        if (objectContext == null) return objectContext;
        if (objectContext is not IDictionary<string, object> dict) return null;
        return Conditions.All(condition => condition.Evaluate(dict)) ? Value : null;
    }
}

[BsonDiscriminator("default")]
public class DefaultCalculation : Calculation
{
    public object Value { get; set; }
    public override object Calculate(object objectContext) => Value;

    public override object Calculate(object objectContext, out bool done)
    {
        done = true;
        return Value;
    }
}

[BsonDiscriminator("expression")]
public class ExpressionCalculation : Calculation
{
    public Calculation[] Calculations { get; set; }

    public override object Calculate(object objectContext) => Calculate(objectContext, out var done);

    public override object Calculate(object objectContext, out bool done)
    {
        if (Calculations == null)
        {
            done = false;
            return null;
        }

        foreach (var calculation in Calculations)
        {
            objectContext = calculation.Calculate(objectContext, out done);
            if (done)
            {
                // short circuit
                return objectContext;
            }
        }

        done = false;
        return objectContext;
    }
}

[BsonDiscriminator("evaluate")]
public class EvaluateExpressionCalculation : Calculation
{
    public string Expression { get; set; }

    public override object Calculate(object objectContext)
    {
        if (string.IsNullOrWhiteSpace(Expression)) return objectContext;

        if (objectContext is not IDictionary<string, object> dict) return null;
        if (!ExpressionEvaluatorService.TryResolve(null, dict, Expression, out var value)) return null;
        return value;
    }
}