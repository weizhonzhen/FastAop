using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace FastAop.Core.Context
{
    public class FastAopContext
    {
        public static dynamic ResolveDyn<T>()
        {
            var data = FastAopExtension.serviceProvider?.GetService(typeof(T));

            if (!typeof(T).GetInterfaces().Any() && data != null)
                return data;
            else if (!typeof(T).GetInterfaces().Any() && data == null)
                return FastAopDyn.Instance(typeof(T));
            else
                return null;
        }

        public static T Resolve<T>()
        {
            if (FastAopExtension.serviceProvider == null)
                return default(T);

            var data = FastAopExtension.serviceProvider.GetService<T>();
            if (typeof(T).IsInterface && data != null)
                return data;
            else if (typeof(T).IsInterface && data == null)
                return default(T);
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
    }
}
