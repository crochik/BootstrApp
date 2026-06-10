using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace PI.OpenAPI.Controllers;

static class Yamler
{
    public static string ToString(JsonNode jsonNode)
    {
        // Convert JsonNode to .NET object
        var dotNetObject = ConvertObject(jsonNode);

        // Configure YAML serializer
        var serializerBuilder = new SerializerBuilder()
            // .WithNamingConvention(CamelCaseNamingConvention.Instance)
            // .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);
            ;
        var serializer = serializerBuilder.Build();

        // Serialize to YAML
        return serializer.Serialize(dotNetObject);
    }

    private static object ConvertObject(JsonNode node)
    {
        if (node == null)
            return null;

        if (node is JsonObject jsonObject)
        {
            var dictionary = new Dictionary<string, object>();
            foreach (var property in jsonObject)
            {
                dictionary[property.Key] = ConvertObject(property.Value);
            }

            return dictionary;
        }

        if (node is JsonArray jsonArray)
        {
            return jsonArray.Select(item => ConvertObject(item)).ToList();
        }

        switch (node.GetValueKind())
        {
            case JsonValueKind.String:
                return node.GetValue<string>();
            case JsonValueKind.Number:
            {
                try
                {
                    return node.GetValue<long>();
                }
                catch (InvalidOperationException ex)
                {
                    return node.GetValue<double>();
                }
            }
            case JsonValueKind.True:
            case JsonValueKind.False:
                return node.GetValue<bool>();
            case JsonValueKind.Null:
                return null;
        }
        
        return null;
    }
}