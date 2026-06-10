using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using PI.Shared.Exceptions;

namespace PI.Shared.Models
{
    public enum ValueType
    {
        Unknown,
        String,
        DateTime,
        Boolean,
        UUID, // TODO: since we serialize it by default as string, converting to Guid seems wrong
        ObjectId,
        Decimal,
        Int,
        Object,
    }

    public class BackingType
    {
        public static BackingType Unknown { get; } = new BackingType { ValueType = ValueType.Unknown };
        public static BackingType String { get; } = new BackingType { ValueType = ValueType.String };
        // public static BackingType UUID { get; } = new BackingType { ValueType = ValueType.UUID };
        // public static BackingType ObjectId { get; } = new BackingType { ValueType = ValueType.ObjectId };
        public static BackingType StringArray { get; } = new BackingType { ValueType = ValueType.String, IsArray = true };
        public static BackingType DateTime { get; } = new BackingType { ValueType = ValueType.DateTime };
        public static BackingType Boolean { get; } = new BackingType { ValueType = ValueType.Boolean };
        public static BackingType Int32 { get; } = new BackingType { ValueType = ValueType.Int, Length = 4 };
        public static BackingType Int64 { get; } = new BackingType { ValueType = ValueType.Int, Length = 8 };
        public static BackingType Decimal { get; } = new BackingType { ValueType = ValueType.Decimal, Length = 16 };
        public static BackingType DateRange { get; } = new BackingType { ValueType = ValueType.DateTime, IsArray = true};

        public bool IsArray { get; init; }
        public bool IsDictionary { get; init; }
        public int? Length { get; init; }
        public ValueType ValueType { get; init; }

        public object AutoConvert(object newValue)
        {
            if (IsArray)
            {
                if (newValue is IEnumerable enumerable and not string)
                {
                    return ValueType switch
                    {
                        ValueType.String => AutoConvertEnumerable<string>(enumerable).ToArray(),
                        ValueType.DateTime => AutoConvertEnumerable<DateTime>(enumerable).ToArray(),
                        ValueType.UUID => AutoConvertEnumerable<Guid>(enumerable).ToArray(),
                        _ => AutoConvertEnumerable(enumerable, ValueType).ToArray(),
                    };
                }

                // convert (single) value and make into array 
                var converted = AutoConvert(newValue, ValueType);
                if (converted == null) return null;
                return converted is IEnumerable array and not string ? array : new[] { converted };
            }

            return AutoConvert(newValue, ValueType);
        }

        private static IEnumerable<T> AutoConvertEnumerable<T>(IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return PropertyValueConverter.ConvertTo<T>(item);
            }
        }

        private static IEnumerable<object> AutoConvertEnumerable(IEnumerable enumerable, ValueType valueType)
        {
            foreach (var item in enumerable)
            {
                yield return AutoConvert(item, valueType);
            }
        }

        private static object AutoConvert(object item, ValueType valueType)
        {
            if (item == null) return item;

            return valueType switch
            {
                ValueType.String => PropertyValueConverter.ConvertTo<string>(item),
                ValueType.DateTime => PropertyValueConverter.ConvertTo<DateTime>(item),
                ValueType.UUID => PropertyValueConverter.ConvertTo<Guid>(item), // TODO: ????

                ValueType.Decimal => PropertyValueConverter.ConvertTo<decimal>(item),
                ValueType.Int => PropertyValueConverter.ConvertTo<int>(item),
                ValueType.Boolean => PropertyValueConverter.ConvertTo<bool>(item),

                ValueType.ObjectId => item switch
                {
                    string str => Guid.TryParse(str, out var uuid) && uuid.TryGetObjectId(out var id) ? id : throw new BadRequestException("Invalid format for ObjectId value"),
                    Guid uuid => uuid.TryGetObjectId(out var id) ? id : throw new BadRequestException("Invalid format for ObjectId value"),
                    _ => item,    
                },
                
                // ...
                _ => item,
            };
        }
    }

    /// <summary>
    /// Attempt to handle "complex"/objects embedded in other objects
    /// </summary>
    public class ObjectBackingType : BackingType 
    {
        public string ObjectType { get; set; }

        public ObjectBackingType() 
        {
            ValueType = ValueType.Object;
        }
    }
}