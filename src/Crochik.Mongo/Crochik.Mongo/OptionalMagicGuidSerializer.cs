using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using MongoDB.Bson;

namespace Crochik.Mongo;

public class OptionalMagicGuidSerializer : SerializerBase<Guid?>
{
    public override Guid? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        switch (context.Reader.CurrentBsonType)
        {
            case BsonType.Null:
                context.Reader.SkipValue();
                return null;
                    
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

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid? value)
    {
        if (value.HasValue)
        {
            if (value.Value.TryGetObjectId(out var objectId))
            {
                context.Writer.WriteObjectId(objectId);
                return;
            }

            context.Writer.WriteString(value.ToString());
        }
        else
        {
            context.Writer.WriteNull();
        }
    }
}