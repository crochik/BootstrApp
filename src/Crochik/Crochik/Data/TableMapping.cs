using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Crochik.Data
{
    public class FieldInfo
    {
        public string Name => PropertyInfo.Name;
        public string SQLType { get; set; }
        public bool IsNullable { get; set; } = false;
        public int MaxLength { get; set; } = 0;
        public PropertyInfo PropertyInfo { get; }

        public FieldInfo(PropertyInfo prop)
        {
            this.PropertyInfo = prop;
        }
    }

    public class TableMapping
    {
        public static Dictionary<Type, TableMapping> Cache { get; } = new Dictionary<Type, TableMapping>();

        public string Name { get; set; }
        public Type Model { get; set; }
        public Dictionary<string, FieldInfo> Fields { get; set; }
        public string[] Keys { get; set; }
        public string Key
        {
            get
            {
                if (Keys == null) return null;
                if (Keys.Length == 1) return Keys[0];
                return Keys.Aggregate((val, prev) => prev + "," + val);
            }
        }

        public string CreateSQL
        {
            get
            {
                var str = new StringBuilder();

                str.AppendLine($"CREATE TABLE [{Name}] (");
                var fields = Fields.Values.ToArray();

                for (var c = 0; c < fields.Length; c++)
                {
                    var _field = fields[c];
                    var nullable = _field.IsNullable ? "NULL" : "NOT NULL";
                    var end = c == fields.Length - 1 ? "" : ",";
                    str.AppendLine($"[{_field.Name}] [{_field.SQLType}] {nullable}{end}");
                }

                if (Keys.Length > 0)
                {
                    str.AppendLine($"CONSTRAINT [PK_{Name}] PRIMARY KEY (");
                    foreach (var key in Keys)
                    {
                        str.AppendLine($"[{key}] ASC");
                    }
                    str.AppendLine(")");
                }

                str.AppendLine(")");

                return str.ToString();
            }
        }
        public static TableMapping Create(Type type)
        {
            if (Cache.TryGetValue(type, out var cached)) return cached;

            var keys = new List<string>();
            var table = Attribute.GetCustomAttribute(type, typeof(TableAttribute)) as TableAttribute;

            var mapping = new TableMapping
            {
                Name = table.Name ?? type.Name,
                Model = table.Model ?? type,
                Fields = new Dictionary<string, FieldInfo>(),
            };

            foreach (var prop in mapping.Model.GetProperties())
            {
                var field = Attribute.GetCustomAttribute(prop, typeof(FieldAttribute)) as FieldAttribute;
                if (field != null || table.IncludeAll)
                {
                    if (table.IncludeAll && Attribute.IsDefined(prop, typeof(NotAFieldAttribute)))
                    {
                        continue;
                    }

                    var fieldInfo = GetFieldInfo(prop);
                    if (fieldInfo == null) continue;

                    var name = field?.Name ?? prop.Name;
                    if (field != null && field.IsKey)
                    {
                        keys.Add(name);
                        fieldInfo.IsNullable = false; // keys can't be nullable
                    }

                    mapping.Fields.Add(name, fieldInfo);
                }
            }

            foreach (var attrib in Attribute.GetCustomAttributes(type, typeof(FieldAttribute)))
            {
                var field = (FieldAttribute)attrib;
                if (field.Name == null)
                {
                    throw new Exception($"{type.FullName}: Field name is required when adding at the class level");
                }
                var prop = mapping.Model.GetProperty(field.Property);
                if (prop == null) throw new Exception($"{type.FullName}.{field.Name}, Can't find property {field.Property} in model {mapping.Model.FullName}");

                var fieldInfo = GetFieldInfo(prop);
                if (fieldInfo == null) continue;
                if (field.IsKey)
                {
                    keys.Add(field.Name);
                    fieldInfo.IsNullable = false; // keys can't be nullable
                }

                mapping.Fields.Add(field.Name, fieldInfo);

            }

            if (keys.Count > 0) mapping.Keys = keys.ToArray();

            Cache[type] = mapping;

            return mapping;
        }

        public override string ToString()
        {
            var str = new StringBuilder();

            str.AppendLine($"Table: {Name}");
            str.AppendLine($"Model: {Model.FullName}");
            str.AppendLine($"Key:   {Key}");
            str.AppendLine("Fields:");
            foreach (var field in Fields)
            {
                var nullable = field.Value.IsNullable ? "NULL" : "NOT NULL";
                str.AppendLine($" - {field.Key}: {field.Value.Name} ({field.Value.SQLType} {nullable})");
            }

            return str.ToString();
        }

        private static FieldInfo GetFieldInfo(PropertyInfo prop)
        {
            Type type = prop.PropertyType;
            var info = new FieldInfo(prop);

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                info.IsNullable = true;
                type = type.GenericTypeArguments[0];
            }

            if (type.IsEnum)
            {
                // string or int?
                // ...
                info.SQLType = "INT";
            }
            else if (typeof(DateTime).IsAssignableFrom(type))
            {
                info.SQLType = "SMALLDATETIME";
            }
            else if (typeof(bool).IsAssignableFrom(type))
            {
                info.SQLType = "BIT";
            }
            else if (typeof(string).IsAssignableFrom(type))
            {
                info.SQLType = "VARCHAR";
                info.IsNullable = true;
            }
            else if (type.IsClass)
            {
                // TODO: allow to automatically convert into json?
                System.Console.WriteLine($"!!! Property {prop.Name} is a Class. SKIP...");
                return null;
            }
            else
            {
                info.SQLType = type.Name;
            }

            return info;
        }

        public static List<Type> GetTables()
        {
            var list = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                list.AddRange(GetTables(assembly));
            }

            return list;
        }

        public static IEnumerable<Type> GetTables(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(TableAttribute), true).Length > 0)
                {

                    yield return type;
                }
            }
        }

        public static List<TableMapping> MapAll()
        {
            return GetTables().ConvertAll((table) => Create(table));
        }
    }
}