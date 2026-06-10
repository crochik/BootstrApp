using System;
using System.Collections.Generic;
using System.Linq;

namespace Crochik.Messaging
{
    public interface ITypeMapper
    {
        Type this[string key] { get; }
    }

    public class TypeMapper : ITypeMapper
    {
        private Dictionary<string, Type> _map = new Dictionary<string, Type>();

        public Type this[string key]
        {
            get
            {
                if (_map.TryGetValue(key, out var value)) return value;
                return null;
            }
            set
            {
                _map[key] = value;
            }
        }

        public void Register<T>() => Register(typeof(T));
        public void Register(Type type) => _map[type.FullName] = type;
        public void RegisterAll<T>() 
        {
            Register<T>();

            foreach (Type type in
                typeof(T).Assembly.GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && typeof(T).IsAssignableFrom(myType)))
            {
                Register(type);
            }            
        }
    }
}