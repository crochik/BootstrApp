using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crochik.Extensions;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;

namespace PI.Shared.Models.Expressions;

/// <summary>
/// NOT YOUR STANDARD CONDITION! 
/// - IT ALLOWS FIELD NAMES TO BE EXPRESSIONS - the normal Condition evaluation will not recognize it
/// - Field names can be in the format A|B|C, {{A.B.C}} or an expression like {{context \"UserId\"}}
/// </summary>
public static class ConditionArraysUsingExpressionInFieldNamesExtensions
{
    /// <summary>
    /// Evaluate whether if any, condition is false
    /// </summary>
    public static bool AnyFalseUsingExpressions(this Condition[] conditions, IEntityContext context, IDictionary<string, object> objectsContext)
        => !conditions.AllTrueUsingExpressions(context, objectsContext);

    /// <summary>
    /// Evaluate whether all, if any, conditions are true
    /// </summary>
    public static bool AllTrueUsingExpressions(this Condition[] conditions, IEntityContext context, IDictionary<string, object> objectsContext)
    {
        if (conditions == null) return true;

        for (var c = 0; c < conditions.Length; c++)
        {
            var condition = conditions[c];

            // replace the condition with an expression that has the value already resolved (in case it is an expression) 
            // - so the value can fully support expressions, including accessing Context and functions
            if (ExpressionEvaluatorService.TryResolve(context, objectsContext, condition.Value, out var expectedValue))
            {
                condition = new Condition
                {
                    FieldName = condition.FieldName,
                    Operator = condition.Operator,
                    Value = expectedValue,
                };
            }
            else
            {
                throw new FailedToResolveExpressionException($"Value: {condition.Value}");
            }
            
            // ----------------------------------------------------------------------------------------------------------------------------------
            // hijack evaluation when the field names use expressions
            // - for simple field paths (e.g. {{a.v.c}}) should be equivalent of using condition.Evaluation
            // - this is used by conditions that relly on expressions beyond field path (e.g. {{a.v.c}}, like {{context \"UserId\"}} )
            // - IT IS USED IN EventType.Trigger.Conditions to hide actions based on claims for example
            if (condition.FieldName.Contains("{{") && condition.FieldName.Contains("}}"))
            {
                if (!ExpressionEvaluatorService.TryResolve(context, objectsContext, condition.FieldName, out expectedValue))
                {
                    throw new FailedToResolveExpressionException($"Field Name: {condition.Value}");
                }
                
                if (!condition.EvaluateValue(expectedValue)) return false;
                continue;
            }
            // ----------------------------------------------------------------------------------------------------------------------------------
            
            // simple paths, not expressions (e.g. A|B|C)
            if (!condition.Evaluate(objectsContext)) return false;
        }

        return true;
    }
}

public static class ConditionExtensions
{
    
    public static bool TryGetEqCondition(this Condition[] conditions, string fieldName, out Condition condition)
    {
        condition = conditions?.FirstOrDefault(x => string.Equals(x.FieldName, fieldName) && x.Operator == Operator.Eq);
        return condition != null;
    }

    public static bool TryGetUidValueFromEqCondition(this Condition[] conditions, string fieldName, out Guid guid)
    {
        if (!conditions.TryGetEqCondition(fieldName, out var condition))
        {
            guid = Guid.Empty;
            return false;
        }

        return condition.TryGetUidValue(out guid);
    }

    public static object ResolveValue(this Condition condition, IEntityContext context, IDictionary<string, object> objectContext = null)
    {
        if (!ExpressionEvaluatorService.TryResolve(context, objectContext, condition.Value, out var value))
        {
            throw new FailedToResolveExpressionException(condition.Value);
        }

        return value;
    }

    public static bool ReplaceValuePlaceHolders(this Condition[] criteria, IDictionary<string, object> objectContext)
    {
        if (criteria == null) return true;
        foreach (var condition in criteria)
        {
            if (!condition.ReplaceValuePlaceHolders(null, objectContext)) return false;
        }

        return true;
    }

