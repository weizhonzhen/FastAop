using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

        internal static MethodInfo GetMethod(List<MethodInfo> list, MethodInfo info, Type[] types)
        {
            var method = new List<MethodInfo>();

            list = list.FindAll(a => a.Name == info.Name && a.GetParameters().Length == info.GetParameters().Length);

            if (info.ReturnType.IsGenericType)
            {
                list = list.FindAll(a => info.ReturnType.IsGenericType && a.ReturnType.GetGenericArguments().Length == info.ReturnType.GetGenericArguments().Length);
                if (info.ReturnType.GetGenericArguments().Length > 0)
                {
                    if (info.ReturnType.GetGenericArguments()[0].IsGenericType)
                    {
                        list = list.FindAll(a => a.ReturnType.GetGenericArguments()[0].IsGenericType);
                        method = list.FindAll(a => a.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].Name == info.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].Name);

                        if (info.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].IsGenericType)
                        {
                            list = list.FindAll(a => a.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].IsGenericType);
                            method = list.FindAll(a => a.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0].Name == info.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0].GetGenericArguments()[0].Name);
                        }
                    }
                    else
                        method = list.FindAll(a => a.ReturnType.GetGenericArguments()[0].Name == info.ReturnType.GetGenericArguments()[0].Name);
                }
                else
                    method = list.FindAll(a => a.ReturnType.Name == info.ReturnType.Name);
            }
            else
                method = list.FindAll(a => a.ReturnType.Name == info.ReturnType.Name && a.ReturnType.GetGenericArguments().Length == info.ReturnType.GetGenericArguments().Length);

            if (method.Count == 1)
                return method[0];

            if (method.Count == 0)
                return null;

            if (method.Count > 1)
            {
                for(int i = 0; i < method.Count; i++)
                {
                    var temp = method[i].GetParameters().Select(d => d.ParameterType).ToArray();
                    for(int j = 0; j < temp.Length; j++)
                    {
                        if (temp[j].Name != types[j].Name)
                            break;

                        if (j + 1 == temp.Length)
                            return method[i];
                    }
                }
            }

            return null;
        }
    }
}