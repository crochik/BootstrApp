using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PI.OpenAPI.Controllers;

static class JsonSorter
{
    public static JsonNode Sort(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            // Sort object properties alphabetically
            var sortedObject = new JsonObject();

            // Get all property names, sort them
            var propertyNames = jsonObject.Select(p => p.Key).OrderBy(k => k).ToList();

            // Add properties in sorted order
            foreach (var key in propertyNames)
            {
                // Recursively sort any nested structures
                sortedObject.Add(key, Sort(jsonObject[key]));
            }

            return sortedObject;
        }

        if (node is JsonArray jsonArray)
        {
            // Create a new array to hold sorted items
            var sortedArray = new JsonArray();

            // First, recursively sort the contents of each array item
            var processedItems = new List<JsonNode>();
            foreach (var item in jsonArray)
            {
                processedItems.Add(Sort(item));
            }

            // Then try to sort the array itself
            if (CanSortPrimitiveArray(processedItems))
            {
                // Sort primitive values
                processedItems.Sort(CompareJsonNodeValues);
            }
            else if (CanSortObjectArray(processedItems))
            {
                // Sort array of objects by their properties
                processedItems.Sort(CompareJsonObjects);
            }

            // Add all items (sorted if possible) to the result array
            foreach (var item in processedItems)
            {
                sortedArray.Add(item);
            }

            return sortedArray;
        }

        // For primitive values (string, number, boolean, null), 
        // just return as is since they don't need sorting
        return Clone(node);
    }

    static JsonNode Clone(JsonNode node)
    {
        // For primitive values (string, number, boolean, null), 
        // create a new JsonNode with the same value
        switch (node?.GetValueKind())
        {
            case JsonValueKind.String:
                return JsonValue.Create(node.GetValue<string>());
            case JsonValueKind.Number:
                // Handle both integer and floating point numbers
                if (node.ToString().Contains("."))
                    return JsonValue.Create(node.GetValue<double>());
                else
                    return JsonValue.Create(node.GetValue<long>());
            case JsonValueKind.True:
            case JsonValueKind.False:
                return JsonValue.Create(node.GetValue<bool>());
            case JsonValueKind.Null:
                return null;
        }
        
        // This should never happen, but return null just in case
        return null;
    }
    
    // Check if an array contains homogeneous primitive types that can be sorted
    static bool CanSortPrimitiveArray(List<JsonNode> items)
    {
        if (items.Count <= 1)
            return false; // No need to sort empty or single-item arrays

        JsonValueKind? firstKind = null;

        foreach (var item in items)
        {
            // Skip complex types - we handle those separately
            if (item is JsonObject || item is JsonArray)
                return false;

            var kind = item?.GetValueKind();

            // Remember the type of the first item
            if (firstKind == null)
                firstKind = kind;
            // If we encounter a different type, we can't sort
            else if (kind != firstKind)
                return false;
        }

        // Only sort if all items are the same primitive type
        return firstKind == JsonValueKind.String ||
               firstKind == JsonValueKind.Number ||
               firstKind == JsonValueKind.True ||
               firstKind == JsonValueKind.False;
    }

    // Check if an array contains homogeneous objects that can be sorted
    static bool CanSortObjectArray(List<JsonNode> items)
    {
        if (items.Count <= 1)
            return false; // No need to sort empty or single-item arrays

        // Ensure all items are objects
        foreach (var item in items)
        {
            if (!(item is JsonObject))
                return false;
        }

        return true;
    }

    // Compare two JSON nodes for sorting primitive values
    static int CompareJsonNodeValues(JsonNode a, JsonNode b)
    {
        // Handle null values
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        var kindA = a.GetValueKind();
        var kindB = b.GetValueKind();

        // If types are the same, compare values
        if (kindA == kindB)
        {
            switch (kindA)
            {
                case JsonValueKind.String:
                    return string.Compare(a.GetValue<string>(), b.GetValue<string>());
                case JsonValueKind.Number:
                    return a.GetValue<double>().CompareTo(b.GetValue<double>());
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return a.GetValue<bool>().CompareTo(b.GetValue<bool>());
                default:
                    return 0;
            }
        }

        // If types are different, sort by type (arbitrary ordering)
        return kindA.CompareTo(kindB);
    }

    // Compare two JSON objects for sorting
    static int CompareJsonObjects(JsonNode a, JsonNode b)
    {
        // Handle null values
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        var objA = a as JsonObject;
        var objB = b as JsonObject;

        if (objA == null || objB == null)
            return 0;

        // Get all keys from both objects for comparison
        var keysA = objA.Select(p => p.Key).ToHashSet();
        var keysB = objB.Select(p => p.Key).ToHashSet();
        var allKeys = keysA.Union(keysB).OrderBy(k => k).ToList();

        // Compare each property in alphabetical order
        foreach (var key in allKeys)
        {
            bool hasKeyA = objA.ContainsKey(key);
            bool hasKeyB = objB.ContainsKey(key);

            // If one object has the key but the other doesn't
            if (hasKeyA != hasKeyB)
            {
                return hasKeyA ? -1 : 1;
            }

            // If both have the key, compare the values
            if (hasKeyA && hasKeyB)
            {
                var valueA = objA[key];
                var valueB = objB[key];

                // Handle null values
                if (valueA == null && valueB == null) continue;
                if (valueA == null) return -1;
                if (valueB == null) return 1;

                var kindA = valueA.GetValueKind();
                var kindB = valueB.GetValueKind();

                // If types are different
                if (kindA != kindB)
                    return kindA.CompareTo(kindB);

                // Compare based on value type
                int comparison = 0;

                if (valueA is JsonObject && valueB is JsonObject)
                {
                    comparison = CompareJsonObjects(valueA, valueB);
                }
                else if (valueA is JsonArray && valueB is JsonArray)
                {
                    // For arrays, we'll compare their string representation
                    // This is not ideal but provides consistent ordering
                    comparison = valueA.ToJsonString().CompareTo(valueB.ToJsonString());
                }
                else
                {
                    comparison = CompareJsonNodeValues(valueA, valueB);
                }

                if (comparison != 0)
                    return comparison;
            }
        }

        return 0; // Objects are equal
    }
}