using System;

namespace Crochik.Data
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public string Name { get; set; }
        public Type Model { get; set; }
        public bool IncludeAll { get; set; } = false;

        public TableAttribute(string name = null)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public class NotAFieldAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public class FieldAttribute : Attribute
    {
        private string _property = null;
        public string Name { get; set; }
        public string Property
        {
            get { return _property ?? Name; }
            set { _property = value; }
        }
        public bool IsKey { get; set; }
        public FieldAttribute(string name = null, string property = null, bool isKey = false)
        {
            Name = name;
            _property = property;
            IsKey = isKey;
        }
    }
}