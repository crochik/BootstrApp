using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PI.Shared.Extensions;

public static class JsonExtensions
{
    public static IEnumerable<T> EnumerateChildren<T>(this JArray array)
    {
        foreach (JToken item in array.Children())
        {
            yield return item.Value<T>();
        }
    }
    
    public static JToken RemoveNulls(this JToken token)
    {
        if (token.Type == JTokenType.Object)
        {
            var copy = new JObject();
            foreach (JProperty prop in token.Children<JProperty>().OrderBy(x => x.Name))
            {
                var child = prop.Value;
                if (child.HasValues) child = child.RemoveNulls();
                if (!child.IsNull()) copy.Add(prop.Name, child);
            }

            return copy;
        }

        if (token.Type == JTokenType.Array)
        {
            var copy = new JArray();
            foreach (JToken item in token.Children())
            {
                var child = item;
                if (child.HasValues) child = child.RemoveNulls();
                if (!child.IsNull()) copy.Add(child);
            }

            return copy;
        }

        return token;
    }

    public static bool IsNull(this JToken token)
    {
        return (token.Type == JTokenType.Null);
    }

    public static IDictionary<string, object> ToDictionary(this IEnumerable<JProperty> jProperties)
        => jProperties.ToDictionary(x => x.Name, x => ConvertJToken(x.Value));

    private static object ConvertJToken(this JToken x)
        => x.Type switch
        {
            JTokenType.Boolean => x.Value<bool>(),
            JTokenType.String => x.Value<string>(),
            JTokenType.Date => x.Value<DateTime>(),
            JTokenType.Guid => x.Value<Guid>(),
            JTokenType.Float => x.Value<decimal>(),
            JTokenType.Integer => x.Value<int>(),
            JTokenType.Null => null,
            JTokenType.Array => ((JArray)x).Select(ConvertJToken).ToArray(),
            JTokenType.Object => ((JObject)x).Properties().ToDictionary(),
            _ => throw new NotImplementedException("Unexpected type"),
        };
}