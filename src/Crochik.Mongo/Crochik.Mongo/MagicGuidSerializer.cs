using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using MongoDB.Bson;

namespace Crochik.Mongo
{
    /*
    public class GuidAsStringSerializer : SerializerBase<Guid>
    {
        public override Guid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var str = context.Reader.ReadString();
            return Guid.Parse(str);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid value)
        {
            context.Writer.WriteString(value.ToString());
        }
    }
    */

    public class MagicGuidSerializer : SerializerBase<Guid>
    {
        public override Guid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            switch (context.Reader.CurrentBsonType)
            {
                case BsonType.String:
                    var str = context.Reader.ReadString();
                    return Guid.Parse(str);

                case BsonType.ObjectId:
                    var uuid = context.Reader.ReadObjectId();
                    return uuid.ToGuid();

                default:
                    throw new Exception($"Can't convert {context.Reader.CurrentBsonType} to Guid");
            }
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid value)
        {
            if (value.TryGetObjectId(out var objectId))
            {
                context.Writer.WriteObjectId(objectId);
                return;
            }

            context.Writer.WriteString(value.ToString());
        }
    }
}
