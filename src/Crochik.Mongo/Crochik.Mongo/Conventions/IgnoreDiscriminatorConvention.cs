using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Conventions;

namespace Crochik.Mongo.Conventions;

public class IgnoreDiscriminatorConvention : IDiscriminatorConvention
{
    public static IgnoreDiscriminatorConvention Instance { get; }
        = new IgnoreDiscriminatorConvention();

    public string ElementName => "_t";

    public Type GetActualType(IBsonReader bsonReader, Type nominalType)
    {
        return nominalType;
    }

    public BsonValue GetDiscriminator(Type nominalType, Type actualType)
    {
        return null;
    }
}
