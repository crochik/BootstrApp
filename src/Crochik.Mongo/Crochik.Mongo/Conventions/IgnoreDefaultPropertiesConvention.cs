using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace Crochik.Mongo.Conventions
{
    public class IgnoreDefaultPropertiesConvention : IMemberMapConvention
    {
        public string Name => "Ignore default properties for all classes";

        public void Apply(BsonMemberMap memberMap)
        {
            memberMap.SetIgnoreIfDefault(true);
        }
    }

    public class IgnoreDefaultPropertiesConvention<T> : IMemberMapConvention
    {
        public string Name => $"Ignore Default Properties for {typeof(T)}";

        public void Apply(BsonMemberMap memberMap)
        {
            if (typeof(T) == memberMap.ClassMap.ClassType)
            {
                memberMap.SetIgnoreIfDefault(true);
            }
        }
    }
}