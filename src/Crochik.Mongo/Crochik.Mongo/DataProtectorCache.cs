using Microsoft.AspNetCore.DataProtection;
using System;
using System.Collections.Concurrent;

namespace Crochik.Mongo
{
    public static class DataProtectorCache
    {
        private static ConcurrentDictionary<Type, IDataProtector> _dictionary = new ConcurrentDictionary<Type, IDataProtector>();
        private static IDataProtectionProvider _provider;

        public static void Init(IDataProtectionProvider provider)
        {
            lock (_dictionary)
            {
                if (_provider != null) throw new Exception("Has already been initialized");
                _provider = provider;
            }
        }

        public static IDataProtector Get<T>()
        {
            if (_dictionary.TryGetValue(typeof(T), out var protector)) return protector;
            protector = _provider.CreateProtector(typeof(T).FullName);
            if (_dictionary.TryAdd(typeof(T), protector)) return protector;
            if (!_dictionary.TryGetValue(typeof(T), out protector)) throw new Exception("something is really wrong");
            return protector;
        }
    }
}
