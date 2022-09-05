using System;
using System.Collections.Concurrent;

namespace FastAop.Cache
{
    internal class FastAopCache
    {
        private static ConcurrentDictionary<string, Type> cache = new ConcurrentDictionary<string, Type>();

        public static Type GetType(string key)
        {
            if (cache.ContainsKey(key))
                return cache[key];
            else
            {
                cache.TryAdd(key, Type.GetType(key));
                return cache[key];
            }
        }
    }
}