    /// <summary>
    /// Replace condition "Value" placeholders using EntityContext and, optionally, ObjectContext (flowrun)
    /// </summary>
    public static bool ReplaceValuePlaceHolders(this Condition[] conditions, IEntityContext context, IDictionary<string, object> objectContext = null)
    {
        if (conditions == null) return true;
        foreach (var condition in conditions)
        {
            if (!condition.ReplaceValuePlaceHolders(context, objectContext))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Replace condition "Value" placeholders using EntityContext and, optionally, ObjectContext (flowrun)
    /// </summary>
    private static bool ReplaceValuePlaceHolders(this Condition condition, IEntityContext context = null, IDictionary<string, object> objectContext = null)
    {
        if (condition.Value is string str && str.StartsWith("#{{") && str.EndsWith("}}"))
        {
            // resolve client side
            return true;
        }

        if (!ExpressionEvaluatorService.TryResolve(context, objectContext, condition.Value, out var resolved))
        {
            return false;
        }

        condition.Value = resolved;
        return true;
    }

    public static object GetSerializableValue(this Condition condition, ObjectType objectType, string actualFieldName = null)
    {
        // instead of relying on type should rely on backingType?
        // ... 

        var value = condition.Value;
        if (value == null) return null;

        if (objectType == null || !objectType.Fields.TryGetValue(actualFieldName ?? condition.FieldName, out var field))
        {
            field = null;
        }

        // SPECIAL HANDLING FOR DATES?
        // TODO: should get rid of it or move into the field autoconvert? 
        if (value is string str && str.StartsWith("{{") && str.EndsWith("}}"))
        {
            value = field?.Field switch
            {
                DateTimeField => DateRangePreset.Calculate(str, TimeZoneInfo.FindSystemTimeZoneById(DateRangeField.DefaultTimeZoneId)),
                DateField => DateRangePreset.Calculate(str, TimeZoneInfo.FindSystemTimeZoneById(DateRangeField.DefaultTimeZoneId)),
                // ...
                _ => value
            };
        }

        var shouldBeArray = (field?.Field.GetBackingType().IsArray ?? false) ||
                            condition.Operator switch
                            {
                                Operator.In or Operator.Nin => true,
                                Operator.ArrayAll or Operator.ArrayAnyIn or Operator.ArrayNotAll => true,
                                _ => false,
                            };

        // BIG HACK TO HANDLE ObjectIds 
        // TODO: replace this with BackingType
        var canBeObjectId = field?.Field switch
        {
            ReferenceField r => r.GetBackingType().ValueType is ValueType.ObjectId or ValueType.Unknown,
            MultiReferenceField r => r.GetBackingType().ValueType is ValueType.ObjectId or ValueType.Unknown,
            // TextField t => // could check format
            // "desperate" fallback to field name
            _ => condition.FieldName == Model.IdFieldName || condition.FieldName.EndsWith("Id") || condition.FieldName.EndsWith("Ids") || condition.FieldName == Condition.LookupId,
        };

        if (shouldBeArray)
        {
            var objArray = value switch
            {
                JArray a => a.Select(x => x.Value<object>()),
                IEnumerable<object> a => a,
                IEnumerable<Guid> a => a.Select(x => (object)x),
                string => throw new BadRequestException($"{condition.FieldName}: value is expected to be array but it is a string: {value}"),
                IEnumerable e => e.ToEnumerableObject(),
                _ => throw new BadRequestException($"{condition.FieldName}: can't convert value to array: {value}"),
            };

            if (field?.Field != null)
            {
                if (field.Field.GetBackingType().IsArray)
                {
                    // backing type is array
                    return field.Field.AutoConvert(objArray);
                }

                // TODO: add IdField type and get rid of this
                if (canBeObjectId && field.Field is TextField)
                {
                    // until we have an IdField we need to hack TextField
                    return objArray.Select(x => x switch
                    {
                        string str => Guid.TryParse(str, out var uuid) ? uuid.AsSerializedId() : field.Field.AutoConvert(x),
                        Guid guid => guid.AsSerializedId(),
                        ObjectId oid => oid,
                        _ => field.Field.AutoConvert(x),
                    }).ToArray();
                }
                
                // convert each object in the array
                objArray = objArray.Select(x => field.Field.AutoConvert(x));
            }

            return canBeObjectId ? objArray.Select(ObjectIdExtensions.BestEffortAsSerializedId).ToArray() : objArray.ToArray();
        }

        // single value
        if (field?.Field != null)
        {
            value = field.Field.AutoConvert(value);
        }

        // TODO: hack
        // some sort of id
        // single value that can be ObjectId
        return canBeObjectId ? ObjectIdExtensions.BestEffortAsSerializedId(value) : value;
    }
}