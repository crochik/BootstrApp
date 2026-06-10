using System;
using System.Globalization;
using System.Linq;

namespace PI.ProductCatalog
{
    public delegate void ValueSetter(object value, CatalogParserContext context);

    public class TokenResult
    {
        public string Message { get; set; }
        public object Value { get; set; }
        public bool Failed { get; set; }
        public string Name { get; internal set; }
        public int Index { get; internal set; }
    }

    public class Token
    {
        public string Name { get; set; }
        public bool IsMandatory { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public TokenType Type { get; set; }
        public ValueSetter Setter { get; set; }
        public object Default { get; set; }

        protected Token()
        {
        }

        public Token(string name, int? minLength, int? maxLength, TokenType type, bool isMandatory, ValueSetter setter = null, object defaultValue = null)
        {
            Name = name;
            IsMandatory = isMandatory;
            MinLength = minLength;
            MaxLength = maxLength.HasValue ? maxLength : minLength;
            Type = type;
            Setter = setter;
            Default = defaultValue;
        }

        public static Token ID(string name, int? minLength, int? maxLength = null, bool isMandatory = true, ValueSetter setter = null, object defaultValue = null)
            => new(name, minLength, maxLength, TokenType.ID, isMandatory, setter, defaultValue);

        public static Token AN(string name, int? minLength, int? maxLength = null, bool isMandatory = true, ValueSetter setter = null, object defaultValue = null)
            => new(name, minLength, maxLength, TokenType.AN, isMandatory, setter, defaultValue);

        public static Token R(string name, int? minLength, int? maxLength = null, bool isMandatory = true, ValueSetter setter = null, object defaultValue = null)
            => new(name, minLength, maxLength, TokenType.R, isMandatory, setter, defaultValue);

        public static Token N0(string name, int? minLength, int? maxLength = null, bool isMandatory = true, ValueSetter setter = null, int? defaultValue = null)
            => new IntToken(name, minLength, maxLength, isMandatory, setter, defaultValue);

        public static Token Const(string name, string value, bool isMandatory = true)
            => new ConstToken(name, value, isMandatory);

        public static Token Date(string name, ValueSetter setter = null, params string[] formats)
            => new DateToken(name, setter, formats);

        public virtual TokenResult Parse(int c, string value)
        {
            var result = new TokenResult
            {
                Name = Name,
                Index = c,
                Value = value
            };

            if (value == null)
            {
                if (IsMandatory)
                {
                    if (Default != null)
                    {
                        result.Message = $"Missing mandatory element #{c}, using {Default}";
                        result.Value = Default;
                    }
                    else
                    {
                        result.Message = $"Missing mandatory element #{c}";
                        result.Failed = result.Value != null;
                    }
                }

                return result;
            }

            if (MinLength.HasValue && (value == null || value.Length < MinLength.Value))
            {
                result.Message = $"Invalid Element length, expected >={MinLength}, got {value?.Length}";
            }
            else if (MaxLength.HasValue && value?.Length > MaxLength.Value)
            {
                result.Message = $"Invalid Element length, expected <={MaxLength}, got {value?.Length}";
            }

            result.Value = Type switch
            {
                TokenType.R => decimal.Parse(value),
                _ => value?.Trim(),
            };

            return result;
        }

        private class ConstToken : Token
        {
            public string Value { get; set; }

            public ConstToken(string name, string value, bool isMandatory = true) : base(name, value.Length, value.Length, TokenType.AN, true)
            {
                Value = value;
                IsMandatory = isMandatory;
            }

            public override TokenResult Parse(int c, string value)
            {
                var result = new TokenResult
                {
                    Name = Name,
                    Index = c,
                    Value = Value
                };

                if (value == null)
                {
                    if (IsMandatory)
                    {
                        result.Message = $"Missing mandatory element #{c}, using {Value}";
                    }

                    return result;
                }

                if (!string.Equals(value, Value))
                {
                    result.Message = $"Invalid value for element #{c}, expected '{Value}', got {value}";
                    result.Failed = true;
                }

                return result;
            }
        }

        private class IntToken : Token
        {
            public IntToken(string name, int? minLength, int? maxLength, bool isMandatory, ValueSetter setter, int? defaultValue)
            {
                Name = name;
                MinLength = minLength;
                MaxLength = maxLength;
                IsMandatory = isMandatory;
                Setter = setter;
                Type = TokenType.N0;
                Default = defaultValue;
            }

            public override TokenResult Parse(int c, string value)
            {
                var result = base.Parse(c, value);
                if (result.Failed || result.Message != null) return result;

                result.Value = result.Value switch
                {
                    int i => i,
                    decimal d => (int)d,
                    string s => int.Parse(s),
                    object other => int.Parse(other.ToString()),
                    _ => null,
                };

                if (result.Value == null)
                {
                    result.Message = $"Can't convert value to int: '{value}'";
                    result.Failed = true;
                }

                return result;
            }
        }

        private class DateToken : Token
        {
            private static CultureInfo Provider { get; } = CultureInfo.InvariantCulture;

            public string[] Formats { get; }

            public DateToken(string name, ValueSetter setter = null, params string[] formats)
            {
                Name = name;
                Formats = formats;
                Setter = setter;

                foreach (var format in formats)
                {
                    if (!MinLength.HasValue || MinLength.Value > format.Length) MinLength = format.Length;
                    if (!MaxLength.HasValue || MaxLength.Value < format.Length) MaxLength = format.Length;
                    if (format.StartsWith("yy", StringComparison.OrdinalIgnoreCase)) Type = TokenType.DT;
                    if (format.StartsWith("HH", StringComparison.OrdinalIgnoreCase)) Type = TokenType.TM;
                }
            }

            public override TokenResult Parse(int c, string value)
            {
                var result = base.Parse(c, value);
                if (result.Failed || result.Message != null) return result;

                var newValue = result.Value?.ToString();
                if (!string.IsNullOrEmpty(newValue))
                {
                    foreach (var format in Formats.Where(x => x.Length == newValue.Length))
                    {
                        if (DateTime.TryParseExact(newValue, format, Provider, DateTimeStyles.None, out var date))
                        {
                            result.Value = date;
                            return result;
                        }
                    }
                }

                result.Message = $"Unexpected format: '{value}'";
                result.Failed = true;
                return result;
            }
        }
    }
}