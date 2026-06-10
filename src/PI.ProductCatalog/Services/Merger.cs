using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog;

public class PropertyUpdateObjectSerializer : ClassSerializerBase<PropertyUpdate>
{
    private static readonly IBsonSerializer<object> _objectSerializer = BsonSerializer.LookupSerializer<object>();
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, PropertyUpdate value)
    {
        if (value is PropertyUpdate propertyUpdate)
        {
            if (propertyUpdate.Name == null) throw new Exception("Missing required value for Name property");
            context.Writer.WriteStartDocument();
            context.Writer.WriteName(nameof(PropertyUpdate.Name));
            context.Writer.WriteString(propertyUpdate.Name);

            if (propertyUpdate.Previous != null)
            {
                context.Writer.WriteName(nameof(PropertyUpdate.Previous));
                var serializer = BsonSerializer.LookupSerializer(propertyUpdate.Previous.GetType());
                serializer.Serialize(context, args, propertyUpdate.Previous);
            }

            if (propertyUpdate.After != null)
            {
                context.Writer.WriteName(nameof(PropertyUpdate.After));
                var serializer = BsonSerializer.LookupSerializer(propertyUpdate.After.GetType());
                serializer.Serialize(context, args, propertyUpdate.After);
            }

            context.Writer.WriteEndDocument();
            return;
        }

        base.Serialize(context, args, value);
    }

    public override PropertyUpdate Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.GetCurrentBsonType() == BsonType.Null)
        {
            context.Reader.SkipValue();
            return null;
        }

        if (context.Reader.GetCurrentBsonType() != BsonType.Document)
        {
            return base.Deserialize(context, args);
        }

        var result = new PropertyUpdate();
        context.Reader.ReadStartDocument();
        while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            var name = context.Reader.ReadName();
            switch (name)
            {
                case nameof(PropertyUpdate.Name):
                    if (context.Reader.GetCurrentBsonType() != BsonType.String) throw new FormatException("Name must be string");
                    result.Name = context.Reader.ReadString();
                    break;

                case nameof(PropertyUpdate.Previous):
                    result.Previous = _objectSerializer.Deserialize(context);
                    break;

                case nameof(PropertyUpdate.After):
                    result.After = _objectSerializer.Deserialize(context);
                    break;

                default:
                    context.Reader.SkipValue();
                    break;
            }
        }

        context.Reader.ReadEndDocument();

        return result;
    }
}

[BsonSerializer(typeof(PropertyUpdateObjectSerializer))]
public class PropertyUpdate
{
    public string Name { get; set; }

    public object Previous { get; set; }

    public object After { get; set; }
}

public class Merger<TSource, TDest>
{
    public static MergeInfo _info = null;
    public static MergeInfo Info => _info ?? MergeInfo.Get<TSource, TDest>();

    public List<PropertyUpdate> Updates { get; } = new List<PropertyUpdate>();
    public TSource Source { get; }
    public TDest Result { get; }
    public bool UpdateTarget { get; }

    public static Merger<TSource, TDest> Merge(TSource source, TDest dest)
        => new Merger<TSource, TDest>(source, dest, true).Merge();

    private Merger(TSource source, TDest dest, bool updateTarget)
    {
        Source = source;
        Result = dest;
        UpdateTarget = updateTarget;
    }

    private Merger<TSource, TDest> Merge()
    {
        var info = Info;
        foreach (var prop in info.Mapped)
        {
            var attrib = prop.GetCustomAttribute<MergeNullAttribute>();
            var value = prop.GetValue(Source);
            if (value == null && attrib==null) continue;
            
            var current = info.Target[prop.Name].GetValue(Result);
            if (IsEqual(prop, value, current)) continue;
            
            Updates.Add(new PropertyUpdate
            {
                Name = prop.Name,
                Previous = current,
                After = value,
            });

            if (UpdateTarget)
            {
                info.Target[prop.Name].SetValue(Result, value);
            }
        }

        return this;
    }

    private bool IsEqual(PropertyInfo prop, object src, object dst)
    {
        // if (prop.PropertyType.IsArray)
        // {
        //     System.Console.WriteLine($"{prop.Name} is array");
        // }

        if (src is DateTime srcDate && dst is DateTime dstDate)
        {
            return srcDate.ToUniversalTime().Equals(dstDate.ToUniversalTime());
        }

        if (Nullable.GetUnderlyingType(prop.PropertyType) == typeof(DateTime))
        {
            var nullSrc = (DateTime?)src;
            var nullDst = (DateTime?)dst;
            if (nullSrc.HasValue != nullDst.HasValue) return false;
            if (!nullSrc.HasValue) return true;
            return nullSrc.Value.ToUniversalTime().Equals(nullDst.Value.ToUniversalTime());
        }

        if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && typeof(string) != prop.PropertyType)
        {
            return IsEqual(src as IEnumerable, dst as IEnumerable);
        }

        return src.Equals(dst);
    }

    private bool IsEqual(IEnumerable src, IEnumerable dst)
    {
        if (dst == null) return false;

        var eSrc = src.GetEnumerator();
        var eDst = dst.GetEnumerator();
        while (eSrc.MoveNext())
        {
            if (!eDst.MoveNext())
            {
                return false;
            }

            if (eSrc.Current == null)
            {
                if (eDst.Current != null)
                {
                    return false;
                }
                continue;
            }

            // recursive so we can handle other types?
            // ...
            if (!eSrc.Current.Equals(eDst.Current))
            {
                return false;
            }
        }

        if (eDst.MoveNext())
        {
            return false;
        }

        return true;
    }
}


public class MergeInfo
{
    public Dictionary<string, PropertyInfo> Source { get; private set; }
    public Dictionary<string, PropertyInfo> Target { get; private set; }
    public PropertyInfo[] Mapped { get; private set; }

    public static MergeInfo Get<TSource, TDest>()
    {
        var src = typeof(TSource).GetProperties().Where(x => x.CanRead);
        var dst = typeof(TDest).GetProperties().Where(x => x.CanWrite);

        var info = new MergeInfo
        {
            Source = src.ToDictionary(x => x.Name),
            Target = dst.ToDictionary(x => x.Name),
            Mapped = src.Intersect(dst, new PropertyInfoNameComparer()).ToArray(),
        };

        // foreach (var prop in info.Mapped.Where(x => typeof(IList).IsAssignableFrom(x.PropertyType)))
        // {
        //     if (prop.PropertyType.IsArray)
        //     {
        //         System.Console.WriteLine($">> Property {prop.Name} is an Array and won't be mapped");
        //     }
        //     else
        //     {
        //         System.Console.WriteLine($">> Property {prop.Name} is a List and won't be mapped");
        //     }
        // }

        // remove arrays for now
        info.Mapped = info.Mapped
            // .Where(x => !typeof(IList).IsAssignableFrom(x.PropertyType))
            .OrderBy(x => x.Name)
            .ToArray();

        return info;
    }
}

public class PropertyInfoNameComparer : IEqualityComparer<PropertyInfo>
{
    public bool Equals([AllowNull] PropertyInfo x, [AllowNull] PropertyInfo y) => string.Equals(x?.Name, y?.Name);
    public int GetHashCode([DisallowNull] PropertyInfo obj) => obj.Name.GetHashCode();
}