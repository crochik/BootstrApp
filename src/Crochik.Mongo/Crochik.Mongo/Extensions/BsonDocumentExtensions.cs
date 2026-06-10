using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MongoDB.Bson;

public static class BsonDocumentExtensions
{
    public static BsonDocument Replace(this BsonDocument doc, string token, BsonValue value)
    {
        for (var index = 0; index < doc.ElementCount; index++)
        {
            var item = doc.GetElement(index);

            if (item.Value is BsonDocument child)
            {
                child.Replace(token, value);
                continue;
            }

            if (item.Value.IsString && string.Equals(item.Value.AsString, token))
            {
                doc.Set(index, value);
            }

            if (item.Value.IsBsonArray)
            {
                foreach (var c in item.Value.AsBsonArray)
                {
                    if (c is BsonDocument doc2)
                    {
                        doc2.Replace(token, value);
                        continue;
                    }
                }
            }
        }

        return doc;
    }

    public static BsonArray FindAndReplaceStringValue(this BsonArray array, Regex regex, Func<string, BsonValue> filterFunc)
    {
        var ret = new BsonArray();
        for (var c = 0; c < array.Count; c++)
        {
            var item = array[c];
            if (item.IsString && regex.IsMatch(item.AsString))
            {
                ret.Add(filterFunc(item.AsString));
                continue;
            }

            if (item is BsonDocument child)
            {
                child.FindAndReplaceStringValue(regex, filterFunc);
                ret.Add(child);
                continue;
            }

            if (item is BsonArray childArray)
            {
                ret.Add(childArray.FindAndReplaceStringValue(regex, filterFunc));
                continue;
            }

            ret.Add(item);
        }

        return ret;
    }

    public static void FindAndReplaceStringValue(this BsonDocument doc, Regex regex, Func<string, BsonValue> filterFunc)
    {
        for (var index = 0; index < doc.ElementCount; index++)
        {
            var item = doc.GetElement(index);

            if (item.Value is BsonDocument child)
            {
                child.FindAndReplaceStringValue(regex, filterFunc);
                continue;
            }

            if (item.Value.IsBsonArray)
            {
                var array = item.Value.AsBsonArray.FindAndReplaceStringValue(regex, filterFunc);
                doc.Set(index, array);
            }

            if (item.Value.IsString && regex.IsMatch(item.Value.AsString))
            {
                doc.Set(index, filterFunc(item.Value.AsString));
            }
        }
    }

    public static void FindStringValue(this BsonArray array, Regex regex, Action<string> filterFunc)
    {
        for (var c = 0; c < array.Count; c++)
        {
            var item = array[c];
            if (item.IsString && regex.IsMatch(item.AsString))
            {
                filterFunc(item.AsString);
                continue;
            }

            if (item is BsonDocument child)
            {
                child.FindStringValue(regex, filterFunc);
                continue;
            }

            if (item is BsonArray childArray)
            {
                childArray.FindStringValue(regex, filterFunc);
                continue;
            }
        }
    }

    public static bool TryGetStringValue(this BsonDocument doc, string property, out string value)
    {
        if (!doc.TryGetElement(property, out var item))
        {
            value = null;
            return false;
        }

        if (item.Value.IsString)
        {
            value = item.Value.AsString;
            return true;
        }
        
        value = null;
        return false;
    }

    public static void FindStringValue(this BsonDocument doc, Regex regex, Action<string> filterFunc)
    {
        for (var index = 0; index < doc.ElementCount; index++)
        {
            var item = doc.GetElement(index);

            if (item.Value is BsonDocument child)
            {
                child.FindStringValue(regex, filterFunc);
                continue;
            }

            if (item.Value.IsBsonArray)
            {
                item.Value.AsBsonArray.FindStringValue(regex, filterFunc);
                continue;
            }

            if (item.Value.IsString && regex.IsMatch(item.Value.AsString))
            {
                filterFunc(item.Value.AsString);
                continue;
            }

            // ...
        }
    }        

    public static BsonDocument ReplaceISODates(this BsonDocument doc)
    {
        var regex = new Regex("^ISODate\\(['\"]?(?<value>.*)['\"]?\\)$");

        doc.FindAndReplaceStringValue(
            regex,
            (strValue) =>
            {
                var match = regex.Match(strValue);
                var value = match.Groups["value"]?.Value;
                if (string.IsNullOrEmpty(value)) return BsonDateTime.Create(DateTime.UtcNow);
                if (DateTime.TryParse(value, out var date)) return BsonDateTime.Create(date);

                return strValue;
            }
        );

        return doc;
    }

    public static BsonDocument ReplaceFunctions(this BsonDocument doc)
    {
        var regex = new Regex("^(new\\s)?(?<func>[a-zA-Z]*)\\(['\"]?(?<value>.*)['\"]?\\)$");

        doc.FindAndReplaceStringValue(
            regex,
            (strValue) =>
            {
                var match = regex.Match(strValue);

                var value = match.Groups["value"]?.Value;
                return match.Groups["func"].Value switch
                {
                    "ISODate" => BsonDateTime.Create(string.IsNullOrEmpty(value) ? (object)DateTime.UtcNow : value),
                    "ObjectId" => string.IsNullOrEmpty(value) ?
                        new BsonObjectId(ObjectId.GenerateNewId()) : // TODO: this will not work as it will generate the value only once 
                        BsonObjectId.Create(value),
                    "NumberInt" => BsonInt32.Create(value),
                    "NumberLong" => BsonInt64.Create(value),
                    "NumberDecimal" => BsonDecimal128.Create(value),
                    // "Timestamp"
                    _ => strValue,
                };
            }
        );

        return doc;
    }

    public static string ToJsonString(this IEnumerable<BsonDocument> array)
        => "[\n" + string.Join(",\n", array.Select(x => $"\t{x}")) + "\n]";

    public static string ToJsonArrayString(this IEnumerable<string> array)
        => "[\n\t" + string.Join(",\n\t", array) + "\n]";

    /// <summary>
    /// Whether the pipeline contains a stage type (e.g. $merge, ... )
    /// </summary>
    public static bool AnyStageOfType(this IEnumerable<BsonDocument> stages, string stageType)
    {
        return stages.Any(x => x.IsStageOfType(stageType));
    }

    public static bool AnyStageOfType(this IEnumerable<BsonDocument> stages, params string[] stageTypes)
    {
        var lookup = stageTypes.ToHashSet();
        return stages.Any(x => lookup.Contains(x.GetStageType()));
    }

    public static bool AllStageOfType(this IEnumerable<BsonDocument> stages, params string[] stageTypes)
    {
        var lookup = stageTypes.ToHashSet();
        return stages.All(x => lookup.Contains(x.GetStageType()));
    }

    /// <summary>
    /// whether the stage is of stage type (e.g. $merge, $match,  ... )
    /// </summary>
    public static bool IsStageOfType(this BsonDocument stage, string stageType)
    {
        return string.Equals(stage.GetStageType(), stageType);
    }

    public static string GetStageType(this BsonDocument stage)
    {
        var keys = stage.Names.ToArray();
        if (keys.Length != 1) throw new Exception("Not a valid pipeline stage");
        return keys[0];
    }

    /// <summary>
    /// Exclude any stages for the stageType (e.g. $merge)
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<BsonDocument> Exclude(this IEnumerable<BsonDocument> stages, string stageType)
    {
        return stages.Where(x => x.Names.Count() == 1 && !string.Equals(x.Names.First(), stageType));
    }
}