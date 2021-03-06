using System;
using System.Collections.Generic;
using System.Linq;

namespace FastAop.Context
{
    public class FastAopContext
    {
        public static T Resolve<T>()
        {
            if (typeof(T).IsInterface)
                return (T)FastAop._types.GetValue(typeof(T));
            else if (!typeof(T).IsInterface && !typeof(T).GetInterfaces().Any() && typeof(T).GetConstructors().Length > 0)
            {
                var model = Constructor.Constructor.Get(typeof(T), null);
                return (T)Activator.CreateInstance(typeof(T), model.dynParam.ToArray());
            }
            else if (!typeof(T).IsInterface && !typeof(T).GetInterfaces().Any() && typeof(T).GetConstructors().Length == 0)
                return (T)Activator.CreateInstance(typeof(T));
            else if (!typeof(T).IsInterface && typeof(T).GetInterfaces().Any() && typeof(T).GetConstructors().Length > 0)
            {
                var model = Constructor.Constructor.Get(typeof(T), null);
                return (T)Activator.CreateInstance(typeof(T), model.dynParam.ToArray());
            }
            else if (!typeof(T).IsInterface && typeof(T).GetInterfaces().Any() && typeof(T).GetConstructors().Length == 0)
                return (T)Activator.CreateInstance(typeof(T));
            else
                throw new Exception($"can't find {typeof(T).Name} Instance class");
        }

        public static dynamic ResolveDyn<T>()
        {
            if (!typeof(T).GetInterfaces().Any())
                return FastAopDyn._types.GetValue(typeof(T));
            else
                return null;
        }
    }
}
