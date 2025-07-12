using System.Collections.Concurrent;
using System.Reflection;

namespace FastAop.Cache
{
    internal class FastAopCache
    {
        private static ConcurrentDictionary<string, MethodInfo> cache = new ConcurrentDictionary<string, MethodInfo>();

        internal static MethodInfo Get(string key)
        {
            if (cache.ContainsKey(key))
                return cache[key];
            else
            {
                return null;
            }
        }

        internal static void Set(string key, MethodInfo method)
        {
            if (!cache.ContainsKey(key))
            {
                cache.TryAdd(key, method);
            }
            else
            {
                cache.TryRemove(key, out _);
                cache.TryAdd(key, method);
            }
        }
    }
}
