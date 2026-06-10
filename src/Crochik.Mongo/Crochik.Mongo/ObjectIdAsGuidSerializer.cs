using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using MongoDB.Bson;

namespace Crochik.Mongo
{
    public class ObjectIdAsGuidSerializer : SerializerBase<Guid>
    {
        public override Guid Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            ObjectId uuid;
            switch (context.Reader.CurrentBsonType)
            {
                case BsonType.String:
                    var str = context.Reader.ReadString();
                    uuid = ObjectId.Parse(str);
                    break;

                case BsonType.ObjectId:
                    uuid = context.Reader.ReadObjectId();
                    break;

                default:
                    throw new Exception($"Can't convert {context.Reader.CurrentBsonType} to Guid");
            }

            return uuid.ToGuid();
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid value)
        {
            if (value.TryGetObjectId(out var objectId))
            {
                context.Writer.WriteObjectId(objectId);
                return;
            }

            throw new Exception($"Can't serialize {value} as ObjectId");
        }
    }
}
