using FastAop.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FastAopExtension
    {
        internal static IServiceProvider serviceProvider;

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService)
        {
            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception ex) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;

                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
                    {
                        var isServiceAttr = false;
                        b.GetMethods().ToList().ForEach(m =>
                        {
                            if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                                isServiceAttr = true;
                        });

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;

                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                if (p.ParameterType.isSysType())
                                    return;

                                if (!p.ParameterType.IsInterface && p.ParameterType.GetInterfaces().Any() && isServiceAttr)
                                    serviceCollection.AddScoped(p.ParameterType.GetInterfaces().First(), FastAop.Core.FastAop.Instance(p.ParameterType, p.ParameterType.GetInterfaces().First()).GetType());
                                else if (!p.ParameterType.IsInterface && p.ParameterType.GetInterfaces().Any())
                                    serviceCollection.AddScoped(p.ParameterType.GetInterfaces().First(), p.ParameterType);
                                else if (!p.ParameterType.IsInterface && !p.ParameterType.GetInterfaces().Any() && isServiceAttr)
                                    serviceCollection.AddScoped(p.ParameterType, s => { return FastAop.Core.FastAop.InstanceDyn(p.ParameterType); });
                                else if (!p.ParameterType.IsInterface && !p.ParameterType.GetInterfaces().Any() && p.ParameterType.GetConstructors().Length == 0)
                                    serviceCollection.AddScoped(p.ParameterType, s => { return Activator.CreateInstance(p.ParameterType); });
                                else if (!p.ParameterType.IsInterface && !p.ParameterType.GetInterfaces().Any() && p.ParameterType.GetConstructors().Length > 0)
                                {
                                    var model = FastAop.Core.FastAop.GetConstructor(p.ParameterType, null);
                                    serviceCollection.AddScoped(p.ParameterType, s => { return Activator.CreateInstance(p.ParameterType, model.dynParam.ToArray()); });
                                }
                            });
                            serviceProvider = serviceCollection.BuildServiceProvider();
                        });

                        if (!b.IsInterface && b.GetInterfaces().Any() && isServiceAttr)
                            serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First()).GetType());
                        else if (!b.IsInterface && b.GetInterfaces().Any())
                            serviceCollection.AddScoped(b.GetInterfaces().First(), b);
                        else if (!b.IsInterface && !b.GetInterfaces().Any() && isServiceAttr)
                            serviceCollection.AddScoped(b, s => { return FastAop.Core.FastAop.InstanceDyn(b); });
                        else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length == 0)
                            serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b); });
                        else if (!b.IsInterface && !b.GetInterfaces().Any() && b.GetConstructors().Length > 0)
                        {
                            var model = FastAop.Core.FastAop.GetConstructor(b, null);
                            serviceCollection.AddScoped(b, s => { return Activator.CreateInstance(b,model.dynParam.ToArray()); });
                        }
                    });
                });
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string nameSpaceService, Type aopType)
        {
            if (aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!string.IsNullOrEmpty(nameSpaceService))
            {
                Assembly.GetCallingAssembly().GetReferencedAssemblies().ToList().ForEach(a =>
                {
                    if (!AppDomain.CurrentDomain.GetAssemblies().ToList().Exists(b => b.GetName().Name == a.Name))
                        try { Assembly.Load(a.Name); } catch (Exception) { }
                });

                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assembly =>
                {
                    if (assembly.IsDynamic)
                        return;
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == nameSpaceService).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (b.BaseType == typeof(FastAopAttribute))
                            return;
                        
                        if (b.BaseType == typeof(Attribute))
                            return;

                        b.GetConstructors().ToList().ForEach(c =>
                        {
                            c.GetParameters().ToList().ForEach(p =>
                            {
                                if (p.ParameterType.isSysType())
                                    return;

                                if (!p.ParameterType.IsInterface && p.ParameterType.GetInterfaces().Any())
                                    serviceCollection.AddScoped(p.ParameterType.GetInterfaces().First(), FastAop.Core.FastAop.Instance(p.ParameterType, p.ParameterType.GetInterfaces().First(), aopType).GetType());
                                else if (!p.ParameterType.IsInterface && !p.ParameterType.GetInterfaces().Any())
                                    serviceCollection.AddScoped(p.ParameterType, s => { return FastAop.Core.FastAop.InstanceDyn(p.ParameterType, aopType); });
                            });
                            serviceProvider = serviceCollection.BuildServiceProvider();
                        });


                        if (!b.IsInterface && b.GetInterfaces().Any())
                            serviceCollection.AddScoped(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First(), aopType).GetType());
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            serviceCollection.AddScoped(b, s => { return FastAop.Core.FastAop.InstanceDyn(b, aopType); });
                    });
                });
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        private static bool isSysType(this System.Type type)
        {
            if (type.IsPrimitive || type.Equals(typeof(string)) || type.Equals(typeof(decimal)) || type.Equals(typeof(DateTime)))
                return true;
            else
                return false;
        }
    }
}

namespace FastAop.Core
{
    public class FastAopContext
    {
        public static dynamic ResolveDyn<T>()
        {
            return FastAopExtension.serviceProvider.GetService(typeof(T));
        }

        public static T Resolve<T>()
        {
            if (typeof(T).IsInterface)
                return FastAopExtension.serviceProvider.GetService<T>();
            else
                return default(T);
        }
    }

    internal class FastAopCache
    {
        private static Dictionary<string,Type> cache = new Dictionary<string, Type>();

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