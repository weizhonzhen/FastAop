using System.Collections.Generic;
using System.Reflection;

namespace FastAop.Core.Cache
{
    internal class FastAopCache
    {
        private static Dictionary<string, MethodInfo> cache = new Dictionary<string, MethodInfo>();

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
                cache.Add(key, method);
            }
            else
            {
                cache.Remove(key);
                cache.Add(key, method);
            }
        }
    }
}
