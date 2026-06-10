using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using Crochik.Extensions;

namespace PI.Shared
{
    public class FieldMapBuilder
    {
        public Dictionary<string, FieldMapperConfig> Map { get; }
        public Dictionary<string, object> Values { get; private set; }
        public bool AutoMap { get; set; }
        public string Prefix { get; set; }

        private static readonly Dictionary<JTokenType, FIELDTYPE> _type = new()
        {
                {JTokenType.Boolean, FIELDTYPE.Boolean},
                // {JTokenType.Bytes, FIELDTYPE.Text},
                {JTokenType.Date, FIELDTYPE.Datetime},
                {JTokenType.Float, FIELDTYPE.Number},
                {JTokenType.Guid, FIELDTYPE.Text},
                {JTokenType.Integer, FIELDTYPE.Number},
                {JTokenType.TimeSpan, FIELDTYPE.Datetime},
                {JTokenType.Uri, FIELDTYPE.Text},
                {JTokenType.String, FIELDTYPE.Text},
                {JTokenType.Undefined, FIELDTYPE.Undefined},
                {JTokenType.None, FIELDTYPE.Undefined},
                {JTokenType.Null, FIELDTYPE.Undefined},
            };

        private static readonly Dictionary<JTokenType, System.Type> _native = new()
        {
                {JTokenType.Boolean, typeof(bool)},
                // {JTokenType.Bytes, FIELDTYPE.Text},
                {JTokenType.Date, typeof(DateTime)},
                {JTokenType.Float, typeof(float)},
                {JTokenType.Guid, typeof(Guid)},
                {JTokenType.Integer, typeof(int)},
                {JTokenType.TimeSpan, typeof(DateTime)},
                {JTokenType.Uri, typeof(string)},
                {JTokenType.String, typeof(string)},
                // {JTokenType.Undefined, FIELDTYPE.Undefined},
                // {JTokenType.None, FIELDTYPE.Undefined},
                // {JTokenType.Null, FIELDTYPE.Undefined},
            };
        public FieldMapBuilder(FieldMapperConfig[] initial = null)
        {
            Map = initial != null ? initial.ToDictionary(f => f.Source) :
                new Dictionary<string, FieldMapperConfig>();
        }

        public static Dictionary<string, FieldMapperConfig> CreateMap(params string[] records)
            => new FieldMapBuilder().Process(records).Map;

        public static FieldMapBuilder Auto(string json, FieldMapperConfig[] config = null)
        {
            var builder = new FieldMapBuilder(config)
            {
                AutoMap = true,
                Values = new Dictionary<string, object>()
            };

            return builder.Process(json.AsEnumerable());
        }

        public static FieldMapBuilder Auto(JObject json, FieldMapperConfig[] config = null)
        {
            var builder = new FieldMapBuilder(config)
            {
                AutoMap = true,
                Values = new Dictionary<string, object>(),
                Prefix = json.Path + "."
            };

            builder.Process(json);

            return builder;
        }

        public FieldMapBuilder Process(IEnumerable<string> records)
        {
            foreach (var row in records)
            {
                dynamic body = JsonConvert.DeserializeObject(row);
                Process(body);
            }

            return this;
        }

        public List<FieldMapperConfig> Add(IEnumerable<string> records)
        {
            Process(records);
            return Map.Values.ToList();
        }

        private void Process(JToken token)
        {
            var val = token.Value<object>();

            switch (token.Type)
            {
                case JTokenType.Array:
                    // foreach ( var child in record.Children() ) {
                    //     Add(child, prefix);
                    // }
                    // System.Console.WriteLine("Array, ignore for now?");
                    break;

                case JTokenType.Object:
                    foreach (var child in token.Children())
                    {
                        Process(child);
                    }
                    break;

                case JTokenType.Property:
                    AddToMap(token);
                    break;

                default:
                    // System.Console.WriteLine($"Unexpected: {token.Path} {token.Type} {val}");
                    break;
            }
        }

        private void AddToMap(JToken property)
        {
            var value = property.Value<JProperty>();
            var type = value.Value.Type;

            if (type == JTokenType.Object)
            {
                Process(value.Value);
                return;
            }

            if (!_type.TryGetValue(type, out var fieldType))
            {
                // type not supported
                return;
            }

            if (Map.TryGetValue(property.Path, out var field))
            {
                // already mapped
                if (AutoMap)
                {
                    var natvalue = _native.TryGetValue(type, out var native) ? value.ToObject(native) : (value.Value.IsNull() ? null : value.Value);
                    if (natvalue != null) Values[field.Name] = natvalue;
                }

                if (fieldType == FIELDTYPE.Undefined)
                {
                    // TODO: mark as optional?
                    // ...
                    return;
                }

                if (field.Type == fieldType)
                {
                    // nothing to do 
                    return;
                }

                if (field.Type == FIELDTYPE.Undefined)
                {
                    // update type
                    field.Type = fieldType;
                    return;
                }

                // System.Console.WriteLine($"Type mismatch for {property.Path}: {field.Type} x {fieldType}");
                return;
            }

            // create field
            var fieldMap = new FieldMapperConfig
            {
                Source = property.Path,
                Type = _type[type]
            };

            if (AutoMap)
            {
                var source = (Prefix != null && fieldMap.Source.StartsWith(Prefix)) ?
                    fieldMap.Source.Substring(Prefix.Length) :
                    fieldMap.Source;

                fieldMap.Name = source.ToJSName();

                var natvalue = _native.TryGetValue(type, out var native) ? value.ToObject(native) : (value.Value.IsNull() ? null : value.Value);
                if (natvalue != null) Values[fieldMap.Name] = natvalue;
            }

            Map.Add(property.Path, fieldMap);
        }
    }
}