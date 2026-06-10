using MongoDB.Bson.Serialization;
using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Crochik.Mongo
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class BsonArraySerializationOptionsAttribute : BsonSerializationOptionsAttribute
    {
        private readonly IBsonSerializer _itemSerializer;

        public BsonArraySerializationOptionsAttribute(Type itemSerializerType)
        {
            this._itemSerializer = Activator.CreateInstance(itemSerializerType) as IBsonSerializer;
            if (this._itemSerializer == null) throw new Exception("Invalid Item Serializer Type");
        }

        // protected methods
        /// <summary>
        /// Reconfigures the specified serializer by applying this attribute to it.
        /// </summary>
        /// <param name="serializer">The serializer.</param>
        /// <returns>A reconfigured serializer.</returns>
        protected override IBsonSerializer Apply(IBsonSerializer serializer)
        {
            var childSerializer = serializer as IChildSerializerConfigurable;
            if (childSerializer != null)
            {
                return childSerializer.WithChildSerializer(_itemSerializer);
            }

            return base.Apply(serializer);
        }
    }
}
