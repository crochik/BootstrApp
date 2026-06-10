using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;

namespace Crochik.Mongo
{
    public class CustomObjectSerializer : ObjectSerializer
    {
        /// <summary>
        /// Starting with version 2.19 of the driver you have to opt in so a type can be serialized
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool SerializationAllowed(Type type)
        {
            if (DefaultAllowedTypes(type)) return true;
            if (type.FullName.StartsWith("PI.") && type.FullName.Contains(".Models.")) return true;

            Console.WriteLine($"{type.FullName} is not flagged as serializable");

            // for now, allow any
            return true;
        }

        public CustomObjectSerializer() : base(SerializationAllowed)
        {
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
             switch (value)
            {
                case decimal dec:
                    context.Writer.WriteDecimal128(dec);
                    return;

                case Guid guid:
                    if (guid.TryGetObjectId(out var objectId))
                    {
                        context.Writer.WriteObjectId(objectId);
                    }
                    else
                    {
                        context.Writer.WriteString(guid.ToString());
                    }

                    return;

                case Single single:
                    context.Writer.WriteDouble(single);
                    return;

                case JArray jArray:
                {
                    context.Writer.WriteStartArray();
                    foreach (var obj in jArray)
                    {
                        if (obj is JValue jValue)
                        {
                            Serialize(context, args, jValue.Value);
                            continue;
                        }

                        Serialize(context, args, obj);
                    }

                    context.Writer.WriteEndArray();
                    return;
                }

                case IDictionary<string, object> dict:
                {
                    context.Writer.WriteStartDocument();
                    foreach (var kvp in dict)
                    {
                        context.Writer.WriteName(kvp.Key);
                        Serialize(context, args, kvp.Value);
                    }

                    context.Writer.WriteEndDocument();
                    return;
                }

                case JObject jObject:
                {
                    context.Writer.WriteStartDocument();
                    foreach (var kvp in jObject)
                    {
                        context.Writer.WriteName(kvp.Key);
                        Serialize(context, args, kvp.Value);
                    }
                    context.Writer.WriteEndDocument();
                    return;
                }
                    
                case IEnumerable<object> objArray:
                {
                    context.Writer.WriteStartArray();
                    foreach (var obj in objArray)
                    {
                        Serialize(context, args, obj);
                    }

                    context.Writer.WriteEndArray();
                    return;
                }
                
                case DateTimeOffset dateTimeOffset:
                    base.Serialize(context, args, dateTimeOffset.UtcDateTime);
                    return;

                // case JValue jValue:
                // {
                //     switch (jValue.Type)
                //     {
                //         case JTokenType.Null:
                //             context.Writer.WriteNull();
                //             return;
                //         
                //         case JTokenType.Guid:
                //             Serialize(context, args, jValue.Value<Guid>());
                //             return;
                //         case JTokenType.Float:
                //             // ???
                //             Serialize(context, args, jValue.Value<float>());
                //             return;
                //         
                //         case JTokenType.String:
                //             base.Serialize(context, args, jValue.Value<string>());
                //             return;
                //         case JTokenType.Boolean:
                //             base.Serialize(context, args, jValue.Value<bool>());
                //             return;
                //         case JTokenType.Date:
                //             base.Serialize(context, args, jValue.Value<DateTime>());
                //             return;
                //         case JTokenType.Bytes:
                //             base.Serialize(context, args, jValue.Value<byte[]>());
                //             return;
                //         case JTokenType.Integer:
                //             base.Serialize(context, args, jValue.Value<int>());
                //             return;
                //         
                //         case JTokenType.Object:
                //             // TODO: create object and iterate over properties? 
                //             // ... 
                //             Serialize(context, args, jValue.Value);
                //             return;
                //         
                //     }
                //
                //     // will fail later 
                //     // ...
                //     break;
                // }
            }

            try
            {
                if (value is BsonValue bson)
                {
                    Serialize(context, args, BsonTypeMapper.MapToDotNetValue(bson));
                    return;
                }

                base.Serialize(context, args, value);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to serialize {value?.GetType().FullName}", ex);
            }
        }
    }
}