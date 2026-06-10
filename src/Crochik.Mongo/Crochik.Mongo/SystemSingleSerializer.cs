using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.IO;

namespace Crochik.Mongo
{

    /// <summary>
    /// Represents a serializer for Singles.
    /// </summary>
    public class CopySingleSerializer : StructSerializerBase<float>, IRepresentationConfigurable<CopySingleSerializer>, IRepresentationConverterConfigurable<CopySingleSerializer>
    {
        // private fields
        private readonly BsonType _representation;
        private readonly RepresentationConverter _converter;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="CopySingleSerializer"/> class.
        /// </summary>
        public CopySingleSerializer()
            : this(BsonType.Double)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopySingleSerializer"/> class.
        /// </summary>
        /// <param name="representation">The representation.</param>
        public CopySingleSerializer(BsonType representation)
            : this(representation, new RepresentationConverter(false, false))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopySingleSerializer"/> class.
        /// </summary>
        /// <param name="representation">The representation.</param>
        /// <param name="converter">The converter.</param>
        public CopySingleSerializer(BsonType representation, RepresentationConverter converter)
        {
            switch (representation)
            {
                case BsonType.Decimal128:
                case BsonType.Double:
                case BsonType.Int32:
                case BsonType.Int64:
                case BsonType.String:
                    break;

                default:
                    var message = string.Format("{0} is not a valid representation for a SingleSerializer.", representation);
                    throw new ArgumentException(message);
            }

            _representation = representation;
            _converter = converter;
        }

        // public properties
        /// <summary>
        /// Gets the converter.
        /// </summary>
        /// <value>
        /// The converter.
        /// </value>
        public RepresentationConverter Converter
        {
            get { return _converter; }
        }

        /// <summary>
        /// Gets the representation.
        /// </summary>
        /// <value>
        /// The representation.
        /// </value>
        public BsonType Representation
        {
            get { return _representation; }
        }

        // public methods
        /// <summary>
        /// Deserializes a value.
        /// </summary>
        /// <param name="context">The deserialization context.</param>
        /// <param name="args">The deserialization args.</param>
        /// <returns>A deserialized value.</returns>
        public override float Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            try
            {
                var bsonReader = context.Reader;

                var bsonType = bsonReader.GetCurrentBsonType();
                switch (bsonType)
                {
                    case BsonType.Decimal128:
                        return _converter.ToSingle(bsonReader.ReadDecimal128());

                    case BsonType.Double:
                        return _converter.ToSingle(bsonReader.ReadDouble());

                    case BsonType.Int32:
                        return _converter.ToSingle(bsonReader.ReadInt32());

                    case BsonType.Int64:
                        return _converter.ToSingle(bsonReader.ReadInt64());

                    case BsonType.String:
                        return JsonConvert.ToSingle(bsonReader.ReadString());

                    default:
                        throw CreateCannotDeserializeFromBsonTypeException(bsonType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// Serializes a value.
        /// </summary>
        /// <param name="context">The serialization context.</param>
        /// <param name="args">The serialization args.</param>
        /// <param name="value">The object.</param>
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, float value)
        {
            var bsonWriter = context.Writer;

            switch (_representation)
            {
                case BsonType.Decimal128:
                    bsonWriter.WriteDecimal128(_converter.ToDecimal128(value));
                    break;

                case BsonType.Double:
                    bsonWriter.WriteDouble(_converter.ToDouble(value));
                    break;

                case BsonType.Int32:
                    bsonWriter.WriteInt32(_converter.ToInt32(value));
                    break;

                case BsonType.Int64:
                    bsonWriter.WriteInt64(_converter.ToInt64(value));
                    break;

                case BsonType.String:
                    bsonWriter.WriteString(JsonConvert.ToString(value));
                    break;

                default:
                    var message = string.Format("'{0}' is not a valid Single representation.", _representation);
                    throw new BsonSerializationException(message);
            }
        }

        /// <summary>
        /// Returns a serializer that has been reconfigured with the specified item serializer.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <returns>The reconfigured serializer.</returns>
        public CopySingleSerializer WithConverter(RepresentationConverter converter)
        {
            if (converter == _converter)
            {
                return this;
            }
            else
            {
                return new CopySingleSerializer(_representation, converter);
            }
        }

        /// <summary>
        /// Returns a serializer that has been reconfigured with the specified representation.
        /// </summary>
        /// <param name="representation">The representation.</param>
        /// <returns>The reconfigured serializer.</returns>
        public CopySingleSerializer WithRepresentation(BsonType representation)
        {
            if (representation == _representation)
            {
                return this;
            }
            else
            {
                return new CopySingleSerializer(representation, _converter);
            }
        }

        // explicit interface implementations
        IBsonSerializer IRepresentationConverterConfigurable.WithConverter(RepresentationConverter converter)
        {
            return WithConverter(converter);
        }

        IBsonSerializer IRepresentationConfigurable.WithRepresentation(BsonType representation)
        {
            return WithRepresentation(representation);
        }
    }

    public class SystemSingleSerializer : SerializerBase<System.Single>
    {
        public SystemSingleSerializer()
        {
        }
        public override System.Single Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.GetCurrentBsonType() == BsonType.Null)
            {
                context.Reader.SkipValue();
                return default(System.Single);
            }

            var str = context.Reader.ReadString();
            if (string.IsNullOrEmpty(str)) return default(System.Single);

            if (System.Single.TryParse(str, out var single))
            {
                return single;
            }

            return default(System.Single);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, System.Single value)
        {
            if (value != System.Single.NaN)
            {
                if (value < System.Single.MinValue || value > System.Single.MaxValue)
                {
                    throw new Exception("Out of range Single");
                }

                context.Writer.WriteString(value.ToString());
            }
            else
            {
                context.Writer.WriteNull();
            }
        }
    }
}
