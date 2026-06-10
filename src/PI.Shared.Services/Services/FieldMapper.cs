using Newtonsoft.Json.Linq;
using PI.Shared.Models;

namespace PI.Shared.Services
{
    public static class FieldMapper 
    {
        public static object Map(FieldMapperConfig config, object body, IIndexedProperties lead)
        {
            object value;

            switch (body)
            {
                case JObject jObject:
                    value = map(config.Source, jObject);
                    break;

                default:
                    return null;
            }

            if (value is JValue jValue) value = jValue.Value;

            return value;
        }

        private static object map(string source, JObject row)
        {
            var parts = source.Split(".");
            var obj = row;
            for (var c = 0; c < parts.Length; c++)
            {
                var propName = parts[c];
                if (obj != null && obj.TryGetValue(propName, out var token))
                {
                    if (token == null) return null;
                    if (c == parts.Length - 1) return token;
                    obj = token as JObject;
                }
                else
                {
                    return null;
                }
            }

            // TODO: should map to native c# type?
            // ...
            return obj;
        }
    }
}
