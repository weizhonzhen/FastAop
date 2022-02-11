using FastAop.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FastAopExtension
    {
        internal static IServiceProvider serviceProvider;

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string NamespaceService)
        {
            if (!string.IsNullOrEmpty(NamespaceService))
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
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == NamespaceService).ToList().ForEach(b =>
                    {
                        var isServiceAttr = false;
                        b.GetMethods().ToList().ForEach(m =>
                        {
                            if (m.GetCustomAttributes().ToList().Exists(a => a.GetType().BaseType == typeof(FastAopAttribute)))
                                isServiceAttr = true;
                        });

                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsInterface && b.GetInterfaces().Any() && isServiceAttr)
                            serviceCollection.AddSingleton(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First()).GetType());
                        else if (!b.IsInterface && b.GetInterfaces().Any())
                            serviceCollection.AddTransient(b.GetInterfaces().First(), b);
                        else if (!b.IsInterface && !b.GetInterfaces().Any() && isServiceAttr)
                            serviceCollection.AddTransient(b, s => { return FastAop.Core.FastAop.InstanceDyn(b); });
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            serviceCollection.AddTransient(b, s => { return Activator.CreateInstance(b); });
                    });
                });
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }

        public static IServiceCollection AddFastAop(this IServiceCollection serviceCollection, string NamespaceService, Type aopType)
        {
            if (aopType.BaseType != typeof(FastAopAttribute))
                throw new Exception($"aopType class not is FastAopAttribute,class name:{aopType.Name}");

            if (!string.IsNullOrEmpty(NamespaceService))
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
                    assembly.ExportedTypes.Where(a => a.Namespace != null && a.Namespace == NamespaceService).ToList().ForEach(b =>
                    {
                        if (b.IsAbstract && b.IsSealed)
                            return;

                        if (!b.IsInterface && b.GetInterfaces().Any())
                            serviceCollection.AddSingleton(b.GetInterfaces().First(), FastAop.Core.FastAop.Instance(b, b.GetInterfaces().First(), aopType).GetType());
                        else if (!b.IsInterface && !b.GetInterfaces().Any())
                            serviceCollection.AddSingleton(b, s => { return FastAop.Core.FastAop.InstanceDyn(b, aopType); });
                    });
                });
            }

            serviceProvider = serviceCollection.BuildServiceProvider();
            return serviceCollection;
        }
    }
}

namespace FastAop.Core
{
    public class FastAopContext
    {
        public static dynamic Resolve<T>()
        {
            if (!typeof(T).GetInterfaces().Any())
                return FastAopExtension.serviceProvider.GetService(typeof(T));
            else
                return FastAopExtension.serviceProvider.GetService<T>();
        }
    }
}