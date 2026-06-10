using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;

namespace PI.Shared.Models.Expressions
{
    public static class IDictionaryExtensions
    {
        /// <summary>
        /// Set Field ysing field Path (e.g. | )
        /// It will automatically add levels as needed 
        /// </summary>
        public static bool SetFieldValue(this IDictionary<string, object> record, string fieldPath, object value)
        {
            var parts = fieldPath.Split('|');
            return SetFieldValue(record, parts, value);
        }

        /// <summary>
        /// Set Field ysing name parts
        /// It will automatically add levels as needed 
        /// </summary>
        public static bool SetFieldValue(this IDictionary<string, object> record, string[] parts, object value)
        {
            if (parts.Length == 1)
            {
                record[parts[0]] = value;
                return true;
            }

            if (!record.TryGetValue(parts[0], out var level))
            {
                var newLevel = new Dictionary<string, object>();
                if (!newLevel.SetFieldValue(parts[1..], value)) return false;
                
                record[parts[0]] = newLevel;
                return true;
            }

            if (level is not IDictionary<string, object> levelDict) return false;
            return levelDict.SetFieldValue(parts[1..], value);
        }

        /// <summary>
        /// Try get field value using field path (e.g. |)
        /// </summary>
        public static bool TryGetFieldValue(this IDictionary<string, object> record, string fieldPath, out object value)
        {
            var parts = fieldPath.Split('|');
            return TryResolveValue(record, parts, out value);
        }

        /// <summary>
        /// Try to resolve path into Guid
        /// Path can be a field path (e.g. A|B|C)
        /// or a path in a handlebars context (e.g. {{Objects.A.Property|B}}) 
        /// </summary>
        public static bool TryResolvePathGuidValue(this IDictionary<string, object> flowContext, string path, out Guid id)
        {
            if (flowContext.TryResolvePathValue(path, out object value) && value.TryToParseObjectId(out var result)) {
                id = result;
                return true;
            }

            id = Guid.Empty;
            return false;
        }

        public static bool TryResolvePathStrValue(this IDictionary<string, object> flowContext, string path, out string value)
        {
            if (flowContext.TryResolvePathValue(path, out var valueObj))
            {
                if (valueObj is string str)
                {
                    value = str;
                    return true;
                }
                
                // null?
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Try to resolve path
        /// Path can be a field path (e.g. A|B|C or A|0|B )
        /// or a path in a handlebars context (e.g. {{Objects.A.Property|B}}) 
        /// </summary>
        public static bool TryResolvePathValue(this IDictionary<string, object> flowContext, string path, out object value)
        {
            var parts = (path.StartsWith("{{") && path.EndsWith("}}")) ?
                path.Substring(2, path.Length - 4).Split(".") : // handlebars context (properties have been resolved)
                path.Split('|'); // path within object

            return TryResolveValue(flowContext, parts, out value);
        }
        
        /// <summary>
        /// Resolve path or return default value
        /// Path can be a field path (e.g. A|B|C)
        /// or a path in a handlebars context (e.g. {{Objects.A.Property|B}}) 
        /// </summary>
        public static object ResolvePathValue(this IDictionary<string, object> context, string path, object defaultValue = null)
        {
            if (!context.TryResolvePathValue(path, out var value))
            {
                return defaultValue;
            }

            return value ?? defaultValue;
        }

        /// <summary>
        /// Resolve field value (path with only '|', no '.')
        /// </summary>
        public static object GetFieldValue(this IDictionary<string, object> record, string fieldPath, object defaultValue = null)
        {
            var parts = fieldPath.Split('|'); // path within object
            if (!record.TryResolveValue(parts, out var value))
            {
                return defaultValue;
            }

            return value ?? defaultValue;
        }

        /// <summary>
        /// Try to get the value for a "path"
        /// </summary>
        public static bool TryResolveValue(this IDictionary<string, object> record, string[] parts, out object value)
        {
            if (record == null)
            {
                value = null;
                return false;
            }

            value = record;
            for (var c = 0; c < parts.Length; c++)
            {
                var propName = parts[c];

                if (value == null)
                {
                    // parent object is null
                    return false;
                }

                if (value is IDictionary<string, object> objDict)
                {
                    if (!objDict.TryGetValue(propName, out var propValue))
                    {
                        value = null;
                        return false;
                    }

                    value = propValue;
                    continue;
                }

                // hack ... there must be a generic way
                if (value is IDictionary<string, Dictionary<string, object>> objDictDict)
                {
                    if (!objDictDict.TryGetValue(propName, out var propValue))
                    {
                        value = null;
                        return false;
                    }

                    value = propValue;
                    continue;
                }

                if (value is IEnumerable<object> array && int.TryParse(propName, out var arrayIndex))
                {
                    value = array.Skip(arrayIndex).FirstOrDefault();
                    continue;
                }

                var valueType = value.GetType();
                var propInfo = valueType.GetProperty(propName);
                if (propInfo == null)
                {
                    // parent object doesn't contain property
                    value = null;
                    return false;
                }

                value = propInfo.GetValue(value);
            }

            // will potentially return an (expando) object
            return true;
        }
    }
}