using System;

namespace MongoDB.Bson;

public static class ObjectIdExtensions
{
    public static object BestEffortAsSerializedId(object value)
    {
        return value switch
        {
            string str => Guid.TryParse(str, out var guid) ? guid.AsSerializedId() : str,
            Guid uid => uid.AsSerializedId(),
            _ => value,
        };
    }
    
    public static bool TryGetObjectId(this Guid id, out ObjectId objectId)
    {
        var str = id.ToString("N");

        if (str.Substring(0, 8) != "00000000")
        {
            objectId = ObjectId.Empty;
            return false;
        }

        objectId = ObjectId.Parse(str.Substring(8));
        return true;
    }

    public static Guid ToGuid(this ObjectId id)
    {
        var str = id.ToString();
        str = "00000000" + str;
        return Guid.Parse(str);
    }

    public static ObjectId ToObjectId(this Guid value)
    {
        if (value.TryGetObjectId(out var objectId)) return objectId;

        throw new Exception("Can't convert GUID to ObjectId");
    }

    public static object AsSerializedId(this Guid? id)
        => id.HasValue ? id.Value.AsSerializedId() : null;

    public static object AsSerializedId(this Guid id)
    {
        if (id.TryGetObjectId(out var objectId))
        {
            return objectId;
        }

        // Guid (as string by convention)
        return id.ToString();
    }

    public static Guid? ToOptionalObjectId(this string idStr)
        => string.IsNullOrEmpty(idStr) ? default(Guid?) : idStr.ToObjectId();

    public static Guid ToObjectId(this string idStr)
        => idStr.Length switch
        {
            24 => ObjectId.Parse(idStr).ToGuid(),
            _ => Guid.Parse(idStr),
        };

    public static bool TryToParseObjectId(this string idStr, out Guid guid)
    {
        if (idStr.Length == 24)
        {
            if (ObjectId.TryParse(idStr, out ObjectId objId))
            {
                guid = objId.ToGuid();
                return true;
            }

            guid = Guid.Empty;
            return false;
        };

        return Guid.TryParse(idStr, out guid);
    }
    
    public static bool TryToParseObjectId(this object idObj, out Guid guid)
    {
        var objectId = idObj switch
        {
            ObjectId oid => oid.ToGuid(),
            Guid uuid => uuid,
            string str => str.TryToParseObjectId(out var uuid) ? uuid : default(Guid?),
            _ => null,
        };

        guid = objectId ?? Guid.Empty;
        return objectId.HasValue;
    }

}