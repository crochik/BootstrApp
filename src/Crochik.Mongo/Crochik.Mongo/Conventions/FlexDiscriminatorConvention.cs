using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Crochik.Mongo.Conventions;

/// <summary>
/// HACKED VERSION of StandardDiscriminator Convention so we can handle not finding discriminator value and fallback to base class.
/// Represents the standard discriminator conventions (see ScalarDiscriminatorConvention and HierarchicalDiscriminatorConvention).
/// </summary>
public class FlexDiscriminatorConvention : IDiscriminatorConvention
{
    private static readonly FlexDiscriminatorConvention _instance = new("_t");

    /// <summary>
    /// Gets an instance of the HierarchicalDiscriminatorConvention.
    /// </summary>
    public static FlexDiscriminatorConvention Instance
    {
        get { return _instance; }
    }

    // private fields
    private readonly string _elementName;

    // constructors
    /// <summary>
    /// Initializes a new instance of the StandardDiscriminatorConvention class.
    /// </summary>
    /// <param name="elementName">The element name.</param>
    protected FlexDiscriminatorConvention(string elementName)
    {
        if (elementName == null)
        {
            throw new ArgumentNullException(nameof(elementName));
        }
        if (elementName.IndexOf('\0') != -1)
        {
            throw new ArgumentException("Element names cannot contain nulls.", nameof(elementName));
        }
        _elementName = elementName;
    }

    // public properties
    /// <summary>
    /// Gets the discriminator element name.
    /// </summary>
    public string ElementName
    {
        get { return _elementName; }
    }

    // public methods
    /// <summary>
    /// Gets the actual type of an object by reading the discriminator from a BsonReader.
    /// </summary>
    /// <param name="bsonReader">The reader.</param>
    /// <param name="nominalType">The nominal type.</param>
    /// <returns>The actual type.</returns>
    public Type GetActualType(IBsonReader bsonReader, Type nominalType)
    {
        // the BsonReader is sitting at the value whose actual type needs to be found
        var bsonType = bsonReader.GetCurrentBsonType();
        if (bsonType == BsonType.Document)
        {
            var bookmark = bsonReader.GetBookmark();
            bsonReader.ReadStartDocument();
            var actualType = nominalType;
            if (bsonReader.FindElement(_elementName))
            {
                var context = BsonDeserializationContext.CreateRoot(bsonReader);
                var discriminator = BsonValueSerializer.Instance.Deserialize(context);
                if (discriminator.IsBsonArray)
                {
                    discriminator = discriminator.AsBsonArray.Last(); // last item is leaf class discriminator
                }

                try
                {
                    actualType = BsonSerializer.LookupActualType(nominalType, discriminator);
                }
                catch (BsonSerializationException)
                {
                    // CROCHIK: ignore exception and use nominaltype
                }
            }
            bsonReader.ReturnToBookmark(bookmark);
            return actualType;
        }

        return nominalType;
    }

    // HACKED version from HierarchicalDiscriminatorConvention
    public BsonValue GetDiscriminator(Type nominalType, Type actualType)
    {
        // TODO: this isn't quite right, not all classes are serialized using  a class map serializer
        var classMap = BsonClassMap.LookupClassMap(actualType);
        if (actualType != nominalType || classMap.DiscriminatorIsRequired || classMap.HasRootClass)
        {
            if (classMap.HasRootClass && !classMap.IsRootClass)
            {
                var values = new List<BsonValue>();
                for (; !classMap.IsRootClass; classMap = classMap.BaseClassMap)
                {
                    values.Add(classMap.Discriminator);
                }
                values.Add(classMap.Discriminator); // add the root class's discriminator
                return new BsonArray(values.Reverse<BsonValue>()); // reverse to put leaf class last
            }
            else
            {
                return classMap.Discriminator;
            }
        }

        return null;
    }
}