using System;
using System.Collections.Generic;

namespace FastAop.Cache
{
    internal class FastAopCache
    {
        private static Dictionary<string, Type> cache = new Dictionary<string, Type>();

        public static Type GetType(string key)
        {
            if (cache.ContainsKey(key))
                return cache[key];
            else
            {
                cache.Add(key, Type.GetType(key));
                return cache[key];
            }
        }
    }
}
