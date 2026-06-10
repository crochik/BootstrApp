using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Bson;

namespace Crochik.Mongo
{
    public class EncryptedStringSerializer : SerializerBase<string>
    {
        private readonly IDataProtector _protector;

        public EncryptedStringSerializer(IDataProtector protector)
        {
            this._protector = protector;
        }

        public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.GetCurrentBsonType() == BsonType.Null)
            {
                context.Reader.SkipValue();
                return null;
            }

            var crypto = context.Reader.ReadString();
            return crypto == null ? null : _protector.Unprotect(crypto);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
        {
            if (value != null)
            {
                var crypto = _protector.Protect(value);
                context.Writer.WriteString(crypto);
            }
            else
            {
                context.Writer.WriteNull();
            }
        }
    }

    public class EncryptedStringSerializer<T> : SerializerBase<string>
    {
        public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.GetCurrentBsonType() == BsonType.Null)
            {
                context.Reader.SkipValue();
                return null;
            }

            var crypto = context.Reader.ReadString();
            return crypto == null ? null : DataProtectorCache.Get<T>().Unprotect(crypto);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
        {
            if (value != null)
            {
                var crypto = DataProtectorCache.Get<T>().Protect(value);
                context.Writer.WriteString(crypto);
            }
            else
            {
                context.Writer.WriteNull();
            }
        }
    }
}
