using System;

namespace PI.Shared.Models
{
    public class PropertyValueConverter
    {
        public class CantConvertException : Exception
        {
            public object Value { get; }
            public Type Type { get; }

            public CantConvertException(object value, Type type, Exception ex = null) :
                base($"Can't convert {value} to {type.Name}", ex)
            {
                Value = value;
                Type = type;
            }
        }

        /// <summary>
        /// Test is value is of the right type
        /// </summary>
        private static bool IsAssignableFrom<T>(object value, out T converted)
        {
            converted = default;
            if (value == null)
            {
                if (converted == null)
                {
                    return true;
                }

                return false;
            }

            if (typeof(T).IsAssignableFrom(value.GetType()))
            {
                converted = (T)value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Convert value into type or throw exception, if it can't
        /// </summary>
        /// <exception cref="CantConvertException"></exception>
        public static T ConvertTo<T>(object value, IFormatProvider provider = null)
        {
            var type = typeof(T);

            if (!IsAssignableFrom(value, out T converted))
            {
                if (value is not IConvertible convertible)
                {
                    if (type == typeof(string))
                    {
                        value = value.ToString();
                    }
                    else
                    {
                        throw new CantConvertException(value, type);
                    }
                }
                else
                {
                    value = convert(convertible);
                }

                if (!IsAssignableFrom(value, out converted))
                {
                    throw new CantConvertException(value, type);
                }
            }

            return converted;

            object convert(IConvertible convertible)
            {
                try
                {
                    if (type == typeof(bool) || type == typeof(bool?)) return convertible.ToBoolean(provider);
                    if (type == typeof(byte) || type == typeof(byte?)) return convertible.ToByte(provider);
                    if (type == typeof(char) || type == typeof(char?)) return convertible.ToChar(provider);
                    if (type == typeof(DateTime) || type == typeof(DateTime?)) return convertible.ToDateTime(provider);
                    if (type == typeof(decimal) || type == typeof(decimal?)) return convertible.ToDecimal(provider);
                    if (type == typeof(double) || type == typeof(double?)) return convertible.ToDouble(provider);
                    if (type == typeof(short) || type == typeof(short?)) return convertible.ToInt16(provider);
                    if (type == typeof(int) || type == typeof(int?)) return convertible.ToInt32(provider);
                    if (type == typeof(long) || type == typeof(long?)) return convertible.ToInt64(provider);
                    if (type == typeof(sbyte) || type == typeof(sbyte?)) return convertible.ToSByte(provider);
                    if (type == typeof(float) || type == typeof(float?)) return convertible.ToSingle(provider);
                    if (type == typeof(string)) return convertible.ToString(provider);
                    if (type == typeof(ushort) || type == typeof(ushort?)) return convertible.ToUInt16(provider);
                    if (type == typeof(uint) || type == typeof(uint?)) return convertible.ToUInt32(provider);
                    if (type == typeof(ulong) || type == typeof(ulong?)) return convertible.ToUInt64(provider);

                    if (type == typeof(Guid) || type == typeof(Guid?)) return Guid.Parse(value.ToString());
                    if (type == typeof(Uri)) return new Uri(value.ToString());

                    return convertible.ToType(type, provider);
                }
                catch (Exception ex)
                {
                    throw new CantConvertException(value, type, ex);
                }
            }
        }
    }
}