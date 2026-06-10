using System;

namespace Crochik.Mongo
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class BsonCollectionAttribute : Attribute
    {
        public string Name { get; }

        public BsonCollectionAttribute(string name)
        {
            Name = name;
        }
    }
}
